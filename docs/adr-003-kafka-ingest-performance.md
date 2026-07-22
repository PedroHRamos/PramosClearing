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

### 6. BatchSize 500 → 2000 to improve per-batch amortization

**Problem:** With `BatchSize = 500`, profiling showed each COPY cycle took ~40 ms:
- `TRUNCATE _stage_price_ticks`: ~5 ms (fixed)
- Binary COPY 500 rows: ~10 ms (variable)
- `INSERT SELECT ON CONFLICT`: ~25 ms (mostly fixed overhead + linear row cost)

The fixed overhead dominated, giving a ceiling of `500 / 0.040 = 12 500 events/s`.

**Solution:** Increased `BatchSize` from 500 to 2000 in `KafkaConsumerOptions`. When a backlog exists, `DrainBatch` fills 2000 messages near-instantly (< 1 ms), then the COPY cycle handles 4× the rows for roughly 1.5–1.7× the time, improving amortization.

**Result:** At 20 000 events/s, lag growth dropped from 960 000 at test end (BatchSize=500) to 587 000 (BatchSize=2000), implying consumer throughput improved from ~12 000 to ~15 100 events/s.

---

## Load Test Results Summary

All tests: 2-minute duration, sidecar on `pramosclearing_default` Docker network, TimescaleDB single-node local Docker.

| Rate (events/s) | Sidecar errors | Consumer lag behaviour | Consumer throughput | Result |
|---|---|---|---|---|
| 1 000 | 0% | 0 throughout | Matches rate | ✅ Stable |
| 5 000 | 0% | 0 throughout | Matches rate | ✅ Stable |
| 10 000 | 0% | Peak ~10 000 → settles ~1 000 | ~10 000/s | ✅ Stable |
| 20 000 (BatchSize=500) | 0% | Grew to ~960 000 at end, then drained | ~12 000/s | ⚠️ Falling behind |
| 20 000 (BatchSize=2000) | 0% | Grew to ~587 000 at end, drained quickly | ~15 100/s | ⚠️ Falling behind |

**Sustainable write-path limit (single consumer, single TimescaleDB node): ~15 000 events/s.**

At 10 000 events/s the consumer achieves true steady-state (lag ≈ 0). At 20 000 events/s the consumer falls behind but can drain the accumulated backlog after the producer stops, meaning the pipeline is resilient to bursts of up to 2× the sustainable rate for short durations.

---

## Bottleneck Analysis (current ceiling ~15 000 events/s)

The remaining bottleneck is the **COPY cycle latency per batch**. With a backlog present, `DrainBatch` completes near-instantly (always full), so throughput is determined by:

$$
\text{Throughput} = \frac{\text{BatchSize}}{\text{CycleTime}}
$$

Where `CycleTime` includes:
- Fixed: `TRUNCATE` + connection overhead + transaction commit ≈ 30 ms
- Variable: binary COPY + `INSERT SELECT` ≈ proportional to rows

The `INSERT SELECT ... ON CONFLICT DO NOTHING` into the TimescaleDB hypertable is the dominant variable cost, as each row must be routed to the correct chunk and checked against the PK index.

---

## Options to Exceed 15 000 events/s (not yet implemented)

| Option | Expected gain | Complexity |
|---|---|---|
| Increase `BatchSize` further (e.g. 5 000) | ~20 000/s (diminishing returns) | Trivial — config change |
| Run 2 consumer instances (requires 12 partitions) | ~30 000/s | Low — increase partitions + scale replicas |
| COPY directly to hypertable (skip staging + INSERT SELECT) | Removes ON CONFLICT guard | Requires application-level idempotency |
| Disable continuous aggregate refresh during ingest | Reduces TimescaleDB write amplification | Medium — operational complexity |
| Dedicated TimescaleDB write connection pool | Reduces connection setup overhead | Low |

---

## Kafka Topic Configuration

The `orderbook-updates` topic was increased from 1 to **6 partitions** during this investigation. With a single consumer instance, all 6 partitions are assigned to it — the extra partitions provide headroom to scale horizontally without topic reconfiguration.

---

## Architecture Comparison

| Metric | Read path (ADR-002) | Write path (this ADR) |
|---|---|---|
| Sustainable limit | ~600 RPS × 1 000 ticks | ~15 000 events/s |
| Bottleneck | Npgsql connection pool exhaustion | COPY cycle latency per batch |
| Single-node? | Yes | Yes |
| Scaling path | Connection pool tuning, read replicas | More partitions + consumer instances |
