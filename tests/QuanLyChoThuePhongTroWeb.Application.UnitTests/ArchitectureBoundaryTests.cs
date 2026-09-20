using System;
using System.Linq;
using System.Reflection;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class ArchitectureBoundaryTests
    {
        private const string DomainNamespace = "QuanLyChoThuePhongTroWeb.Domain";
        private const string ApplicationNamespace = "QuanLyChoThuePhongTroWeb.Application";
        private const string InfrastructureNamespace = "QuanLyChoThuePhongTroWeb.Infrastructure";
        private const string WebNamespace = "QuanLyChoThuePhongTroWeb.Web";

        [Fact]
        public void DomainLayer_ShouldNotDependOnOtherLayers()
        {
            var domainAssembly = typeof(HopDong).Assembly;
            var referencedAssemblies = domainAssembly.GetReferencedAssemblies()
                .Select(a => a.Name)
                .ToList();

            Assert.DoesNotContain(referencedAssemblies, name => name != null && name.StartsWith(ApplicationNamespace));
            Assert.DoesNotContain(referencedAssemblies, name => name != null && name.StartsWith(InfrastructureNamespace));
            Assert.DoesNotContain(referencedAssemblies, name => name != null && name.StartsWith(WebNamespace));
        }

        [Fact]
        public void ApplicationLayer_ShouldNotDependOnInfrastructureOrWeb()
        {
            var applicationAssembly = typeof(IHoaDonCalculatorService).Assembly;
            var referencedAssemblies = applicationAssembly.GetReferencedAssemblies()
                .Select(a => a.Name)
                .ToList();

            Assert.DoesNotContain(referencedAssemblies, name => name != null && name.StartsWith(InfrastructureNamespace));
            Assert.DoesNotContain(referencedAssemblies, name => name != null && name.StartsWith(WebNamespace));
        }

        [Fact]
        public void ApplicationLayer_ShouldNotDependOnHosting()
        {
            var applicationAssembly = typeof(IHoaDonCalculatorService).Assembly;
            var referencedAssemblies = applicationAssembly.GetReferencedAssemblies()
                .Select(a => a.Name)
                .ToList();

            Assert.DoesNotContain(referencedAssemblies, name => name != null && name.Contains("Hosting"));
        }

        [Fact]
        public void ApplicationLayer_SourceCode_DoesNotUseHostEnvironmentOrDirectFileIo()
        {
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "QuanLyChoThuePhongTroWeb.sln")))
            {
                dir = dir.Parent;
            }
            Assert.NotNull(dir);

            var appSourceDir = System.IO.Path.Combine(dir.FullName, "src", "QuanLyChoThuePhongTroWeb.Application");
            var csFiles = System.IO.Directory.GetFiles(appSourceDir, "*.cs", System.IO.SearchOption.AllDirectories)
                .Where(f => !f.Contains(System.IO.Path.DirectorySeparatorChar + "obj" + System.IO.Path.DirectorySeparatorChar) &&
                            !f.Contains(System.IO.Path.DirectorySeparatorChar + "bin" + System.IO.Path.DirectorySeparatorChar))
                .ToList();

            foreach (var file in csFiles)
            {
                var content = System.IO.File.ReadAllText(file);
                Assert.False(content.Contains("IHostEnvironment"), $"File {file} violates clean architecture by using IHostEnvironment");
                Assert.False(content.Contains("Microsoft.Extensions.Hosting"), $"File {file} violates clean architecture by using Microsoft.Extensions.Hosting");
                Assert.False(content.Contains("File.WriteAllText") || content.Contains("File.ReadAllText"), $"File {file} violates clean architecture by directly calling physical File I/O");
            }
        }

        [Fact]
        public void ApplicationServiceInterfaces_ShouldNotExposeObjectOrDomainEntities()
        {
            var applicationAssembly = typeof(IHoaDonCalculatorService).Assembly;
            var serviceInterfaces = applicationAssembly.GetExportedTypes()
                .Where(t => t.IsInterface && t.Namespace != null && t.Namespace.Contains(".Features.") && t.Namespace.EndsWith(".Services"))
                .ToList();

            Assert.NotEmpty(serviceInterfaces);

            foreach (var iface in serviceInterfaces)
            {
                foreach (var method in iface.GetMethods())
                {
                    AssertMethodReturnTypeDoesNotViolateBoundary(iface, method.Name, method.ReturnType);
                }
            }
        }

        private static void AssertMethodReturnTypeDoesNotViolateBoundary(Type iface, string methodName, Type type)
        {
            var unwrapped = type;
            if (unwrapped.IsGenericType && (unwrapped.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>) ||
                                           unwrapped.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.ValueTask<>)))
            {
                unwrapped = unwrapped.GetGenericArguments()[0];
            }

            Assert.False(unwrapped == typeof(object),
                $"Interface {iface.FullName}.{methodName} returns object/Task<object>, which violates clean architecture boundary.");

            AssertTypeDoesNotContainDomainEntities(iface, methodName, unwrapped);
        }

        private static void AssertTypeDoesNotContainDomainEntities(Type iface, string methodName, Type type)
        {
            if (type.Namespace != null && type.Namespace.StartsWith("QuanLyChoThuePhongTroWeb.Domain.Entities"))
            {
                Assert.Fail($"Interface {iface.FullName}.{methodName} exposes domain entity {type.FullName}, which violates clean architecture boundary.");
            }

            if (type.IsGenericType)
            {
                foreach (var arg in type.GetGenericArguments())
                {
                    AssertTypeDoesNotContainDomainEntities(iface, methodName, arg);
                }
            }
        }
    }
}
