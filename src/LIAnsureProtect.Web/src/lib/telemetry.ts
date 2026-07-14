export type SafeTelemetryFields = Record<string, string | number | boolean | undefined>;

type CloudWatchRumBridge = {
  recordError(error: Error): void;
  recordEvent?(name: string, fields: SafeTelemetryFields): void;
};

declare global {
  interface Window {
    AwsRum?: CloudWatchRumBridge;
  }
}

const enabled = import.meta.env.VITE_CLOUDWATCH_RUM_ENABLED === "true";

export function captureClientError(error: unknown) {
  if (!enabled || !window.AwsRum) return;
  const safeError = new Error(
    error instanceof Error ? error.name : "UnexpectedBrowserError",
  );
  window.AwsRum.recordError(safeError);
}

export function captureSafeEvent(name: string, fields: SafeTelemetryFields) {
  if (!enabled || !window.AwsRum?.recordEvent) return;
  window.AwsRum.recordEvent(name, fields);
}
