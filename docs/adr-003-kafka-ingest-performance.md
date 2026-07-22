# ADR-003 — Kafka Ingest Pipeline: Performance Investigation & Optimizations

**Date:** July 2026  
**Status:** Concluded  
**Context:** Market Service write path — `Kafka → consumer → TimescaleDB`

---

## Objective

Find the maximum sustainable ingest rate for the write path by progressively increasing load:  
1 000 → 5 000 → 10 000 → 20 000 → 50 000 → 100 000 events/s.

Fix any bottleneck discovered along the way and measure the result before continuing.

---

## Problems Found & Solutions Applied

### 1. Producer sidecar crashing at ≥ 10 000 events/s

**Problem:** The k6 producer sidecar (`tests/k6/producer-sidecar/server.js`) used `setInterval` to flush the internal buffer every 10 ms. When the flush took longer than 10 ms (under high load), a second interval tick fired before the first had finished, triggering two concurrent Kafka `send()` calls on the same buffer. This doubled-up flushing caused buffer corruption and eventually OOM, crashing the Node.js process.

**Solution:**
- Added a `flushing` boolean mutex: if `flushing === true` the interval tick returns immediately.
- Added `MAX_BUFFER = 2000` backpressure: requests are rejected with HTTP 503 before reading the body when the buffer is already full, preventing unbounded memory growth.
- Added batch message support: the sidecar now accepts `{ messages: [{key, value}, ...] }` in addition to single `{key, value}` payloads.

**Result:** Sidecar stable at all tested rates. No crashes, no OOM.

---

### 2. k6 sending 10 000 individual HTTP requests/s

**Problem:** At 10 000 events/s, k6 was configured to send one HTTP request per event. This generated 10 000 req/s against the sidecar's Node.js HTTP server, saturating the event loop independently of Kafka.

**Solution:** Added `BATCH_SIZE` (default `10`) to `kafka-ingest-load.js`. Each k6 iteration sends a single HTTP request containing 10 events, reducing HTTP overhead to 1 000 req/s while maintaining 10 000 events/s throughput. The `rate` for the `constant-arrival-rate` executor is set to `EVENTS_PER_SEC / BATCH_SIZE`.

**Result:** Sidecar latency dropped significantly. HTTP throughput to the sidecar reduced 10× with no impact on Kafka event rate.

---

### 3. Consumer throughput ceiling: ~700 events/s (EF Core per-row INSERT)

**Problem:** The original `OrderBookUpdateConsumer` processed messages one at a time in a tight loop, calling `DbContext.AddAsync()` + `SaveChangesAsync()` per message. EF Core translates this to individual `INSERT` statements (~3 ms each), giving a hard ceiling of ~333 inserts/s — and in practice only ~700 events/s due to additional deserialization and resolution overhead.

**Solution:** Replaced the single-message loop with a two-phase approach:

1. **`DrainBatch`** — accumulates up to `BatchSize` messages from Kafka within a `FlushIntervalMs` window (whichever limit is reached first), using `consumer.Consume(TimeSpan)` with a sliding deadline to avoid busy-waiting.
2. **`BulkPersistAsync`** — persists the entire batch using **Npgsql binary COPY** via a staging table:
   - `TRUNCATE _stage_price_ticks` (temp table, no indexes)
   - Binary `COPY` stream into staging
   - `INSERT INTO price_ticks ... SELECT ... FROM _stage_price_ticks ON CONFLICT (time, asset_id) DO NOTHING`

The `ON CONFLICT DO NOTHING` guard makes consumer restarts safe: if a batch was written to TimescaleDB but the offset commit was lost, replayed messages are silently skipped.

**Result:** Consumer throughput increased from ~700 events/s to ~12 000 events/s (17× improvement) with `BatchSize = 500`.

---

### 4. EF Core identity map crash with 9-column primary key

**Problem:** After switching to batch drain, two messages within the same batch could share the same value across all 9 original PK columns (`time`, `asset_id`, `symbol`, `exchange`, `bid`, `ask`, `last`, `volume`, `source`). EF Core's identity map threw an exception when it detected two tracked entities with the same key.

