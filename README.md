# PramosClearing

**PramosClearing** is a fictional stock exchange clearing system built to simulate asset trading operations, portfolio management, and real-time price tracking.

---

## 📋 Domain

Users of the platform can:

- **Create an account** – Register with secure authentication.
- **Receive a fictional balance** – Starting balance for trading simulation.
- **Buy stocks** – Acquire assets available on the simulated exchange.
- **Sell stocks** – Close positions and return funds to the account.
- **View portfolio** – Inspect the investment portfolio with positions and returns.
- **Track prices in real time** – Continuous price streaming via WebSocket/events.

---

## 🛠️ Technology Stack

| Layer | Technology |
|---|---|
| **Backend** | C# .NET |
| **API** | ASP.NET Core |
| **Messaging** | Apache Kafka / RabbitMQ |
| **Cache** | Redis |
| **Container** | Docker |
| **Orchestration** | Kubernetes |
| **Observability** | Prometheus + Grafana |

### Details

- **C# .NET** – All business logic is implemented in .NET, leveraging the mature Microsoft ecosystem for high performance and maintainability.
- **ASP.NET Core** – Exposes REST and WebSocket endpoints consumed by front-end clients and external integrations.
- **Apache Kafka / RabbitMQ** – Asynchronous service-to-service communication (e.g. order execution, price updates, notifications). Kafka is preferred for high-throughput streams and event replay; RabbitMQ for flexible routing with lower latency.
- **Redis** – Distributed cache for recent quotes, user sessions, and frequently read portfolio data, reducing load on the primary databases.
- **Docker** – Packages each service into a container image, ensuring parity across development, test, and production environments.
- **Kubernetes** – Orchestrates containers in production, providing auto-scaling, self-healing, rolling updates, and secrets/config management.
- **Prometheus + Grafana** – Collects application and infrastructure metrics (Prometheus) and visualises them in interactive dashboards (Grafana), with alert support.

---

## 🏗️ High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                          Clients                                │
│                  (Web App / Mobile / CLI)                       │
└───────────────────────────────┬─────────────────────────────────┘
                                │ HTTP / WebSocket
┌───────────────────────────────▼─────────────────────────────────┐
│                    ASP.NET Core API Gateway                      │
└──────┬──────────────┬──────────────┬──────────────┬─────────────┘
       │              │              │              │
┌──────▼──────┐ ┌─────▼──────┐ ┌────▼──────┐ ┌────▼───────────┐
│    User     │ │   Market   │ │  Order    │ │   Portfolio    │
│   Service   │ │  Service   │ │  Service  │ │    Service     │
│             │ │            │ │           │ │                │
│ - register  │ │ - assets   │ │ - accepts │ │ - user         │
│ - auth      │ │ - prices   │ │   orders  │ │   position     │
│ - balance   │ │   current  │ │           │ │ - total value  │
└──────┬──────┘ └──────▲─────┘ └─────┬─────┘ └────────────────┘
       │               │             │
       │        ┌──────┴──────┐      │
       │        │    Price    │      │
       │        │  Generator  │      │
       │        │             │      │
       │        │ - simulates │      │
       │        │   market    │      │
       │        └──────┬──────┘      │
       │               │             │
       └───────────────▼─────────────┘
                  Apache Kafka
                  (Event Bus)
         Topics: OrderExecuted · PriceUpdated
                       │
              ┌────────▼────────┐
              │  Notification   │
              │    Service      │
              │                 │
              │ listens:        │
              │ - OrderExecuted │
              │ - PriceUpdated  │
              └─────────────────┘
       ┌──────────────┬──────────────┐
       │              │              │
