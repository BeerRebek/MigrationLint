using MigrationLint.Core.Model;
using MigrationLint.Core.Parsing;
using Xunit;

namespace MigrationLint.Tests;

public class ProviderDetectionTests
{
    [Theory]
    [InlineData("postgres", Provider.PostgreSql)]
    [InlineData("postgresql", Provider.PostgreSql)]
    [InlineData("sqlserver", Provider.SqlServer)]
    [InlineData("mysql", Provider.MySql)]
    [InlineData("sqlite", Provider.Sqlite)]
    [InlineData("nonsense", Provider.Unknown)]
    public void FromStringMapsProviders(string value, Provider expected)
    {
        Assert.Equal(expected, ProviderDetector.FromString(value));
    }

    [Theory]
    [InlineData("HasAnnotation(\"Npgsql:ValueGenerationStrategy\", ...)", Provider.PostgreSql)]
    [InlineData(".Annotation(\"SqlServer:Identity\", ...)", Provider.SqlServer)]
    [InlineData(".Annotation(\"MySql:Charset\", ...)", Provider.MySql)]
    [InlineData(".Annotation(\"Sqlite:Autoincrement\", ...)", Provider.Sqlite)]
    public void FromAnnotationTextDetects(string text, Provider expected)
    {
        Assert.Equal(expected, ProviderDetector.FromAnnotationText(text));
    }

    [Fact]
    public void AutoDetectReadsSnapshotAnnotations()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mlint_pg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "AppDbContextModelSnapshot.cs"),
                "modelBuilder.HasAnnotation(\"Npgsql:ValueGenerationStrategy\", 1);");
            Assert.Equal(Provider.PostgreSql, ProviderDetector.AutoDetect(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AutoDetectFallsBackToCsprojPackageReference()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mlint_ss_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "App.csproj"),
                "<Project><ItemGroup><PackageReference Include=\"Microsoft.EntityFrameworkCore.SqlServer\" /></ItemGroup></Project>");
            Assert.Equal(Provider.SqlServer, ProviderDetector.AutoDetect(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
