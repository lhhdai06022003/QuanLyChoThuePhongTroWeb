using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    public class ArchitectureBoundaryTests
    {
        private static string GetSolutionRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "QuanLyChoThuePhongTroWeb.sln")))
            {
                dir = dir.Parent;
            }
            Assert.NotNull(dir);
            return dir.FullName;
        }

        [Fact]
        public void Web_ProjectFile_DoesNotReference_DomainProject()
        {
            var slnRoot = GetSolutionRoot();
            var csprojPath = Path.Combine(slnRoot, "src", "QuanLyChoThuePhongTroWeb.Web", "QuanLyChoThuePhongTroWeb.Web.csproj");

            Assert.True(File.Exists(csprojPath), $"Web.csproj not found at: {csprojPath}");

            var doc = XDocument.Load(csprojPath);
            var projectReferences = doc.Descendants("ProjectReference")
                .Select(pr => (string?)pr.Attribute("Include"))
                .Where(inc => !string.IsNullOrEmpty(inc))
                .ToList();

            Assert.DoesNotContain(projectReferences, pr => pr!.Contains("QuanLyChoThuePhongTroWeb.Domain"));
        }

        [Fact]
        public void Web_ProjectFile_DoesNotReference_NpgsqlDirectly()
        {
            var slnRoot = GetSolutionRoot();
            var csprojPath = Path.Combine(slnRoot, "src", "QuanLyChoThuePhongTroWeb.Web", "QuanLyChoThuePhongTroWeb.Web.csproj");

            Assert.True(File.Exists(csprojPath), $"Web.csproj not found at: {csprojPath}");

            var doc = XDocument.Load(csprojPath);
            var packageReferences = doc.Descendants("PackageReference")
                .Select(pr => (string?)pr.Attribute("Include"))
                .Where(inc => !string.IsNullOrEmpty(inc))
                .ToList();

            Assert.DoesNotContain(packageReferences, pkg => pkg!.Contains("Npgsql"));
        }

        [Fact]
        public void Web_ProjectFile_DoesNotReference_EntityFrameworkCorePackages()
        {
            var slnRoot = GetSolutionRoot();
            var csprojPath = Path.Combine(slnRoot, "src", "QuanLyChoThuePhongTroWeb.Web", "QuanLyChoThuePhongTroWeb.Web.csproj");

            Assert.True(File.Exists(csprojPath), $"Web.csproj not found at: {csprojPath}");

            var doc = XDocument.Load(csprojPath);
            var packageReferences = doc.Descendants("PackageReference")
                .Select(pr => (string?)pr.Attribute("Include"))
                .Where(inc => !string.IsNullOrEmpty(inc))
                .ToList();

            Assert.DoesNotContain(packageReferences, pkg => pkg != null && pkg.StartsWith("Microsoft.EntityFrameworkCore"));
        }

        [Fact]
        public void Web_SourceCode_DoesNotUse_DomainNamespace()
        {
            var slnRoot = GetSolutionRoot();
            var webSourceDir = Path.Combine(slnRoot, "src", "QuanLyChoThuePhongTroWeb.Web");

            var csAndCshtmlFiles = Directory.GetFiles(webSourceDir, "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(webSourceDir, "*.cshtml", SearchOption.AllDirectories))
                .Where(f => !f.Contains(Path.Combine("wwwroot", "Theme")) && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .ToList();

            foreach (var file in csAndCshtmlFiles)
            {
                var content = File.ReadAllText(file);
                Assert.False(content.Contains("QuanLyChoThuePhongTroWeb.Domain"),
                    $"File {file} violates clean architecture boundary by using 'QuanLyChoThuePhongTroWeb.Domain'");
            }
        }

        [Fact]
        public void Domain_ProjectFile_HasZeroDependencies()
        {
            var slnRoot = GetSolutionRoot();
            var csprojPath = Path.Combine(slnRoot, "src", "QuanLyChoThuePhongTroWeb.Domain", "QuanLyChoThuePhongTroWeb.Domain.csproj");

            var doc = XDocument.Load(csprojPath);
            var projectReferences = doc.Descendants("ProjectReference").ToList();
            var packageReferences = doc.Descendants("PackageReference").ToList();

            Assert.Empty(projectReferences);
            Assert.Empty(packageReferences);
        }

        [Fact]
        public void Application_ProjectFile_DoesNotReference_InfrastructureOrWeb()
        {
            var slnRoot = GetSolutionRoot();
            var csprojPath = Path.Combine(slnRoot, "src", "QuanLyChoThuePhongTroWeb.Application", "QuanLyChoThuePhongTroWeb.Application.csproj");

            var doc = XDocument.Load(csprojPath);
            var projectReferences = doc.Descendants("ProjectReference")
                .Select(pr => (string?)pr.Attribute("Include"))
                .Where(inc => !string.IsNullOrEmpty(inc))
                .ToList();

            Assert.DoesNotContain(projectReferences, pr => pr!.Contains("QuanLyChoThuePhongTroWeb.Infrastructure") || pr!.Contains("QuanLyChoThuePhongTroWeb.Web"));
        }
    }
}
