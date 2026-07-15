import { Link, useParams } from "react-router";

import { Breadcrumbs } from "../../../components/Breadcrumbs";
import { getUserErrorMessage } from "../../../lib/apiClient";
import { formatCurrency } from "../../../lib/currency";
import { useQuoteHistory } from "../hooks/useQuoteHistory";

export function QuoteHistoryPage() {
  const { submissionId } = useParams();
  const query = useQuoteHistory(submissionId);

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
      <section className="mx-auto max-w-5xl">
        <Breadcrumbs items={[
          { label: "Dashboard", to: "/dashboard" },
          { label: "Submissions", to: "/submissions" },
          { label: "Submission", to: submissionId ? `/submissions/${submissionId}` : "/submissions" },
          { label: "Quote history" },
        ]} />
        <p className="mt-8 text-sm font-semibold uppercase tracking-wide text-emerald-400">Quote history</p>
        <h1 className="mt-4 text-4xl font-bold tracking-tight">All quote versions</h1>
        <p className="mt-3 text-slate-300">The current quote is actionable. Earlier versions remain available as immutable audit history.</p>

        {query.isPending && <p className="mt-8 text-slate-300">Loading quote history...</p>}
        {query.isError && (
          <p className="mt-8 rounded-lg border border-red-900 bg-red-950 p-5 text-red-200">
            {getUserErrorMessage(query.error, "Unable to load quote history.")}
          </p>
        )}
        {query.data && (
          <ol className="mt-8 space-y-4">
            {query.data.quotes.map((quote) => {
              const historical = quote.status === "Superseded";
              return (
                <li key={quote.quoteId} className={`rounded-lg border p-5 ${historical ? "border-slate-800 bg-slate-900/60" : "border-emerald-700 bg-slate-900"}`}>
                  <div className="flex flex-wrap items-start justify-between gap-4">
                    <div>
                      <h2 className="text-lg font-semibold">Quote version {quote.version}</h2>
                      <p className="mt-2 text-sm text-slate-300">{formatCurrency(quote.premium)} · {quote.riskTier} risk · {quote.assuranceStatus}</p>
                      <p className="mt-2 text-xs text-slate-400">Created {new Date(quote.createdAtUtc).toLocaleString()}</p>
                    </div>
                    <span className={`rounded-full px-3 py-1 text-xs font-bold ${historical ? "bg-slate-700 text-slate-200" : "bg-emerald-300 text-slate-950"}`}>
                      {historical ? "Superseded" : "Current"}
                    </span>
                  </div>
                  <Link className="mt-4 inline-flex font-semibold text-emerald-300 underline" to={`/submissions/${submissionId}/quotes/${quote.quoteId}`}>
                    View quote version {quote.version}
                  </Link>
                </li>
              );
            })}
          </ol>
        )}
      </section>
    </main>
  );
}
