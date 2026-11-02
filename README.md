# Ticketing

An ASP.NET Core 8 and PostgreSQL starter for the e-ticketing assessment. The initial milestone provides the ticket catalog, an EF Core migration, and a PostgreSQL integration test. Checkout, QR confirmation, ledger posting, and the React UI are the next milestones.

## Run the catalog

Install Docker Desktop or Docker Engine with Compose. From this directory run:

```sh
docker compose up --build -d
```

When the API is listening, open http://localhost:5080/api/tickets. The first build downloads the .NET and PostgreSQL images. Use `docker compose logs api` to follow startup.

| Ticket | Price in AED | Initial quota |
| --- | ---: | ---: |
| Gold | 100 | 100 |
| Premium | 200 | 100 |
| VIP | 500 | 100 |

Premium and VIP each use a demo quota of 100. EF applies the catalog migration on startup. Restarts preserve the database volume and available quota.

## Run the integration test

With the .NET 8 SDK installed and the Compose database running:

```sh
dotnet build
dotnet test --no-build
```

Tests use a separate local database named `ticketing_tests` on port 55432. The local Compose user can create it automatically. To point tests at another test database, set `TICKETING_TEST_DB`. The push workflow runs the same build and tests with a PostgreSQL service.

## Develop

Use `dotnet tool restore` for the pinned EF CLI. The catalog model and migrations are in `src/Ticketing.Api/`. To add a migration, set `ASPNETCORE_ENVIRONMENT` to `Development` and run `dotnet ef migrations add <Name> --project src/Ticketing.Api --output-dir Data/Migrations` from this directory.

Use `docker compose down` to stop the stack and preserve data.
