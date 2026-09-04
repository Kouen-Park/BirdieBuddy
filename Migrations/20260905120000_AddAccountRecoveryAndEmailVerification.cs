using BirdieBuddy.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BirdieBuddy.Migrations;

[Migration("20260905120000_AddAccountRecoveryAndEmailVerification")]
[DbContext(typeof(ApplicationDbContext))]
public partial class AddAccountRecoveryAndEmailVerification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(name: "EmailVerifiedAt", table: "Users",
            type: "timestamp with time zone", nullable: true);
        migrationBuilder.CreateTable(name: "AccountTokens", columns: table => new
        {
            Id = table.Column<int>(type: "integer", nullable: false)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            UserId = table.Column<int>(type: "integer", nullable: false),
            Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_AccountTokens", x => x.Id);
            table.ForeignKey("FK_AccountTokens_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex(name: "IX_AccountTokens_TokenHash", table: "AccountTokens", column: "TokenHash", unique: true);
        migrationBuilder.CreateIndex(name: "IX_AccountTokens_UserId", table: "AccountTokens", column: "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AccountTokens");
        migrationBuilder.DropColumn(name: "EmailVerifiedAt", table: "Users");
    }
}
