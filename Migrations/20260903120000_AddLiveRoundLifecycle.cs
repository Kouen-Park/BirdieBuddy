using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using BirdieBuddy.Data;

#nullable disable

namespace BirdieBuddy.Migrations;

[Migration("20260903120000_AddLiveRoundLifecycle")]
[DbContext(typeof(ApplicationDbContext))]
public partial class AddLiveRoundLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Status", table: "Rounds", type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Completed");
        migrationBuilder.AddColumn<DateTime>(name: "StartedAt", table: "Rounds", type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()");
        migrationBuilder.AddColumn<DateTime>(name: "UpdatedAt", table: "Rounds", type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()");
        migrationBuilder.AddColumn<DateTime>(name: "CompletedAt", table: "Rounds", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<int>(name: "CurrentHole", table: "Rounds", type: "integer", nullable: false, defaultValue: 1);
        migrationBuilder.Sql("UPDATE \"Rounds\" SET \"CompletedAt\" = \"Date\" WHERE \"Status\" = 'Completed';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("Status", "Rounds");
        migrationBuilder.DropColumn("StartedAt", "Rounds");
        migrationBuilder.DropColumn("UpdatedAt", "Rounds");
        migrationBuilder.DropColumn("CompletedAt", "Rounds");
        migrationBuilder.DropColumn("CurrentHole", "Rounds");
    }
}
