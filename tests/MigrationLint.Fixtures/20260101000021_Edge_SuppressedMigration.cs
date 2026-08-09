using Microsoft.EntityFrameworkCore.Migrations;

namespace SampleApi.Migrations;

[SuppressMigrationLint("LegacyNotes is confirmed unused; safe to drop.", "MIG001")]
public partial class Edge_SuppressedMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LegacyNotes",
            table: "Orders");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LegacyNotes",
            table: "Orders",
            type: "text",
            nullable: true);
    }
}
