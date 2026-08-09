using Microsoft.EntityFrameworkCore.Migrations;

namespace SampleApi.Migrations;

public partial class Bad_TooManyOperations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "C01", table: "Wide", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "C02", table: "Wide", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "C03", table: "Wide", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "C04", table: "Wide", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "C05", table: "Wide", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "C06", table: "Wide", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "C07", table: "Wide", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "C08", table: "Wide", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "C09", table: "Wide", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "C10", table: "Wide", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "C11", table: "Wide", type: "text", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "C01", table: "Wide");
    }
}
