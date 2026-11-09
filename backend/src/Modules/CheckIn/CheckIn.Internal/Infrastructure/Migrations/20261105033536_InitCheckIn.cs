using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckIn.Internal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitCheckIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "checkin");

            migrationBuilder.CreateTable(
                name: "CheckInLogs",
                schema: "checkin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    Result = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeviceInfo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInLogs", x => x.Id);
                    table.CheckConstraint("ck_checkinlogs_result", "\"Result\" IN ('Valid', 'AlreadyUsed', 'Invalid')");
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                schema: "checkin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Handler = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "checkin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_checkinlogs_event_scanned",
                schema: "checkin",
                table: "CheckInLogs",
                columns: new[] { "EventId", "ScannedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_checkinlogs_event_valid",
                schema: "checkin",
                table: "CheckInLogs",
                column: "EventId",
                filter: "\"Result\" = 'Valid'");

            migrationBuilder.CreateIndex(
                name: "ix_checkinlogs_ticket",
                schema: "checkin",
                table: "CheckInLogs",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_EventId_Handler",
                schema: "checkin",
                table: "InboxMessages",
                columns: new[] { "EventId", "Handler" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Unprocessed",
                schema: "checkin",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "CreatedAt" },
                filter: "\"ProcessedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckInLogs",
                schema: "checkin");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "checkin");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "checkin");
        }
    }
}
