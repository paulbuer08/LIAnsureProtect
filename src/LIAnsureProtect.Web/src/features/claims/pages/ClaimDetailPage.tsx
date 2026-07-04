import { useState } from "react";
import { Link, useParams } from "react-router";

import { getOwnerClaimDocumentDownloadUrl } from "../api/claimsApi";
import { useClaimDetail, useClaimantActions } from "../hooks/useClaims";
import { claimDocumentKinds } from "../types";

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Something went wrong.";
}

function formatMoney(amount: number) {
  return amount.toLocaleString("en-US");
}

export function ClaimDetailPage() {
  const { claimId = "" } = useParams();
  const detailQuery = useClaimDetail(claimId);
  const actions = useClaimantActions(claimId);
  const [responseTexts, setResponseTexts] = useState<Record<string, string>>({});
  const [claimedAmountInput, setClaimedAmountInput] = useState("");
  const [documentKind, setDocumentKind] = useState<string>(
    claimDocumentKinds[0],
  );
  const [documentFiles, setDocumentFiles] = useState<File[]>([]);

  const claim = detailQuery.data;
  const isOpen =
    claim !== undefined &&
    !["Accepted", "Denied", "Closed"].includes(claim.status);

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
      <section className="mx-auto max-w-5xl">
        <Link
          to="/claims"
          className="inline-flex text-sm font-semibold text-emerald-300 hover:text-emerald-200"
        >
          Back to my claims
        </Link>

        {detailQuery.isPending && (
          <p className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-300">
            Loading claim...
          </p>
        )}

        {detailQuery.isError && (
          <p className="mt-8 whitespace-pre-wrap rounded-lg border border-red-900 bg-red-950 p-4 text-sm text-red-200">
            {getErrorMessage(detailQuery.error)}
          </p>
        )}

        {claim && (
          <>
            <div className="mt-8 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
              <div>
                <p className="text-sm font-semibold uppercase tracking-wide text-emerald-400">
                  Claim
                </p>
                <h1 className="mt-4 text-4xl font-bold tracking-tight">
                  {claim.claimNumber}
                </h1>
                <p className="mt-4 max-w-2xl text-slate-300">
                  {claim.incidentType} on policy {claim.policyNumber}
                </p>
              </div>
              <span className="inline-flex w-fit rounded-md border border-emerald-800 px-3 py-1 text-xs font-semibold uppercase tracking-wide text-emerald-300">
                {claim.status}
              </span>
            </div>

            <section className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-200">
              <h2 className="text-lg font-semibold text-white">Incident</h2>
              <p className="mt-2 whitespace-pre-wrap text-slate-300">
                {claim.description}
              </p>
              <p className="mt-3 text-slate-400">
                Incident {new Date(claim.incidentAtUtc).toLocaleDateString()} ·
                Discovered{" "}
                {new Date(claim.discoveredAtUtc).toLocaleDateString()} · Policy
                limit {formatMoney(claim.policyLimitAtFiling)} · Retention{" "}
                {formatMoney(claim.policyRetentionAtFiling)}
              </p>
            </section>

            {(claim.settlementAmount !== null ||
              claim.denialReason !== null) && (
              <section className="mt-6 rounded-lg border border-emerald-900 bg-slate-900 p-5 text-sm">
                <h2 className="text-lg font-semibold text-white">Decision</h2>
                {claim.settlementAmount !== null && (
                  <p className="mt-2 text-slate-200">
                    Settlement of {formatMoney(claim.settlementAmount)} — paid{" "}
                    {formatMoney(claim.paidAmount)}.
                  </p>
                )}
                {claim.denialReason !== null && (
                  <p className="mt-2 text-slate-200">
                    Denied ({claim.denialReason}): {claim.denialNarrative}
                  </p>
                )}
                {claim.decidedAtUtc && (
                  <p className="mt-2 text-slate-400">
                    Decided {new Date(claim.decidedAtUtc).toLocaleDateString()}
                    {claim.closedAtUtc
                      ? ` · Closed ${new Date(claim.closedAtUtc).toLocaleDateString()}`
                      : ""}
                  </p>
                )}
              </section>
            )}

            {isOpen && (
              <section className="mt-6 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm">
                <h2 className="text-lg font-semibold text-white">
                  Claimed amount
                </h2>
                <p className="mt-2 text-slate-300">
                  {claim.claimedAmount === null
                    ? "You have not declared a claimed amount yet."
                    : `Currently declared: ${formatMoney(claim.claimedAmount)}.`}
                </p>
                <form
                  className="mt-4 flex flex-col gap-3 sm:flex-row"
                  onSubmit={(event) => {
                    event.preventDefault();
                    const amount = Number(claimedAmountInput);
                    if (Number.isFinite(amount) && amount > 0) {
                      actions.declareClaimedAmount.mutate({ amount });
                    }
                  }}
                >
                  <label className="sr-only" htmlFor="claimed-amount">
                    Claimed amount
                  </label>
                  <input
                    id="claimed-amount"
                    type="number"
                    min="1"
                    step="0.01"
                    required
                    value={claimedAmountInput}
                    onChange={(event) =>
                      setClaimedAmountInput(event.target.value)
                    }
                    className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400 sm:max-w-xs"
                  />
                  <button
                    type="submit"
                    disabled={actions.declareClaimedAmount.isPending}
                    className="rounded-lg bg-emerald-400 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-emerald-300 disabled:opacity-60"
                  >
                    Update claimed amount
                  </button>
                </form>
                {actions.declareClaimedAmount.isError && (
                  <p className="mt-3 rounded-lg border border-red-900 bg-red-950 p-3 text-red-200">
                    {getErrorMessage(actions.declareClaimedAmount.error)}
                  </p>
                )}
              </section>
            )}

            <section className="mt-6 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm">
              <h2 className="text-lg font-semibold text-white">
                Questions from your adjuster
              </h2>
              {claim.informationRequests.length === 0 && (
                <p className="mt-2 text-slate-300">
                  No information requests on this claim.
                </p>
              )}
              {claim.informationRequests.map((request) => (
                <article
                  key={request.informationRequestId}
                  className="mt-4 rounded-lg border border-slate-800 bg-slate-950 p-4"
                >
                  <h3 className="font-semibold text-white">{request.title}</h3>
                  <p className="mt-1 whitespace-pre-wrap text-slate-300">
                    {request.message}
                  </p>
                  {request.isAnswered ? (
                    <p className="mt-3 rounded-lg border border-emerald-900 bg-slate-900 p-3 text-slate-200">
                      Your response: {request.responseText}
                    </p>
                  ) : (
                    <form
                      className="mt-3"
                      onSubmit={(event) => {
                        event.preventDefault();
                        const responseText =
                          responseTexts[request.informationRequestId] ?? "";
                        if (responseText.trim().length > 0) {
                          actions.respond.mutate({
                            informationRequestId: request.informationRequestId,
                            request: { responseText },
                          });
                        }
                      }}
                    >
                      <label
                        className="block font-semibold text-slate-200"
                        htmlFor={`response-${request.informationRequestId}`}
                      >
                        Your response
                      </label>
                      <textarea
                        id={`response-${request.informationRequestId}`}
                        rows={3}
                        required
                        value={responseTexts[request.informationRequestId] ?? ""}
                        onChange={(event) =>
                          setResponseTexts((current) => ({
                            ...current,
                            [request.informationRequestId]: event.target.value,
                          }))
                        }
                        className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white outline-none focus:border-emerald-400"
                      />
                      <button
                        type="submit"
                        disabled={actions.respond.isPending}
                        className="mt-3 rounded-lg bg-emerald-400 px-4 py-2 font-semibold text-slate-950 hover:bg-emerald-300 disabled:opacity-60"
                      >
                        Send response
                      </button>
                    </form>
                  )}
                </article>
              ))}
              {actions.respond.isError && (
                <p className="mt-3 rounded-lg border border-red-900 bg-red-950 p-3 text-red-200">
                  {getErrorMessage(actions.respond.error)}
                </p>
              )}
            </section>

            <section className="mt-6 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm">
              <h2 className="text-lg font-semibold text-white">
                Supporting documents
              </h2>

              {claim.documents.length > 0 && (
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
                        <a
                          href={getOwnerClaimDocumentDownloadUrl(
                            claim.claimId,
                            document.documentId,
                          )}
                          className="font-semibold text-emerald-300 hover:text-emerald-200"
                        >
                          Download {document.originalFileName}
                        </a>
                      ) : (
                        <span className="text-slate-400">
                          Not downloadable ({document.scanStatus})
                        </span>
                      )}
                    </li>
                  ))}
                </ul>
              )}

              {isOpen && (
                <form
                  className="mt-4"
                  onSubmit={(event) => {
                    event.preventDefault();
                    if (documentFiles.length > 0) {
                      actions.uploadDocuments.mutate({
                        kind: documentKind,
                        files: documentFiles,
                      });
                    }
                  }}
                >
                  <label
                    className="block font-semibold text-slate-200"
                    htmlFor="document-kind"
                  >
                    Document kind
                  </label>
                  <select
                    id="document-kind"
                    value={documentKind}
                    onChange={(event) => setDocumentKind(event.target.value)}
                    className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white outline-none focus:border-emerald-400 sm:max-w-xs"
                  >
                    {claimDocumentKinds.map((kind) => (
                      <option key={kind} value={kind}>
                        {kind}
                      </option>
                    ))}
                  </select>

                  <label
                    className="mt-4 block font-semibold text-slate-200"
                    htmlFor="document-files"
                  >
                    Supporting documents
                  </label>
                  <input
                    id="document-files"
                    type="file"
                    multiple
                    onChange={(event) =>
                      setDocumentFiles(Array.from(event.target.files ?? []))
                    }
                    className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white outline-none file:mr-4 file:rounded-md file:border-0 file:bg-emerald-400 file:px-3 file:py-2 file:font-semibold file:text-slate-950 focus:border-emerald-400"
                  />
                  <p className="mt-2 text-slate-400">
                    Up to 5 files, 10 MB each. Every upload is security-scanned
                    before anyone can download it.
                  </p>

                  <button
                    type="submit"
                    disabled={actions.uploadDocuments.isPending}
                    className="mt-4 rounded-lg bg-emerald-400 px-4 py-2 font-semibold text-slate-950 hover:bg-emerald-300 disabled:opacity-60"
                  >
                    Upload documents
                  </button>
                </form>
              )}
              {actions.uploadDocuments.isError && (
                <p className="mt-3 rounded-lg border border-red-900 bg-red-950 p-3 text-red-200">
                  {getErrorMessage(actions.uploadDocuments.error)}
                </p>
              )}
            </section>

            <section className="mt-6 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm">
              <h2 className="text-lg font-semibold text-white">Timeline</h2>
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
            </section>
          </>
        )}
      </section>
    </main>
  );
}