**Solution:**
- Replaced the 9-column PK with a semantically correct 2-column PK: `(time, asset_id)`. This is the natural business key — a single asset cannot have two price ticks at the exact same timestamp.
- Applied the PK change to the live database (`TRUNCATE` + `ALTER TABLE DROP CONSTRAINT` + `ALTER TABLE ADD PRIMARY KEY`).
- Created migration `infra/db/timescaledb/V5__fix_price_ticks_pk.sql` for fresh deployments.
- Updated `PriceTickConfiguration.cs`: `builder.HasKey(tick => new { tick.Time, tick.AssetId })`.
- Added in-batch dedup via `Dictionary<(DateTime, Guid), PriceTickEntity>` before COPY, so duplicate messages within a single batch are collapsed at the application layer before hitting the DB.

**Result:** No more identity map exceptions. Consumer runs stably through all load levels.

---

### 5. Kafka consumer lag monitor unreliable

**Problem:** `kafka-lag-monitor.js` polled the Provectus Kafka UI REST API for the `messagesBehind` field. This field does not update in real time in the version deployed — it always returned 0, making lag monitoring via k6 useless.

**Solution:** Removed the lag monitor script from active use. Consumer lag is now monitored directly via:
- **Kafka UI** → Consumer Groups → `market-price-tick-consumer` → *Messages Behind* column (updates in real time in the UI, even though the REST API does not).
- CLI: `docker exec pramosclearing-kafka-1 kafka-consumer-groups --bootstrap-server localhost:9092 --group market-price-tick-consumer --describe`

---

### 6. BatchSize progression: 500 → 2000 → 5000 to improve per-batch amortization

**Problem:** With `BatchSize = 500`, profiling showed each COPY cycle took ~40 ms:
- `TRUNCATE _stage_price_ticks`: ~5 ms (fixed)
- Binary COPY 500 rows: ~10 ms (variable)
- `INSERT SELECT ON CONFLICT`: ~25 ms (mostly fixed overhead + linear row cost)

The fixed overhead dominated, giving a ceiling of `500 / 0.040 = 12 500 events/s`.

**Solution:** Progressively increased `BatchSize` in `KafkaConsumerOptions`. When a backlog exists, `DrainBatch` fills the batch near-instantly (< 1 ms), so a larger batch amortizes the fixed COPY overhead over more rows. Three values were tested at 20 000 events/s:

| BatchSize | Lag at end of 2 min test | Consumer throughput |
|---|---|---|
| 500 | ~960 000 | ~12 000/s |
| 2 000 | ~587 000 | ~15 100/s |
| 5 000 | ~100 000 | ~19 200/s |

**Trade-off at BatchSize=5000:** Each COPY cycle processes 5 000 rows, which is significantly heavier. Under sustained 20 000 events/s, the COPY cycles caused brief CPU spikes on the Docker host that temporarily backed up the Kafka broker. This surfaced in the k6 sidecar metrics: 37 HTTP 503 errors (0.01%), p95 latency increased from ~19 ms to ~32 ms, and a single max spike of 1.59 s was observed. `dropped_iterations` rose from 33 to 406. All errors were transient and the produce error rate remained well within the 1% threshold.

**Current value:** `BatchSize = 5000`.

---

### 7. Sidecar MAX_BUFFER too small + single consumer instance as joint ceiling at 20 000 events/s

**Problem (sidecar):** Even with the `flushing` mutex, `MAX_BUFFER = 2000` was insufficient when the COPY cycles for 5 000 rows caused multi-second CPU spikes on the Docker host. During those spikes, kafkajs `send()` stalled, the `flushing` flag stayed `true`, and incoming messages piled into the buffer. At 2 000 sidecar req/s, the 2 000-message buffer was exhausted in under 1 second of stall, producing 503 responses.

**Problem (consumer):** With a single consumer instance owning all 6 partitions, `DrainBatch` is sequential — it drains one batch, COPYs it, commits, then drains the next. Two concurrent consumers each own 3 partitions and run their COPY cycles in parallel, doubling effective throughput.

**Solution:**
- Increased `MAX_BUFFER` from 2 000 to 10 000 in `server.js` (default, overridable via `MAX_BUFFER` env var). This provides ~5 seconds of buffer headroom at 2 000 sidecar req/s before any 503 is returned.
- Added `market-service-consumer` service to `docker-compose.yml`: same image as `market-service`, same `Kafka__GroupId`, no port binding. Kafka automatically rebalances the 6 partitions — 3 per instance — on startup.

