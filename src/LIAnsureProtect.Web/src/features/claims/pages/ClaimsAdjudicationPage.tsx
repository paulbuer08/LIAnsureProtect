import { useState } from "react";
import { useAuth0 } from "@auth0/auth0-react";
import { useSearchParams } from "react-router";
import { Breadcrumbs } from "../../../components/Breadcrumbs";

import { getUserErrorMessage } from "../../../lib/apiClient";
import { formatCurrency } from "../../../lib/currency";
import { downloadAdjudicationClaimDocument } from "../api/claimsApi";
import {
  useAdjudicationActions,
  useAdjudicationDetail,
  useAdjudicationQueue,
} from "../hooks/useClaimsAdjudication";
import { claimDenialReasons } from "../types";

function getErrorMessage(error: unknown) {
  return getUserErrorMessage(error, "Something went wrong.");
}

export function ClaimsAdjudicationPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [search, setSearch] = useState("");
  const [appliedSearch, setAppliedSearch] = useState("");
  const [status, setStatus] = useState("");
  const [assignment, setAssignment] = useState("");
  const [questions, setQuestions] = useState("");
  const queueQuery = useAdjudicationQueue({
    search: appliedSearch || undefined,
    status: status || undefined,
    assignment: assignment || undefined,
    hasOpenInformationRequests:
      questions === "open" ? true : questions === "none" ? false : undefined,
  });
  const { getAccessTokenSilently } = useAuth0();
  const selectedClaimId = searchParams.get("claimId");
  const [downloadError, setDownloadError] = useState<string>();

  function selectClaim(claimId: string | null) {
    const next = new URLSearchParams(searchParams);
    if (claimId) next.set("claimId", claimId);
    else next.delete("claimId");
    setSearchParams(next, { replace: true });
  }

  async function handleDownloadDocument(
    documentClaimId: string,
    documentId: string,
    fileName: string,
  ) {
    try {
      setDownloadError(undefined);
      const accessToken = await getAccessTokenSilently();
      await downloadAdjudicationClaimDocument(
        accessToken,
        documentClaimId,
        documentId,
        fileName,
      );
    } catch (error) {
      setDownloadError(getErrorMessage(error));
    }
  }
  const detailQuery = useAdjudicationDetail(selectedClaimId);
  const actions = useAdjudicationActions(selectedClaimId);

  const [noteText, setNoteText] = useState("");
  const [requestTitle, setRequestTitle] = useState("");
  const [requestMessage, setRequestMessage] = useState("");
  const [reserveAmount, setReserveAmount] = useState("");
  const [reserveReason, setReserveReason] = useState("");
  const [settlementAmount, setSettlementAmount] = useState("");
  const [acceptReason, setAcceptReason] = useState("");
  const [denialCategory, setDenialCategory] = useState<string>(
    claimDenialReasons[0],
  );
  const [denialNarrative, setDenialNarrative] = useState("");

  const queue = queueQuery.data?.claims ?? [];
  const claim = detailQuery.data;
  const isDecided =
    claim !== undefined && ["Accepted", "Denied"].includes(claim.status);
  const isWorkable =
    claim !== undefined &&
    !["Accepted", "Denied", "Closed"].includes(claim.status);

  const actionErrors = [
    actions.assign,
    actions.release,
    actions.addNote,
    actions.requestInformation,
    actions.setReserve,
    actions.accept,
    actions.deny,
    actions.close,
  ]
    .filter((mutation) => mutation.isError)
    .map((mutation) => getErrorMessage(mutation.error));

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
      <section className="mx-auto max-w-6xl">
        <Breadcrumbs items={[{ label: "Dashboard", to: "/dashboard" }, { label: "Claims adjudication" }]} />

        <p className="mt-8 text-sm font-semibold uppercase tracking-wide text-emerald-400">
          Claims adjudication
        </p>
        <h1 className="mt-4 text-4xl font-bold tracking-tight">
          Adjuster workbench
        </h1>
        <p className="mt-4 max-w-2xl text-slate-300">
          Open claims land here. Claim a file, ask the claimant for what you
          need, set the reserve, and record the decision.
        </p>

        <form className="mt-6 grid gap-4 rounded-lg border border-slate-800 bg-slate-900 p-4 md:grid-cols-4" onSubmit={(event) => { event.preventDefault(); setAppliedSearch(search.trim()); }}>
          <label className="text-sm font-semibold text-slate-200">Search queue<input className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" placeholder="Claim, policy, or adjuster" value={search} onChange={(event) => setSearch(event.target.value)} /></label>
          <label className="text-sm font-semibold text-slate-200">Status<select className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" value={status} onChange={(event) => setStatus(event.target.value)}><option value="">All statuses</option>{['Filed', 'UnderReview', 'InformationRequested', 'Accepted', 'Denied'].map((value) => <option key={value}>{value}</option>)}</select></label>
          <label className="text-sm font-semibold text-slate-200">Assignment<select className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" value={assignment} onChange={(event) => setAssignment(event.target.value)}><option value="">Any assignment</option><option value="assigned">Assigned</option><option value="unassigned">Unassigned</option></select></label>
          <label className="text-sm font-semibold text-slate-200">Claimant questions<select className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" value={questions} onChange={(event) => setQuestions(event.target.value)}><option value="">Any</option><option value="open">Has open questions</option><option value="none">No open questions</option></select></label>
          <div className="flex gap-3 md:col-span-4"><button type="submit" className="rounded-md bg-emerald-400 px-4 py-2 font-semibold text-slate-950">Search</button><button type="button" className="rounded-md border border-slate-600 px-4 py-2 font-semibold" onClick={() => { setSearch(""); setAppliedSearch(""); setStatus(""); setAssignment(""); setQuestions(""); }}>Clear</button></div>
        </form>

        {queueQuery.isPending && (
          <p className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-300">
            Loading the claims queue...
          </p>
        )}

        {queueQuery.isError && (
          <p className="mt-8 rounded-lg border border-red-900 bg-red-950 p-4 text-sm text-red-200">
            {getErrorMessage(queueQuery.error)}
          </p>
        )}

        {queueQuery.isSuccess && queue.length === 0 && (
          <section className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-lg font-semibold text-white">
              The queue is empty.
            </h2>
            <p className="mt-2 text-sm text-slate-300">
              New claims appear here as soon as they are filed.
            </p>
          </section>
        )}

        {queue.length > 0 && (
          <div className="mt-8 overflow-hidden rounded-lg border border-slate-800">
            <div className="grid grid-cols-1 gap-0 bg-slate-900 text-sm text-slate-200">
              {queue.map((item) => (
                <article
                  key={item.claimId}
                  className="border-b border-slate-800 p-5 last:border-b-0"
                >
                  <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                    <div>
                      <h2 className="text-lg font-semibold text-white">
                        {item.claimNumber}
                      </h2>
                      <p className="mt-1 text-slate-300">
                        {item.incidentType} · policy {item.policyNumber}
                      </p>
                      <p className="mt-1 text-slate-400">
                        Filed {new Date(item.filedAtUtc).toLocaleDateString()} ·{" "}
                        {item.assignedAdjusterUserId
                          ? `Assigned to ${item.assignedAdjusterUserId}`
                          : "Unassigned"}
                        {item.openInformationRequestCount > 0
                          ? ` · ${item.openInformationRequestCount} open question(s)`
                          : ""}
                      </p>
                    </div>
                    <div className="flex flex-col gap-3 md:items-end">
                      <span className="inline-flex w-fit rounded-md border border-emerald-800 px-3 py-1 text-xs font-semibold uppercase tracking-wide text-emerald-300">
                        {item.status}
                      </span>
                      <button
                        type="button"
                        aria-label={`Open claim ${item.claimNumber}`}
                        onClick={() =>
                          selectClaim(
                            selectedClaimId === item.claimId
                              ? null
                              : item.claimId,
                          )
                        }
                        className="inline-flex text-sm font-semibold text-emerald-300 hover:text-emerald-200"
                      >
                        {selectedClaimId === item.claimId
                          ? "Close working file"
                          : "Open working file"}
                      </button>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          </div>
        )}

        {selectedClaimId && detailQuery.isPending && (
          <p className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-300">
            Loading the working file...
          </p>
        )}

        {actionErrors.length > 0 && (
          <div className="mt-6 rounded-lg border border-red-900 bg-red-950 p-4 text-sm text-red-200">
            {actionErrors.map((message) => (
              <p key={message}>{message}</p>
            ))}
          </div>
        )}

        {claim && (
          <section className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-6 text-sm text-slate-200">
            <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
              <div>
                <h2 className="text-2xl font-bold text-white">
                  {claim.claimNumber}
                </h2>
                <p className="mt-2 max-w-2xl whitespace-pre-wrap text-slate-300">
                  {claim.description}
                </p>
                <p className="mt-2 text-slate-400">
                  {claim.incidentType} · incident{" "}
                  {new Date(claim.incidentAtUtc).toLocaleDateString()} ·
                  discovered{" "}
                  {new Date(claim.discoveredAtUtc).toLocaleDateString()} ·
                  claimant {claim.ownerUserId}
                </p>
                <p className="mt-1 text-slate-400">
                  Policy {claim.policyNumber} · limit{" "}
                  {formatCurrency(claim.policyLimitAtFiling)} · retention{" "}
                  {formatCurrency(claim.policyRetentionAtFiling)}
                </p>
              </div>
              <div className="flex flex-col gap-2 md:items-end">
                <span className="inline-flex w-fit rounded-md border border-emerald-800 px-3 py-1 text-xs font-semibold uppercase tracking-wide text-emerald-300">
                  {claim.status}
                </span>
                <p className="text-slate-400">
                  {claim.assignedAdjusterUserId
                    ? `Assigned to ${claim.assignedAdjusterUserId}`
                    : "Unassigned"}
                </p>
                {isWorkable && (
                  <div className="flex gap-2">
                    <button
                      type="button"
                      onClick={() => actions.assign.mutate()}
                      disabled={actions.assign.isPending}
                      className="rounded-lg bg-emerald-400 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-emerald-300 disabled:opacity-60"
                    >
                      Assign to me
                    </button>
                    <button
                      type="button"
                      onClick={() => actions.release.mutate()}
                      disabled={actions.release.isPending}
                      className="rounded-lg border border-slate-700 px-4 py-2 text-sm font-semibold text-slate-200 hover:border-emerald-400 disabled:opacity-60"
                    >
                      Release assignment
                    </button>
                  </div>
                )}
              </div>
            </div>

            <div className="mt-6 grid grid-cols-1 gap-4 md:grid-cols-3">
              <div className="rounded-lg border border-slate-800 bg-slate-950 p-4">
                <p className="text-xs uppercase tracking-wide text-slate-400">
                  Claimed
                </p>
                <p className="mt-1 text-xl font-semibold text-white">
                  {claim.claimedAmount === null
                    ? "—"
                    : formatCurrency(claim.claimedAmount)}
                </p>
              </div>
              <div className="rounded-lg border border-slate-800 bg-slate-950 p-4">
                <p className="text-xs uppercase tracking-wide text-slate-400">
                  Reserve
                </p>
                <p className="mt-1 text-xl font-semibold text-white">
                  {formatCurrency(claim.reserveAmount)}
                </p>
              </div>
              <div className="rounded-lg border border-slate-800 bg-slate-950 p-4">
                <p className="text-xs uppercase tracking-wide text-slate-400">
                  Paid
                </p>
                <p className="mt-1 text-xl font-semibold text-white">
                  {formatCurrency(claim.paidAmount)}
                </p>
              </div>
            </div>

            {isWorkable && (
              <div className="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-2">
                <form
                  className="rounded-lg border border-slate-800 bg-slate-950 p-4"
                  onSubmit={(event) => {
                    event.preventDefault();
                    const amount = Number(reserveAmount);
                    if (Number.isFinite(amount) && reserveReason.trim()) {
                      actions.setReserve.mutate(
                        { amount, reason: reserveReason },
                        { onSuccess: () => setReserveReason("") },
                      );
                    }
                  }}
                >
                  <h3 className="font-semibold text-white">Reserve</h3>
                  <label
                    className="mt-3 block text-slate-200"
                    htmlFor="reserve-amount"
                  >
                    Reserve amount
                  </label>
                  <input
                    id="reserve-amount"
                    type="number"
                    min="0"
                    step="0.01"
                    required
                    value={reserveAmount}
                    onChange={(event) => setReserveAmount(event.target.value)}
                    className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white outline-none focus:border-emerald-400"
                  />
                  <label
                    className="mt-3 block text-slate-200"
                    htmlFor="reserve-reason"
                  >
                    Reserve reason
                  </label>
                  <input
                    id="reserve-reason"
                    type="text"
                    required
                    value={reserveReason}
                    onChange={(event) => setReserveReason(event.target.value)}
                    className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white outline-none focus:border-emerald-400"
                  />
                  <button
                    type="submit"
                    disabled={actions.setReserve.isPending}
                    className="mt-4 rounded-lg bg-emerald-400 px-4 py-2 font-semibold text-slate-950 hover:bg-emerald-300 disabled:opacity-60"
                  >
                    Update reserve
                  </button>
                </form>

                <form
                  className="rounded-lg border border-slate-800 bg-slate-950 p-4"
                  onSubmit={(event) => {
                    event.preventDefault();
                    if (requestTitle.trim() && requestMessage.trim()) {
                      actions.requestInformation.mutate(
                        { title: requestTitle, message: requestMessage },
                        {
                          onSuccess: () => {
                            setRequestTitle("");
                            setRequestMessage("");
                          },
                        },
                      );
                    }
                  }}
                >
                  <h3 className="font-semibold text-white">
                    Ask the claimant
                  </h3>
                  <label
                    className="mt-3 block text-slate-200"
                    htmlFor="request-title"
                  >
                    Information request title
                  </label>
                  <input
                    id="request-title"
                    type="text"
                    required
                    value={requestTitle}
                    onChange={(event) => setRequestTitle(event.target.value)}
                    className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white outline-none focus:border-emerald-400"
                  />
                  <label
                    className="mt-3 block text-slate-200"
                    htmlFor="request-message"
                  >
                    Information request message
                  </label>
                  <textarea
                    id="request-message"
                    rows={3}
                    required
                    value={requestMessage}
                    onChange={(event) => setRequestMessage(event.target.value)}
                    className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white outline-none focus:border-emerald-400"
                  />
                  <button
                    type="submit"
                    disabled={actions.requestInformation.isPending}
                    className="mt-4 rounded-lg bg-emerald-400 px-4 py-2 font-semibold text-slate-950 hover:bg-emerald-300 disabled:opacity-60"
                  >
                    Send information request
                  </button>
                </form>

                <form
                  className="rounded-lg border border-slate-800 bg-slate-950 p-4"
                  onSubmit={(event) => {
                    event.preventDefault();
                    const amount = Number(settlementAmount);
                    if (Number.isFinite(amount) && acceptReason.trim()) {
                      actions.accept.mutate({
                        settlementAmount: amount,
                        reason: acceptReason,
                        notes: null,
                      });
                    }
                  }}
                >
                  <h3 className="font-semibold text-white">Accept</h3>
                  <label
                    className="mt-3 block text-slate-200"
                    htmlFor="settlement-amount"
                  >
                    Settlement amount
                  </label>
                  <input
                    id="settlement-amount"
                    type="number"
                    min="0.01"
                    step="0.01"
                    required
                    value={settlementAmount}
                    onChange={(event) => setSettlementAmount(event.target.value)}
                    className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white outline-none focus:border-emerald-400"
                  />
                  <p className="mt-1 text-xs text-slate-400">
                    Cap: limit net of retention ={" "}
                    {formatCurrency(
                      claim.policyLimitAtFiling - claim.policyRetentionAtFiling,
                    )}
                  </p>
                  <label
                    className="mt-3 block text-slate-200"
                    htmlFor="accept-reason"
                  >
                    Acceptance reason
                  </label>
                  <input
                    id="accept-reason"
                    type="text"
                    required
                    value={acceptReason}
                    onChange={(event) => setAcceptReason(event.target.value)}
                    className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white outline-none focus:border-emerald-400"
                  />
                  <button
                    type="submit"
                    disabled={actions.accept.isPending}
                    className="mt-4 rounded-lg bg-emerald-400 px-4 py-2 font-semibold text-slate-950 hover:bg-emerald-300 disabled:opacity-60"
                  >
                    Accept claim
                  </button>
                </form>

                <form
                  className="rounded-lg border border-slate-800 bg-slate-950 p-4"
                  onSubmit={(event) => {
                    event.preventDefault();
                    if (denialNarrative.trim()) {
                      actions.deny.mutate({
                        reasonCategory: denialCategory,
                        narrative: denialNarrative,
                      });
                    }
                  }}
                >
                  <h3 className="font-semibold text-white">Deny</h3>
                  <label
                    className="mt-3 block text-slate-200"
                    htmlFor="denial-category"
                  >
                    Denial reason
                  </label>
                  <select
                    id="denial-category"
                    value={denialCategory}
                    onChange={(event) => setDenialCategory(event.target.value)}
                    className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white outline-none focus:border-emerald-400"
                  >
                    {claimDenialReasons.map((reason) => (
                      <option key={reason} value={reason}>
                        {reason}
                      </option>
                    ))}
                  </select>
                  <label
                    className="mt-3 block text-slate-200"
                    htmlFor="denial-narrative"
                  >
                    Denial narrative
                  </label>
                  <textarea
                    id="denial-narrative"
                    rows={3}
                    required
                    value={denialNarrative}
                    onChange={(event) => setDenialNarrative(event.target.value)}
                    className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white outline-none focus:border-emerald-400"
                  />
                  <button
                    type="submit"
                    disabled={actions.deny.isPending}
                    className="mt-4 rounded-lg border border-red-800 px-4 py-2 font-semibold text-red-300 hover:border-red-500 disabled:opacity-60"
                  >
                    Deny claim
                  </button>
                </form>
              </div>
            )}

            {isDecided && (
              <div className="mt-6 rounded-lg border border-emerald-900 bg-slate-950 p-4">
                <h3 className="font-semibold text-white">Decision recorded</h3>
                <p className="mt-2 text-slate-300">
                  {claim.settlementAmount !== null
                    ? `Accepted with a settlement of ${formatCurrency(claim.settlementAmount)}.`
                    : `Denied (${claim.denialReason}): ${claim.denialNarrative}`}
                </p>
                <button
                  type="button"
                  onClick={() => actions.close.mutate()}
                  disabled={actions.close.isPending}
                  className="mt-4 rounded-lg bg-emerald-400 px-4 py-2 font-semibold text-slate-950 hover:bg-emerald-300 disabled:opacity-60"
                >
                  Close claim
                </button>
              </div>
            )}

            {isWorkable && (
              <form
                className="mt-6 rounded-lg border border-slate-800 bg-slate-950 p-4"
                onSubmit={(event) => {
                  event.preventDefault();
                  if (noteText.trim()) {
                    actions.addNote.mutate(
                      { note: noteText },
                      { onSuccess: () => setNoteText("") },
                    );
                  }
                }}
              >
                <h3 className="font-semibold text-white">Internal notes</h3>
                <label className="mt-3 block text-slate-200" htmlFor="work-note">
                  Work note
                </label>
                <textarea
                  id="work-note"
                  rows={2}
                  required
                  value={noteText}
                  onChange={(event) => setNoteText(event.target.value)}
                  className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white outline-none focus:border-emerald-400"
                />
                <button
                  type="submit"
                  disabled={actions.addNote.isPending}
                  className="mt-3 rounded-lg bg-emerald-400 px-4 py-2 font-semibold text-slate-950 hover:bg-emerald-300 disabled:opacity-60"
                >
                  Add note
                </button>
              </form>
            )}

            {claim.workNotes.length > 0 && (
              <ul className="mt-4 space-y-2">
                {claim.workNotes.map((note) => (
                  <li
                    key={note.noteId}
                    className="rounded-lg border border-slate-800 bg-slate-950 p-3"
                  >
                    <p className="text-slate-200">{note.note}</p>
                    <p className="mt-1 text-xs text-slate-400">
                      {note.createdByUserId} ·{" "}
                      {new Date(note.createdAtUtc).toLocaleString()}
                    </p>
                  </li>
                ))}
              </ul>
            )}

            {claim.informationRequests.length > 0 && (
              <div className="mt-6">
                <h3 className="font-semibold text-white">
                  Information requests
                </h3>
                <ul className="mt-3 space-y-2">
                  {claim.informationRequests.map((request) => (
                    <li
                      key={request.informationRequestId}
                      className="rounded-lg border border-slate-800 bg-slate-950 p-3"
                    >
                      <p className="font-semibold text-white">
                        {request.title}
                      </p>
                      <p className="mt-1 text-slate-300">{request.message}</p>
                      <p className="mt-2 text-slate-400">
                        {request.isAnswered
                          ? `Answered: ${request.responseText}`
                          : "Waiting for the claimant."}
                      </p>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {claim.documents.length > 0 && (
              <div className="mt-6">
                <h3 className="font-semibold text-white">Documents</h3>
                {downloadError && (
                  <p className="mt-2 rounded-lg border border-red-900 bg-red-950 p-3 text-red-200">
                    {downloadError}
                  </p>
                )}
                <ul className="mt-3 space-y-2">
                  {claim.documents.map((document) => (
                    <li
                      key={document.documentId}
                      className="flex flex-col gap-2 rounded-lg border border-slate-800 bg-slate-950 p-3 sm:flex-row sm:items-center sm:justify-between"
                    >
                      <div>
                        <p className="font-semibold text-white">
                          {document.originalFileName}
                        </p>
                        <p className="text-slate-400">
                          {document.kind} · scan {document.scanStatus}
                        </p>
                      </div>
                      {document.isDownloadAvailable ? (
                        <button
                          type="button"
                          onClick={() =>
                            void handleDownloadDocument(
                              claim.claimId,
                              document.documentId,
                              document.originalFileName,
                            )
                          }
                          className="cursor-pointer font-semibold text-emerald-300 hover:text-emerald-200"
                        >
                          Download {document.originalFileName}
                        </button>
                      ) : (
                        <span className="text-slate-400">
                          Not downloadable ({document.scanStatus})
                        </span>
                      )}
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {claim.reserveHistory.length > 0 && (
              <div className="mt-6">
                <h3 className="font-semibold text-white">Reserve history</h3>
                <ul className="mt-3 space-y-2">
                  {claim.reserveHistory.map((change) => (
                    <li
                      key={change.changeId}
                      className="rounded-lg border border-slate-800 bg-slate-950 p-3"
                    >
                      <p className="text-slate-200">
                        {formatCurrency(change.oldAmount)} →{" "}
                        {formatCurrency(change.newAmount)}
                      </p>
                      <p className="mt-1 text-slate-300">{change.reason}</p>
                      <p className="mt-1 text-xs text-slate-400">
                        {change.changedByUserId} ·{" "}
                        {new Date(change.changedAtUtc).toLocaleString()}
                      </p>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {claim.decisions.length > 0 && (
              <div className="mt-6">
                <h3 className="font-semibold text-white">Decision audit</h3>
                <ul className="mt-3 space-y-2">
                  {claim.decisions.map((decision) => (
                    <li
                      key={`${decision.outcome}-${decision.decidedAtUtc}`}
                      className="rounded-lg border border-slate-800 bg-slate-950 p-3"
                    >
                      <p className="font-semibold text-white">
                        {decision.outcome}
                        {decision.settlementAmount !== null
                          ? ` — ${formatCurrency(decision.settlementAmount)}`
                          : ""}
                      </p>
                      <p className="mt-1 text-slate-300">{decision.reason}</p>
                      <p className="mt-1 text-xs text-slate-400">
                        {decision.decidedByUserId} ·{" "}
                        {new Date(decision.decidedAtUtc).toLocaleString()}
                      </p>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            <div className="mt-6">
              <h3 className="font-semibold text-white">Timeline</h3>
              <ol className="mt-3 space-y-2">
                {claim.timeline.map((entry) => (
                  <li
                    key={entry.entryId}
                    className="rounded-lg border border-slate-800 bg-slate-950 p-3"
                  >
                    <p className="text-slate-200">{entry.summary}</p>
                    <p className="mt-1 text-xs text-slate-400">
                      {entry.entryType} ·{" "}
                      {new Date(entry.createdAtUtc).toLocaleString()}
                    </p>
                  </li>
                ))}
              </ol>
            </div>
          </section>
        )}
      </section>
    </main>
  );
}
