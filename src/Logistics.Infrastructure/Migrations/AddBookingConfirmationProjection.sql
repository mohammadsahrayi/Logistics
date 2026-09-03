START TRANSACTION;

CREATE TABLE booking_confirmation_projection (
    booking_id uuid NOT NULL,
    message_id uuid NOT NULL,
    hold_id uuid NOT NULL,
    voyage_id uuid NOT NULL,
    capacity_units integer NOT NULL,
    received_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_booking_confirmation_projection" PRIMARY KEY (booking_id)
);

CREATE UNIQUE INDEX "IX_booking_confirmation_projection_message_id"
    ON booking_confirmation_projection (message_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260903160050_AddBookingConfirmationProjection', '8.0.26');

COMMIT;