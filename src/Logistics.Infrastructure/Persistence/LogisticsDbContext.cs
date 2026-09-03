using Microsoft.EntityFrameworkCore;
using System;
using Logistics.Domain.Aggregates;

namespace Logistics.Infrastructure.Persistence
{
    public class LogisticsDbContext : DbContext
    {
        public LogisticsDbContext(DbContextOptions<LogisticsDbContext> options) : base(options)
        {
        }

        public DbSet<VoyageCapacityEntity> VoyageCapacities { get; set; }
        public DbSet<CapacityHoldEntity> CapacityHolds { get; set; }
        public DbSet<BookingEntity> Bookings { get; set; }
        public DbSet<OutboxMessageEntity> OutboxMessages { get; set; }
        public DbSet<InboxEntryEntity> InboxEntries { get; set; }
        public DbSet<BookingConfirmationProjectionEntity> BookingConfirmationProjections { get; set; }
        public DbSet<IdempotencyEntryEntity> IdempotencyEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<VoyageCapacityEntity>(b =>
            {
                b.ToTable("voyage_capacity");
                b.HasKey(x => x.VoyageId);
                b.Property(x => x.VoyageId).HasColumnName("voyage_id");
                b.Property(x => x.TotalCapacity).HasColumnName("total_capacity");
                b.Property(x => x.HeldCapacity).HasColumnName("held_capacity");
                b.Property(x => x.ConfirmedCapacity).HasColumnName("confirmed_capacity");
                b.Property(x => x.OperationalStatus).HasColumnName("operational_status");
                b.Property(x => x.Version).HasColumnName("version");
                b.Property(x => x.CreatedAt).HasColumnName("created_at");
                b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

                // checks
                b.HasCheckConstraint("ck_voyage_non_negative", "total_capacity >= 0 AND held_capacity >= 0 AND confirmed_capacity >= 0");
                b.HasCheckConstraint("ck_voyage_capacity_sum", "(held_capacity + confirmed_capacity) <= total_capacity");

                // concurrency token
                b.Property(x => x.Version).IsConcurrencyToken();
            });

