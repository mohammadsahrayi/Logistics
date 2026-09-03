START TRANSACTION;

ALTER TABLE voyage_capacity ADD CONSTRAINT ck_voyage_capacity_sum CHECK ((held_capacity + confirmed_capacity) <= total_capacity);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260901191927_AddVoyageCapacitySumCheck', '8.0.26');

COMMIT;

