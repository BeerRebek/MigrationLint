using Microsoft.EntityFrameworkCore.Migrations;

namespace SampleApi.Migrations;

public partial class Good_AddNotNullToNewTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AuditLogs",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false),
            });

        // Adding a NOT NULL column to a table created in THIS migration is safe.
        migrationBuilder.AddColumn<string>(
            name: "Action",
            table: "AuditLogs",
            type: "text",
            nullable: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AuditLogs");
    }
}
