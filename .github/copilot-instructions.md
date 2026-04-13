# PramosClearing — Copilot Instructions

## Project Type
Microservices, .NET 8, Clean Architecture, DDD, CQRS.

---

## Architecture Rules

- API -> Application -> Domain
- Infrastructure depends on Application + Domain
- Domain must not depend on Infrastructure

---

## Domain Rules

- Asset is abstract base class
- Stock inherits from Asset
- Not all assets have Symbol
- Symbol exists only in Stock

---

## Coding Guidelines

- Use async/await everywhere
- Always support CancellationToken
- Prefer IReadOnlyList over List
- Avoid unnecessary allocations
- Avoid LINQ in hot paths

---

## API Guidelines

- Minimal API, use controller only when I ask to make a controller
- Return DTOs only
- Do not expose domain entities

---

## Application Layer

- Use CQRS
- Commands mutate state
- Queries are read-optimized
- No business logic in handlers

---

## Domain Layer

- Rich domain model
- No anemic entities
- Enforce invariants in constructors/methods

---

## Infrastructure

- EF Core with Fluent API
- No EF attributes in Domain
- Use AsNoTracking for reads

---

## Database Strategy

- SQL Server -> Master Data
- TimescaleDB -> Market Data

---

## Market Data Rules

- Price updates are event-driven
- Do not persist every tick directly via API
- Use streaming approach

---

## Workers

- Use BackgroundService
- Long-running loops
- In-memory state when needed (e.g. order books)

---

## Performance

- Avoid boxing
- Avoid reflection
- Prefer struct for small value objects when needed
- Minimize allocations in loops

---

## Naming

- Commands: CreateStockCommand
- Handlers: CreateStockCommandHandler
- Repositories: IStockRepository

---

## Do NOT

- Put business logic in API layer
- Use database entities outside Infrastructure
- Create generic repositories
- Mix read/write models
- Create comments

---

## Output Expectations

- Production-ready code
- Clean, minimal, no over-explanation
- Follow existing project patterns strictly
