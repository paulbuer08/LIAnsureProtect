import { useState } from "react";
import { useAuth0 } from "@auth0/auth0-react";
import type { FormEvent } from "react";
import { Link } from "react-router";

import { downloadOwnerEvidenceDocument } from "../api/evidenceRequestsApi";
import {
  useEvidenceRequests,
  useRespondToEvidenceRequest,
  useUploadReplacementEvidenceDocuments,
} from "../hooks/useEvidenceRequests";
import type { QuoteEvidenceRequest } from "../types";

const dateFormatter = new Intl.DateTimeFormat("en-US", {
  month: "short",
  day: "numeric",
  year: "numeric",
});

function formatDate(value: string) {
  return dateFormatter.format(new Date(value));
}

function getErrorMessage(error: unknown, fallback: string) {
  return error instanceof Error ? error.message : fallback;
}

function formatEvidenceDueLabel(request: QuoteEvidenceRequest) {
  if (request.isOverdue) {
    const overdueDays = Math.abs(request.daysUntilDue);

    return overdueDays === 1
      ? "Overdue by 1 day"
      : `Overdue by ${overdueDays} days`;
  }

  if (request.daysUntilDue === 0) return "Due today";
  if (request.daysUntilDue === 1) return "Due in 1 day";

  return `Due in ${request.daysUntilDue} days`;
}

