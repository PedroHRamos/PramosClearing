# ADR-002: Market Service Performance Baseline and Improvement Roadmap

## Status

Accepted

---

## Context

Load tests executed in July 2026 against the full Docker Compose stack established the first quantified performance baseline for the Market Service read path. The test scenario — `GET /api/price-ticks` returning 1 000 rows per request at increasing RPS — was designed to stress the three main components in the critical path: the ASP.NET Core API, the Npgsql connection pool, and TimescaleDB.

### Results summary

| RPS | p50 | p90 | p95 | Error rate | Data/s | Behaviour |
|---|---|---|---|---|---|---|
| 100 | 6 ms | 10 ms | 11 ms | 0 % | 12 MB/s | Comfortable |
| 500 | 12 ms | 58 ms | 89 ms | 0 % | 58 MB/s | Healthy |
| 600 | 22 ms | 773 ms | 1 020 ms | 0 % | 69 MB/s | Safe operational limit |
| 800 | 3 900 ms | 5 560 ms | 30 730 ms | 0 % | 76 MB/s | Pool saturated — latency explodes |
| 1 500 | 17 800 ms | 37 000 ms | 44 690 ms | 61 % | 28 MB/s | Full collapse |

The degradation between 600 and 800 RPS is non-linear — a cliff, not a slope. This is the signature of **connection pool exhaustion**: requests queue inside ASP.NET Core waiting for a Npgsql connection, latency accumulates, and K6 spawns more VUs to compensate, amplifying the problem. At 800 RPS the system still returns 0 % HTTP errors because ASP.NET Core's request queue absorbs everything; at 1 500 RPS the queue overflows and errors begin.

At 600 RPS the system transferred 69 MB/s over the Docker bridge network, approaching the practical inter-container bandwidth ceiling on a single developer machine. This is a second independent constraint.

### Architecture at time of measurement

- **API**: ASP.NET Core 8, Controllers, no response caching
- **ORM**: EF Core 8 with `AsNoTracking` for reads
- **Connection pool**: Npgsql default (`Maximum Pool Size = 100`)
- **Database**: TimescaleDB 2.x (PostgreSQL 15), default `max_connections = 100`
- **Transport**: HTTP/1.1, JSON (`System.Text.Json` defaults)
- **Caching**: none
- **Serialisation**: uncompressed JSON

---

## Decision

Establish **600 RPS × 1 000 ticks as the documented baseline** against which all future architectural improvements will be measured. No changes are required to the current architecture at this time; the platform is in the study and simulation phase.

---

## Improvement Roadmap

Options are ordered from lowest to highest implementation cost. Each entry states the expected impact and the specific bottleneck it addresses.

---

### Tier 1 — Configuration changes (hours)

**1.1 Increase Npgsql connection pool size**

The single highest-impact change with zero code modification.

```
Server=timescaledb;...;Maximum Pool Size=300;Minimum Pool Size=10
```

Expected impact: raises the safe RPS limit from ~600 to ~1 800–2 000 before `max_connections` becomes the next bottleneck.

**1.2 Increase TimescaleDB `max_connections`**

Set `max_connections = 400` in `postgresql.conf` mounted via Docker volume. Must accompany the pool increase above; a large pool against a low `max_connections` causes immediate connection errors on startup.

**1.3 Enforce a maximum `TICK_TAKE` at the API layer**

Cap the endpoint at 500 rows. Clients requesting 1 000 rows are reading more data than the real-time path is designed to deliver. This halves the data volume per request and roughly doubles sustainable RPS with no infrastructure change.

```csharp
take = Math.Min(take, 500);
```

---

### Tier 2 — Caching (days)

**2.1 Redis read-through cache for recent price ticks**

Introduce a Redis layer in front of TimescaleDB for the two hottest queries:

- `GET /price-ticks/latest` — cache per `symbol+exchange`, TTL 1 s
- `GET /price-ticks` with small `take` — cache per `symbol+exchange+take`, TTL 1 s

Expected impact: reduces TimescaleDB query load by 80–90 % for repeated reads of the same symbol, pushing the safe RPS limit above 5 000 for cached symbols.

**2.2 In-process memory cache via `TopOfBookProjector`**

The `TopOfBookProjector` already maintains in-memory state derived from Kafka events. Expose the latest price directly from this projection for `GET /price-ticks/latest` without hitting TimescaleDB. The projection is always current — TTL is irrelevant.

Expected impact: the latest-price endpoint becomes a dictionary lookup (sub-millisecond), removing it from the database pressure calculation entirely.

---

### Tier 3 — Query and schema optimisation (days)

**3.1 Keyset pagination instead of LIMIT**

Replace `LIMIT N` with cursor-based pagination (`WHERE timestamp < :cursor ORDER BY timestamp DESC LIMIT N`). TimescaleDB's chunk exclusion optimises this pattern significantly compared to offset-based scans on large hypertables.

**3.2 Covering index per symbol**

Create a covering index on `(symbol, exchange, timestamp DESC) INCLUDE (price, quantity)` to make the most common query fully index-scannable without a heap fetch.

**3.3 Continuous aggregates for OHLCV reads**

Move candle/OHLCV queries to pre-materialised TimescaleDB continuous aggregates refreshed every second. This offloads analytical queries from the raw tick table entirely.

