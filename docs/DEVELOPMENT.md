# Development

## Prerequisites
- .NET SDK 10.0 (`global.json` pins the 10.0 feature band)
- PostgreSQL 16+ (local) – development connection string in `appsettings.Development.json` (`postgres`/`postgres`)

## Run
```bash
dotnet run --project src/BurhanNexus.HMS.Web
```
Open http://localhost:5000 – the setup wizard appears on an empty database.

## Tests
Integration tests start the real application (`WebApplicationFactory`) against a **real PostgreSQL** database; each test uses its own temporary database which is dropped afterwards.
```bash
export HMS_TEST_PG="Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres"
dotnet test
```
`pg_dump`/`pg_restore` must be installed for the backup tests.

## Database migrations
```bash
dotnet tool install --global dotnet-ef --version "10.*"
dotnet ef migrations add <Name> --project src/BurhanNexus.HMS.Infrastructure \
  --startup-project src/BurhanNexus.HMS.Web --output-dir Persistence/Migrations
```

## Continuous integration
`.github/workflows/ci.yml` on every push:
1. builds the solution with .NET 10,
2. runs all integration tests against PostgreSQL 16,
3. publishes the app and runs a full browser walk-through (`tools/ui-check/screenshots.mjs`, Playwright) producing screenshots,
4. publishes the **self-contained Windows server package** (artifact `BurhanNexus-HMS-Server-win-x64`, download from the Actions run page),
5. stores logs and screenshots on the `ci-results` branch.

## Conventions
- Business rules live in Infrastructure services; pages stay thin.
- Every state-changing service method writes an audit entry.
- Every page/API declares a permission; handlers re-check action-level permissions.
- No inline `<script>` (CSP); UI behaviour goes in `wwwroot/js/site.js` using `data-*` attributes.
