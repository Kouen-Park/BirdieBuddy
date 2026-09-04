using BirdieBuddy.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BirdieBuddy.Migrations;

[Migration("20260905150000_AddProductTelemetry")]
[DbContext(typeof(ApplicationDbContext))]
public partial class AddProductTelemetry : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "ProductEvents", columns: table => new
        {
            Id = table.Column<int>(type: "integer", nullable: false)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            UserId = table.Column<int>(type: "integer", nullable: false),
            RoundId = table.Column<int>(type: "integer", nullable: true),
            ClientEventId = table.Column<Guid>(type: "uuid", nullable: false),
            EventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
            DurationMs = table.Column<int>(type: "integer", nullable: true),
            OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_ProductEvents", x => x.Id);
            table.ForeignKey("FK_ProductEvents_Rounds_RoundId", x => x.RoundId, "Rounds", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_ProductEvents_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex(name: "IX_ProductEvents_EventType_OccurredAt", table: "ProductEvents", columns: new[] { "EventType", "OccurredAt" });
        migrationBuilder.CreateIndex(name: "IX_ProductEvents_RoundId", table: "ProductEvents", column: "RoundId");
        migrationBuilder.CreateIndex(name: "IX_ProductEvents_UserId_ClientEventId", table: "ProductEvents", columns: new[] { "UserId", "ClientEventId" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "ProductEvents");
}
