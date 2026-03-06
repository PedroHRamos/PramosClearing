# Database Architecture — PramosClearing

## Overview

PramosClearing uses **polyglot persistence**: two purpose-built databases, each optimised for its data access pattern. A single database cannot efficiently serve both workloads without significant compromises to performance, scalability, or cost.

```
┌─────────────────────────────────────────────────────────────────┐
│                        PramosClearing                           │
│                                                                 │
│   ┌─────────────────┐              ┌─────────────────────────┐  │
│   │  market-service │              │    price-generator      │  │
│   │  order-service  │              │       (worker)          │  │
│   │  user-service   │              └──────────┬──────────────┘  │
│   │  portfolio-svc  │                         │                 │
│   └────────┬────────┘                         │                 │
│            │                                  │                 │
│            │ master data                      │ market data     │
│            ▼                                  ▼                 │
│   ┌─────────────────┐              ┌─────────────────────────┐  │
│   │   SQL Server    │              │      TimescaleDB        │  │
│   │  (master data)  │              │   (time-series data)    │  │
│   └─────────────────┘              └─────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Separation of Concerns

### SQL Server — Master Data Store

Stores the authoritative reference data of the system. This data is:

- Written infrequently (asset registration, exchange configuration)
- Highly relational
- Subject to strong consistency requirements (no duplicate symbols per exchange)
- Read heavily by services that need to validate or enrich incoming market data

**Entities stored:**

| Entity | Description |
|---|---|
| `stocks` | Financial stocks with symbol, exchange, sector |
| `etfs` | Exchange-traded funds |
| `cryptos` | Crypto-currency assets |
| `exchanges` | Exchange definitions and metadata |
| `currencies` | ISO 4217 currency definitions |
| `orders` | Order lifecycle records |
| `users` | User accounts and profiles |
| `portfolios` | Position records per user |

SQL Server is appropriate here because:

- ACID transactions enforce business invariants (a stock's symbol must be unique per exchange)
- Foreign keys maintain referential integrity across entities
- Complex JOIN queries across relational data are a first-class concern
- Entity Framework Core maps cleanly onto its relational model
- Write throughput is low and predictable

---

### TimescaleDB — Market Data Store

TimescaleDB is a PostgreSQL extension that adds native time-series capabilities. It stores data that is:

- Written at extremely high frequency (thousands of price ticks per second per asset)
- Append-only by nature (market data is never updated after insertion)
- Always queried by time range (last N candles, ticks between T1 and T2)
- Subject to time-based retention (data older than N months is archived or dropped)

**Entities stored:**

| Entity | Description |
|---|---|
| `price_ticks` | Raw bid/ask/last price at a point in time |
| `candles_1m` | OHLCV aggregated over 1-minute windows |
| `candles_5m` | OHLCV aggregated over 5-minute windows |
| `candles_1h` | OHLCV aggregated over 1-hour windows |
| `market_events` | Circuit breaker triggers, halts, reopens |

TimescaleDB is appropriate here because:

- Automatic **hypertables** partition data by time under the hood — inserts remain O(1) regardless of table size
- Native **continuous aggregates** compute candles incrementally without full-table scans
- **Compression policies** reduce storage by 90–95 % on older chunks without changing the query API
- **Retention policies** automatically drop chunks older than a configured threshold
- Time-range queries use chunk exclusion to scan only relevant partitions

---

## Why Not a Single Database

### Single SQL Server

| Concern | Problem |
|---|---|
| Write throughput | SQL Server is not designed for millions of append-only writes per minute; lock contention on `price_ticks` would degrade the entire server, impacting `orders` and `stocks` |
| Index overhead | Every insert into a heavily indexed relational table (to support JOINs) carries extra cost; market data needs almost no indexes beyond a time-based one |
| Storage growth | A year of tick data for 500 assets at 10 ticks/sec = ~150 billion rows; SQL Server has no native chunk-based compression designed for this pattern |
| Partitioning | Table partitioning in SQL Server is manual, verbose, and not optimised for time-series access patterns |
| Cost | A SQL Server instance sized for OLTP master data would need to be dramatically over-provisioned to absorb tick ingestion |

### Why TimescaleDB Instead of SQL Server for Market Data

Storing stock price history in SQL Server would work at small scale but breaks down under the write volume and query patterns of a real market data pipeline. TimescaleDB is the better choice for the following reasons:

| Concern | SQL Server limitation | TimescaleDB advantage |
|---|---|---|
| Write throughput | Row-level locking and full index maintenance on every insert; at 50,000+ ticks/sec, contention becomes a bottleneck | Inserts always target the current time chunk with minimal locking; throughput scales linearly with hardware |
| Time-range queries | Full or partial table scans even with a datetime index; no native concept of chunk exclusion | Chunk exclusion automatically limits scans to relevant time partitions — a 1-hour query touches one or two chunks regardless of total table size |
| Candle aggregation | Candles must be computed on demand or maintained by custom jobs and indexed views | Native continuous aggregates incrementally materialise OHLCV candles; reads are O(1) against pre-computed results |
| Storage at scale | A year of tick data for 500 assets at 10 ticks/sec ≈ 150 billion rows; SQL Server has no chunk-based columnar compression for this pattern | Compression policies reduce closed chunks by 90–95 % with no change to the query API |
| Retention management | Purging old data requires custom scheduled jobs and careful index maintenance to avoid fragmentation | Native retention policies drop entire time chunks atomically — no fragmentation, no custom jobs |
| Append-only fit | SQL Server's OLTP engine is optimised for mixed read/write/update workloads; append-only patterns carry unnecessary locking overhead | TimescaleDB is designed for append-only time-series; no update or delete path means no locking overhead on ingestion |

---

## Performance Considerations

### Inserts

```
SQL Server  (master data):   < 100 writes/sec   — row-level locking, full index maintenance
TimescaleDB (market data):   > 50,000 writes/sec — append to latest chunk, minimal locking
```

TimescaleDB's hypertable partitioning ensures inserts always target the current time chunk. There is no contention with reads on historical chunks. Batched inserts using `COPY` or bulk insert APIs further increase ingestion throughput.

### Reads

**Master data reads** benefit from:
- Covering indexes on `(symbol, exchange)` for asset lookups
- Query plan caching in SQL Server
- Low row counts (thousands, not billions)

**Market data reads** benefit from:
- Chunk exclusion: a 1-hour candle query touches one or two chunks, not the full table
- Continuous aggregates precompute candles at materialisation time, making reads O(1)
- Columnar compression on closed chunks reduces I/O by orders of magnitude

---

## Scalability Considerations

### SQL Server

Master data scales **vertically**. The dataset is small and write frequency is low. Read replicas can be added for reporting workloads without affecting the OLTP primary.

### TimescaleDB

Market data scales in two dimensions:

- **Vertical**: more CPU and RAM increases ingestion throughput linearly
- **Horizontal**: TimescaleDB Distributed (multi-node) shards hypertables across nodes by time and space (e.g. by asset symbol), enabling near-linear horizontal scaling

The separation also means an incident in the market data pipeline (e.g. a burst of ticks from a faulty price generator) cannot affect the availability or latency of the master data services.

---

## Data Retention Strategy

### SQL Server

Master data is retained indefinitely. Soft deletes (`IsActive`, `DeletedAt`) are used rather than physical removal to preserve referential integrity and audit trails.

### TimescaleDB

Market data uses a tiered retention model:

| Tier | Retention | Storage | Access |
|---|---|---|---|
| Hot | 0–7 days | Uncompressed hypertable chunks | Real-time queries |
| Warm | 7–90 days | Compressed hypertable chunks | Analytical queries |
| Cold | 90 days–2 years | Compressed + tablespace on cheaper storage | Historical backtesting |
| Archive | > 2 years | Exported to object storage (S3/Blob) | Compliance / audit |

Retention policies are configured as TimescaleDB jobs and run automatically on a schedule:

```sql
SELECT add_retention_policy('price_ticks', INTERVAL '2 years');
SELECT add_compression_policy('price_ticks', INTERVAL '7 days');
```

---

## Architectural Diagram — Data Flow

```
price-generator (worker)
        │
        │  price ticks (high frequency)
        ▼
   TimescaleDB
   ├── price_ticks     (hypertable, partitioned by time)
   ├── candles_1m      (continuous aggregate)
   ├── candles_5m      (continuous aggregate)
   └── candles_1h      (continuous aggregate)


market-service / order-service / user-service
        │
        │  asset registration, order creation (low frequency)
        ▼
   SQL Server
   ├── stocks
   ├── etfs
   ├── cryptos
   ├── orders
   ├── portfolios
   └── users
```

---

## Summary — Polyglot Persistence Rationale

| Dimension | SQL Server | TimescaleDB |
|---|---|---|
| Data type | Relational master data | Time-series market data |
| Write pattern | Low frequency, transactional | High frequency, append-only |
| Read pattern | JOIN-heavy lookups | Time-range scans |
| Consistency | Strong ACID | Eventual (per chunk) |
| Scaling model | Vertical + read replicas | Vertical + horizontal sharding |
| Retention | Indefinite (soft delete) | Policy-driven, tiered |
| ORM | Entity Framework Core | Raw SQL / Dapper for hot path |

Using a single database for both workloads would require accepting at least one of:

- Degraded ingestion throughput due to OLTP locking
- Over-provisioned infrastructure to absorb both access patterns
- Application-level workarounds replacing database-native features (partitioning, compression, continuous aggregates)
- Operational coupling: scaling one workload forces scaling the other

The polyglot approach assigns each workload to the database engine designed for it, with no compromises in either direction.