---

### Tier 4 — Connection pooling infrastructure (days)

**4.1 PgBouncer in transaction-pooling mode**

Introduce PgBouncer between the API and TimescaleDB. Transaction-mode pooling allows hundreds of application connections to multiplex over a small number of real PostgreSQL connections (20–30), eliminating `max_connections` as a bottleneck permanently.

Expected impact: sustainable RPS above 3 000 without any application code changes.

---

### Tier 5 — Protocol and transport changes (weeks)

**5.1 Server-Sent Events or WebSocket push**

Replace the polling pattern (`GET /price-ticks/latest` called repeatedly by clients) with a push model: the Market Service pushes new ticks to connected clients over SSE or WebSocket as they arrive from Kafka. The database is read once per Kafka message, not once per client per polling interval. This eliminates read amplification entirely regardless of the number of connected clients.

**5.2 gRPC with Protobuf**

Replace HTTP/1.1 JSON with gRPC (HTTP/2) and Protobuf binary encoding. Benefits:
- Protobuf is 3–5× smaller than JSON for numeric tick data, reducing bandwidth from 69 MB/s to ~15 MB/s at the same RPS
- HTTP/2 multiplexing reduces connection overhead
- Server-streaming RPCs enable push semantics natively without SSE/WebSocket complexity

**5.3 Response compression**

Enable `brotli` or `gzip` on the `/price-ticks` endpoint. Tick data is highly compressible (repetitive symbol names, sequential timestamps, small price deltas). Expected payload reduction: 60–70 %, directly relieving the 69 MB/s network bottleneck with a two-line change to `Program.cs`.

---

### Tier 6 — Architecture redesign (weeks to months)

**6.1 Read replicas for TimescaleDB**

Add one or more streaming replicas of TimescaleDB. Route all read queries to replicas via a load balancer (e.g. HAProxy, Pgpool-II), reserving the primary for tick ingestion writes. Read throughput scales linearly with replica count.

**6.2 CQRS with a dedicated in-process read model**

Introduce a materialised read model (`ConcurrentDictionary<string, PriceTick>` or Redis sorted set) updated by the Kafka consumer as ticks arrive. Queries read from this model instead of TimescaleDB. The database becomes write-only for the ingestion path; the read model is always in memory.

**6.3 Apache Arrow Flight for bulk tick delivery**

For large historical queries (`TICK_TAKE > 500`), replace the JSON REST endpoint with an Arrow Flight endpoint. Arrow's columnar binary format and zero-copy transport reduce serialisation CPU by 10× and bandwidth by 5× compared to JSON for bulk numeric data.

---

### Tier 7 — Low-latency and exchange-grade protocols (months)

**7.1 FIX Protocol (FIXT 1.1 / FIX 5.0SP2)**

The Financial Information eXchange protocol is the industry standard for order routing and market data distribution. The Market Data Request / Market Data Snapshot / Incremental Refresh message types map directly to the current price-tick and order-book domain model. Implementing a FIX acceptor would allow standard buy-side clients (Bloomberg, Reuters) to connect without a custom adapter.

**7.2 FAST (FIX Adapted for Streaming)**

FAST is the binary encoding layer designed for high-volume market data feeds. It uses field presence bitmaps and delta compression across sequential messages, achieving 10–50× compression over text FIX for streaming order-book updates. Applicable to the Kafka topic as a message encoding scheme, reducing broker storage and consumer bandwidth in parallel.

**7.3 SBE (Simple Binary Encoding)**

SBE is a zero-copy, allocation-free binary codec for ultra-low-latency financial messaging. Fields are encoded directly into a flyweight buffer with no heap allocation and no object graph. Applicable to the Kafka producer in `orderbook-simulator` and the Kafka consumer in the Market Service. Expected processing latency: sub-microsecond per message vs. current JSON microseconds.

**7.4 Aeron messaging**

Aeron is a lock-free, UDP-based messaging library designed for sub-microsecond IPC and low-latency inter-node communication. Replacing Kafka with Aeron for the `orderbook-simulator` → Market Service path would remove broker round-trip latency entirely (typically 1–5 ms for Kafka vs. < 100 µs for Aeron), at the cost of losing Kafka's durability and replay guarantees.

**7.5 RDMA (Remote Direct Memory Access)**

For environments where `orderbook-simulator` and Market Service run on separate physical hosts, RDMA allows one host to read or write another host's memory without involving either CPU's kernel. This enables sub-10-microsecond tick delivery, comparable to co-location throughput at a real exchange data centre. Applicable only in bare-metal or SR-IOV cloud environments (AWS EFA, Azure InfiniBand).

---

## Consequences

- The 600 RPS × 1 000 ticks measurements are the reference point for all future performance work.
- Any change claiming to improve throughput must be validated by re-running `market-heavy-read.js` and comparing p95 against this baseline.
- Tier 1 changes (pool size, `max_connections`) should be applied before any new feature development that materially increases read traffic.
- Tier 5.1 (push model via SSE/WebSocket) is the highest-leverage change for client-facing scalability and should be prioritised when real-time streaming becomes a product requirement.
- Tier 7 options (FIX, FAST, SBE, Aeron, RDMA) are study targets for understanding how professional exchange infrastructure achieves sub-millisecond market data delivery at millions of messages per second.