            modelBuilder.Entity<CapacityHoldEntity>(b =>
            {
                b.ToTable("capacity_hold");
                b.HasKey(x => x.HoldId);
                b.Property(x => x.HoldId).HasColumnName("hold_id");
                b.Property(x => x.BookingId).HasColumnName("booking_id");
                b.Property(x => x.VoyageId).HasColumnName("voyage_id");
                b.Property(x => x.CapacityUnits).HasColumnName("capacity_units");
                b.Property(x => x.CreatedAt).HasColumnName("created_at");
                b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
                b.Property(x => x.Status).HasColumnName("status");
                b.Property(x => x.Version).HasColumnName("version");
                b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

                // Concurrency token for optimistic concurrency
                b.Property(x => x.Version).IsConcurrencyToken();

                b.HasIndex(x => new { x.Status, x.ExpiresAt }).HasDatabaseName("ix_capacity_hold_status_expiresat");

                // Index to support lookup of active hold per booking
                b.HasIndex(x => new { x.BookingId, x.Status }).HasDatabaseName("ix_capacity_hold_booking_status");
                b.HasIndex(x => x.BookingId)
                    .HasDatabaseName("ux_capacity_hold_active_booking")
                    .IsUnique()
                    .HasFilter("status = 'Active'");

                // Foreign keys
                b.HasOne<BookingEntity>().WithMany().HasForeignKey(x => x.BookingId).HasConstraintName("fk_capacityhold_booking").OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
                b.HasOne<VoyageCapacityEntity>().WithMany().HasForeignKey(x => x.VoyageId).HasConstraintName("fk_capacityhold_voyage").OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<BookingEntity>(b =>
            {
                b.ToTable("booking");
                b.HasKey(x => x.BookingId);
                b.Property(x => x.BookingId).HasColumnName("booking_id");
                b.Property(x => x.VoyageId).HasColumnName("voyage_id");
                b.Property(x => x.RequestedCapacity).HasColumnName("requested_capacity");
                b.Property(x => x.State).HasColumnName("state");
                b.Property(x => x.ActiveHoldId).HasColumnName("active_hold_id");
                b.Property(x => x.Version).HasColumnName("version");

                // Concurrency token
                b.Property(x => x.Version).IsConcurrencyToken();

                // Map timestamps to snake_case
                b.Property(x => x.CreatedAt).HasColumnName("created_at");
                b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

                // Foreign key to voyage_capacity
                b.HasOne<VoyageCapacityEntity>().WithMany().HasForeignKey(x => x.VoyageId).HasConstraintName("fk_booking_voyage").OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<OutboxMessageEntity>(b =>
            {
                b.ToTable("outbox_message");
                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("id");
                b.Property(x => x.MessageType).HasColumnName("message_type");
                b.Property(x => x.Payload).HasColumnName("payload");
                b.Property(x => x.OccurredAt).HasColumnName("occurred_at");
                b.Property(x => x.Processed).HasColumnName("processed");
                b.Property(x => x.PublishedAt).HasColumnName("published_at");
                b.Property(x => x.AttemptCount).HasColumnName("attempt_count");
                b.Property(x => x.LastError).HasColumnName("last_error");

                // Index to support publisher polling
                b.HasIndex(x => new { x.Processed, x.OccurredAt }).HasDatabaseName("ix_outbox_processed_occurredat");
            });

            modelBuilder.Entity<InboxEntryEntity>(b =>
            {
                b.ToTable("inbox_entry");
                b.HasKey(x => x.MessageId);
                b.Property(x => x.MessageId).HasColumnName("message_id");
                b.Property(x => x.ReceivedAt).HasColumnName("received_at");
            });

            modelBuilder.Entity<BookingConfirmationProjectionEntity>(b =>
            {
                b.ToTable("booking_confirmation_projection");
                b.HasKey(x => x.BookingId);
                b.Property(x => x.BookingId).HasColumnName("booking_id");
                b.Property(x => x.MessageId).HasColumnName("message_id");
                b.Property(x => x.HoldId).HasColumnName("hold_id");
                b.Property(x => x.VoyageId).HasColumnName("voyage_id");
                b.Property(x => x.CapacityUnits).HasColumnName("capacity_units");
                b.Property(x => x.ReceivedAt).HasColumnName("received_at");
                b.HasIndex(x => x.MessageId).IsUnique();
            });

            modelBuilder.Entity<IdempotencyEntryEntity>(b =>
            {
                b.ToTable("idempotency_entry");
                b.HasKey(x => x.IdempotencyKey);
                b.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key");
                b.Property(x => x.CreatedAt).HasColumnName("created_at");
                b.Property(x => x.RequestHash).HasColumnName("request_hash");
                b.Property(x => x.Status).HasColumnName("status");
                b.Property(x => x.ResponseStatusCode).HasColumnName("response_status_code");
                b.Property(x => x.ResponseBody).HasColumnName("response_body");
                b.Property(x => x.CompletedAt).HasColumnName("completed_at");
                b.Property(x => x.ResultJson).HasColumnName("result_json");
            });

            base.OnModelCreating(modelBuilder);
        }
    }

    public class VoyageCapacityEntity
    {
        public Guid VoyageId { get; set; }
        public int TotalCapacity { get; set; }
        public int HeldCapacity { get; set; }
        public int ConfirmedCapacity { get; set; }
        public string OperationalStatus { get; set; }
        public int Version { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CapacityHoldEntity
    {
        public Guid HoldId { get; set; }
        public Guid BookingId { get; set; }
        public Guid VoyageId { get; set; }
        public int CapacityUnits { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Status { get; set; }
        public int Version { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class BookingEntity
    {
        public Guid BookingId { get; set; }
        public Guid VoyageId { get; set; }
        public int RequestedCapacity { get; set; }
        public string State { get; set; }
        public Guid? ActiveHoldId { get; set; }
        public int Version { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class OutboxMessageEntity
    {
        public Guid Id { get; set; }
        public string MessageType { get; set; }
        public string Payload { get; set; }
        public DateTime OccurredAt { get; set; }
        public bool Processed { get; set; }
        public DateTime? PublishedAt { get; set; }
        public int AttemptCount { get; set; }
        public string? LastError { get; set; }
    }

    public class InboxEntryEntity
    {
        public Guid MessageId { get; set; }
        public DateTime ReceivedAt { get; set; }
    }

    public class BookingConfirmationProjectionEntity
    {
        public Guid BookingId { get; set; }
        public Guid MessageId { get; set; }
        public Guid HoldId { get; set; }
        public Guid VoyageId { get; set; }
        public int CapacityUnits { get; set; }
        public DateTime ReceivedAt { get; set; }
    }
}
