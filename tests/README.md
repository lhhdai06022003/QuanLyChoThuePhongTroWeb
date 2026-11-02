# Automated tests

The solution contains two .NET 8 xUnit projects.

- `QuanLyChoThuePhongTroWeb.UnitTests/`: add isolated tests for calculations, permissions, and state transitions. Name files `*Tests.cs`.
- `QuanLyChoThuePhongTroWeb.IntegrationTests/`: add HTTP, service, and EF Core tests once a dedicated test database and app factory are configured. Never point these tests at the development or production database; `Program.cs` migrates and seeds on startup.

Run `dotnet test QuanLyChoThuePhongTroWeb.sln` from the repository root. The projects currently contain no test cases; add tests alongside the first feature rather than keeping template assertions.

The public deposit checkout script also has a Node.js regression suite with no package dependencies:

```powershell
node --test tests/browser/guest-dat-coc.test.cjs
```

It exercises checkout without jQuery, duplicate clicks, authenticated POST headers, rejected responses, network failures, and unsafe redirects. It does not use a database or contact a payment service.
