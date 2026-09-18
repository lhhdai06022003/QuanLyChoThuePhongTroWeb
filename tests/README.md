# Automated tests

The solution contains two .NET 8 xUnit projects.

- `QuanLyChoThuePhongTroWeb.UnitTests/`: add isolated tests for calculations, permissions, and state transitions. Name files `*Tests.cs`.
- `QuanLyChoThuePhongTroWeb.IntegrationTests/`: add HTTP, service, and EF Core tests once a dedicated test database and app factory are configured. Never point these tests at the development or production database; `Program.cs` migrates and seeds on startup.

Run `dotnet test QuanLyChoThuePhongTroWeb.sln` from the repository root. The projects currently contain no test cases; add tests alongside the first feature rather than keeping template assertions.
