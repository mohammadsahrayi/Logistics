CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

CREATE TABLE booking (
    booking_id uuid NOT NULL,
    voyage_id uuid NOT NULL,
    requested_capacity integer NOT NULL,
    state text NOT NULL,
    active_hold_id uuid,
    version integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_booking" PRIMARY KEY (booking_id)
);

CREATE TABLE capacity_hold (
    hold_id uuid NOT NULL,
    booking_id uuid NOT NULL,
    voyage_id uuid NOT NULL,
    capacity_units integer NOT NULL,
    created_at timestamp with time zone NOT NULL,
    expires_at timestamp with time zone NOT NULL,
    status text NOT NULL,
    version integer NOT NULL,
    updated_at timestamp with time zone,
    CONSTRAINT "PK_capacity_hold" PRIMARY KEY (hold_id)
);

CREATE TABLE idempotency_entry (
    idempotency_key text NOT NULL,
    created_at timestamp with time zone NOT NULL,
    result_json text,
    CONSTRAINT "PK_idempotency_entry" PRIMARY KEY (idempotency_key)
);

CREATE TABLE inbox_entry (
    message_id uuid NOT NULL,
    received_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_inbox_entry" PRIMARY KEY (message_id)
);

CREATE TABLE outbox_message (
    id uuid NOT NULL,
    message_type text NOT NULL,
    payload text NOT NULL,
    occurred_at timestamp with time zone NOT NULL,
    processed boolean NOT NULL,
    "PublishedAt" timestamp with time zone,
    "AttemptCount" integer NOT NULL,
    "LastError" text,
    CONSTRAINT "PK_outbox_message" PRIMARY KEY (id)
);

CREATE TABLE voyage_capacity (
    voyage_id uuid NOT NULL,
    total_capacity integer NOT NULL,
    held_capacity integer NOT NULL,
    confirmed_capacity integer NOT NULL,
    operational_status text NOT NULL,
    version integer NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone,
    CONSTRAINT "PK_voyage_capacity" PRIMARY KEY (voyage_id),
    CONSTRAINT ck_voyage_non_negative CHECK (total_capacity >= 0 AND held_capacity >= 0 AND confirmed_capacity >= 0)
);

CREATE INDEX ix_capacity_hold_status_expiresat ON capacity_hold (status, expires_at);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260901185816_InitialCreate', '8.0.26');

COMMIT;

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

