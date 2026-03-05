# AI Context — PramosClearing Monorepo

This file is the authoritative reference for any AI assistant (GitHub Copilot, Claude, GPT, etc.)
working on this codebase. Read it before generating, reviewing, or refactoring any code.

---

## Non-Negotiable Rules

- **No comments in generated code.** No XML docs (`///`), no inline comments (`//`), no block comments (`/* */`).
- No parameterless constructors in domain entities.
- No infrastructure concerns inside Domain or Application layers.
- No breaking changes to existing public contracts without explicit instruction.
- No unnecessary abstractions — only introduce interfaces when there is a concrete reason (testability, multiple implementations, DI).

---

## Repository Layout

```
pramos-clearing/
├── docs/                     # Architecture docs and AI context
├── infra/                    # Docker, Kubernetes, Observability configs
├── gateway/                  # API Gateway
├── services/                 # Bounded contexts (microservices)
├── workers/                  # Background workers
└── shared/                   # Cross-cutting building blocks
```

---

## Architecture

### Patterns
Domain-Driven Design + Clean Architecture + CQRS + Event-Driven

### Dependency Rule

```
API → Application → Domain
Infrastructure → Domain + Application
```

- Domain has zero external dependencies.
- Application depends only on Domain abstractions.
- Infrastructure implements those abstractions.
- API wires everything via DI.

---

## Services

Each service follows this structure:

```
{service}-service/
├── {service}-service.slnx
├── src/
│   ├── PramosClearing.{Service}.API             # Controllers, middleware, DI wiring
│   ├── PramosClearing.{Service}.Application     # Commands, Queries, Handlers (MediatR)
│   ├── PramosClearing.{Service}.Domain          # Entities, Value Objects, Repository interfaces
│   └── PramosClearing.{Service}.Infrastructure  # EF Core, external integrations
└── tests/
    └── PramosClearing.{Service}.Tests
```

### Active Services

| Service | Responsibility |
|---|---|
| `user-service` | Identity and user management |
| `order-service` | Order lifecycle and matching |
| `portfolio-service` | Position and portfolio tracking |
| `market-service` | Financial asset catalogue and market data |

---

## Domain Layer

- All entities inherit from an `abstract` base when sharing common properties.
- Properties use `private` or `protected` setters.
- Constructors enforce valid state via `ArgumentException.ThrowIfNullOrWhiteSpace` and guard clauses.
- No data annotations — all validation lives in constructors.
- Repository interfaces live in `Domain/Repositories/`.
- Enums live in `Domain/Enums/` or alongside their entity in `Domain/Entities/`.
- EF-only parameterless constructors assign `null!` to non-nullable properties, never used in domain logic.

### market-service Domain Example

```
Domain/
├── Entities/
│   ├── Asset.cs        # abstract base
│   ├── AssetType.cs    # enum discriminator
│   ├── Stock.cs
│   ├── Etf.cs
│   └── Crypto.cs
└── Repositories/
    └── IStockRepository.cs
```

---

## Application Layer

- Commands and Queries are `sealed record` types implementing `IRequest<T>` from MediatR.
- Handlers are `sealed class` implementing `IRequestHandler<TRequest, TResponse>`.
- Request DTOs live in `Application/Commands/Requests/` or `Application/Queries/Requests/`.
- `ConfigureAwait(false)` is mandatory on all awaited calls.
- No EF Core, no infrastructure types referenced.
- Normalisation of inputs (e.g. `ToUpperInvariant`) happens in the handler before constructing entities.

---

## Infrastructure Layer

- `IEntityTypeConfiguration<T>` for every entity.
- Table names are lowercase snake_case.
- All indexes have explicit `HasDatabaseName(...)`.
- `ValueGeneratedNever()` on domain-assigned Guids.
- Repository implementations live in `Infrastructure/Persistence/Repositories/`.
- EF package: `Microsoft.EntityFrameworkCore.Relational` for relational API (`ToTable`, `IsFixedLength`, `HasDatabaseName`).

---

## API Layer

- Controllers are `sealed`.
- Return `TypedResults.*` — never `IActionResult` (avoids boxing).
- No business logic in controllers.
- Domain exceptions are caught at the controller boundary and mapped to HTTP status codes.
- Request DTOs come from `Application/Commands/Requests/` or `Application/Queries/Requests/` — never defined inline in the controller file.

---

## Workers

```
workers/
└── price-generator/
    └── src/
        └── PramosClearing.PriceGenerator.Worker   # .NET BackgroundService
```

---

## Shared / Building Blocks

```
shared/building-blocks/
└── src/
    ├── EventBus        # Integration event contracts and bus abstraction
    ├── SharedKernel    # Base entity, value object, domain event primitives
    ├── Contracts       # Shared DTOs and integration event definitions
    └── Observability   # OpenTelemetry setup, structured logging helpers
```

---

## Naming Conventions

| Artefact | Convention | Example |
|---|---|---|
| Entity | PascalCase noun | `Stock`, `Order` |
| Command | PascalCase + `Command` | `CreateStockCommand` |
| Query | PascalCase + `Query` | `GetStockBySymbolQuery` |
| Handler | Command/Query name + `Handler` | `CreateStockCommandHandler` |
| Repository interface | `I` + Entity + `Repository` | `IStockRepository` |
| Request DTO | PascalCase + `Request` | `CreateStockRequest` |
| EF Config | Entity + `Configuration` | `StockConfiguration` |
| DB table | lowercase snake_case | `stocks`, `order_items` |
| DB index | `ix_{table}_{columns}` | `ix_stocks_symbol_exchange` |

---

## Technology Stack

| Concern | Technology |
|---|---|
| Runtime | .NET 8 |
| Web | ASP.NET Core 8 |
| ORM | Entity Framework Core 8 (Relational) |
| CQRS / Messaging | MediatR 12 |
| Event Bus | TBD — RabbitMQ or Kafka |
| Observability | OpenTelemetry + Structured Logging |
| Testing | xUnit |
| Containerisation | Docker + Kubernetes |
