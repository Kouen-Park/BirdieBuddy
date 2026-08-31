using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BirdieBuddy.Migrations;

public partial class AddGolfNzCourseData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "GolfNzClubId",
            table: "Courses",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "CourseTees",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CourseId = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                CourseType = table.Column<string>(type: "text", nullable: false),
                Gender = table.Column<string>(type: "text", nullable: false),
                NineHoles = table.Column<bool>(type: "boolean", nullable: false),
                Rating = table.Column<decimal>(type: "numeric", nullable: true),
                Slope = table.Column<int>(type: "integer", nullable: true),
                Colour = table.Column<string>(type: "text", nullable: true),
                TotalPar = table.Column<int>(type: "integer", nullable: true),
                FrontNinePar = table.Column<int>(type: "integer", nullable: true),
                BackNinePar = table.Column<int>(type: "integer", nullable: true),
                FrontNineMetres = table.Column<int>(type: "integer", nullable: true),
                BackNineMetres = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CourseTees", x => x.Id);
                table.ForeignKey(
                    name: "FK_CourseTees_Courses_CourseId",
                    column: x => x.CourseId,
                    principalTable: "Courses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.AddColumn<int>(
            name: "CourseTeeId",
            table: "CourseHoles",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "StrokeIndex",
            table: "CourseHoles",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CourseTeeId",
            table: "Rounds",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LegacyTee",
            table: "Rounds",
            type: "text",
            nullable: true);

        // Move every existing course's old shared holes under one legacy tee.
        migrationBuilder.Sql("""
            INSERT INTO "CourseTees" ("CourseId", "Name", "CourseType", "Gender", "NineHoles")
            SELECT "Id", 'Legacy', 'LEGACY', '', false
            FROM "Courses";
            """);

        migrationBuilder.Sql("""
            UPDATE "CourseHoles" AS ch
            SET "CourseTeeId" = ct."Id"
            FROM "CourseTees" AS ct
            WHERE ct."CourseId" = ch."CourseId"
              AND ct."CourseType" = 'LEGACY';
            """);

        migrationBuilder.Sql("""
            UPDATE "Rounds" AS r
            SET "LegacyTee" = r."Tee",
                "CourseTeeId" = ct."Id"
            FROM "CourseTees" AS ct
            WHERE ct."CourseId" = r."CourseId"
              AND ct."CourseType" = 'LEGACY';
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_CourseHoles_Courses_CourseId",
            table: "CourseHoles");

        migrationBuilder.DropIndex(
            name: "IX_CourseHoles_CourseId_HoleNumber",
            table: "CourseHoles");

        migrationBuilder.DropColumn(
            name: "CourseId",
            table: "CourseHoles");

        migrationBuilder.AlterColumn<int>(
            name: "CourseTeeId",
            table: "CourseHoles",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "Tee",
            table: "Rounds");

        migrationBuilder.CreateIndex(
            name: "IX_Courses_GolfNzClubId",
            table: "Courses",
            column: "GolfNzClubId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CourseTees_CourseId_CourseType_Gender_NineHoles_Name",
            table: "CourseTees",
            columns: new[] { "CourseId", "CourseType", "Gender", "NineHoles", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CourseHoles_CourseTeeId_HoleNumber",
            table: "CourseHoles",
            columns: new[] { "CourseTeeId", "HoleNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Rounds_CourseTeeId",
            table: "Rounds",
            column: "CourseTeeId");

        migrationBuilder.AddForeignKey(
            name: "FK_CourseHoles_CourseTees_CourseTeeId",
            table: "CourseHoles",
            column: "CourseTeeId",
            principalTable: "CourseTees",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_Rounds_CourseTees_CourseTeeId",
            table: "Rounds",
            column: "CourseTeeId",
            principalTable: "CourseTees",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "CourseHoles" ADD COLUMN "CourseId" integer;
            UPDATE "CourseHoles" AS ch
            SET "CourseId" = ct."CourseId"
            FROM "CourseTees" AS ct
            WHERE ct."Id" = ch."CourseTeeId";
            """);

        migrationBuilder.AlterColumn<int>(
            name: "CourseId",
            table: "CourseHoles",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.DropForeignKey("FK_CourseHoles_CourseTees_CourseTeeId", "CourseHoles");
        migrationBuilder.DropForeignKey("FK_Rounds_CourseTees_CourseTeeId", "Rounds");
        migrationBuilder.DropIndex("IX_CourseHoles_CourseTeeId_HoleNumber", "CourseHoles");
        migrationBuilder.DropIndex("IX_Rounds_CourseTeeId", "Rounds");
        migrationBuilder.DropIndex("IX_Courses_GolfNzClubId", "Courses");
        migrationBuilder.DropIndex("IX_CourseTees_CourseId_CourseType_Gender_NineHoles_Name", "CourseTees");

        migrationBuilder.AddColumn<string>(
            name: "Tee",
            table: "Rounds",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql("""
            UPDATE "Rounds"
            SET "Tee" = COALESCE(NULLIF("LegacyTee", ''), 'Legacy');
            """);

        migrationBuilder.DropColumn("LegacyTee", "Rounds");
        migrationBuilder.DropColumn("CourseTeeId", "Rounds");
        migrationBuilder.DropColumn("StrokeIndex", "CourseHoles");
        migrationBuilder.DropColumn("CourseTeeId", "CourseHoles");

        migrationBuilder.CreateIndex(
            name: "IX_CourseHoles_CourseId_HoleNumber",
            table: "CourseHoles",
            columns: new[] { "CourseId", "HoleNumber" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_CourseHoles_Courses_CourseId",
            table: "CourseHoles",
            column: "CourseId",
            principalTable: "Courses",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.DropTable("CourseTees");
        migrationBuilder.DropColumn("GolfNzClubId", "Courses");
    }
}