**Result:** At 20 000 events/s, consumer lag accumulated only ~7 000 messages over the full 2-minute test (growth rate ≈ 58/s, effective combined throughput ≈ **19 940 events/s**). Zero sidecar errors. The pipeline is effectively stable at 20 000 events/s.

---

## Load Test Results Summary

All tests: 2-minute duration, sidecar on `pramosclearing_default` Docker network, TimescaleDB single-node local Docker.

| Rate (events/s) | Consumers | BatchSize | Sidecar errors | Consumer lag at end | Consumer throughput | Result |
|---|---|---|---|---|---|---|
| 1 000 | 1 | 500 | 0% | ~0 | ~1 000/s | ✅ Stable |
| 5 000 | 1 | 500 | 0% | ~0 | ~5 000/s | ✅ Stable |
| 10 000 | 1 | 500 | 0% | ~1 000 (steady) | ~10 000/s | ✅ Stable |
| 20 000 | 1 | 500 | 0% | ~960 000 | ~12 000/s | ⚠️ Falling behind |
| 20 000 | 1 | 2 000 | 0% | ~587 000 | ~15 100/s | ⚠️ Falling behind |
| 20 000 | 1 | 5 000 | 0.01% | ~100 000 | ~19 200/s | ⚠️ Near limit |
| **20 000** | **2** | **5 000** | **0%** | **~7 000** | **~19 940/s** | ✅ **Stable** |

**Sustainable write-path limit (2 consumers, single TimescaleDB node, BatchSize=5 000): ~20 000 events/s.**

At 10 000 events/s a single consumer achieves true steady-state. At 20 000 events/s, two consumer instances (3 partitions each) combined with `BatchSize = 5 000` and `MAX_BUFFER = 10 000` on the sidecar produce a fully stable pipeline: lag of only ~7 000 messages after 2 minutes of sustained load (≈ 350 ms of buffering at that rate), zero sidecar errors.

---

## Bottleneck Analysis (current ceiling ~20 000 events/s)

The remaining bottleneck is the **COPY cycle latency per batch**. With a backlog present, `DrainBatch` completes near-instantly (always full), so throughput is determined by:

$$
\text{Throughput} = \frac{\text{BatchSize}}{\text{CycleTime}}
$$

Where `CycleTime` includes:
- Fixed: `TRUNCATE` + connection overhead + transaction commit ≈ 30 ms
- Variable: binary COPY + `INSERT SELECT` ≈ proportional to rows

The `INSERT SELECT ... ON CONFLICT DO NOTHING` into the TimescaleDB hypertable is the dominant variable cost, as each row must be routed to the correct chunk and checked against the PK index.

At BatchSize=5 000, the cycle time increases enough to create intermittent CPU pressure on the Docker host, briefly starving the Kafka broker and causing transient producer backpressure. This is the observed secondary effect at 20 000 events/s.

---

## Options to Exceed 20 000 events/s (not yet implemented)

| Option | Expected gain | Complexity |
|---|---|---|
| ~~Increase `BatchSize` to 5 000~~ | ~~Tried: ~19 200/s~~ | ~~Done~~ |
| ~~2 consumer instances~~ | ~~Stable at 20 000/s~~ | ~~Done~~ |
| Add a 3rd consumer instance (requires 9 partitions) | ~30 000/s | Low — increase partitions + one more replica |
| COPY directly to hypertable (skip staging + INSERT SELECT) | Removes ON CONFLICT guard | Requires application-level idempotency |
| Disable continuous aggregate refresh during ingest | Reduces TimescaleDB write amplification | Medium — operational complexity |
| Dedicated TimescaleDB node (off Docker host) | Eliminates CPU contention | Infrastructure change |

---

## Kafka Topic Configuration

The `orderbook-updates` topic was increased from 1 to **6 partitions** during this investigation. With a single consumer instance, all 6 partitions are assigned to it — the extra partitions provide headroom to scale horizontally without topic reconfiguration.

---

## Architecture Comparison

| Metric | Read path (ADR-002) | Write path (this ADR) |
|---|---|---|
| Sustainable limit | ~600 RPS × 1 000 ticks | ~20 000 events/s |
| Bottleneck | Npgsql connection pool exhaustion | COPY cycle latency per batch + TimescaleDB CPU |
| Single-node? | Yes | Yes (TimescaleDB) |
| Consumer instances | 1 | 2 (3 partitions each) |
| Scaling path | Connection pool tuning, read replicas | More partitions + consumer instances |
