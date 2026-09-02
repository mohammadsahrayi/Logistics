using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logistics.Infrastructure.Migrations.AddVoyageCapacitySumCheck
{
    /// <inheritdoc />
    public partial class AddVoyageCapacitySumCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_voyage_capacity_sum",
                table: "voyage_capacity",
                sql: "(held_capacity + confirmed_capacity) <= total_capacity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_voyage_capacity_sum",
                table: "voyage_capacity");
        }
    }
}
