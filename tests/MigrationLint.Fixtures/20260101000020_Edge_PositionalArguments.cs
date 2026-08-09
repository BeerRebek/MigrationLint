using Microsoft.EntityFrameworkCore.Migrations;

namespace SampleApi.Migrations;

public partial class Edge_PositionalArguments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Positional (not named) arguments — the parser must still resolve name/table.
        migrationBuilder.DropColumn("LegacyFlag", "Orders");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "LegacyFlag",
            table: "Orders",
            nullable: true);
    }
}
