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

The first start takes a few minutes while SQL Server initialises and migrations run.

### Service URLs

| Service | URL | Notes |
|---|---|---|
| **Market Service API** | http://localhost:5001/swagger | Asset catalogue and market data |
| **User Service API** | http://localhost:5002/swagger | Registration, auth, balances |
| **Kafka UI** | http://localhost:8090 | Browse topics, messages, consumer groups |

### Observing the OrderBook Simulator

Open **Kafka UI** at http://localhost:8090 and navigate to **Topics → orderbook-updates** to watch `OrderBookUpdate` messages arriving in real time.

To follow the worker logs directly:

```bash
docker compose logs -f orderbook-simulator
```

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
