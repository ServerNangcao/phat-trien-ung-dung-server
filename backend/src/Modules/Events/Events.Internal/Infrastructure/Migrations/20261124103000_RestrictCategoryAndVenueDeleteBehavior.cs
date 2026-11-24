using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Events.Internal.Infrastructure.Migrations;

/// <inheritdoc />
public partial class RestrictCategoryAndVenueDeleteBehavior : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Events_Categories_CategoryId",
            schema: "events",
            table: "Events");

        migrationBuilder.DropForeignKey(
            name: "FK_Events_Venues_VenueId",
            schema: "events",
            table: "Events");

        migrationBuilder.AddForeignKey(
            name: "FK_Events_Categories_CategoryId",
            schema: "events",
            table: "Events",
            column: "CategoryId",
            principalSchema: "events",
            principalTable: "Categories",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Events_Venues_VenueId",
            schema: "events",
            table: "Events",
            column: "VenueId",
            principalSchema: "events",
            principalTable: "Venues",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Events_Categories_CategoryId",
            schema: "events",
            table: "Events");

        migrationBuilder.DropForeignKey(
            name: "FK_Events_Venues_VenueId",
            schema: "events",
            table: "Events");

        migrationBuilder.AddForeignKey(
            name: "FK_Events_Categories_CategoryId",
            schema: "events",
            table: "Events",
            column: "CategoryId",
            principalSchema: "events",
            principalTable: "Categories",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_Events_Venues_VenueId",
            schema: "events",
            table: "Events",
            column: "VenueId",
            principalSchema: "events",
            principalTable: "Venues",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
