using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SampleApi;

namespace SampleApi.Migrations;

[DbContext(typeof(AppDbContext))]
public partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        // The "Npgsql:" prefix is how MigrationLint auto-detects the PostgreSQL provider.
        modelBuilder.HasAnnotation("Npgsql:ValueGenerationStrategy", 1);
    }
}
