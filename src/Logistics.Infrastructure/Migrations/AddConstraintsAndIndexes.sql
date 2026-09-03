START TRANSACTION;
ALTER TABLE outbox_message RENAME COLUMN "PublishedAt" TO published_at;

ALTER TABLE outbox_message RENAME COLUMN "LastError" TO last_error;

ALTER TABLE outbox_message RENAME COLUMN "AttemptCount" TO attempt_count;

ALTER TABLE booking RENAME COLUMN "UpdatedAt" TO updated_at;

ALTER TABLE booking RENAME COLUMN "CreatedAt" TO created_at;

ALTER TABLE idempotency_entry ADD completed_at timestamp with time zone;

ALTER TABLE idempotency_entry ADD request_hash text;

ALTER TABLE idempotency_entry ADD response_body text;

ALTER TABLE idempotency_entry ADD response_status_code integer;

ALTER TABLE idempotency_entry ADD status text;

CREATE INDEX ix_outbox_processed_occurredat ON outbox_message (processed, occurred_at);

CREATE INDEX ix_capacity_hold_booking_status ON capacity_hold (booking_id, status);

CREATE INDEX "IX_capacity_hold_voyage_id" ON capacity_hold (voyage_id);

CREATE INDEX "IX_booking_voyage_id" ON booking (voyage_id);

ALTER TABLE booking ADD CONSTRAINT fk_booking_voyage FOREIGN KEY (voyage_id) REFERENCES voyage_capacity (voyage_id) ON DELETE RESTRICT;

ALTER TABLE capacity_hold ADD CONSTRAINT fk_capacityhold_booking FOREIGN KEY (booking_id) REFERENCES booking (booking_id) ON DELETE RESTRICT;

ALTER TABLE capacity_hold ADD CONSTRAINT fk_capacityhold_voyage FOREIGN KEY (voyage_id) REFERENCES voyage_capacity (voyage_id) ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260901191623_AddConstraintsAndIndexes', '8.0.26');

COMMIT;