function EvidenceRequestCard({ request }: { request: QuoteEvidenceRequest }) {
  const { getAccessTokenSilently } = useAuth0();
  const respondToEvidenceRequest = useRespondToEvidenceRequest();
  const uploadReplacementEvidenceDocuments = useUploadReplacementEvidenceDocuments();
  const [respondentName, setRespondentName] = useState("");
  const [respondentTitle, setRespondentTitle] = useState("");
  const [responseText, setResponseText] = useState("");
  const [attachments, setAttachments] = useState<File[]>([]);
  const [replacementAttachments, setReplacementAttachments] = useState<File[]>([]);
  const [savedStatus, setSavedStatus] = useState<string>();
  const [savedDocuments, setSavedDocuments] = useState(request.documents ?? []);
  const documents = savedDocuments.length > 0 ? savedDocuments : request.documents ?? [];
  const needsSupplementalEvidence =
    request.reviewDecision === "Insufficient" ||
    request.reviewDecision === "NeedsClarification";
  const canSubmitResponse = request.status === "Open" || needsSupplementalEvidence;
  const canUploadReplacement = documents.some(
    (document) => document.scanStatus === "Rejected" || document.scanStatus === "Failed",
  );

  const [downloadError, setDownloadError] = useState<string>();

  async function handleDownloadDocument(documentId: string, fileName: string) {
    try {
      setDownloadError(undefined);
      const accessToken = await getAccessTokenSilently();
      await downloadOwnerEvidenceDocument(
        accessToken,
        request.evidenceRequestId,
        documentId,
        fileName,
      );
    } catch (error) {
      setDownloadError(getErrorMessage(error, "Unable to download the document."));
    }
  }

  async function handleRespond(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const result = await respondToEvidenceRequest.mutateAsync({
      evidenceRequestId: request.evidenceRequestId,
      request: {
        respondentName: respondentName.trim(),
        respondentTitle: respondentTitle.trim(),
        responseText: responseText.trim(),
        attachments,
      },
    });

    setSavedStatus(result.status);
    setSavedDocuments(result.documents ?? []);
  }

  async function handleReplacementUpload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const result = await uploadReplacementEvidenceDocuments.mutateAsync({
      evidenceRequestId: request.evidenceRequestId,
      attachments: replacementAttachments,
    });

    setSavedStatus(result.status);
    setSavedDocuments(result.documents ?? []);
    setReplacementAttachments([]);
  }

  return (
    <article className="rounded-lg border border-slate-800 bg-slate-900 p-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div>
          <h2 className="text-xl font-semibold text-white">{request.title}</h2>
          <p className="mt-1 text-sm text-slate-400">
            Quote {request.quoteId} | Submission {request.submissionId}
          </p>
        </div>
        <span className="w-fit rounded-md border border-amber-700 px-3 py-1 text-xs font-semibold text-amber-200">
          {request.status}
        </span>
        <span
          className={`w-fit rounded-md border px-3 py-1 text-xs font-semibold ${
            request.isOverdue
              ? "border-red-700 text-red-200"
              : "border-emerald-700 text-emerald-200"
          }`}
        >
          {formatEvidenceDueLabel(request)}
        </span>
      </div>

      <dl className="mt-4 grid gap-3 text-sm md:grid-cols-3">
        <div>
          <dt className="text-slate-400">Category</dt>
          <dd className="font-medium text-white">{request.category}</dd>
        </div>
        <div>
          <dt className="text-slate-400">Due</dt>
          <dd className="font-medium text-white">{formatDate(request.dueAtUtc)}</dd>
        </div>
        <div>
          <dt className="text-slate-400">Requested by</dt>
          <dd className="break-all font-medium text-white">{request.requestedByUserId}</dd>
        </div>
      </dl>

      <p className="mt-4 text-sm text-slate-300">{request.description}</p>

      <section className="mt-4 rounded-md border border-slate-800 bg-slate-950 p-4 text-sm">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-slate-400">Review decision</span>
          <span className="rounded-md border border-cyan-800 px-2 py-1 text-xs font-semibold text-cyan-200">
            {request.reviewDecision}
          </span>
        </div>
        {request.reviewReason && (
          <p className="mt-3 text-slate-300">
            <span className="font-semibold text-white">Underwriter reason:</span>{" "}
            {request.reviewReason}
          </p>
        )}
        {request.remediationGuidance && (
          <p className="mt-2 text-amber-100">
            <span className="font-semibold text-white">What to send next:</span>{" "}
            {request.remediationGuidance}
          </p>
        )}
        {request.reviewedByUserId && request.reviewedAtUtc && (
          <p className="mt-2 text-xs text-slate-400">
            Reviewed by {request.reviewedByUserId} on{" "}
            {formatDate(request.reviewedAtUtc)}.
          </p>
        )}
      </section>

      {savedStatus && (
        <p className="mt-4 rounded-md border border-emerald-800 bg-emerald-950 p-3 text-sm font-semibold text-emerald-100">
          Evidence response saved: {savedStatus}
        </p>
      )}

      {respondToEvidenceRequest.error !== null &&
        respondToEvidenceRequest.error !== undefined && (
          <p className="mt-4 whitespace-pre-wrap rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">
            {getErrorMessage(
              respondToEvidenceRequest.error,
              "Unable to submit evidence response.",
            )}
          </p>
        )}

      {uploadReplacementEvidenceDocuments.error !== null &&
        uploadReplacementEvidenceDocuments.error !== undefined && (
          <p className="mt-4 whitespace-pre-wrap rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">
            {getErrorMessage(
              uploadReplacementEvidenceDocuments.error,
              "Unable to upload replacement evidence.",
            )}
          </p>
        )}

      {canSubmitResponse && (
        <form className="mt-5 space-y-4" onSubmit={handleRespond}>
          <label className="block text-sm font-medium text-slate-200">
            Respondent name
            <input
              required
              value={respondentName}
              onChange={(event) => setRespondentName(event.target.value)}
              className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400"
            />
          </label>
          <label className="block text-sm font-medium text-slate-200">
            Respondent title
            <input
              required
              value={respondentTitle}
              onChange={(event) => setRespondentTitle(event.target.value)}
              className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400"
            />
          </label>
          <label className="block text-sm font-medium text-slate-200">
            Evidence response
            <textarea
              required
              value={responseText}
              onChange={(event) => setResponseText(event.target.value)}
              className="mt-2 min-h-24 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400"
            />
          </label>
          <label className="block text-sm font-medium text-slate-200">
            Evidence files
            <input
              aria-label="Evidence files"
              multiple
              type="file"
              accept=".pdf,.png,.jpg,.jpeg,.txt,.csv,.docx,.xlsx"
              onChange={(event) =>
                setAttachments(Array.from(event.target.files ?? []))
              }
              className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none file:mr-4 file:rounded-md file:border-0 file:bg-emerald-400 file:px-3 file:py-2 file:text-sm file:font-semibold file:text-slate-950 focus:border-emerald-400"
            />
            <span className="mt-2 block text-xs text-slate-400">
              Upload up to 5 files. Supported formats: PDF, PNG, JPEG, TXT, CSV,
              DOCX, and XLSX.
            </span>
          </label>
          <button
            type="submit"
            className="rounded-lg bg-emerald-400 px-5 py-3 text-sm font-semibold text-slate-950 hover:bg-emerald-300"
          >
            {needsSupplementalEvidence
              ? "Submit supplemental evidence"
              : "Submit evidence response"}
          </button>
        </form>
      )}

      {documents.length > 0 && (
        <section className="mt-5 rounded-md border border-slate-800 bg-slate-950 p-4">
          <h3 className="text-sm font-semibold text-white">Evidence documents</h3>
          {downloadError && (
            <p className="mt-2 rounded-md border border-red-900 bg-red-950 p-2 text-xs text-red-200">
              {downloadError}
            </p>
          )}
          <ul className="mt-3 space-y-2 text-sm">
            {documents.map((document) => (
              <li key={document.documentId} className="rounded-md border border-slate-800 p-3">
                {document.isDownloadAvailable ? (
                  <button
                    type="button"
                    onClick={() =>
                      void handleDownloadDocument(
                        document.documentId,
                        document.originalFileName,
                      )
                    }
                    className="font-semibold text-emerald-300 hover:text-emerald-200"
                  >
                    Download {document.originalFileName}
                  </button>
                ) : (
                  <span className="font-semibold text-slate-200">
                    {document.originalFileName}
                  </span>
                )}
                <span className="ml-2 text-slate-400">
                  {document.contentType} | {document.sizeBytes.toLocaleString()} bytes
                </span>
                <div className="mt-2 flex flex-wrap items-center gap-2">
                  <span className="rounded-md border border-cyan-800 px-2 py-1 text-xs font-semibold text-cyan-200">
                    {document.scanStatus}
                  </span>
                  {document.scanResultReason && (
                    <span className="text-xs text-slate-400">
                      {document.scanResultReason}
                    </span>
                  )}
                </div>
                {!document.isDownloadAvailable && (
                  <p className="mt-2 text-xs text-amber-200">
                    Download unavailable until security screening is clean.
                  </p>
                )}
              </li>
            ))}
          </ul>
          {canUploadReplacement && (
            <form className="mt-4 space-y-3" onSubmit={handleReplacementUpload}>
              <label className="block text-sm font-medium text-slate-200">
                Replacement evidence files
                <input
                  aria-label="Replacement evidence files"
                  multiple
                  type="file"
                  accept=".pdf,.png,.jpg,.jpeg,.txt,.csv,.docx,.xlsx"
                  onChange={(event) =>
                    setReplacementAttachments(Array.from(event.target.files ?? []))
                  }
                  className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none file:mr-4 file:rounded-md file:border-0 file:bg-amber-300 file:px-3 file:py-2 file:text-sm file:font-semibold file:text-slate-950 focus:border-amber-300"
                />
              </label>
              <button
                type="submit"
                className="rounded-lg bg-amber-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-amber-200"
              >
                Upload replacement evidence
              </button>
            </form>
          )}
        </section>
      )}
    </article>
  );
}

