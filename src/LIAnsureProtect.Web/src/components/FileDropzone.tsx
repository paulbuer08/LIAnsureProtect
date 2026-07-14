import { useId, useRef, useState } from "react";
import type { ChangeEvent, DragEvent } from "react";

type FileDropzoneProps = {
  accept?: string;
  description: string;
  disabled?: boolean;
  files: File[];
  label: string;
  maxFiles?: number;
  multiple?: boolean;
  onFilesChange: (files: File[]) => void;
  required?: boolean;
  tone?: "emerald" | "amber";
};

function fileKey(file: File) {
  return `${file.name}:${file.size}:${file.lastModified}`;
}

function matchesAccept(file: File, accept?: string) {
  if (!accept) return true;

  const fileName = file.name.toLowerCase();
  const fileType = file.type.toLowerCase();

  return accept
    .split(",")
    .map((entry) => entry.trim().toLowerCase())
    .filter(Boolean)
    .some((entry) => {
      if (entry.startsWith(".")) return fileName.endsWith(entry);
      if (entry.endsWith("/*")) return fileType.startsWith(entry.slice(0, -1));
      return fileType === entry;
    });
}

export function FileDropzone({
  accept,
  description,
  disabled = false,
  files,
  label,
  maxFiles = 5,
  multiple = true,
  onFilesChange,
  required = false,
  tone = "emerald",
}: FileDropzoneProps) {
  const inputId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [selectionError, setSelectionError] = useState<string>();
  const accentClasses =
    tone === "amber"
      ? "border-amber-400 bg-amber-950/20"
      : "border-emerald-400 bg-emerald-950/20";
  const buttonClasses =
    tone === "amber"
      ? "bg-amber-300 hover:bg-amber-200"
      : "bg-emerald-400 hover:bg-emerald-300";

  function clearNativeSelection() {
    if (inputRef.current) inputRef.current.value = "";
  }

  function addFiles(selectedFiles: File[]) {
    const supportedFiles = selectedFiles.filter((file) => matchesAccept(file, accept));
    if (supportedFiles.length !== selectedFiles.length) {
      setSelectionError("One or more files use an unsupported format and were not added.");
    } else {
      setSelectionError(undefined);
    }

    const existingKeys = new Set(files.map(fileKey));
    const newFiles = supportedFiles.filter((file) => !existingKeys.has(fileKey(file)));
    const combinedFiles = multiple ? [...files, ...newFiles] : newFiles.slice(-1);

    if (combinedFiles.length > maxFiles) {
      setSelectionError(`You can select up to ${maxFiles} files at a time.`);
    }

    onFilesChange(combinedFiles.slice(0, maxFiles));
    clearNativeSelection();
  }

  function handleInputChange(event: ChangeEvent<HTMLInputElement>) {
    addFiles(Array.from(event.target.files ?? []));
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    setIsDragging(false);
    if (disabled) return;
    addFiles(Array.from(event.dataTransfer.files));
  }

  function removeFile(fileToRemove: File) {
    const keyToRemove = fileKey(fileToRemove);
    onFilesChange(files.filter((file) => fileKey(file) !== keyToRemove));
    setSelectionError(undefined);
    clearNativeSelection();
  }

  return (
    <div>
      <label className="block text-sm font-medium text-slate-200" htmlFor={inputId}>
        {label}
      </label>
      <div
        onDragEnter={(event) => {
          event.preventDefault();
          if (!disabled) setIsDragging(true);
        }}
        onDragLeave={(event) => {
          event.preventDefault();
          if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
            setIsDragging(false);
          }
        }}
        onDragOver={(event) => event.preventDefault()}
        onDrop={handleDrop}
        className={`mt-2 rounded-lg border border-dashed p-4 transition-colors ${
          isDragging ? accentClasses : "border-slate-600 bg-slate-950"
        } ${disabled ? "opacity-60" : ""}`}
      >
        <input
          ref={inputRef}
          id={inputId}
          aria-label={label}
          required={required && files.length === 0}
          disabled={disabled}
          multiple={multiple}
          type="file"
          accept={accept}
          onChange={handleInputChange}
          className="sr-only"
        />
        <div className="flex flex-wrap items-center gap-3">
          <button
            type="button"
            onClick={() => inputRef.current?.click()}
            disabled={disabled}
            className={`inline-flex cursor-pointer rounded-md px-4 py-2 text-sm font-semibold text-slate-950 focus:outline-none focus:ring-2 focus:ring-white disabled:cursor-not-allowed ${buttonClasses}`}
          >
            Choose files
          </button>
          <span className="text-sm text-slate-300">or drag and drop files here</span>
        </div>

        {files.length > 0 && (
          <ul className="mt-4 space-y-2" aria-label={`${label} selected files`}>
            {files.map((file) => (
              <li
                key={fileKey(file)}
                className="flex items-center justify-between gap-3 rounded-md border border-slate-700 bg-slate-900 px-3 py-2"
              >
                <span className="min-w-0 truncate text-sm font-medium text-white">
                  {file.name}
                </span>
                <button
                  type="button"
                  onClick={() => removeFile(file)}
                  disabled={disabled}
                  aria-label={`Remove ${file.name}`}
                  title={`Remove ${file.name}`}
                  className="inline-flex h-7 w-7 shrink-0 cursor-pointer items-center justify-center rounded-full border border-slate-600 text-lg leading-none text-slate-300 hover:border-red-400 hover:bg-red-950 hover:text-red-200 focus:outline-none focus:ring-2 focus:ring-emerald-400 disabled:cursor-not-allowed"
                >
                  <span aria-hidden="true">×</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
      <p className="mt-2 text-xs text-slate-400">{description}</p>
      {selectionError && (
        <p role="alert" className="mt-2 text-xs font-medium text-amber-200">
          {selectionError}
        </p>
      )}
    </div>
  );
}
