using Microsoft.EntityFrameworkCore.Migrations;

namespace SampleApi.Migrations;

public partial class Bad_MixedDdlDml : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Region",
            table: "Customers",
            type: "text",
            nullable: true);

        migrationBuilder.Sql("UPDATE \"Customers\" SET \"Region\" = 'unknown' WHERE \"Region\" IS NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Region", table: "Customers");
    }
}
