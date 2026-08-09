using Microsoft.EntityFrameworkCore.Migrations;

namespace SampleApi.Migrations;

// CONCURRENTLY annotation is present, but there is NO `SuppressTransaction => true` override,
// so this migration fails at runtime (MIG017). MIG007 stays silent because the annotation is set.
public partial class Bad_ConcurrentNoSuppress : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
                name: "IX_Orders_Notes",
                table: "Orders",
                column: "Notes")
            .Annotation("Npgsql:CreatedConcurrently", true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Orders_Notes", table: "Orders");
    }
}
