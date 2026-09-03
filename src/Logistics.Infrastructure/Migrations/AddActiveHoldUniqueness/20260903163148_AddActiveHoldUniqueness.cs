using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logistics.Infrastructure.Migrations.AddActiveHoldUniqueness
{
    /// <inheritdoc />
    public partial class AddActiveHoldUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_capacity_hold_active_booking",
                table: "capacity_hold",
                column: "booking_id",
                unique: true,
                filter: "status = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_capacity_hold_active_booking",
                table: "capacity_hold");
        }
    }
}
