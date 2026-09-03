using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logistics.Infrastructure.Migrations.AddBookingConfirmationProjection
{
    /// <inheritdoc />
    public partial class AddBookingConfirmationProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "booking_confirmation_projection",
                columns: table => new
                {
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hold_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voyage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capacity_units = table.Column<int>(type: "integer", nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_confirmation_projection", x => x.booking_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_booking_confirmation_projection_message_id",
                table: "booking_confirmation_projection",
                column: "message_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_confirmation_projection");
        }
    }
}
