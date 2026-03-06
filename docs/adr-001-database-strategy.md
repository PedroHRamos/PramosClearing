# ADR-001: Database Strategy for Master Data and Market Data

## Status

Accepted

---

## Context

PramosClearing simulates the infrastructure of a financial market platform. The system handles two fundamentally different categories of data, each with access patterns and consistency requirements that are incompatible with a single database engine.

### Master Data

Master data defines the reference entities that the rest of the system depends on: assets, stocks, exchanges, currencies, sectors, users, orders, and portfolios. Its characteristics are:

- Written infrequently — asset registration and exchange configuration happen on the order of tens of operations per day
- Highly relational — a stock belongs to an exchange, an order references an asset, a portfolio belongs to a user
- Requires strong consistency — a stock symbol must be globally unique per exchange; partial writes must never leave the system in an invalid state
- Moderate read volume — services perform point lookups and JOIN-heavy queries to validate and enrich incoming data
- Small total dataset — thousands to tens of thousands of rows

### Market Data

Market data is the continuous stream of prices emitted by the trading simulation. Its characteristics are:

- Written at extremely high frequency — thousands of price ticks per second across all tracked assets
- Append-only — no tick is ever updated after insertion
- Always queried by time range — "last N candles", "all ticks between T1 and T2"
- Subject to time-based retention — data older than a defined threshold is compressed, tiered, or dropped
- Unbounded growth — a year of tick data for 500 assets at 10 ticks/sec exceeds 150 billion rows

### The Problem with a Single Database

Both workloads routed to the same database engine require that engine to make compromises that degrade one or both workloads.

**If a single relational OLTP database (SQL Server) is used for everything:**

- The tick ingestion pipeline saturates the write path. Row-level locking and full index maintenance on every insert — necessary for relational integrity — become bottlenecks under sustained high-frequency writes.
- There is no native concept of time-based chunking, chunk exclusion for range queries, or columnar compression for append-only historical data.
- Purging old tick data requires custom scheduled jobs and careful defragmentation to avoid degrading OLTP performance.
- A spike in tick ingestion directly competes with master data reads on the same I/O and CPU resources.

**If a single time-series database (TimescaleDB) is used for everything:**

- TimescaleDB hypertables cannot be the target of foreign key constraints. Relational integrity for master data degrades to application-level enforcement.
- EF Core's SQL Server provider and migration tooling cannot manage hypertable-specific features (compression policies, continuous aggregates, retention policies). Schema management for master data becomes irregular.
- OLTP transactions that span multiple entities — creating an order while validating an asset and updating a portfolio — are simpler and safer in a purpose-built relational store.
- Operational scaling decisions become coupled: resizing for tick ingestion affects the same instance serving master data reads.

---

## Decision

PramosClearing adopts **polyglot persistence**, using two purpose-built databases aligned to each workload:

**SQL Server** is used for all master data.

- The relational engine enforces foreign key constraints, unique constraints, and multi-table ACID transactions without application-level workarounds.
- EF Core 8 with the SQL Server provider manages schema via code-first migrations, with one `DbContext` per bounded context.
- Indexing is designed for selective lookups and JOIN performance: covering indexes on `(symbol, exchange)`, non-clustered indexes on `exchange` and `sector`.
- Sequential GUIDs (`NEWSEQUENTIALID()`) are used on clustered primary keys to prevent page fragmentation.

**TimescaleDB** is used for all market data.

- Hypertables automatically partition `price_ticks` by time, keeping inserts O(1) regardless of total table size.
- Continuous aggregates materialise OHLCV candles (`candles_1m`, `candles_5m`, `candles_1h`) incrementally, making candle reads O(1).
- Compression policies reduce closed chunks by 90–95 % with no change to the query API.
- Retention policies drop chunks older than a configured threshold atomically, without index fragmentation or custom jobs.
- Ingestion bypasses EF Core; bulk writes use `COPY` or batched `INSERT` via Dapper for maximum throughput.

---

## Consequences

### Advantages

- Each database is operated at the performance envelope it was designed for. SQL Server handles complex relational queries without competing against high-frequency appends. TimescaleDB handles sustained tick ingestion without the locking overhead of a general-purpose OLTP engine.
- Failures are isolated. A tick ingestion spike or TimescaleDB maintenance window does not affect the availability or latency of master data services.
- Storage cost for market data is reduced by 90–95 % through TimescaleDB's columnar compression, compared to uncompressed rows in a relational table.
- Retention management for market data is declarative and operational — no custom jobs, no fragmentation risk.
- The system can scale each store independently. SQL Server scales vertically with read replicas for reporting. TimescaleDB scales vertically or horizontally (multi-node) for tick ingestion.

### Operational Complexity

- Two database engines must be operated, monitored, backed up, and upgraded independently.
- Connection string management, health checks, and observability instrumentation must be configured for both stores.
- Developers must understand the access patterns and tooling constraints of each engine. Writes that belong in TimescaleDB must not be routed to SQL Server, and vice versa.
- Integration tests require both engines to be available, increasing the complexity of the local development environment (managed via Docker Compose).

### Polyglot Persistence Implications

- There is no cross-database transaction boundary. An operation that writes to both SQL Server and TimescaleDB cannot be made atomic. By design, no such operation exists in PramosClearing — master data writes and market data writes are always independent.
- Data that originates in SQL Server (e.g. an asset's `Id`) is referenced by value in TimescaleDB (e.g. as `asset_id` in `price_ticks`). Referential integrity between stores is enforced at the application layer, not at the database layer.
- Adding a third data store in the future (e.g. Redis for real-time order book state) follows the same principle: choose the engine whose access pattern matches the workload.

### Scaling Benefits

| Dimension | SQL Server | TimescaleDB |
|---|---|---|
| Write volume | Low — scales with vertical CPU/memory | Very high — scales vertically or via multi-node sharding |
| Read volume | Moderate — read replicas absorb reporting load | High — chunk exclusion + continuous aggregates limit scan scope |
| Storage growth | Bounded — master data grows slowly | Unbounded — managed by compression and retention policies |
| Failure domain | Isolated from market data pipeline | Isolated from master data services |

### Future Extensibility

- New asset types (bonds, derivatives) are added as new entities in SQL Server with standard EF Core migrations.
- New candle resolutions (e.g. `candles_15m`) are added as new TimescaleDB continuous aggregates without schema changes to the underlying `price_ticks` hypertable.
- A read model optimised for real-time order book queries (e.g. Redis Sorted Sets) can be introduced alongside the existing stores without disrupting either workload.
- If tick volume grows beyond a single TimescaleDB node, TimescaleDB's multi-node distributed hypertables provide a horizontal scaling path without a change to the query API or application code.
