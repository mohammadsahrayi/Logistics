using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logistics.Infrastructure.Migrations.AddConstraintsAndIndexes
{
    /// <inheritdoc />
    public partial class AddConstraintsAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PublishedAt",
                table: "outbox_message",
                newName: "published_at");

            migrationBuilder.RenameColumn(
                name: "LastError",
                table: "outbox_message",
                newName: "last_error");

            migrationBuilder.RenameColumn(
                name: "AttemptCount",
                table: "outbox_message",
                newName: "attempt_count");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "booking",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "booking",
                newName: "created_at");

            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at",
                table: "idempotency_entry",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "request_hash",
                table: "idempotency_entry",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "response_body",
                table: "idempotency_entry",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "response_status_code",
                table: "idempotency_entry",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "idempotency_entry",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_processed_occurredat",
                table: "outbox_message",
                columns: new[] { "processed", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_capacity_hold_booking_status",
                table: "capacity_hold",
                columns: new[] { "booking_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_capacity_hold_voyage_id",
                table: "capacity_hold",
                column: "voyage_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_voyage_id",
                table: "booking",
                column: "voyage_id");

            migrationBuilder.AddForeignKey(
                name: "fk_booking_voyage",
                table: "booking",
                column: "voyage_id",
                principalTable: "voyage_capacity",
                principalColumn: "voyage_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_capacityhold_booking",
                table: "capacity_hold",
                column: "booking_id",
                principalTable: "booking",
                principalColumn: "booking_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_capacityhold_voyage",
                table: "capacity_hold",
                column: "voyage_id",
                principalTable: "voyage_capacity",
                principalColumn: "voyage_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_booking_voyage",
                table: "booking");

            migrationBuilder.DropForeignKey(
                name: "fk_capacityhold_booking",
                table: "capacity_hold");

            migrationBuilder.DropForeignKey(
                name: "fk_capacityhold_voyage",
                table: "capacity_hold");

            migrationBuilder.DropIndex(
                name: "ix_outbox_processed_occurredat",
                table: "outbox_message");

            migrationBuilder.DropIndex(
                name: "ix_capacity_hold_booking_status",
                table: "capacity_hold");

            migrationBuilder.DropIndex(
                name: "IX_capacity_hold_voyage_id",
                table: "capacity_hold");

            migrationBuilder.DropIndex(
                name: "IX_booking_voyage_id",
                table: "booking");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "idempotency_entry");

            migrationBuilder.DropColumn(
                name: "request_hash",
                table: "idempotency_entry");

            migrationBuilder.DropColumn(
                name: "response_body",
                table: "idempotency_entry");

            migrationBuilder.DropColumn(
                name: "response_status_code",
                table: "idempotency_entry");

            migrationBuilder.DropColumn(
                name: "status",
                table: "idempotency_entry");

            migrationBuilder.RenameColumn(
                name: "published_at",
                table: "outbox_message",
                newName: "PublishedAt");

            migrationBuilder.RenameColumn(
                name: "last_error",
                table: "outbox_message",
                newName: "LastError");

            migrationBuilder.RenameColumn(
                name: "attempt_count",
                table: "outbox_message",
                newName: "AttemptCount");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "booking",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "booking",
                newName: "CreatedAt");
        }
    }
}
