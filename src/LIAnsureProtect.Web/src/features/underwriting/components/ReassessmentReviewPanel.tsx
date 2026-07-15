import { useState } from "react";

import { getUserErrorMessage } from "../../../lib/apiClient";
import { useReassessmentRequests, useReviewReassessmentRequest } from "../hooks/useReassessmentRequests";

export function ReassessmentReviewPanel() {
  const requestsQuery = useReassessmentRequests();
  const reviewMutation = useReviewReassessmentRequest();
  const [reasons, setReasons] = useState<Record<string, string>>({});

  return (
    <section className="mt-8 rounded-xl border border-sky-800 bg-sky-950/20 p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-xl font-semibold text-white">Reassessment review queue</h2>
          <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-300">Requests that exceed self-service safeguards wait here. Approval creates the next quote version; decline leaves the current quote unchanged.</p>
        </div>
        <span className="rounded-full bg-sky-300 px-3 py-1 text-xs font-bold text-slate-950">{requestsQuery.data?.length ?? 0} pending</span>
      </div>
      {requestsQuery.isPending && <p className="mt-4 text-sm text-slate-300">Loading reassessment requests...</p>}
      {requestsQuery.isError && <p className="mt-4 rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">{getUserErrorMessage(requestsQuery.error, "Unable to load reassessment requests.")}</p>}
      {requestsQuery.data?.length === 0 && <p className="mt-4 text-sm text-slate-400">No reassessment requests are waiting for review.</p>}
      <ul className="mt-4 space-y-4">
        {requestsQuery.data?.map((request) => {
          const reason = reasons[request.reassessmentRequestId] ?? "";
          return (
            <li key={request.reassessmentRequestId} className="rounded-lg border border-slate-700 bg-slate-900 p-4">
              <h3 className="font-semibold text-white">{request.companyName} · {request.submissionReference}</h3>
              <p className="mt-1 text-sm text-slate-300">Requested from quote version {request.baseQuoteVersion} on {new Date(request.requestedAtUtc).toLocaleString()}</p>
              <label className="mt-3 block text-sm font-semibold text-slate-200">
                Decision reason
                <textarea maxLength={2000} value={reason} onChange={(event) => setReasons((current) => ({ ...current, [request.reassessmentRequestId]: event.target.value }))} className="mt-2 min-h-20 w-full rounded-md border border-slate-700 bg-slate-950 p-3" />
              </label>
              <div className="mt-3 flex flex-wrap gap-3">
                <button type="button" disabled={reason.trim().length < 3 || reviewMutation.isPending} onClick={() => reviewMutation.mutate({ reassessmentRequestId: request.reassessmentRequestId, decision: "approve", reason: reason.trim() })} className="rounded-md bg-emerald-300 px-4 py-2 font-semibold text-slate-950 disabled:opacity-50">Approve and create quote</button>
                <button type="button" disabled={reason.trim().length < 3 || reviewMutation.isPending} onClick={() => reviewMutation.mutate({ reassessmentRequestId: request.reassessmentRequestId, decision: "decline", reason: reason.trim() })} className="rounded-md border border-red-700 px-4 py-2 font-semibold text-red-200 disabled:opacity-50">Decline</button>
              </div>
            </li>
          );
        })}
      </ul>
      {reviewMutation.isError && <p className="mt-4 rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">{getUserErrorMessage(reviewMutation.error, "Unable to review this reassessment request.")}</p>}
    </section>
  );
}
