using Microsoft.EntityFrameworkCore.Migrations;

namespace SampleApi.Migrations;

public partial class Bad_WidenIntToBigint : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<long>(
            name: "Id",
            table: "Orders",
            type: "bigint",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(
            name: "Id",
            table: "Orders",
            type: "integer",
            nullable: false,
            oldClrType: typeof(long),
            oldType: "bigint");
    }
}
