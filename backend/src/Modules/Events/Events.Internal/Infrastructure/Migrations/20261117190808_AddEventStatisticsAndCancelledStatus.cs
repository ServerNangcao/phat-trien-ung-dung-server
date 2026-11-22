using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Events.Internal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventStatisticsAndCancelledStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_events_status",
                schema: "events",
                table: "Events");

            migrationBuilder.CreateTable(
                name: "EventStatistics",
                schema: "events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false, defaultValue: 0m),
                    TotalTicketsSold = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalCheckedIn = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TiersBreakdownJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    LastCalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventStatistics", x => x.Id);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_events_status",
                schema: "events",
                table: "Events",
                sql: "\"Status\" IN ('Draft','PendingApproval','OnSale','Ended','Rejected','Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_EventStatistics_EventId",
                schema: "events",
                table: "EventStatistics",
                column: "EventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventStatistics",
                schema: "events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_events_status",
                schema: "events",
                table: "Events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_events_status",
                schema: "events",
                table: "Events",
                sql: "\"Status\" IN ('Draft','PendingApproval','OnSale','Ended','Rejected')");
        }
    }
}
