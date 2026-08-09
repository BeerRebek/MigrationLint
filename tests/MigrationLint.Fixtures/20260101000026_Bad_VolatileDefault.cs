using Microsoft.EntityFrameworkCore.Migrations;

namespace SampleApi.Migrations;

public partial class Bad_VolatileDefault : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "RowId",
            table: "Orders",
            type: "uuid",
            nullable: false,
            defaultValueSql: "gen_random_uuid()");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "RowId", table: "Orders");
    }
}
