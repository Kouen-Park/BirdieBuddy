using BirdieBuddy.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BirdieBuddy.Migrations;

[Migration("20260908100000_GrandfatherExistingEmailVerification")]
[DbContext(typeof(ApplicationDbContext))]
public partial class GrandfatherExistingEmailVerification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("UPDATE \"Users\" SET \"EmailVerifiedAt\" = \"CreatedAt\" WHERE \"EmailVerifiedAt\" IS NULL;");

    // Verification cannot be safely reversed because accounts may have been
    // genuinely verified after this migration was applied.
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
