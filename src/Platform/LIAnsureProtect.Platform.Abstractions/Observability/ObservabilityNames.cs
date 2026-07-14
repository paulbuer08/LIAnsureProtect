namespace LIAnsureProtect.Platform.Abstractions.Observability;

public static class ObservabilityNames
{
    public const string ServiceName = "LIAnsureProtect";
    public const string ApiServiceName = "LIAnsureProtect.Api";
    public const string WorkerServiceName = "LIAnsureProtect.Worker";
    public const string ActivitySourceName = "LIAnsureProtect";
    public const string MeterName = "LIAnsureProtect";
    public const string CorrelationIdHeaderName = "X-Correlation-ID";
    public const string ApiRequestsMetric = "liansureprotect.api.requests";
    public const string ApiRequestDurationMetric = "liansureprotect.api.request.duration_ms";

    public const string OutboxDispatchBatchesMetric = "liansureprotect.outbox.dispatch.batches";
    public const string OutboxDispatchPendingMessagesMetric = "liansureprotect.outbox.dispatch.pending_messages";
    public const string OutboxDispatchProcessedMessagesMetric = "liansureprotect.outbox.dispatch.processed_messages";
    public const string OutboxDispatchFailedMessagesMetric = "liansureprotect.outbox.dispatch.failed_messages";
    public const string OutboxDispatchDurationMetric = "liansureprotect.outbox.dispatch.duration_ms";
}
