using BirdieBuddy.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BirdieBuddy.Migrations;

[Migration("20260915100000_AddGolfNzImportHistoryAndStaleRecords")]
[DbContext(typeof(ApplicationDbContext))]
public partial class AddGolfNzImportHistoryAndStaleRecords : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "GolfNzImportRuns",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SourceName = table.Column<string>(type: "text", nullable: false),
                SourceVersion = table.Column<string>(type: "text", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CoursesCreated = table.Column<int>(type: "integer", nullable: false),
                CoursesUpdated = table.Column<int>(type: "integer", nullable: false),
                TeesCreated = table.Column<int>(type: "integer", nullable: false),
                TeesUpdated = table.Column<int>(type: "integer", nullable: false),
                HolesCreated = table.Column<int>(type: "integer", nullable: false),
                HolesUpdated = table.Column<int>(type: "integer", nullable: false),
                RecordsDeactivated = table.Column<int>(type: "integer", nullable: false),
                ErrorMessage = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_GolfNzImportRuns", x => x.Id));

        migrationBuilder.AddColumn<bool>("IsActive", "CourseTees", type: "boolean", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<long>("LastSeenImportRunId", "CourseTees", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<string>("SourceKey", "CourseTees", type: "text", nullable: true);
        migrationBuilder.AddColumn<bool>("IsActive", "CourseHoles", type: "boolean", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<long>("LastSeenImportRunId", "CourseHoles", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<string>("SourceKey", "CourseHoles", type: "text", nullable: true);

        migrationBuilder.CreateIndex("IX_CourseTees_SourceKey", "CourseTees", "SourceKey", unique: true, filter: "\"SourceKey\" IS NOT NULL");
        migrationBuilder.CreateIndex("IX_CourseHoles_SourceKey", "CourseHoles", "SourceKey", unique: true, filter: "\"SourceKey\" IS NOT NULL");
        migrationBuilder.CreateIndex("IX_GolfNzImportRuns_StartedAt", "GolfNzImportRuns", "StartedAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_CourseTees_SourceKey", "CourseTees");
        migrationBuilder.DropIndex("IX_CourseHoles_SourceKey", "CourseHoles");
        migrationBuilder.DropIndex("IX_GolfNzImportRuns_StartedAt", "GolfNzImportRuns");
        migrationBuilder.DropColumn("IsActive", "CourseTees");
        migrationBuilder.DropColumn("LastSeenImportRunId", "CourseTees");
        migrationBuilder.DropColumn("SourceKey", "CourseTees");
        migrationBuilder.DropColumn("IsActive", "CourseHoles");
        migrationBuilder.DropColumn("LastSeenImportRunId", "CourseHoles");
        migrationBuilder.DropColumn("SourceKey", "CourseHoles");
        migrationBuilder.DropTable("GolfNzImportRuns");
    }
}
