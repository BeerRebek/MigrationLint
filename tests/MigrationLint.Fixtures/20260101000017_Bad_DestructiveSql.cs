using Microsoft.EntityFrameworkCore.Migrations;

namespace SampleApi.Migrations;

public partial class Bad_DestructiveSql : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("TRUNCATE TABLE \"AuditLogs\";");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
