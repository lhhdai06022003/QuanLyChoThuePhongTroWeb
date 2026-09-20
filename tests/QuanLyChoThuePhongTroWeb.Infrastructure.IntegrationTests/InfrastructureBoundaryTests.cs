using System;
using System.Linq;
using System.Reflection;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests
{
    public class InfrastructureBoundaryTests
    {
        private const string WebNamespace = "QuanLyChoThuePhongTroWeb.Web";

        [Fact]
        public void InfrastructureLayer_ShouldNotDependOnWeb()
        {
            var infrastructureAssembly = typeof(ApplicationDbContext).Assembly;
            var referencedAssemblies = infrastructureAssembly.GetReferencedAssemblies()
                .Select(a => a.Name)
                .ToList();

            Assert.DoesNotContain(referencedAssemblies, name => name != null && name.StartsWith(WebNamespace));
        }
    }
}
