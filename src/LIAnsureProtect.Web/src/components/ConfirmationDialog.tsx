import { useEffect, useId, useRef, type KeyboardEvent } from "react";

type ConfirmationDialogProps = {
  title: string;
  description: string;
  confirmLabel: string;
  isPending?: boolean;
  onCancel: () => void;
  onConfirm: () => void;
};

export function ConfirmationDialog({
  title,
  description,
  confirmLabel,
  isPending = false,
  onCancel,
  onConfirm,
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
          className="flex h-11 w-11 items-center justify-center rounded-full bg-red-500/15 text-xl text-red-300"
        >
          !
        </div>
        <h2 id={titleId} className="mt-5 text-xl font-semibold text-white">
          {title}
        </h2>
        <p id={descriptionId} className="mt-3 text-sm leading-6 text-slate-300">
          {description}
        </p>
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
            className="inline-flex min-h-11 items-center justify-center rounded-md bg-red-500 px-4 py-2 text-sm font-semibold text-white hover:bg-red-400 disabled:cursor-not-allowed disabled:bg-red-950 disabled:text-red-300"
          >
            {isPending ? "Deleting draft..." : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
