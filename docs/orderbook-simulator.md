# OrderBook Simulator Worker — Engineering Document

**Service:** `orderbook-simulator`
**Location:** `workers/orderbook-simulator/`
**Runtime:** .NET 8 `BackgroundService`
**Status:** Active development

---

## Table of Contents

1. [Why a Worker Service](#1-why-a-worker-service)
2. [Why Order Books Are Kept in Memory](#2-why-order-books-are-kept-in-memory)
3. [Simulation Algorithm](#3-simulation-algorithm)
4. [Interaction with the Market Service](#4-interaction-with-the-market-service)
5. [Resemblance to Real Market Data Feeds](#5-resemblance-to-real-market-data-feeds)
6. [Scaling Strategies](#6-scaling-strategies)
7. [Architecture Overview](#7-architecture-overview)
8. [Configuration Reference](#8-configuration-reference)

---

## 1. Why a Worker Service

An order book simulator is a **continuous, stateful background process** — it has no inbound HTTP requests, no user interactions, and no request/response lifecycle. These characteristics make an HTTP API the wrong abstraction.

`.NET BackgroundService` is the idiomatic choice because:

- It runs for the entire lifetime of the host process.
- It participates in `IHostedService` lifecycle: the runtime calls `StartAsync` and `StopAsync`, giving it cooperative cancellation via `CancellationToken`.
- It does not block a thread pool thread. The simulation loop uses `await Task.Delay(...)` which releases the thread between ticks, making the process efficient at scale.
- It integrates naturally with `IServiceScopeFactory` for resolving scoped services (database access) at startup, without requiring the simulation loop itself to hold a database connection.

Workers are fundamentally different from services. A `market-service` answers questions. The `orderbook-simulator` *produces* a continuous stream of state changes. Putting that responsibility inside a web API would conflate concerns, introduce an always-polling endpoint with no caller, and couple the simulation lifecycle to HTTP request handling.

---

## 2. Why Order Books Are Kept in Memory

Each of the 100 stocks has an independent order book. The order book state changes on every tick — at up to 50 Hz, that is up to 5,000 state mutations per second across all instruments.

Persisting each mutation to a database would:

- Saturate a relational database with high-frequency micro-writes.
- Introduce latency inconsistent with the sub-millisecond mutation semantics of a real order book.
- Create a bottleneck where the simulation speed is bounded by I/O rather than compute.

Real exchange matching engines and market data distributors keep their order books entirely in memory for these same reasons. The canonical approach in production systems (e.g., NASDAQ ITCH, CME MDP 3.0) is:

1. Maintain the active order book in a lock-free in-memory data structure.
2. Publish deltas (individual price-level changes) to a stream.
3. Allow downstream consumers to reconstruct the full book from the stream.

This worker follows the same philosophy. The in-memory `OrderBook` is the authoritative state. Published `OrderBookUpdate` events are the durable projection downstream systems consume.

The consequence of this design: if the worker restarts, order books are re-initialized from scratch with freshly seeded prices. This is acceptable because the data is synthetic. A production system would recover by replaying recent events or snapshotting the book to a time-series store.

---

## 3. Simulation Algorithm

### 3.1 Initialization

At startup, each active stock is assigned an `OrderBook` with a randomly seeded mid-price in the range $10–$1,000. Five bid levels and five ask levels are placed symmetrically around the mid-price at increments of one tick ($0.01).

### 3.2 Mid-Price Drift

On each call to `GenerateUpdate`, the mid-price drifts with a 20% probability. The direction is chosen with equal probability (up or down by one tick). This is a discrete approximation of a random walk, analogous to a simplified Brownian motion:

$$
P_{t+1} = P_t + \epsilon \cdot \Delta t, \quad \epsilon \in \{-1, +1\} \text{ with equal probability}
$$

The mid-price is bounded from below at one tick to prevent it reaching zero.

### 3.3 Crossed-Market Prevention

After each drift, price levels that would create a crossed market (a bid price ≥ mid-price, or an ask price ≤ mid-price) are pruned. This mirrors exchange-level order rejection logic that prevents bids from crossing asks.

### 3.4 Order Book Action

For each tick, a side (bid or ask) is selected at random with equal probability. An action is then chosen based on the current depth of that side:

| Condition | Action |
|---|---|
| Level count ≥ `MaxLevels` (10) | Force Remove |
| Level count > 2 and roll < 20% | Remove |
| Otherwise and roll < 60% | Add |
| Otherwise | Modify |

**Add:** A new price level is placed at a random distance (1–10 ticks) from the mid-price. If the chosen price already exists, the algorithm retries up to 20 times before falling back to the nearest available tick. A random quantity between 100 and 2,000 is assigned.

**Modify:** A random existing level is selected and its quantity is updated to a new random value in the same range.

**Remove:** A random existing level is deleted from the book.

### 3.5 Tick Rate and Concurrency Model

The worker is designed to approximate, at small scale, the message cadence of a real market data provider. Two parameters control this behaviour.

**Concurrent threads per batch (`ConcurrencyLevel = 50`)**

Each call to `SimulationEngine.TickAsync` fires 50 `GenerateAndPublishAsync` tasks concurrently via `Task.WhenAll`. Each task independently selects a random symbol, generates an update under a per-symbol lock, and publishes to the event bus. This mirrors how a professional feed handler processes multiple instruments in parallel — a single feed handler process at a venue such as NASDAQ or B3 handles hundreds of instruments simultaneously with no sequential bottleneck between them.

**Tick interval (`MinDelayMs = 1`, `MaxDelayMs = 10`)**

After each batch of 50 concurrent updates, the worker pauses for a random duration between 1 ms and 10 ms. In isolation this produces:

$$
\text{Updates/s} = \frac{\text{ConcurrencyLevel}}{\text{AvgDelayMs} \times 10^{-3}} = \frac{50}{5.5 \times 10^{-3}} \approx 9{,}090 \text{ updates/s}
$$

Real Level 2 feeds from major exchanges publish at 100,000–1,000,000 messages/second per venue using hardware timestamping and kernel-bypass networking. The values chosen here are a deliberate **small-scale approximation**: high enough to exercise concurrency, buffering, and downstream consumer pressure, but low enough to run on a development laptop or a single container without saturating the broker.

### 3.6 Event Structure

Each mutation produces an `OrderBookUpdate` record:

```json
{
  "symbol":    "AAPL",
  "exchange":  "NASDAQ",
  "side":      "Bid",
  "price":     182.30,
  "size":      200,
  "action":    "Add",
  "timestamp": "2026-03-06T14:22:01.123456Z"
}
```

A `size` of `0` indicates the level was removed. Downstream consumers reconstruct the full book by applying deltas in timestamp order.

---

## 4. Interaction with the Market Service

The simulator does not communicate with the `market-service` over HTTP. It reads directly from the market database using a dedicated, read-only `MarketReadDbContext`.

### Design Rationale

Calling the `market-service` HTTP API would introduce:

- A runtime dependency on the service being healthy before the worker can start.
- Network latency and potential throttling during bulk stock loading.
- Coupling between the worker's startup sequence and the API's availability.

Since both services share the same SQL Server instance in this deployment topology, a direct database read is simpler, faster, and more reliable at startup. This is a deliberate **read-side bypass** — a pattern common in CQRS systems where read models are accessed directly from the store rather than through the command-owning service.

### Isolation

The `MarketReadDbContext` in the Infrastructure layer maps a flat `StockProjection` using a keyless entity backed by an inline SQL query:

```sql
SELECT s.symbol, s.exchange, a.currency, a.name
FROM assets a
INNER JOIN stocks s ON a.id = s.id
WHERE a.is_active = 1
```

This context is registered with `NoTracking` behavior and change tracking disabled. It has zero write capability — it cannot accidentally mutate the market database.

The `StockProjection` type is `internal` to the Infrastructure project. The Application layer only sees the clean `StockInfo` record returned by `IStockLoader`.

### Lifecycle Scope

`IStockLoader` is registered as **scoped** because it depends on a scoped `MarketReadDbContext`. However, `SimulationEngine` is a singleton — it holds the in-memory order books for the lifetime of the process. To reconcile these two lifetimes, the `OrderBookSimulatorWorker` uses `IServiceScopeFactory` to create a short-lived scope at startup:

```
Worker.ExecuteAsync
  └─ CreateAsyncScope()
       └─ IStockLoader.LoadAllActiveAsync()   ← scoped DbContext
  └─ SimulationEngine.Initialize(stocks)       ← populates singleton
  └─ simulation loop (no DB dependency)
```

After initialization, the worker never touches the database again. The simulation loop is a pure in-memory computation.

---

## 5. Resemblance to Real Market Data Feeds

The architecture of this simulator closely mirrors the design of professional market data distribution systems.

### Level 2 Data (Market Depth)

In production, exchanges publish order book state via binary protocols such as:

- **NASDAQ ITCH 5.0** — UDP multicast, ~1 million messages/second per symbol
- **CME MDP 3.0** — SBE-encoded incremental refresh messages
- **FIX/FAST** — Financial Information eXchange with FAST compression

In all of these protocols, the message structure is identical to the `OrderBookUpdate` event emitted here: a tuple of `(symbol, side, price, size, action)`. Consumers maintain their own in-memory book by applying each delta, exactly as the downstream consumer of this event stream would.

### Simulated Market Microstructure

The simulation models core market microstructure behaviors:

| Real Phenomenon | Simulation Equivalent |
|---|---|
| Quote revisions | `Update` actions on existing levels |
| Cancelled orders | `Remove` actions |
| New limit orders | `Add` actions |
| Price discovery | Random-walk mid-price drift |
| Spread maintenance | Bid prices always < mid, ask prices always > mid |
| Market depth limits | `MaxLevels` cap causing forced removals |

### Concurrency as a Proxy for Feed Parallelism

A real market data provider — whether an exchange direct feed or a consolidated tape aggregator — receives and distributes updates for all instruments in parallel. There is no serialisation point between, say, AAPL and MSFT updates arriving on different multicast groups. A single feed handler process maintains hundreds of in-memory books concurrently, updating them as messages arrive from multiple network queues simultaneously.

This simulator replicates that property deliberately:

- **`ConcurrencyLevel = 50`** concurrent tasks fire per batch, each targeting an independently chosen symbol. No instrument blocks another.
- Per-symbol `lock` objects ensure that two tasks targeting the *same* instrument are serialised at the book level — matching exchange semantics where updates to a single instrument are processed in sequence by the matching engine, but updates to different instruments are fully independent.
- The 1–10 ms inter-batch delay reproduces bursty arrival patterns: real feeds do not deliver messages at a perfectly uniform rate. Messages arrive in bursts determined by trading activity, followed by quieter periods.

The result is a publisher that stresses downstream consumers — Kafka producers, topic partitioners, and event consumers — in a way that resembles actual market data ingestion pressure, while remaining runnable on commodity hardware.

### Event-Driven Architecture

The `IOrderBookEventPublisher` abstraction mirrors the role of an exchange feed handler gateway. The current `LoggingOrderBookEventPublisher` is a structured-log implementation used during development. The interface is designed to be replaced by a Kafka producer once the event bus (TBD per `ai-context.md`) is decided:

```
IOrderBookEventPublisher
  ├── LoggingOrderBookEventPublisher     ← development (current)
  └── KafkaOrderBookEventPublisher       ← production (future)
```

Swapping implementations requires only a single DI registration change in `Program.cs`. No application or domain code changes.

---

## 6. Scaling Strategies

### Vertical Scaling (Single Instance)

The current implementation uses `Random.Shared` (a thread-safe shared instance introduced in .NET 6) for all random number generation. Each `TickAsync` call dispatches `ConcurrencyLevel` tasks concurrently. Contention between tasks targeting the same symbol is resolved by a per-symbol `lock` — tasks targeting different symbols are fully parallel with zero synchronisation overhead between them.

To push throughput further on a single instance, `ConcurrencyLevel` can be increased or the inter-batch delay removed. The practical upper bound is determined by the Kafka producer throughput, not by CPU. If the event bus becomes the bottleneck, consider batching publishes: accumulate all updates from a batch into a single `ProduceAsync` call before awaiting, reducing round-trip overhead.

### Horizontal Scaling (Multiple Instances)

To run multiple worker instances without duplicate event publishing:

**Strategy 1 — Symbol Partitioning**

Each worker instance is assigned a subset of stock symbols via configuration:

```json
"Simulation": {
  "SymbolPartition": { "Index": 0, "Total": 4 }
}
```

On startup, `StockLoader` returns all stocks; the engine filters to its partition using `symbol.GetHashCode() % Total == Index`. This is the same approach Kafka's consumer groups use for partition assignment.

**Strategy 2 — Kafka Partitioning**

If the event bus is Kafka, map each stock symbol to a Kafka partition (`symbol → partition` by consistent hash). Each worker instance is a Kafka consumer group member and owns a subset of partitions. Kafka guarantees that only one consumer in a group processes a given partition, providing natural exclusivity without coordination.

**Strategy 3 — Leader Election**

For coordination-heavy scenarios, use a distributed lock (Redis `SETNX`, Kubernetes leader election via leases) to elect a single simulation leader. This is simpler operationally but introduces a single point of failure.

### Event Bus Throughput

At 50 concurrent updates per batch × ~182 batches/second (avg 5.5 ms between batches), the expected peak event rate is approximately **9,000 messages/second**. A single Kafka broker handles millions of messages per second without partitioning. The current implementation is not the throughput bottleneck — the inter-batch delay intentionally caps rate to simulate realistic market data cadence. Reducing `MinDelayMs` and `MaxDelayMs` toward zero, or increasing `ConcurrencyLevel`, can push the simulator to hundreds of thousands of updates per second, which remains within Kafka's capacity.

### Observability

Each `OrderBookUpdate` includes a `Timestamp` field. Downstream consumers can calculate end-to-end latency by comparing event `Timestamp` to consumption time. The `LoggingOrderBookEventPublisher` emits structured log events compatible with OpenTelemetry log exporters, enabling aggregation in Grafana or Elastic without code changes.

---

## 7. Architecture Overview

```
workers/orderbook-simulator/
├── src/
│   ├── PramosClearing.OrderBook.Domain
│   │   ├── Entities/
│   │   │   ├── OrderBook.cs          # Aggregate: maintains bid/ask depth, generates updates
│   │   │   └── OrderBookEntry.cs     # Price level: Price value object + Quantity value object
│   │   ├── ValueObjects/
│   │   │   ├── Price.cs              # Positive decimal, immutable
│   │   │   └── Quantity.cs           # Positive integer, immutable
│   │   ├── Enums/
│   │   │   ├── OrderSide.cs          # Bid | Ask
│   │   │   └── OrderAction.cs        # Add | Update | Remove
│   │   └── Events/
│   │       └── OrderBookUpdate.cs    # Immutable record: the published delta
│   │
│   ├── PramosClearing.OrderBook.Application
│   │   ├── Models/
│   │   │   └── StockInfo.cs          # Read model passed to SimulationEngine.Initialize
│   │   ├── Options/
│   │   │   └── SimulationOptions.cs  # MinDelayMs, MaxDelayMs
│   │   ├── Ports/
│   │   │   ├── IStockLoader.cs       # Abstraction over the market DB read
│   │   │   └── IOrderBookEventPublisher.cs  # Abstraction over the event bus
│   │   └── Services/
│   │       └── SimulationEngine.cs   # Owns all OrderBook instances, orchestrates ticks
│   │
│   ├── PramosClearing.OrderBook.Infrastructure
│   │   ├── ConnectionStringsOptions.cs
│   │   ├── Persistence/
│   │   │   ├── MarketReadDbContext.cs    # Read-only EF context; no migrations, no tracking
│   │   │   └── StockProjection.cs       # Keyless entity: output of the JOIN query
│   │   ├── StockLoader.cs               # Implements IStockLoader via MarketReadDbContext
│   │   └── Publishers/
│   │       └── LoggingOrderBookEventPublisher.cs  # Implements IOrderBookEventPublisher
│   │
│   └── PramosClearing.OrderBook.Worker
│       ├── Program.cs                   # DI composition root
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       └── Workers/
│           └── OrderBookSimulatorWorker.cs  # BackgroundService: init → loop
│
└── tests/
    └── PramosClearing.OrderBook.Tests
        ├── OrderBookTests.cs
        ├── PriceTests.cs
        ├── QuantityTests.cs
        └── SimulationEngineTests.cs
```

### Dependency Graph

```
Worker  ──────────────────────────────────────────► Application
  │                                                       │
  │                                                       ▼
  └──────────────────────────────────────────────────► Domain
                                                          ▲
Infrastructure ───────────────────────────────────────────┘
     └──────────────────────────────────────────────► Application
```

No upward dependencies. Domain has zero external package references.

---

## 8. Configuration Reference

| Key | Default | Description |
|---|---|---|
| `ConnectionStrings:MarketConnection` | (required) | SQL Server connection string for the market database |
| `Simulation:MinDelayMs` | `1` | Minimum inter-batch delay (ms). At 50 threads this is ~50,000 updates/s upper bound |
| `Simulation:MaxDelayMs` | `10` | Maximum inter-batch delay (ms). At 50 threads this is ~5,000 updates/s lower bound |

The `ConcurrencyLevel` constant (`50`) is set in `SimulationEngine` and controls how many order book updates are generated and published in parallel per batch. It is intentionally not externalised to configuration — it is a design parameter, not an operational tuning knob. Increase it in code when the target environment has more CPU cores or a higher-throughput event bus.

Environment variable override syntax (Docker/Kubernetes):

```
ConnectionStrings__MarketConnection=<value>
Simulation__MinDelayMs=1
Simulation__MaxDelayMs=10
```
