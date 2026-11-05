using System.Reflection;
using System.Runtime.Versioning;

namespace ZipConvertCustomerBarCode.Tests
{
    public class TargetFrameworkTests
    {
        [Fact]
        public void 指定したコアのビルドでテストしている()
        {
            // dotnet test -p:CoreTargetFramework=netstandard2.0 で指定したビルドが実際に使われているか
            var expected = typeof(TargetFrameworkTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(x => x.Key == "CoreTargetFramework").Value;
            var actual = typeof(BarCode).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName;

            var expectedName = expected switch
            {
                "netstandard2.0" => ".NETStandard,Version=v2.0",
                "net8.0" => ".NETCoreApp,Version=v8.0",
                _ => throw new InvalidOperationException($"想定していない CoreTargetFramework: {expected}"),
            };
            Assert.Equal(expectedName, actual);
        }
    }
}
