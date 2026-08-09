using Microsoft.EntityFrameworkCore.Migrations;

namespace SampleApi.Migrations;

/// <summary>
/// Illustrative migration for the README screenshot: an index built without CONCURRENTLY
/// on PostgreSQL (MIG007) plus a NOT NULL column with no default (MIG004).
/// </summary>
public partial class AddOrderNotes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Notes",
            table: "Orders",
            type: "text",
            nullable: false);

        migrationBuilder.CreateIndex(
            name: "IX_Orders_Notes",
            table: "Orders",
            column: "Notes");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Orders_Notes", table: "Orders");
        migrationBuilder.DropColumn(name: "Notes", table: "Orders");
    }
}
