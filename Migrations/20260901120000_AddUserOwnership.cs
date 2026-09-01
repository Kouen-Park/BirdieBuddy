using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BirdieBuddy.Migrations;

public partial class AddUserOwnership : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                PasswordHash = table.Column<string>(type: "text", nullable: false),
                PasswordSalt = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.AddColumn<int>(
            name: "UserId",
            table: "Courses",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "UserId",
            table: "Rounds",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Courses_UserId",
            table: "Courses",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Rounds_UserId",
            table: "Rounds",
            column: "UserId");

        migrationBuilder.AddForeignKey(
            name: "FK_Courses_Users_UserId",
            table: "Courses",
            column: "UserId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Rounds_Users_UserId",
            table: "Rounds",
            column: "UserId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_Courses_Users_UserId", "Courses");
        migrationBuilder.DropForeignKey("FK_Rounds_Users_UserId", "Rounds");
        migrationBuilder.DropIndex("IX_Courses_UserId", "Courses");
        migrationBuilder.DropIndex("IX_Rounds_UserId", "Rounds");
        migrationBuilder.DropIndex("IX_Users_Email", "Users");
        migrationBuilder.DropColumn("UserId", "Courses");
        migrationBuilder.DropColumn("UserId", "Rounds");
        migrationBuilder.DropTable("Users");
    }
}
