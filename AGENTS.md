# Repository Guidelines

## Project Structure & Module Organization

This .NET 8 ASP.NET Core MVC solution has four layers under `src/`: `Domain` holds entities and rules; `Application` holds services, DTOs, and persistence interfaces; `Infrastructure` implements EF Core stores, migrations, and integrations; `Web` contains controllers, Razor views, Areas, SignalR, and static assets in `wwwroot/`. Keep business rules in Domain or Application and HTTP concerns in Web. Four test projects live under `tests/`. Design and deployment notes are in `docs/`; use case diagrams are in `tai_lieu_usecase/`.

## Build, Test, and Development Commands

Run from the repository root with the .NET 8 SDK:

- `dotnet restore QuanLyChoThuePhongTroWeb.sln` restores NuGet packages.
- `dotnet build QuanLyChoThuePhongTroWeb.sln` compiles the solution.
- `dotnet watch run --project src/QuanLyChoThuePhongTroWeb.Web` starts the site with reload.
- `dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests` and `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests` run database-free unit suites.
- `dotnet test QuanLyChoThuePhongTroWeb.sln` runs all four test projects after configuring a dedicated PostgreSQL test database.

## Coding Style & Naming Conventions

Follow nearby C# code: four-space indentation, PascalCase for types and methods, camelCase for parameters and locals, and `I` prefixes for interfaces (`IPhongTroStore`). Use `*Tests.cs` for test files. Nullable reference types and implicit usings are enabled. There is no repository-wide formatter or lint configuration; keep edits consistent with the surrounding file. Preserve layer boundaries: Application must not depend on EF Core or ASP.NET Core, and Web must not reference Domain directly.

## Testing Guidelines

Tests use xUnit (`[Fact]` and `[Theory]`) with Coverlet available for coverage collection; no coverage threshold is configured. Add focused tests in the matching layer. Infrastructure and Web integration tests require `QLCTPT_TEST_CONNECTION_STRING`; the database name must end in `_test` or `_integration_test`. Their fixtures run migrations, so never point them at a development or production database.

## Commit & Pull Request Guidelines

Recent commits mix Vietnamese descriptions with prefixes such as `feat:`, `docs:`, `refactor:`, and `chore:`. Prefer a short, specific subject using one of those prefixes. In pull requests, summarize behavior and schema changes, link an issue or design note, list tests run, and include screenshots for UI changes. Review new EF migrations; do not edit existing migrations for a refactor.

## Configuration & Secrets

Copy `src/QuanLyChoThuePhongTroWeb.Web/appsettings.example.json` to `appsettings.json` for local use. Keep connection strings, API keys, and passwords out of commits; supply them through local configuration or environment variables. Web startup applies migrations and seeds data, so use a local database.