┌──────▼──────┐ ┌─────▼─────┐ ┌─────▼──────┐
│  Database   │ │   Redis   │ │ Prometheus │
│  (SQL/TS)   │ │  (Cache)  │ │ + Grafana  │
└─────────────┘ └───────────┘ └────────────┘
```

### Microservices

| Service | Responsibilities |
|---|---|
| **User Service** | User registration, authentication, and fictional balance provisioning |
| **Market Service** | Asset catalogue and current price queries |
| **Price Generator** | Simulates market behaviour, publishing price updates to Kafka (`PriceUpdated`) |
| **Order Service** | Accepts buy/sell orders, validates them, and publishes `OrderExecuted` to Kafka |
| **Portfolio Service** | Maintains user positions and calculates total portfolio value |
| **Notification Service** | Consumes Kafka events (`OrderExecuted`, `PriceUpdated`) and notifies clients |

The entire stack is packaged with **Docker** and orchestrated via **Kubernetes**.

---

## OrderBook Simulator Worker

The `orderbook-simulator` is a .NET `BackgroundService` that continuously generates synthetic Level 2 market data and publishes it to Kafka. It is designed to approximate, at small scale, the message cadence of a real market data provider.

- **50 concurrent tasks** fire per batch — each independently selects a symbol, generates an `OrderBookUpdate` (add / modify / remove on a price level), and publishes to the `orderbook-updates` topic. This mirrors how professional feed handlers process multiple instruments in parallel with no sequential bottleneck between them.
- **1–10 ms inter-batch delay** reproduces the bursty arrival pattern of real exchange feeds. Combined with 50-way concurrency this yields roughly **5,000–50,000 updates/second**, constrained in practice by Kafka producer throughput.
- Each instrument keeps an in-memory order book with a random-walk mid-price, bid/ask depth capped at 10 levels, and crossed-market prevention.

Full design rationale is in [`docs/orderbook-simulator.md`](docs/orderbook-simulator.md).

---

## 🗄️ Database Architecture

PramosClearing intentionally uses **polyglot persistence** — two purpose-built databases, each matched to its workload.

| Database | Workload | Characteristics |
|---|---|---|
| **SQL Server** | Master data | Low write frequency · relational · strong consistency · ACID transactions |
| **TimescaleDB** | Market time-series data | Very high write frequency · append-only · time-range queries · retention policies |

**SQL Server** stores the reference entities the system depends on: assets, stocks, exchanges, currencies, users, orders, and portfolios. This data is relational by nature — a stock belongs to an exchange, an order references an asset — and requires foreign key constraints, unique indexes, and multi-table ACID transactions. Entity Framework Core 8 manages schema and queries for this layer.

**TimescaleDB** (a PostgreSQL extension) stores the continuous stream of price ticks and OHLCV candles generated by the price simulation. At peak load the system produces tens of thousands of ticks per second; TimescaleDB's automatic time-based hypertable partitioning keeps inserts O(1) regardless of total table size. Continuous aggregates materialise candles incrementally, compression policies shrink historical data by up to 95 %, and retention policies drop old chunks automatically.

Separating the two workloads means each database is sized and scaled independently. A tick ingestion spike does not compete with master data reads, and a SQL Server maintenance window does not affect market data availability.

Full rationale is documented in [`docs/adr-001-database-strategy.md`](docs/adr-001-database-strategy.md) and [`docs/database-architecture.md`](docs/database-architecture.md).

---

## 🚀 Running Locally

> **Prerequisites:** Docker and Docker Compose installed.

```bash
# Clone the repository
git clone https://github.com/PedroHRamos/PramosClearing.git
cd PramosClearing

