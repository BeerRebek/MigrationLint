using Microsoft.EntityFrameworkCore.Migrations;

namespace SampleApi.Migrations;

[SuppressMigrationLint]
public partial class Edge_SuppressionNoJustification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Obsolete",
            table: "Orders");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Obsolete",
            table: "Orders",
            type: "text",
            nullable: true);
    }
}
