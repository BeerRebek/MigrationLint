using Microsoft.EntityFrameworkCore.Migrations;

namespace SampleApi.Migrations;

public partial class Bad_DropIndexNoConcurrently : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Orders_Legacy",
            table: "Orders");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
                name: "IX_Orders_Legacy", table: "Orders", column: "Legacy")
            .Annotation("Npgsql:CreatedConcurrently", true);
    }
}