# Build and start all services
docker compose up -d --build
```

The first start takes a few minutes while SQL Server initialises and TimescaleDB applies the bootstrap SQL files from `infra/db/timescaledb/`.

### Service URLs

| Service | URL | Notes |
|---|---|---|
| **Market Service API** | http://localhost:5001/swagger | Asset catalogue and market data |
| **User Service API** | http://localhost:5002/swagger | Registration, auth, balances |
| **Kafka UI** | http://localhost:8090 | Browse topics, messages, consumer groups |

### Local database ports

| Database | Port | Notes |
|---|---|---|
| **SQL Server** | `1433` | Master data store |
| **TimescaleDB** | `5432` | Time-series store initialised from `infra/db/timescaledb/` |

### Observing the OrderBook Simulator

Open **Kafka UI** at http://localhost:8090 and navigate to **Topics → orderbook-updates** to watch `OrderBookUpdate` messages arriving in real time.

To follow the worker logs directly:

```bash
docker compose logs -f orderbook-simulator
```

The simulator is configured for **10 prices per second** by default (`ConcurrencyLevel: 1`, `MinDelayMs: 100`, `MaxDelayMs: 100`). To change the rate, edit `Simulation` in the worker's `appsettings.json`:

| Goal | MinDelayMs | MaxDelayMs | ConcurrencyLevel | Approx. prices/sec |
|---|---|---|---|---|
| 10/s (default) | 100 | 100 | 1 | ~10 |
| 100/s | 10 | 10 | 1 | ~100 |
| 1 000/s | 10 | 10 | 10 | ~1 000 |
| 5 000/s (original) | 1 | 10 | 50 | ~5 000–50 000 |

---

## 🔥 Load Testing

> **Prerequisites:** [k6](https://k6.io/docs/get-started/installation/) installed and all services running via `docker compose up -d --build`.

Load tests live in [`tests/k6/`](tests/k6/). They target the **Market Service** read paths — the architectural hotspot when price simulation is running.

### Measured baseline (July 2026)

Tests were executed locally against the full Docker Compose stack. Payload: `/api/price-ticks` returning **1 000 rows per request** (`TICK_TAKE=1000`).

| RPS | p50 | p90 | p95 | Error rate | Data/s | Result |
|---|---|---|---|---|---|---|
| 100 | 6 ms | 10 ms | 11 ms | 0 % | 12 MB/s | ✅ Comfortable |
| 500 | 12 ms | 58 ms | 89 ms | 0 % | 58 MB/s | ✅ Healthy |
| **600** | **22 ms** | **773 ms** | **1 020 ms** | **0 %** | **69 MB/s** | ⚠️ Max supported |
| 800 | 3 900 ms | 5 560 ms | 30 730 ms | 0 % | 76 MB/s | ⚠️ Saturated |
| 1 500 | 17 800 ms | 37 000 ms | 44 690 ms | 61 % | 28 MB/s | ❌ Collapsed |

**Operational safe limit without tuning: ~600 RPS × 1 000 ticks.** Above this threshold the Npgsql connection pool saturates and latency increases non-linearly. Full diagnosis and improvement roadmap: [`docs/adr-002-market-service-performance.md`](docs/adr-002-market-service-performance.md).

### Running the tests

```bash
# Steady-state baseline (50 RPS, 60 s)
k6 run tests/k6/market-baseline.js

# Heavy read — replicate the benchmark above
k6 run -e TARGET_RPS=500 -e TICK_TAKE=1000 tests/k6/market-heavy-read.js

# Stress ramp (10 → 500 VUs)
k6 run tests/k6/market-stress.js

# Instant spike to 2 000 VUs
k6 run tests/k6/market-spike.js

# Soak test (~10 min)
k6 run tests/k6/market-soak.js
```

### Kafka ingest pipeline stress test

Stresses the **write path**: `Kafka producer → consumer → TimescaleDB`.  
Answers: *at what events/sec does the consumer start accumulating lag?*

Uses a **producer sidecar** (`tests/k6/producer-sidecar/`) — a tiny Node.js service that bridges k6 HTTP calls to the native Kafka protocol via kafkajs. The Kafka UI REST API was discarded: it is a debug tool that saturates at ~100 RPS.

**Prerequisites:**

```bash
# 1. Start the full stack
docker compose up -d --build

