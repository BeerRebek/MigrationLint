using Microsoft.EntityFrameworkCore.Migrations;

namespace SampleApi.Migrations;

public partial class Edge_NonLiteralArguments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var required = ComputeNullability();
        var length = 10 * 5;

        // Non-literal arguments must parse without throwing and must NOT produce a violation,
        // because the parser cannot see the value (silence is correct).
        migrationBuilder.AddColumn<string>(
            name: "Flexible",
            table: "Orders",
            maxLength: length,
            nullable: required);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Flexible", table: "Orders");
    }

    private static bool ComputeNullability() => false;
}
