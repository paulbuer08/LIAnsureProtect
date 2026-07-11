import { useEffect, useRef, useState, type ReactNode } from "react";

type TransientStatusMessageProps = {
  children: ReactNode;
  className?: string;
  durationMs?: number;
  onDismiss: () => void;
  tone?: "success" | "warning";
};

const fadeDurationMs = 500;

export function TransientStatusMessage({
  children,
  className = "",
  durationMs = 5_000,
  onDismiss,
  tone = "success",
}: TransientStatusMessageProps) {
  const [isFading, setIsFading] = useState(false);
  const onDismissRef = useRef(onDismiss);

  useEffect(() => {
    onDismissRef.current = onDismiss;
  }, [onDismiss]);

  useEffect(() => {
    const fadeTimer = window.setTimeout(
      () => setIsFading(true),
      Math.max(0, durationMs - fadeDurationMs),
    );
    const dismissTimer = window.setTimeout(
      () => onDismissRef.current(),
      durationMs,
    );

    return () => {
      window.clearTimeout(fadeTimer);
      window.clearTimeout(dismissTimer);
    };
  }, [durationMs]);

  return (
    <div
      role="status"
      aria-live="polite"
      className={`grid transition-[grid-template-rows,opacity,transform] duration-500 ease-out motion-reduce:transition-none ${
        isFading
          ? "grid-rows-[0fr] -translate-y-1 opacity-0"
          : "grid-rows-[1fr] translate-y-0 opacity-100"
      } ${className}`}
    >
      <div className="overflow-hidden">
        <div
          className={`rounded-md border p-4 ${
            tone === "warning"
              ? "border-amber-500/40 bg-amber-950/30 text-amber-100"
              : "border-emerald-500/40 bg-emerald-950/30 text-emerald-100"
          }`}
        >
          {children}
        </div>
      </div>
    </div>
  );
}
