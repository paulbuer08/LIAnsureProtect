import { useEffect, useId, useRef, type KeyboardEvent } from "react";

type ConfirmationDialogInformation = {
  title: string;
  description: string;
};

type ConfirmationDialogProps = {
  title: string;
  description: string;
  confirmLabel: string;
  information?: ConfirmationDialogInformation;
  isPending?: boolean;
  onCancel: () => void;
  onConfirm: () => void;
  pendingLabel?: string;
  tone?: "danger" | "warning";
};

export function ConfirmationDialog({
  title,
  description,
  confirmLabel,
  information,
  isPending = false,
  onCancel,
  onConfirm,
  pendingLabel = "Working...",
  tone = "danger",
}: ConfirmationDialogProps) {
  const titleId = useId();
  const descriptionId = useId();
  const dialogRef = useRef<HTMLDivElement>(null);
  const cancelButtonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    const previouslyFocused = document.activeElement as HTMLElement | null;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    cancelButtonRef.current?.focus();

    return () => {
      document.body.style.overflow = previousOverflow;
      previouslyFocused?.focus();
    };
  }, []);

  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === "Escape" && !isPending) {
      event.preventDefault();
      onCancel();
      return;
    }

    if (event.key !== "Tab") {
      return;
    }

    const focusableElements = dialogRef.current?.querySelectorAll<HTMLElement>(
      "button:not(:disabled)",
    );
    if (!focusableElements || focusableElements.length === 0) {
      return;
    }

    const firstElement = focusableElements[0];
    const lastElement = focusableElements[focusableElements.length - 1];
    if (event.shiftKey && document.activeElement === firstElement) {
      event.preventDefault();
      lastElement.focus();
    } else if (!event.shiftKey && document.activeElement === lastElement) {
      event.preventDefault();
      firstElement.focus();
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/80 px-4 backdrop-blur-sm"
      role="presentation"
    >
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={descriptionId}
        className="w-full max-w-md rounded-xl border border-slate-700 bg-slate-900 p-6 text-left shadow-2xl shadow-black/50"
        onKeyDown={handleKeyDown}
      >
        <div
          aria-hidden="true"
          className={`flex h-11 w-11 items-center justify-center rounded-full text-xl ${
            tone === "warning"
              ? "bg-amber-500/15 text-amber-300"
              : "bg-red-500/15 text-red-300"
          }`}
        >
          !
        </div>
        <h2 id={titleId} className="mt-5 text-xl font-semibold text-white">
          {title}
        </h2>
        <p id={descriptionId} className="mt-3 text-sm leading-6 text-slate-300">
          {description}
        </p>
        {information && (
          <aside className="mt-5 rounded-lg border border-sky-400/30 bg-sky-950/30 p-4 text-sm text-sky-100">
            <div className="flex items-start gap-3">
              <span
                aria-hidden="true"
                className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full border border-sky-300/60 font-semibold"
              >
                ?
              </span>
              <div>
                <h3 className="font-semibold text-white">{information.title}</h3>
                <p className="mt-2 leading-6 text-slate-200">
                  {information.description}
                </p>
              </div>
            </div>
          </aside>
        )}
        <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <button
            ref={cancelButtonRef}
            type="button"
            disabled={isPending}
            onClick={onCancel}
            className="inline-flex min-h-11 items-center justify-center rounded-md border border-slate-600 px-4 py-2 text-sm font-semibold text-slate-100 hover:border-slate-400 disabled:cursor-not-allowed disabled:text-slate-500"
          >
            Cancel
          </button>
          <button
            type="button"
            disabled={isPending}
            onClick={onConfirm}
            className={`inline-flex min-h-11 items-center justify-center rounded-md px-4 py-2 text-sm font-semibold text-white disabled:cursor-not-allowed ${
              tone === "warning"
                ? "bg-amber-500 hover:bg-amber-400 disabled:bg-amber-950 disabled:text-amber-300"
                : "bg-red-500 hover:bg-red-400 disabled:bg-red-950 disabled:text-red-300"
            }`}
          >
            {isPending ? pendingLabel : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