export function EvidenceRequestsPage() {
  const evidenceRequestsQuery = useEvidenceRequests();
  const evidenceRequests = evidenceRequestsQuery.data?.evidenceRequests ?? [];

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
      <section className="mx-auto max-w-5xl">
        <Link
          to="/dashboard"
          className="inline-flex text-sm font-semibold text-emerald-300 hover:text-emerald-200"
        >
          Back to dashboard
        </Link>

        <p className="mt-8 text-sm font-semibold uppercase tracking-wide text-emerald-400">
          Evidence requests
        </p>
        <h1 className="mt-4 text-4xl font-bold tracking-tight">
          Underwriting evidence requests
        </h1>
        <p className="mt-4 max-w-3xl text-slate-300">
          Respond to underwriter requests for supporting cyber-control evidence.
          This milestone stores uploaded evidence files privately through the API.
        </p>

        {evidenceRequestsQuery.isPending && (
          <p className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-300">
            Loading evidence requests...
          </p>
        )}

        {evidenceRequestsQuery.isError && (
          <p className="mt-8 whitespace-pre-wrap rounded-lg border border-red-900 bg-red-950 p-4 text-sm text-red-200">
            {getErrorMessage(
              evidenceRequestsQuery.error,
              "Unable to load evidence requests.",
            )}
          </p>
        )}

        {evidenceRequestsQuery.isSuccess && evidenceRequests.length === 0 && (
          <section className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-lg font-semibold text-white">
              No evidence requests are waiting for response.
            </h2>
            <p className="mt-2 text-sm text-slate-300">
              Requests appear here when underwriting needs more information for
              one of your referred quotes.
            </p>
          </section>
        )}

        {evidenceRequests.length > 0 && (
          <section className="mt-8 space-y-4">
            {evidenceRequests.map((request) => (
              <EvidenceRequestCard
                key={request.evidenceRequestId}
                request={request}
              />
            ))}
          </section>
        )}
      </section>
    </main>
  );
}
