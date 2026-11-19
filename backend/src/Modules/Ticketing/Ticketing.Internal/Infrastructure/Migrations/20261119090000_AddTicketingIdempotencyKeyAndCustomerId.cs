using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Ticketing.Internal.Infrastructure;

#nullable disable

namespace Ticketing.Internal.Infrastructure.Migrations;

[DbContext(typeof(TicketingDbContext))]
[Migration("20261119090000_AddTicketingIdempotencyKeyAndCustomerId")]
public partial class AddTicketingIdempotencyKeyAndCustomerId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "IdempotencyKey",
            schema: "ticketing",
            table: "Reservations",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CustomerId",
            schema: "ticketing",
            table: "Tickets",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.CreateIndex(
            name: "ix_reservations_customer_idempotency",
            schema: "ticketing",
            table: "Reservations",
            columns: new[] { "CustomerId", "IdempotencyKey" });

        migrationBuilder.CreateIndex(
            name: "ix_tickets_customer",
            schema: "ticketing",
            table: "Tickets",
            column: "CustomerId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_reservations_customer_idempotency",
            schema: "ticketing",
            table: "Reservations");

        migrationBuilder.DropIndex(
            name: "ix_tickets_customer",
            schema: "ticketing",
            table: "Tickets");

        migrationBuilder.DropColumn(
            name: "IdempotencyKey",
            schema: "ticketing",
            table: "Reservations");

        migrationBuilder.DropColumn(
            name: "CustomerId",
            schema: "ticketing",
            table: "Tickets");
    }
}
