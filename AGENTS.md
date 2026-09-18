# Repository Guidelines

## Project Structure & Module Organization

This is one .NET 8 ASP.NET Core MVC application. `Areas/QuanLyNhaTro/` serves managers and staff; `Areas/KhachThue/` serves tenants. `Areas/KhachVangLai/` is reserved for the future public rental flow. Place versioned JSON endpoints and request/response DTOs in `Api/V1/Controllers/` and `Api/V1/Contracts/`. MVC and API controllers should call shared business services rather than each other. Existing controllers, models, services, filters, and helpers remain at the repository root. `Data/ApplicationDbContext.cs` defines EF Core persistence, `Migrations/` stores schema changes, and `wwwroot/` contains static assets. Keep feature designs and the [60-day roadmap](docs/roadmap-60-ngay.md) in `docs/`.

## Build, Test, and Development Commands

Install the .NET 8 SDK and PostgreSQL. Copy `appsettings.example.json` to ignored `appsettings.json` and configure `ConnectionStrings:DefaultConnection` plus the integrations you use.

- `dotnet restore QuanLyChoThuePhongTroWeb.sln` restores NuGet packages.
- `dotnet build QuanLyChoThuePhongTroWeb.sln` compiles the web and test projects.
- `dotnet test QuanLyChoThuePhongTroWeb.sln` runs automated tests once test cases exist.
- `dotnet ef database update` applies EF Core migrations; startup also attempts migration.
- `dotnet watch run --project QuanLyChoThuePhongTroWeb.csproj` runs the site with reload.

## Coding Style & Naming Conventions

Use four spaces in C#, PascalCase for types/members, and `I` prefixes for interfaces (for example, `IHoaDonService`). Keep controllers focused on HTTP handling; place calculations, state transitions, and database access in services. Match Razor view folders to controllers. Follow nearby formatting; the repository has no shared `.editorconfig`. Avoid editing bundled files under `wwwroot/Theme/`.

## Testing Guidelines

`tests/QuanLyChoThuePhongTroWeb.UnitTests/` is for isolated business rules. `tests/QuanLyChoThuePhongTroWeb.IntegrationTests/` is for HTTP and database flows; use a separate test database because application startup migrates and seeds data. Name test files `*Tests.cs`. Add meaningful tests alongside each new feature; there is no coverage percentage requirement yet.

## Commit & Pull Request Guidelines

Recent commits use short, descriptive Vietnamese subjects without a mandatory prefix. Keep commits focused. Pull requests should explain behavior, list checks run, identify migrations/configuration changes, link an issue when available, and include screenshots for UI changes.

## Security & Configuration

Keep database passwords, SMTP credentials, and API keys out of Git. Commit placeholders only in `appsettings.example.json`; local `appsettings.json` is ignored.
