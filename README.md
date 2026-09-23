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

## Local setup — API

Requires the .NET 10 SDK and a MySQL/MariaDB 8.x instance.

```bash
cd BCKash.Api
dotnet user-secrets set "ConnectionStrings:BCKashDb" "Server=localhost;Database=bckash;User=root;Password=<your-password>;"
dotnet user-secrets set "Jwt:SigningKey" "<a long random value, 32+ bytes>"
dotnet run
```

Both values are required — the app fails fast at startup with a clear error if either is missing, rather than booting into a broken state. Never commit real secrets to `appsettings.json`/`appsettings.Development.json`; those files only carry non-secret defaults and placeholders.

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
