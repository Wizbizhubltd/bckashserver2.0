# BCKash MfB Portal — C# Rewrite

Phase 0 (foundation) of the BCKash core banking portal rewrite. See `BCKash.Api/BRD.md`, `BCKash.Api/FRD.md`, and `BCKash.Api/DEVELOPMENT_PHASES.md` for the full requirements and phased build plan.

## Solution layout

```
BCKash.Api               ASP.NET Core Web API host — controllers, auth wiring, Program.cs
BCKash.Application       Use-case contracts, DTOs (no implementation)
BCKash.Domain            Entities, enums, domain services — mirrors the legacy 74-table schema
BCKash.Infrastructure    EF Core DbContext, entity configurations, auth/audit implementations
BCKash.SharedKernel      Cross-cutting contracts (audit, result types, current-user)
tests/                   BCKash.Domain.Tests, BCKash.Application.Tests, BCKash.Api.IntegrationTests
BCKashWebClient           React/TypeScript SPA (branding reference — see below)
```

## Configuration

All secrets and environment-specific config live in a `.env` file at the repo root (gitignored —
never commit it). Copy the template and fill in real values:

```bash
cp .env.example .env
```

Keys that match a config section (e.g. `Jwt__SigningKey` → `Jwt:SigningKey`) use .NET's
double-underscore environment variable convention. `ConnectionStrings:BCKashDb` and
`Jwt:SigningKey` are required — the app fails fast at startup with a clear error if either is
missing. `appsettings.json`/`appsettings.Development.json` only carry non-secret defaults.

## Running with Docker (recommended)

Requires Docker and Docker Compose. Brings up the API, a MySQL 8 instance, and Redis together:

```bash
cp .env.example .env   # fill in real values first
docker compose up --build
```

The API is then reachable at `http://localhost:8080` (`API_PORT` in `.env`), MySQL at
`localhost:3307` (`DB_EXTERNAL_PORT`), Redis at `localhost:6380` (`REDIS_EXTERNAL_PORT`). All
three containers' data (MySQL data dir, Redis data dir, uploaded files) persist in named volumes
across restarts. Tear down with `docker compose down` (add `-v` to also drop the volumes).

Redis backs an EF Core second-level cache (`EFCoreSecondLevelCacheInterceptor`): every query
`BCKashDbContext` runs is cached in Redis, and every `SaveChanges` automatically invalidates the
cache entries for whichever tables it just wrote to — so every DB read and write across the API
routes through Redis without each service having to know about it. It's also registered as the
app's `IDistributedCache` (and a raw `IConnectionMultiplexer`) for anything that wants to use it
directly. Required at startup, like the DB connection — the app fails fast if it's missing.

Cache entries expire after 5 minutes even without a write (`CacheExpirationMode.Absolute`, tunable
in `BCKash.Infrastructure/DependencyInjection.cs`), and any `SaveChanges` invalidates the entries
for the tables it touched immediately — so a read right after a write always sees fresh data. The
main thing to be aware of running a financial system on this: it's a blanket, table-level cache
applied uniformly to every query, not tuned per entity. If a specific read turns out to need
guaranteed real-time consistency regardless of caching, exclude that one query with
`.NotCacheable()`.

## Local setup — API (without Docker)

Requires the .NET 10 SDK, a MySQL/MariaDB 8.x instance, and a Redis instance (e.g. the `db` and
`redis` services above, started with `docker compose up db redis`).

```bash
cp .env.example .env   # fill in real values first, ConnectionStrings__BCKashDb pointing at your MySQL
cd BCKash.Api
dotnet run
```

`Program.cs` loads `../.env` automatically via DotNetEnv when it's present, so no extra tooling
is needed for local dev outside Docker either.

Run the test suite (no MySQL needed — tests run against SQLite, see `DEVELOPMENT_PHASES.md` Phase 0 notes on why):

```bash
dotnet test BCKash.slnx
```

## Local setup — SPA

```bash
cd BCKashWebClient
npm install
npm run dev
```

`VITE_NEW_API_BASE_URL` points the new auth flow at the C# API (default `http://localhost:5000/api`); the existing `VITE_API_BASE_URL` is left pointed at the prior backend so already-built screens keep working during the module-by-module rewrite.
