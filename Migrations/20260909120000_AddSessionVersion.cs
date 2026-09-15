using BirdieBuddy.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BirdieBuddy.Migrations;

[Migration("20260909120000_AddSessionVersion")]
[DbContext(typeof(ApplicationDbContext))]
public partial class AddSessionVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "SessionVersion",
            table: "Users",
            type: "integer",
            nullable: false,
            defaultValue: 1);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SessionVersion",
            table: "Users");
    }
}
