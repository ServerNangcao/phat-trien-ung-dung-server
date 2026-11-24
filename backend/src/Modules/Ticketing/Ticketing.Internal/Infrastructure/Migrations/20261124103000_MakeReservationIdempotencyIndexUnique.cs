using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketing.Internal.Infrastructure.Migrations;

/// <inheritdoc />
public partial class MakeReservationIdempotencyIndexUnique : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_reservations_customer_idempotency",
            schema: "ticketing",
            table: "Reservations");

        migrationBuilder.CreateIndex(
            name: "ix_reservations_customer_idempotency",
            schema: "ticketing",
            table: "Reservations",
            columns: new[] { "CustomerId", "IdempotencyKey" },
            unique: true,
            filter: "\"IdempotencyKey\" IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_reservations_customer_idempotency",
            schema: "ticketing",
            table: "Reservations");

        migrationBuilder.CreateIndex(
            name: "ix_reservations_customer_idempotency",
            schema: "ticketing",
            table: "Reservations",
            columns: new[] { "CustomerId", "IdempotencyKey" });
    }
}