# 2. Build and start the producer sidecar (exposes :3001 on localhost)
docker build -t k6-producer-sidecar tests/k6/producer-sidecar
docker run --rm -d --name k6-producer --network pramosclearing_default -p 3001:3000 k6-producer-sidecar
```

**Monitoring consumer lag — Kafka UI:**

Open **http://localhost:8090** → **Consumer Groups** → **market-price-tick-consumer**.
The *Messages Behind* column updates in real time and is the authoritative lag metric.

Alternatively, from the CLI:

```bash
docker exec pramosclearing-kafka-1 kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --group market-price-tick-consumer \
  --describe
```

**Scaling up — run each scenario and watch lag in Kafka UI:**

| Scenario | events/sec | Hypothesis | Terminal 1 command |
|---|---|---|---|
| ✅ Warm-up | 1 000 | Zero lag (confirmed) | `k6 run -e EVENTS_PER_SEC=1000 tests/k6/kafka-ingest-load.js` |
| Step 1 | 5 000 | Likely still zero lag | `k6 run -e EVENTS_PER_SEC=5000 -e DURATION=2m tests/k6/kafka-ingest-load.js` |
| Step 2 | 10 000 | Lag may start growing | `k6 run -e EVENTS_PER_SEC=10000 -e DURATION=2m tests/k6/kafka-ingest-load.js` |
| Step 3 | 20 000 | Consumer / TimescaleDB under pressure | `k6 run -e EVENTS_PER_SEC=20000 -e DURATION=2m tests/k6/kafka-ingest-load.js` |
| Step 4 | 50 000 | Expecting saturation | `k6 run -e EVENTS_PER_SEC=50000 -e DURATION=2m tests/k6/kafka-ingest-load.js` |
| Step 5 | 100 000 | Beyond single-node capacity (confirm collapse) | `k6 run -e EVENTS_PER_SEC=100000 -e DURATION=2m tests/k6/kafka-ingest-load.js` |

**How to read the results:**

- *Messages Behind* = 0 throughout → consumer keeps up, move to next step
- *Messages Behind* growing steadily → consumer falling behind — previous step was the sustainable limit
- `produce_errors > 1%` in k6 before lag grows → sidecar is the bottleneck, not the consumer; real consumer limit is higher

**Stop the sidecar when done:**

```bash
docker stop k6-producer
```

Record the breaking point in [`docs/adr-002-market-service-performance.md`](docs/adr-002-market-service-performance.md) as the write-path baseline, equivalent to the 600 RPS read limit already documented.

### Environment variables

| Variable | Default | Description |
|---|---|---|
| `BASE_URL` | `http://localhost:5001` | Market Service base URL |
| `TICKERS` | `AAPL@NASDAQ,MSFT@NASDAQ,…` | `symbol@exchange` pairs — must match seeded stocks |
| `TARGET_RPS` | `50` | Target requests/sec (baseline, heavy-read) |
| `DURATION` | `60s` | Test duration (baseline, heavy-read) |
| `TICK_TAKE` | `100` (heavy-read: `1000`) | Ticks fetched per `/price-ticks` call |
| `EVENTS_PER_SEC` | `1000` | Kafka events/sec injected by `kafka-ingest-load.js` |
| `BATCH_SIZE` | `10` | Events per HTTP request to the sidecar |

---

## 📈 Observability

- **Prometheus** scrapes metrics exposed at `/metrics` by each service.
- **Grafana** provides pre-configured dashboards accessible at `http://localhost:3000`.
- Alerts can be configured for API latency, order errors, and Kafka/RabbitMQ consumer lag.

---

## 🔮 Future Improvements

Planned platform evolutions include support for new financial instrument types:

- **Fixed Income** – Buy and sell government and corporate bonds with yield and maturity calculations.
- **Certificate of Deposit (CD)** – Trade bank certificates of deposit with fixed and floating return simulation.
- **Structured Notes** – Hybrid instruments combining fixed income with derivatives, enabling capital protection and multi-asset exposure.
- **Investment Funds** – Subscribe to and redeem fund units (fixed income, multi-market, equities) with NAV and management fee calculations.

---

## 📝 License

This project is for personal and educational use.
