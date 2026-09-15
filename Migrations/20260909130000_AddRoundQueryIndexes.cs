using BirdieBuddy.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BirdieBuddy.Migrations;

[Migration("20260909130000_AddRoundQueryIndexes")]
[DbContext(typeof(ApplicationDbContext))]
public partial class AddRoundQueryIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Rounds_UserId_CourseId_Date",
            table: "Rounds",
            columns: new[] { "UserId", "CourseId", "Date" });

        migrationBuilder.CreateIndex(
            name: "IX_Rounds_UserId_Status_Date",
            table: "Rounds",
            columns: new[] { "UserId", "Status", "Date" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Rounds_UserId_CourseId_Date",
            table: "Rounds");
        migrationBuilder.DropIndex(
            name: "IX_Rounds_UserId_Status_Date",
            table: "Rounds");
    }
}
