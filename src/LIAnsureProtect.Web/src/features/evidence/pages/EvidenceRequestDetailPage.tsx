import { useParams } from "react-router";

import { Breadcrumbs } from "../../../components/Breadcrumbs";
import { getUserErrorMessage } from "../../../lib/apiClient";
import { useEvidenceRequest } from "../hooks/useEvidenceRequests";
import { EvidenceRequestCard } from "./EvidenceRequestsPage";

export function EvidenceRequestDetailPage() {
  const { evidenceRequestId } = useParams();
  const query = useEvidenceRequest(evidenceRequestId);

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
      <section className="mx-auto max-w-5xl">
        <Breadcrumbs items={[{ label: "Dashboard", to: "/dashboard" }, { label: "Evidence requests", to: "/evidence-requests" }, { label: query.data?.title ?? "Request detail" }]} />
        <p className="mt-8 text-sm font-semibold uppercase tracking-wide text-emerald-400">
          Evidence request
        </p>
        <h1 className="mt-4 text-4xl font-bold tracking-tight">
          Review and respond
        </h1>

        {query.isPending && (
          <p className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-300">
            Loading evidence request...
          </p>
        )}
        {query.isError && (
          <p className="mt-8 rounded-lg border border-red-900 bg-red-950 p-5 text-sm text-red-200">
            {getUserErrorMessage(query.error, "Unable to load this evidence request.")}
          </p>
        )}
        {query.data && (
          <section className="mt-8">
            <EvidenceRequestCard request={query.data} />
          </section>
        )}
      </section>
    </main>
  );
}
