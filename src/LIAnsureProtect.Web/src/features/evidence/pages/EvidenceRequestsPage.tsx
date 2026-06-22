import { useState } from "react";
import type { FormEvent } from "react";
import { Link } from "react-router";

import {
  useEvidenceRequests,
  useRespondToEvidenceRequest,
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

function EvidenceRequestCard({ request }: { request: QuoteEvidenceRequest }) {
  const respondToEvidenceRequest = useRespondToEvidenceRequest();
  const [respondentName, setRespondentName] = useState("");
  const [respondentTitle, setRespondentTitle] = useState("");
  const [responseText, setResponseText] = useState("");
  const [attachmentFileName, setAttachmentFileName] = useState("");
  const [attachmentContentType, setAttachmentContentType] = useState("");
  const [attachmentSizeBytes, setAttachmentSizeBytes] = useState("");
  const [savedStatus, setSavedStatus] = useState<string>();

  async function handleRespond(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const result = await respondToEvidenceRequest.mutateAsync({
      evidenceRequestId: request.evidenceRequestId,
      request: {
        respondentName: respondentName.trim(),
        respondentTitle: respondentTitle.trim(),
        responseText: responseText.trim(),
        attachmentFileName: attachmentFileName.trim() || null,
        attachmentContentType: attachmentContentType.trim() || null,
        attachmentSizeBytes: attachmentSizeBytes.trim()
          ? Number(attachmentSizeBytes)
          : null,
      },
    });

    setSavedStatus(result.status);
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

      {request.status === "Open" && (
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
          <div className="grid gap-4 md:grid-cols-3">
            <label className="block text-sm font-medium text-slate-200">
              Attachment file name
              <input
                value={attachmentFileName}
                onChange={(event) => setAttachmentFileName(event.target.value)}
                className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400"
              />
            </label>
            <label className="block text-sm font-medium text-slate-200">
              Attachment content type
              <input
                value={attachmentContentType}
                onChange={(event) => setAttachmentContentType(event.target.value)}
                className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400"
              />
            </label>
            <label className="block text-sm font-medium text-slate-200">
              Attachment size bytes
              <input
                min="0"
                type="number"
                value={attachmentSizeBytes}
                onChange={(event) => setAttachmentSizeBytes(event.target.value)}
                className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400"
              />
            </label>
          </div>
          <button
            type="submit"
            className="rounded-lg bg-emerald-400 px-5 py-3 text-sm font-semibold text-slate-950 hover:bg-emerald-300"
          >
            Submit evidence response
          </button>
        </form>
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
          This milestone records response text and safe attachment metadata only.
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
