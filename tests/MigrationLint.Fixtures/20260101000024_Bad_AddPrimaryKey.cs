using Microsoft.EntityFrameworkCore.Migrations;

namespace SampleApi.Migrations;

public partial class Bad_AddPrimaryKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddPrimaryKey(
            name: "PK_Orders",
            table: "Orders",
            column: "Id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(name: "PK_Orders", table: "Orders");
    }
}
