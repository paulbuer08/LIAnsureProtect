using System.Diagnostics;
using System.Diagnostics.Metrics;
using LIAnsureProtect.Platform.Abstractions.Observability;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox;

internal static class OutboxDispatcherDiagnostics
{
    public static readonly ActivitySource ActivitySource = new(ObservabilityNames.ActivitySourceName);
    public static readonly Meter Meter = new(ObservabilityNames.MeterName);

    public static readonly Counter<long> Batches =
        Meter.CreateCounter<long>(ObservabilityNames.OutboxDispatchBatchesMetric);

    public static readonly Counter<long> PendingMessages =
        Meter.CreateCounter<long>(ObservabilityNames.OutboxDispatchPendingMessagesMetric);

    public static readonly Counter<long> ProcessedMessages =
        Meter.CreateCounter<long>(ObservabilityNames.OutboxDispatchProcessedMessagesMetric);

    public static readonly Counter<long> FailedMessages =
        Meter.CreateCounter<long>(ObservabilityNames.OutboxDispatchFailedMessagesMetric);

    public static readonly Histogram<double> DurationMs =
        Meter.CreateHistogram<double>(ObservabilityNames.OutboxDispatchDurationMetric, "ms");
}
