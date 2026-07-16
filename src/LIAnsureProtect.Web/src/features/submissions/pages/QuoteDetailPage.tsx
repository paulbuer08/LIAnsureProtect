import { Link, useParams } from "react-router";

import { getUserErrorMessage } from "../../../lib/apiClient";
import { formatCurrency } from "../../../lib/currency";
import { useQuoteDetail } from "../hooks/useQuoteDetail";
import { useAcknowledgeNotificationSubject } from "../../notifications/hooks/useNotifications";

export function QuoteDetailPage() {
  const { submissionId, quoteId } = useParams();
  const query = useQuoteDetail(submissionId, quoteId);
  const quote = query.data;
  useAcknowledgeNotificationSubject("quote", quoteId, { enabled: query.isSuccess });

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
      <section className="mx-auto max-w-4xl">
        <Link
          to={submissionId ? `/submissions/${submissionId}` : "/submissions"}
          className="text-sm font-semibold text-emerald-300"
        >
          Back to submission
        </Link>
        {submissionId && (
          <Link className="ml-5 text-sm font-semibold text-sky-300 underline" to={`/submissions/${submissionId}/quotes`}>
            All quote versions
          </Link>
        )}
        <p className="mt-8 text-sm font-semibold uppercase tracking-wide text-emerald-400">
          Quote history
        </p>
        <h1 className="mt-4 text-4xl font-bold tracking-tight">
          {quote ? `Quote version ${quote.version}` : "Quote detail"}
        </h1>

        {query.isPending && <p className="mt-8 text-slate-300">Loading quote...</p>}
        {query.isError && (
          <p className="mt-8 rounded-lg border border-red-900 bg-red-950 p-5 text-red-200">
            {getUserErrorMessage(query.error, "Unable to load this quote.")}
          </p>
        )}
        {quote && (
          <article className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-6">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <span className="rounded-md border border-emerald-500/50 px-3 py-1 text-sm font-semibold text-emerald-200">
                {quote.status}
              </span>
              <span className="text-sm text-slate-400">Expires {new Date(quote.expiresAtUtc).toLocaleDateString()}</span>
            </div>
            <dl className="mt-6 grid gap-5 sm:grid-cols-2">
              <div><dt className="text-slate-400">Premium</dt><dd className="mt-1 text-xl font-semibold">{formatCurrency(quote.premium)}</dd></div>
              <div><dt className="text-slate-400">Risk tier</dt><dd className="mt-1 font-semibold">{quote.riskTier}</dd></div>
              <div><dt className="text-slate-400">Limit</dt><dd className="mt-1 font-semibold">{formatCurrency(quote.requestedLimit)}</dd></div>
              <div><dt className="text-slate-400">Retention</dt><dd className="mt-1 font-semibold">{formatCurrency(quote.retention)}</dd></div>
              <div><dt className="text-slate-400">Assurance</dt><dd className="mt-1 font-semibold">{quote.assuranceStatus}</dd></div>
              <div><dt className="text-slate-400">Created</dt><dd className="mt-1 font-semibold">{new Date(quote.createdAtUtc).toLocaleString()}</dd></div>
            </dl>
            {quote.status === "Superseded" && (
              <p className="mt-6 rounded-md border border-slate-700 bg-slate-950 p-4 text-sm text-slate-300">
                This is an immutable historical quote. A later reassessment replaced it, but this version remains available for audit history.
                {quote.supersededAtUtc ? ` It became historical on ${new Date(quote.supersededAtUtc).toLocaleString()}.` : ""}
              </p>
            )}
          </article>
        )}
      </section>
    </main>
  );
}
