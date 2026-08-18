using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BirdieBuddy.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Courses",
                columns: table => new
                {
                    Id = table.Column<int>(
                        type: "integer",
                        nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),

                    Name = table.Column<string>(
                        type: "text",
                        nullable: false),

                    Location = table.Column<string>(
                        type: "text",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourseHoles",
                columns: table => new
                {
                    Id = table.Column<int>(
                        type: "integer",
                        nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),

                    CourseId = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    HoleNumber = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    Par = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    Distance = table.Column<int>(
                        type: "integer",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseHoles", x => x.Id);

                    table.CheckConstraint(
                        "CK_CourseHole_HoleNumber",
                        "\"HoleNumber\" BETWEEN 1 AND 18");

                    table.ForeignKey(
                        name: "FK_CourseHoles_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rounds",
                columns: table => new
                {
                    Id = table.Column<int>(
                        type: "integer",
                        nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),

                    CourseId = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    Date = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false),

                    Tee = table.Column<string>(
                        type: "text",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rounds", x => x.Id);

                    table.ForeignKey(
                        name: "FK_Rounds_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Holes",
                columns: table => new
                {
                    Id = table.Column<int>(
                        type: "integer",
                        nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),

                    RoundId = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    HoleNumber = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    Par = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    Score = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    Putts = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    GIR = table.Column<bool>(
                        type: "boolean",
                        nullable: false),

                    FairwayHit = table.Column<bool>(
                        type: "boolean",
                        nullable: true),

                    Penalty = table.Column<int>(
                        type: "integer",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holes", x => x.Id);

                    table.CheckConstraint(
                        "CK_Hole_HoleNumber",
                        "\"HoleNumber\" BETWEEN 1 AND 18");

                    table.CheckConstraint(
                        "CK_Hole_Penalty",
                        "\"Penalty\" >= 0");

                    table.CheckConstraint(
                        "CK_Hole_Putts",
                        "\"Putts\" >= 0");

                    table.CheckConstraint(
                        "CK_Hole_Score",
                        "\"Score\" > 0");

                    table.ForeignKey(
                        name: "FK_Holes_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseHoles_CourseId_HoleNumber",
                table: "CourseHoles",
                columns: new[] { "CourseId", "HoleNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Holes_RoundId_HoleNumber",
                table: "Holes",
                columns: new[] { "RoundId", "HoleNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_CourseId",
                table: "Rounds",
                column: "CourseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseHoles");

            migrationBuilder.DropTable(
                name: "Holes");

            migrationBuilder.DropTable(
                name: "Rounds");

            migrationBuilder.DropTable(
                name: "Courses");
        }
    }
}