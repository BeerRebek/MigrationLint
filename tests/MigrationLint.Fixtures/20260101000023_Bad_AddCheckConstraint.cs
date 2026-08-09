using Microsoft.EntityFrameworkCore.Migrations;

namespace SampleApi.Migrations;

public partial class Bad_AddCheckConstraint : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddCheckConstraint(
            name: "CK_Orders_Total_Positive",
            table: "Orders",
            sql: "\"Total\" >= 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "CK_Orders_Total_Positive", table: "Orders");
    }
}
