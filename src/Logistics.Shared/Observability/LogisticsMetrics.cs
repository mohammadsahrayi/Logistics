using System.Diagnostics.Metrics;

namespace Logistics.Shared.Observability
{
    public static class LogisticsMetrics
    {
        public static readonly Meter Meter = new("Logistics.Capacity", "1.0.0");
        public static readonly Counter<long> CapacityHoldCreated = Meter.CreateCounter<long>("capacity_hold_created_total");
        public static readonly Counter<long> CapacityHoldExpired = Meter.CreateCounter<long>("capacity_hold_expired_total");
        public static readonly Counter<long> CapacityHoldConfirmed = Meter.CreateCounter<long>("capacity_hold_confirmed_total");
        public static readonly Counter<long> CapacityConflict = Meter.CreateCounter<long>("capacity_conflict_total");
        public static readonly Histogram<double> ExpiryLagSeconds = Meter.CreateHistogram<double>("expiry_lag_seconds", "s");
        public static readonly Histogram<long> OutboxBacklog = Meter.CreateHistogram<long>("outbox_backlog", "messages");
        public static readonly Histogram<double> ConfirmationDuration = Meter.CreateHistogram<double>("confirmation_duration", "s");
    }
}
