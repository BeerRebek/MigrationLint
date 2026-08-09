using Microsoft.EntityFrameworkCore.Migrations;

namespace SampleApi.Migrations;

public partial class Good_CreateIndexConcurrently : Migration
{
    protected override bool SuppressTransaction => true;

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
        migrationBuilder.DropIndex(
            name: "IX_Orders_Notes",
            table: "Orders");
    }
}
