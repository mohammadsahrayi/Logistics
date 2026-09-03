START TRANSACTION;

CREATE UNIQUE INDEX ux_capacity_hold_active_booking
    ON capacity_hold (booking_id)
    WHERE status = 'Active';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260903163148_AddActiveHoldUniqueness', '8.0.26');

COMMIT;