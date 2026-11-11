using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketing.Internal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitTicketing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ticketing");

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                schema: "ticketing",
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
                schema: "ticketing",
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

            migrationBuilder.CreateTable(
                name: "Reservations",
                schema: "ticketing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.CheckConstraint("ck_reservations_status", "\"Status\" IN ('Active', 'PaymentPending', 'Confirmed', 'Released')");
                });

            migrationBuilder.CreateTable(
                name: "TicketTypes",
                schema: "ticketing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    TotalQuantity = table.Column<int>(type: "integer", nullable: false),
                    ReservedQuantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SoldQuantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MaxPerOrder = table.Column<int>(type: "integer", nullable: false),
                    SaleStartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SaleEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketTypes", x => x.Id);
                    table.CheckConstraint("ck_tickettypes_inventory", "\"ReservedQuantity\" >= 0 AND \"SoldQuantity\" >= 0 AND \"ReservedQuantity\" + \"SoldQuantity\" <= \"TotalQuantity\"");
                    table.CheckConstraint("ck_tickettypes_price", "\"Price\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ReservationItems",
                schema: "ticketing",
                columns: table => new
                {
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationItems", x => new { x.ReservationId, x.TicketTypeId });
                    table.CheckConstraint("ck_reservationitems_quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_ReservationItems_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalSchema: "ticketing",
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReservationItems_TicketTypes_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalSchema: "ticketing",
                        principalTable: "TicketTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                schema: "ticketing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    QrSignature = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsedByStaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.CheckConstraint("ck_tickets_status", "\"Status\" IN ('Valid', 'Used', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_Tickets_TicketTypes_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalSchema: "ticketing",
                        principalTable: "TicketTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_EventId_Handler",
                schema: "ticketing",
                table: "InboxMessages",
                columns: new[] { "EventId", "Handler" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Unprocessed",
                schema: "ticketing",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "CreatedAt" },
                filter: "\"ProcessedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationItems_TicketTypeId",
                schema: "ticketing",
                table: "ReservationItems",
                column: "TicketTypeId");

            migrationBuilder.CreateIndex(
                name: "ix_reservations_expiry",
                schema: "ticketing",
                table: "Reservations",
                column: "ExpiresAt",
                filter: "\"Status\" IN ('Active', 'PaymentPending')");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_event_status",
                schema: "ticketing",
                table: "Tickets",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_order",
                schema: "ticketing",
                table: "Tickets",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TicketTypeId",
                schema: "ticketing",
                table: "Tickets",
                column: "TicketTypeId");

            migrationBuilder.CreateIndex(
                name: "ux_tickets_code",
                schema: "ticketing",
                table: "Tickets",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_tickets_order_type_seq",
                schema: "ticketing",
                table: "Tickets",
                columns: new[] { "OrderId", "TicketTypeId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketTypes_EventId",
                schema: "ticketing",
                table: "TicketTypes",
                column: "EventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "ticketing");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "ticketing");

            migrationBuilder.DropTable(
                name: "ReservationItems",
                schema: "ticketing");

            migrationBuilder.DropTable(
                name: "Tickets",
                schema: "ticketing");

            migrationBuilder.DropTable(
                name: "Reservations",
                schema: "ticketing");

            migrationBuilder.DropTable(
                name: "TicketTypes",
                schema: "ticketing");
        }
    }
}
