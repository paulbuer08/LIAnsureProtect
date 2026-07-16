import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth0 } from "@auth0/auth0-react";
import type { FormEvent } from "react";
import { useSearchParams } from "react-router";
import { Breadcrumbs } from "../../../components/Breadcrumbs";

import { TransientStatusMessage } from "../../../components/TransientStatusMessage";
import { getUserErrorMessage } from "../../../lib/apiClient";
import { formatCurrency } from "../../../lib/currency";
import { downloadUnderwritingEvidenceDocument } from "../api/underwritingApi";
import { ReassessmentReviewPanel } from "../components/ReassessmentReviewPanel";
import { useAcknowledgeNotificationSubject } from "../../notifications/hooks/useNotifications";
import {
  useAddQuoteReferralNote,
  useAddQuoteReferralTask,
  useAdjustQuoteReferral,
  useApproveQuoteReferral,
  useAcceptQuoteEvidenceRequest,
  useAssignQuoteReferralToMe,
  useCancelQuoteEvidenceRequest,
  useCompleteQuoteReferralTask,
  useCreateQuoteEvidenceRequest,
  useDeclineQuoteReferral,
  useFollowUpQuoteEvidenceRequest,
  useGetQuoteEvidenceRequest,
  useGenerateAiUnderwritingReview,
  useMarkQuoteEvidenceFollowUpViewed,
  useQuoteReferralTimeline,
  useRecordQuoteEvidenceReviewDecision,
  useReleaseQuoteReferralAssignment,
  useTriageQuoteReferralOperation,
} from "../hooks/useUnderwritingActions";
import { useQuoteReferrals } from "../hooks/useQuoteReferrals";
import { useEvidenceQueue } from "../hooks/useEvidenceQueue";
import type {
  AiUnderwritingReviewResponse,
  QuoteEvidenceRequest,
  QuoteReferral,
  QuoteReferralOperationsSummary,
  UnderwriteQuoteReferralResult,
} from "../types";

type QueueFilter = "all" | "high" | "expiring";

const dateFormatter = new Intl.DateTimeFormat("en-US", {
  month: "short",
  day: "numeric",
  year: "numeric",
});

const riskRank: Record<string, number> = {
  Severe: 4,
  High: 3,
  Moderate: 2,
  Low: 1,
};

function getErrorMessage(error: unknown, fallback: string) {
  return getUserErrorMessage(error, fallback);
}

function formatDate(value: string) {
  return dateFormatter.format(new Date(value));
}

function formatDateTimeLocal(value: string) {
  return value.slice(0, 16);
}

function toUtcIsoFromLocal(value: string) {
  if (value.length === 16) {
    return `${value}:00.000Z`;
  }

  return new Date(value).toISOString();
}

function getDaysUntilExpiry(expiresAtUtc: string) {
  const today = new Date();
  const todayUtc = Date.UTC(
    today.getUTCFullYear(),
    today.getUTCMonth(),
    today.getUTCDate(),
  );
  const expiry = new Date(expiresAtUtc);
  const expiryUtc = Date.UTC(
    expiry.getUTCFullYear(),
    expiry.getUTCMonth(),
    expiry.getUTCDate(),
  );

  return Math.ceil((expiryUtc - todayUtc) / 86_400_000);
}

function getExpiryLabel(referral: QuoteReferral) {
  const days = getDaysUntilExpiry(referral.expiresAtUtc);

  if (days < 0) return "Expired";
  if (days === 0) return "Expires today";
  if (days === 1) return "Expires in 1 day";

  return `Expires in ${days} days`;
}

function formatEvidenceDueLabel(daysUntilDue: number) {
  if (daysUntilDue < 0) {
    const overdueDays = Math.abs(daysUntilDue);

    return overdueDays === 1
      ? "Overdue by 1 day"
      : `Overdue by ${overdueDays} days`;
  }

  if (daysUntilDue === 0) return "Due today";
  if (daysUntilDue === 1) return "Due in 1 day";

  return `Due in ${daysUntilDue} days`;
}

function getUrgencyRank(referral: QuoteReferral) {
  const days = getDaysUntilExpiry(referral.expiresAtUtc);

  if (days < 0) return 4;
  if (days <= 3) return 3;
  if (days <= 7) return 2;

  return 1;
}

function sortForTriage(referrals: QuoteReferral[]) {
  return [...referrals].sort((left, right) => {
    const urgencyDelta = getUrgencyRank(right) - getUrgencyRank(left);
    if (urgencyDelta !== 0) return urgencyDelta;

    const riskDelta =
      (riskRank[right.riskTier] ?? 0) - (riskRank[left.riskTier] ?? 0);
    if (riskDelta !== 0) return riskDelta;

    return (
      new Date(left.createdAtUtc).getTime() -
      new Date(right.createdAtUtc).getTime()
    );
  });
}

function filterReferrals(referrals: QuoteReferral[], filter: QueueFilter) {
  if (filter === "high") {
    return referrals.filter(
      (referral) =>
        referral.riskTier === "High" || referral.riskTier === "Severe",
    );
  }

  if (filter === "expiring") {
    return referrals.filter(
      (referral) => getDaysUntilExpiry(referral.expiresAtUtc) <= 7,
    );
  }

  return referrals;
}

function TextList({
  emptyLabel,
  items,
}: {
  emptyLabel: string;
  items: string[];
}) {
  if (items.length === 0) {
    return <p className="text-sm text-slate-400">{emptyLabel}</p>;
  }

  return (
    <ul className="mt-2 space-y-2 text-sm text-slate-300">
      {items.map((item) => (
        <li className="rounded-md border border-slate-800 bg-slate-950 p-3" key={item}>
          {item}
        </li>
      ))}
    </ul>
  );
}

function OperationsSummary({ operations }: { operations: QuoteReferralOperationsSummary | null }) {
  if (!operations) {
    return (
      <p className="mt-4 rounded-md border border-slate-800 bg-slate-950 p-3 text-sm text-slate-400">
        Referral operations have not been initialized for this quote.
      </p>
    );
  }

  return (
    <dl className="mt-4 grid gap-3 text-sm md:grid-cols-2">
      <div>
        <dt className="text-slate-400">Assignment</dt>
        <dd className="break-all font-medium text-white">
          {operations.assignedUnderwriterUserId
            ? `Assigned to ${operations.assignedUnderwriterUserId}`
            : "Unassigned"}
        </dd>
      </div>
      <div>
        <dt className="text-slate-400">Priority</dt>
        <dd className="font-medium text-white">{operations.priority} priority</dd>
      </div>
      <div>
        <dt className="text-slate-400">Operations status</dt>
        <dd className="font-medium text-white">{operations.status}</dd>
      </div>
      <div>
        <dt className="text-slate-400">SLA</dt>
        <dd className={operations.isSlaBreached ? "font-medium text-red-200" : "font-medium text-emerald-200"}>
          {operations.isSlaBreached ? "SLA breached" : "SLA on track"} by{" "}
          {formatDate(operations.dueAtUtc)}
        </dd>
      </div>
      <div>
        <dt className="text-slate-400">Follow-up tasks</dt>
        <dd className="font-medium text-white">
          {operations.openTaskCount === 1
            ? "1 open task"
            : `${operations.openTaskCount} open tasks`}
        </dd>
      </div>
      <div>
        <dt className="text-slate-400">Latest timeline</dt>
        <dd className="font-medium text-white">
          {operations.latestTimelineAtUtc
            ? formatDate(operations.latestTimelineAtUtc)
            : "No timeline yet"}
        </dd>
      </div>
    </dl>
  );
}

function EvidenceSummary({ quote }: { quote: Pick<QuoteReferral, "evidence"> }) {
  const evidence = quote.evidence;

  return (
    <dl className="mt-4 grid gap-3 text-sm md:grid-cols-2">
      <div>
        <dt className="text-slate-400">Open evidence</dt>
        <dd className="font-medium text-white">
          {evidence.openRequestCount === 1
            ? "1 open evidence request"
            : `${evidence.openRequestCount} open evidence requests`}
        </dd>
      </div>
      <div>
        <dt className="text-slate-400">Responses</dt>
        <dd className="font-medium text-white">
          {evidence.respondedRequestCount === 1
            ? "1 response awaiting review"
            : `${evidence.respondedRequestCount} responses awaiting review`}
        </dd>
      </div>
      <div>
        <dt className="text-slate-400">Unreviewed responses</dt>
        <dd className="font-medium text-white">
          {evidence.unreviewedRespondedRequestCount === 1
            ? "1 unreviewed evidence response"
            : `${evidence.unreviewedRespondedRequestCount} unreviewed evidence responses`}
        </dd>
      </div>
      <div>
        <dt className="text-slate-400">Satisfied evidence</dt>
        <dd className="font-medium text-emerald-200">
          {evidence.satisfiedRequestCount === 1
            ? "1 satisfied evidence request"
            : `${evidence.satisfiedRequestCount} satisfied evidence requests`}
        </dd>
      </div>
      <div>
        <dt className="text-slate-400">Needs follow-up</dt>
        <dd className={evidence.needsAttentionRequestCount > 0 ? "font-medium text-amber-200" : "font-medium text-emerald-200"}>
          {evidence.needsAttentionRequestCount === 1
            ? "1 evidence request needs attention"
            : `${evidence.needsAttentionRequestCount} evidence requests need attention`}
        </dd>
      </div>
      <div>
        <dt className="text-slate-400">Overdue evidence</dt>
        <dd className={evidence.overdueRequestCount > 0 ? "font-medium text-red-200" : "font-medium text-emerald-200"}>
          {evidence.overdueRequestCount === 1
            ? "1 overdue evidence request"
            : `${evidence.overdueRequestCount} overdue evidence requests`}
        </dd>
      </div>
      <div>
        <dt className="text-slate-400">Next open due date</dt>
        <dd className="font-medium text-white">
          {evidence.nextOpenDueAtUtc
            ? `Next evidence due ${formatDate(evidence.nextOpenDueAtUtc)}`
            : "No open evidence due"}
        </dd>
      </div>
      <div>
        <dt className="text-slate-400">Information state</dt>
        <dd className={evidence.isWaitingForInformation ? "font-medium text-amber-200" : "font-medium text-emerald-200"}>
          {evidence.isWaitingForInformation ? "Waiting for information" : "No evidence blocker"}
        </dd>
      </div>
      <div>
        <dt className="text-slate-400">Latest evidence activity</dt>
        <dd className="font-medium text-white">
          {evidence.latestEvidenceActivityAtUtc
            ? formatDate(evidence.latestEvidenceActivityAtUtc)
            : "No evidence activity yet"}
        </dd>
      </div>
    </dl>
  );
}

type EvidencePanelQuote = Pick<QuoteReferral, "quoteId" | "companyName" | "operations" | "evidence">;

function EvidencePanel({
  quote,
  initialEvidenceRequestId,
}: {
  quote: EvidencePanelQuote;
  initialEvidenceRequestId?: string;
}) {
  const { getAccessTokenSilently } = useAuth0();
  const createEvidenceRequest = useCreateQuoteEvidenceRequest();
  const acceptEvidenceRequest = useAcceptQuoteEvidenceRequest();
  const cancelEvidenceRequest = useCancelQuoteEvidenceRequest();
  const followUpEvidenceRequest = useFollowUpQuoteEvidenceRequest();
  const getEvidenceRequest = useGetQuoteEvidenceRequest();
  const markEvidenceFollowUpViewed = useMarkQuoteEvidenceFollowUpViewed();
  const recordEvidenceReviewDecision = useRecordQuoteEvidenceReviewDecision();
  const [category, setCategory] = useState("MultiFactorAuthentication");
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [documentRequirement, setDocumentRequirement] = useState<"Required" | "Optional" | "NarrativeOnly">("Required");
  const [dueAtUtc, setDueAtUtc] = useState(
    quote.operations?.dueAtUtc ? formatDateTimeLocal(quote.operations.dueAtUtc) : "",
  );
  const [evidenceRequestId, setEvidenceRequestId] = useState("");
  const [reviewNotes, setReviewNotes] = useState("");
  const [reviewDecision, setReviewDecision] = useState("Satisfied");
  const [reviewReason, setReviewReason] = useState("");
  const [remediationGuidance, setRemediationGuidance] = useState("");
  const [lastEvidenceResult, setLastEvidenceResult] = useState<QuoteEvidenceRequest>();
  const [downloadError, setDownloadError] = useState<string>();
  const autoLoadedKey = useRef<string | undefined>(undefined);

  useEffect(() => {
    if (!initialEvidenceRequestId) return;
    const key = `${quote.quoteId}:${initialEvidenceRequestId}`;
    if (autoLoadedKey.current === key) return;
    autoLoadedKey.current = key;
    setEvidenceRequestId(initialEvidenceRequestId);
    void getEvidenceRequest.mutateAsync({
      quoteId: quote.quoteId,
      evidenceRequestId: initialEvidenceRequestId,
    }).then(setLastEvidenceResult).catch(() => undefined);
  }, [getEvidenceRequest, initialEvidenceRequestId, quote.quoteId]);

  useAcknowledgeNotificationSubject(
    "evidence-request",
    lastEvidenceResult?.evidenceRequestId,
    { enabled: Boolean(lastEvidenceResult), scope: "team" },
  );

  async function handleDownloadDocument(
    requestId: string,
    documentId: string,
    fileName: string,
  ) {
    try {
      setDownloadError(undefined);
      const accessToken = await getAccessTokenSilently();
      await downloadUnderwritingEvidenceDocument(
        accessToken,
        quote.quoteId,
        requestId,
        documentId,
        fileName,
      );
    } catch (error) {
      setDownloadError(getErrorMessage(error, "Unable to download the document."));
    }
  }
  const evidenceError =
    createEvidenceRequest.error ??
    acceptEvidenceRequest.error ??
    cancelEvidenceRequest.error ??
    followUpEvidenceRequest.error ??
    getEvidenceRequest.error ??
    markEvidenceFollowUpViewed.error ??
    recordEvidenceReviewDecision.error;

  async function handleCreateEvidenceRequest(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const result = await createEvidenceRequest.mutateAsync({
      quoteId: quote.quoteId,
      request: {
        category,
        title: title.trim(),
        description: description.trim(),
        dueAtUtc: toUtcIsoFromLocal(dueAtUtc),
        documentRequirement,
      },
    });
    setLastEvidenceResult(result);
    setTitle("");
    setDescription("");
  }

  async function handleAcceptEvidence(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const result = await acceptEvidenceRequest.mutateAsync({
      quoteId: quote.quoteId,
      evidenceRequestId: evidenceRequestId.trim(),
      request: { reviewNotes: reviewNotes.trim() },
    });
    setLastEvidenceResult(result);
  }

  async function handleCancelEvidence() {
    const result = await cancelEvidenceRequest.mutateAsync({
      quoteId: quote.quoteId,
      evidenceRequestId: evidenceRequestId.trim(),
      request: { reviewNotes: reviewNotes.trim() },
    });
    setLastEvidenceResult(result);
  }

  async function handleFollowUpEvidence() {
    const result = await followUpEvidenceRequest.mutateAsync({
      quoteId: quote.quoteId,
      evidenceRequestId: evidenceRequestId.trim(),
    });
    setLastEvidenceResult(result);
  }

  async function handleRecordEvidenceDecision() {
    const result = await recordEvidenceReviewDecision.mutateAsync({
      quoteId: quote.quoteId,
      evidenceRequestId: evidenceRequestId.trim(),
      request: {
        decision: reviewDecision,
        reason: reviewReason.trim(),
        remediationGuidance:
          remediationGuidance.trim().length > 0
            ? remediationGuidance.trim()
            : null,
      },
    });
    setLastEvidenceResult(result);
  }

  async function handleLoadEvidenceRequest() {
    const result = await getEvidenceRequest.mutateAsync({
      quoteId: quote.quoteId,
      evidenceRequestId: evidenceRequestId.trim(),
    });
    setLastEvidenceResult(result);
  }

  async function handleOpenEvidenceFollowUp(responseId: string) {
    if (!lastEvidenceResult) return;

    const result = await markEvidenceFollowUpViewed.mutateAsync({
      quoteId: quote.quoteId,
      evidenceRequestId: lastEvidenceResult.evidenceRequestId,
      responseId,
    });
    setLastEvidenceResult(result);
  }

  return (
    <section className="rounded-lg border border-amber-900 bg-amber-950/25 p-5">
      <p className="text-sm font-semibold uppercase tracking-wide text-amber-300">
        Evidence requests
      </p>
      <h2 className="mt-2 text-xl font-semibold text-white">
        Customer and broker information requests
      </h2>
      <p className="mt-2 text-sm text-amber-100">
        Request cyber-control evidence and review uploaded documents only after
        security screening marks them clean.
      </p>

      <EvidenceSummary quote={quote} />

      {lastEvidenceResult && (
        <div className="mt-4 rounded-md border border-emerald-800 bg-emerald-950 p-3 text-sm text-emerald-100">
          <p className="font-semibold">Evidence request saved: {lastEvidenceResult.status}</p>
          <h3 className="mt-2 text-base font-semibold text-white">
            {lastEvidenceResult.title}
          </h3>
          <p className="mt-1 text-emerald-100/80">
            Control: {lastEvidenceResult.category}
          </p>
          {downloadError && (
            <p className="mt-1 rounded-md border border-red-900 bg-red-950 p-2 text-xs text-red-200">
              {downloadError}
            </p>
          )}
          <p className="mt-1">{formatEvidenceDueLabel(lastEvidenceResult.daysUntilDue)}</p>
          <p className="mt-1 text-emerald-100/80">
            {lastEvidenceResult.companyName ?? quote.companyName} · {lastEvidenceResult.submissionReference ?? lastEvidenceResult.submissionId}
          </p>
          {lastEvidenceResult.respondentEmail &&
            !(
              lastEvidenceResult.responses?.at(-1)?.kind === "FollowUp" &&
              !lastEvidenceResult.responses?.at(-1)?.viewedAtUtc
            ) && (
            <div className="mt-3 rounded-md border border-emerald-900 p-2">
              <p className="font-semibold">Latest respondent</p>
              <p className="mt-1">
                {lastEvidenceResult.respondentName} · {lastEvidenceResult.respondentTitle} · {lastEvidenceResult.respondentEmail}
                {lastEvidenceResult.respondentMobileNumber ? ` · Mobile ${lastEvidenceResult.respondentMobileNumber}` : ""}
                {lastEvidenceResult.respondentTelephoneNumber ? ` · Telephone ${lastEvidenceResult.respondentTelephoneNumber}` : ""}
                {!lastEvidenceResult.respondentMobileNumber && !lastEvidenceResult.respondentTelephoneNumber && lastEvidenceResult.respondentPhone ? ` · Contact ${lastEvidenceResult.respondentPhone}` : ""}
              </p>
              {lastEvidenceResult.otherConcerns && <p className="mt-1">Other concerns: {lastEvidenceResult.otherConcerns}</p>}
            </div>
          )}
          {(lastEvidenceResult.responses?.length ?? 0) > 0 && (
            <div className="mt-3">
              <p className="font-semibold">Response history</p>
              <ol
                aria-label="Underwriter response history entries"
                tabIndex={0}
                className="mt-2 max-h-80 space-y-2 overflow-y-auto pr-2 focus:outline-none focus:ring-2 focus:ring-emerald-300"
              >
                {lastEvidenceResult.responses?.map((response) => (
                  <li key={response.responseId} className={`rounded-md border p-2 ${response.kind === "FollowUp" && !response.viewedAtUtc ? "border-amber-400 bg-amber-950/20" : "border-emerald-900"}`}>
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <p className="font-semibold">{response.kind} · {new Date(response.respondedAtUtc).toLocaleString()}</p>
                      {response.kind === "FollowUp" && !response.viewedAtUtc && (
                        <button
                          type="button"
                          disabled={markEvidenceFollowUpViewed.isPending}
                          onClick={() => void handleOpenEvidenceFollowUp(response.responseId)}
                          className="rounded-md bg-amber-300 px-3 py-2 text-xs font-semibold text-slate-950 hover:bg-amber-200 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-400"
                        >
                          Open follow-up
                        </button>
                      )}
                      {response.kind === "FollowUp" && response.viewedAtUtc && (
                        <span className="text-xs text-emerald-200">Opened {new Date(response.viewedAtUtc).toLocaleString()}</span>
                      )}
                    </div>
                    {response.kind !== "FollowUp" || response.viewedAtUtc ? (
                      <>
                        <p className="mt-1">{response.respondentName} · {response.respondentTitle} · {response.respondentEmail}{response.respondentMobileNumber ? ` · Mobile ${response.respondentMobileNumber}` : ""}{response.respondentTelephoneNumber ? ` · Telephone ${response.respondentTelephoneNumber}` : ""}{!response.respondentMobileNumber && !response.respondentTelephoneNumber && response.respondentPhone ? ` · Contact ${response.respondentPhone}` : ""}</p>
                        <div className="mt-2 flex flex-wrap gap-2 text-xs">
                          <span className="rounded-md border border-emerald-800 px-2 py-1">
                            Domain: {response.emailDomainStatus ?? "Unverified"}
                          </span>
                          <span className={`rounded-md border px-2 py-1 ${response.emailVerificationStatus === "Verified" ? "border-emerald-500 text-emerald-100" : "border-amber-600 text-amber-100"}`}>
                            Email: {response.emailVerificationStatus ?? "Unverified"}
                          </span>
                        </div>
                        {response.responseText && <p className="mt-1 whitespace-pre-wrap">{response.responseText}</p>}
                        {response.otherConcerns && <p className="mt-1 whitespace-pre-wrap">Other concerns: {response.otherConcerns}</p>}
                      </>
                    ) : (
                      <p className="mt-1 text-amber-100">This customer follow-up is unread. Open it to view the details and restore one follow-up slot for the customer.</p>
                    )}
                  </li>
                ))}
              </ol>
            </div>
          )}
          {lastEvidenceResult.reviewDecision !== "NotReviewed" && (
            <>
              <p className="mt-2 font-semibold">
                Evidence decision saved: {lastEvidenceResult.reviewDecision}
              </p>
              {lastEvidenceResult.reviewReason && (
                <p className="mt-1">Reason: {lastEvidenceResult.reviewReason}</p>
              )}
              {lastEvidenceResult.remediationGuidance && (
                <p className="mt-1">
                  Remediation guidance: {lastEvidenceResult.remediationGuidance}
                </p>
              )}
            </>
          )}
          {(lastEvidenceResult.documents?.length ?? 0) > 0 && (
            <div className="mt-3">
              <p className="font-semibold">Submitted documents</p>
              <ul className="mt-2 space-y-2">
                {lastEvidenceResult.documents?.map((document) => (
                  <li key={document.documentId} className="rounded-md border border-emerald-900 p-2">
                    {document.isDownloadAvailable ? (
                      <button
                        type="button"
                        onClick={() =>
                          void handleDownloadDocument(
                            lastEvidenceResult.evidenceRequestId,
                            document.documentId,
                            document.originalFileName,
                          )
                        }
                        className="cursor-pointer font-semibold text-emerald-200 hover:text-white"
                      >
                        Download {document.originalFileName}
                      </button>
                    ) : (
                      <span className="font-semibold text-emerald-100">
                        {document.originalFileName}
                      </span>
                    )}
                    <span className="ml-2 text-emerald-100/80">
                      {document.contentType} | {document.sizeBytes.toLocaleString()} bytes
                    </span>
                    <div className="mt-1 flex flex-wrap items-center gap-2">
                      <span className="rounded-md border border-cyan-700 px-2 py-1 text-xs font-semibold text-cyan-100">
                        {document.scanStatus}
                      </span>
                      {document.scanResultReason && (
                        <span className="text-xs text-emerald-100/80">
                          {document.scanResultReason}
                        </span>
                      )}
                    </div>
                    {!document.isDownloadAvailable && (
                      <p className="mt-1 text-xs text-amber-100">
                        Download unavailable until security screening is clean.
                      </p>
                    )}
                    {document.plausibilityStatus && (
                      <div className="mt-2 rounded-md border border-cyan-900 bg-cyan-950/30 p-2 text-xs text-cyan-100">
                        <p className="font-semibold">
                          Advisory document assessment: {document.plausibilityStatus}
                        </p>
                        <p className="mt-1">
                          Claim consistency: {document.claimConsistencyStatus}. This
                          automated result cannot approve evidence or change quote terms.
                        </p>
                        {(document.advisoryFindings?.length ?? 0) > 0 && (
                          <ul className="mt-1 list-disc space-y-1 pl-4">
                            {document.advisoryFindings?.map((finding) => (
                              <li key={finding}>{finding}</li>
                            ))}
                          </ul>
                        )}
                      </div>
                    )}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}

      {evidenceError !== null && evidenceError !== undefined && (
        <p className="mt-4 whitespace-pre-wrap rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">
          {getErrorMessage(evidenceError, "Unable to update evidence request.")}
        </p>
      )}

      <form className="mt-5 space-y-4" onSubmit={handleCreateEvidenceRequest}>
        <label className="block text-sm font-medium text-slate-200">
          Evidence category
          <select
            value={category}
            onChange={(event) => setCategory(event.target.value)}
            className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-amber-300"
          >
            <option value="MultiFactorAuthentication">MFA</option>
            <option value="EndpointDetectionAndResponse">EDR</option>
            <option value="BackupRecovery">Backup recovery</option>
            <option value="IncidentResponsePlan">Incident response plan</option>
            <option value="PriorLossDetails">Prior loss details</option>
            <option value="SecurityQuestionnaireClarification">Questionnaire clarification</option>
            <option value="Other">Other</option>
          </select>
        </label>
        <label className="block text-sm font-medium text-slate-200">
          Evidence request title
          <input
            required
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-amber-300"
          />
        </label>
        <ReviewTextField
          label="Evidence request description"
          required
          value={description}
          onChange={setDescription}
        />
        <label className="block text-sm font-medium text-slate-200">
          Supporting document requirement
          <select value={documentRequirement} onChange={(event) => setDocumentRequirement(event.target.value as "Required" | "Optional" | "NarrativeOnly")} className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-amber-300">
            <option value="Required">At least one document required</option>
            <option value="Optional">Document optional</option>
            <option value="NarrativeOnly">Written response only</option>
          </select>
          <span className="mt-2 block text-xs text-slate-400">Choose Required when underwriting must validate the assertion from an uploaded artifact. Choose written response only when files would not add useful assurance.</span>
        </label>
        <label className="block text-sm font-medium text-slate-200">
          Evidence due date
          <input
            required
            type="datetime-local"
            value={dueAtUtc}
            onChange={(event) => setDueAtUtc(event.target.value)}
            className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-amber-300"
          />
        </label>
        <button
          type="submit"
          className="rounded-lg bg-amber-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-amber-200"
        >
          Create evidence request
        </button>
      </form>

      <form className="mt-6 space-y-4" onSubmit={handleAcceptEvidence}>
        <label className="block text-sm font-medium text-slate-200">
          Evidence request id
          <input
            required
            value={evidenceRequestId}
            onChange={(event) => setEvidenceRequestId(event.target.value)}
            className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-amber-300"
          />
        </label>
        <button
          type="button"
          onClick={() => void handleLoadEvidenceRequest()}
          disabled={getEvidenceRequest.isPending || evidenceRequestId.trim().length === 0}
          className="rounded-lg border border-amber-300 px-4 py-2 text-sm font-semibold text-amber-100 hover:bg-amber-300 hover:text-slate-950 disabled:cursor-not-allowed disabled:border-slate-700 disabled:text-slate-500"
        >
          Load evidence request
        </button>
        <ReviewTextField
          label="Evidence review notes"
          value={reviewNotes}
          onChange={setReviewNotes}
        />
        <label className="block text-sm font-medium text-slate-200">
          Evidence review decision
          <select
            value={reviewDecision}
            onChange={(event) => setReviewDecision(event.target.value)}
            className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-amber-300"
          >
            <option value="Satisfied">Satisfied</option>
            <option value="Insufficient">Insufficient</option>
            <option value="NeedsClarification">Needs clarification</option>
          </select>
        </label>
        <ReviewTextField
          label="Evidence review reason"
          value={reviewReason}
          onChange={setReviewReason}
        />
        <ReviewTextField
          label="Owner remediation guidance"
          value={remediationGuidance}
          onChange={setRemediationGuidance}
        />
        <div className="flex flex-wrap gap-3">
          <button
            type="button"
            onClick={() => void handleRecordEvidenceDecision()}
            className="rounded-lg bg-emerald-400 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-emerald-300"
          >
            Record evidence decision
          </button>
          <button
            type="submit"
            className="rounded-lg border border-emerald-400 px-4 py-2 text-sm font-semibold text-emerald-100 hover:bg-emerald-950"
          >
            Accept evidence
          </button>
          <button
            type="button"
            onClick={() => void handleCancelEvidence()}
            className="rounded-lg border border-amber-400 px-4 py-2 text-sm font-semibold text-amber-100 hover:bg-amber-950"
          >
            Cancel evidence
          </button>
          <button
            type="button"
            onClick={() => void handleFollowUpEvidence()}
            className="rounded-lg border border-cyan-400 px-4 py-2 text-sm font-semibold text-cyan-100 hover:bg-cyan-950"
          >
            Send evidence follow-up
          </button>
        </div>
      </form>
    </section>
  );
}

function OperationsPanel({ quote }: { quote: QuoteReferral }) {
  const timelineQuery = useQuoteReferralTimeline(quote.quoteId);
  const assignToMe = useAssignQuoteReferralToMe();
  const releaseAssignment = useReleaseQuoteReferralAssignment();
  const triageOperation = useTriageQuoteReferralOperation();
  const addNote = useAddQuoteReferralNote();
  const addTask = useAddQuoteReferralTask();
  const completeTask = useCompleteQuoteReferralTask();
  const operations = quote.operations;
  const [priority, setPriority] = useState(operations?.priority ?? "Normal");
  const [status, setStatus] = useState(operations?.status ?? "New");
  const [dueAtUtc, setDueAtUtc] = useState(
    operations?.dueAtUtc ? formatDateTimeLocal(operations.dueAtUtc) : "",
  );
  const [note, setNote] = useState("");
  const [taskTitle, setTaskTitle] = useState("");
  const [taskDueAtUtc, setTaskDueAtUtc] = useState(
    operations?.dueAtUtc ? formatDateTimeLocal(operations.dueAtUtc) : "",
  );
  const [completeTaskId, setCompleteTaskId] = useState("");
  const operationError =
    assignToMe.error ??
    releaseAssignment.error ??
    triageOperation.error ??
    addNote.error ??
    addTask.error ??
    completeTask.error ??
    timelineQuery.error;

  async function handleTriage(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await triageOperation.mutateAsync({
      quoteId: quote.quoteId,
      request: {
        priority,
        status,
        dueAtUtc: toUtcIsoFromLocal(dueAtUtc),
      },
    });
  }

  async function handleAddNote(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await addNote.mutateAsync({
      quoteId: quote.quoteId,
      request: { note: note.trim() },
    });
    setNote("");
  }

  async function handleAddTask(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await addTask.mutateAsync({
      quoteId: quote.quoteId,
      request: {
        title: taskTitle.trim(),
        dueAtUtc: toUtcIsoFromLocal(taskDueAtUtc),
      },
    });
    setTaskTitle("");
  }

  async function handleCompleteTask(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await completeTask.mutateAsync({
      quoteId: quote.quoteId,
      taskId: completeTaskId.trim(),
    });
    setCompleteTaskId("");
  }

  return (
    <section className="rounded-lg border border-blue-900 bg-blue-950/30 p-5">
      <p className="text-sm font-semibold uppercase tracking-wide text-blue-300">
        Referral operations
      </p>
      <h2 className="mt-2 text-xl font-semibold text-white">
        Internal workflow tracking
      </h2>
      <p className="mt-2 text-sm text-blue-100">
        Assignment, SLA, notes, and follow-up tasks support the underwriter;
        they do not replace the final manual decision controls.
      </p>

      <OperationsSummary operations={operations} />

      {operationError !== null && operationError !== undefined && (
        <p className="mt-4 whitespace-pre-wrap rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">
          {getErrorMessage(operationError, "Unable to update referral operations.")}
        </p>
      )}

      <div className="mt-5 flex flex-wrap gap-3">
        <button
          type="button"
          onClick={() => void assignToMe.mutateAsync(quote.quoteId)}
          className="rounded-lg bg-blue-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-blue-200"
        >
          Assign to me
        </button>
        <button
          type="button"
          onClick={() => void releaseAssignment.mutateAsync(quote.quoteId)}
          className="rounded-lg border border-blue-400 px-4 py-2 text-sm font-semibold text-blue-100 hover:bg-blue-950"
        >
          Release assignment
        </button>
      </div>

      <form className="mt-5 grid gap-4 md:grid-cols-3" onSubmit={handleTriage}>
        <label className="text-sm font-medium text-slate-200">
          Operations priority
          <select
            value={priority}
            onChange={(event) => setPriority(event.target.value)}
            className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-blue-300"
          >
            <option value="Normal">Normal</option>
            <option value="High">High</option>
            <option value="Urgent">Urgent</option>
          </select>
        </label>
        <label className="text-sm font-medium text-slate-200">
          Operations status
          <select
            value={status}
            onChange={(event) => setStatus(event.target.value)}
            className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-blue-300"
          >
            <option value="New">New</option>
            <option value="InReview">In review</option>
            <option value="WaitingForInformation">Waiting for information</option>
            <option value="Escalated">Escalated</option>
            <option value="ReadyForDecision">Ready for decision</option>
          </select>
        </label>
        <label className="text-sm font-medium text-slate-200">
          Operations due date
          <input
            required
            type="datetime-local"
            value={dueAtUtc}
            onChange={(event) => setDueAtUtc(event.target.value)}
            className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-blue-300"
          />
        </label>
        <button
          type="submit"
          className="rounded-lg bg-blue-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-blue-200 md:col-span-3 md:w-fit"
        >
          Save triage
        </button>
      </form>

      <form className="mt-5 space-y-3" onSubmit={handleAddNote}>
        <ReviewTextField
          label="Internal work note"
          required
          value={note}
          onChange={setNote}
        />
        <button
          type="submit"
          className="rounded-lg bg-blue-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-blue-200"
        >
          Add note
        </button>
      </form>

      <form className="mt-5 grid gap-4 md:grid-cols-[minmax(0,1fr)_220px_auto]" onSubmit={handleAddTask}>
        <label className="text-sm font-medium text-slate-200">
          Follow-up task title
          <input
            required
            value={taskTitle}
            onChange={(event) => setTaskTitle(event.target.value)}
            className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-blue-300"
          />
        </label>
        <label className="text-sm font-medium text-slate-200">
          Follow-up task due date
          <input
            required
            type="datetime-local"
            value={taskDueAtUtc}
            onChange={(event) => setTaskDueAtUtc(event.target.value)}
            className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-blue-300"
          />
        </label>
        <button
          type="submit"
          className="self-end rounded-lg bg-blue-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-blue-200"
        >
          Add task
        </button>
      </form>

      <form className="mt-5 grid gap-4 md:grid-cols-[minmax(0,1fr)_auto]" onSubmit={handleCompleteTask}>
        <label className="text-sm font-medium text-slate-200">
          Complete task id
          <input
            required
            value={completeTaskId}
            onChange={(event) => setCompleteTaskId(event.target.value)}
            className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-blue-300"
          />
        </label>
        <button
          type="submit"
          className="self-end rounded-lg border border-blue-400 px-4 py-2 text-sm font-semibold text-blue-100 hover:bg-blue-950"
        >
          Complete task
        </button>
      </form>

      <section className="mt-5">
        <h3 className="text-sm font-semibold uppercase tracking-wide text-blue-200">
          Timeline
        </h3>
        {timelineQuery.isPending && (
          <p className="mt-2 text-sm text-slate-300">Loading timeline...</p>
        )}
        {timelineQuery.data && (
          <ol className="mt-3 space-y-2 text-sm text-slate-200">
            {timelineQuery.data.entries.map((entry) => (
              <li
                className="rounded-md border border-slate-800 bg-slate-950 p-3"
                key={`${entry.entryType}-${entry.createdAtUtc}-${entry.summary}`}
              >
                <p className="font-semibold text-white">{entry.entryType}</p>
                <p className="mt-1">{entry.summary}</p>
                <p className="mt-1 text-xs text-slate-400">
                  {entry.createdByUserId} on {formatDate(entry.createdAtUtc)}
                </p>
              </li>
            ))}
          </ol>
        )}
      </section>
    </section>
  );
}

function AiReviewPanel({
  aiReview,
  error,
  isPending,
  onGenerate,
}: {
  aiReview?: AiUnderwritingReviewResponse;
  error: unknown;
  isPending: boolean;
  onGenerate: () => void;
}) {
  return (
    <section className="rounded-lg border border-cyan-900 bg-cyan-950/40 p-5">
      <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
        <div>
          <p className="text-sm font-semibold uppercase tracking-wide text-cyan-300">
            Advisory AI review
          </p>
          <h2 className="mt-2 text-xl font-semibold text-white">
            Decision support only
          </h2>
          <p className="mt-2 text-sm text-cyan-100">
            AI can summarize risk evidence and suggest questions, but the manual
            underwriter actions below remain the only decision path.
          </p>
        </div>

        <button
          type="button"
          onClick={onGenerate}
          disabled={isPending}
          className="rounded-lg bg-cyan-300 px-4 py-3 text-sm font-semibold text-slate-950 hover:bg-cyan-200 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-300"
        >
          {isPending ? "Requesting AI review..." : "Request advisory AI review"}
        </button>
      </div>

      {error !== null && error !== undefined && (
        <p className="mt-4 whitespace-pre-wrap rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">
          {getErrorMessage(error, "Unable to request advisory AI review.")}
        </p>
      )}

      {aiReview && (
        <div className="mt-5 space-y-5">
          {aiReview.advisoryDisclaimer && (
            <p className="rounded-md border border-amber-600 bg-amber-950 p-3 text-sm font-semibold text-amber-100">
              {aiReview.advisoryDisclaimer}
            </p>
          )}

          {aiReview.executiveSummary && (
            <section>
              <h3 className="text-sm font-semibold uppercase tracking-wide text-cyan-200">
                Executive summary
              </h3>
              <p className="mt-2 text-sm text-slate-200">
                {aiReview.executiveSummary}
              </p>
            </section>
          )}

          <div className="grid gap-4 lg:grid-cols-2">
            <section>
              <h3 className="text-sm font-semibold text-white">
                Positive risk signals
              </h3>
              <TextList emptyLabel="No positive signals returned." items={aiReview.positiveRiskSignals} />
            </section>

            <section>
              <h3 className="text-sm font-semibold text-white">
                Negative risk signals
              </h3>
              <TextList emptyLabel="No negative signals returned." items={aiReview.negativeRiskSignals} />
            </section>

            <section>
              <h3 className="text-sm font-semibold text-white">Control gaps</h3>
              <TextList emptyLabel="No control gaps returned." items={aiReview.controlGaps} />
            </section>

            <section>
              <h3 className="text-sm font-semibold text-white">
                Suggested questions
              </h3>
              <TextList
                emptyLabel="No questions returned."
                items={aiReview.suggestedUnderwritingQuestions}
              />
            </section>

            <section>
              <h3 className="text-sm font-semibold text-white">
                Subjectivity candidates
              </h3>
              <TextList
                emptyLabel="No subjectivity candidates returned."
                items={aiReview.suggestedSubjectivityCandidates}
              />
            </section>

            <section>
              <h3 className="text-sm font-semibold text-white">Limitations</h3>
              <TextList emptyLabel="No limitations returned." items={aiReview.limitations} />
            </section>
          </div>

          <dl className="grid gap-3 rounded-md border border-cyan-900 bg-slate-950 p-4 text-sm md:grid-cols-2">
            <div>
              <dt className="text-slate-400">Provider</dt>
              <dd className="mt-1 font-medium text-slate-100">
                {aiReview.providerName}
              </dd>
            </div>
            <div>
              <dt className="text-slate-400">Prompt version</dt>
              <dd className="mt-1 font-medium text-slate-100">
                {aiReview.promptVersion}
              </dd>
            </div>
            <div>
              <dt className="text-slate-400">Output schema</dt>
              <dd className="mt-1 font-medium text-slate-100">
                {aiReview.outputSchemaVersion}
              </dd>
            </div>
            <div>
              <dt className="text-slate-400">Input snapshot hash</dt>
              <dd className="mt-1 break-all font-mono text-xs text-cyan-200">
                {aiReview.inputSnapshotHash}
              </dd>
            </div>
          </dl>
        </div>
      )}
    </section>
  );
}

function ReviewTextField({
  label,
  onChange,
  required = false,
  value,
}: {
  label: string;
  onChange: (value: string) => void;
  required?: boolean;
  value: string;
}) {
  return (
    <label className="block text-sm font-medium text-slate-200">
      {label}
      <textarea
        required={required}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="mt-2 min-h-20 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400"
      />
    </label>
  );
}

function ManualActionsPanel({
  actionError,
  actionResult,
  onAdjust,
  onApprove,
  onDecline,
  onDismissActionResult,
  quote,
}: {
  actionError: unknown;
  actionResult?: UnderwriteQuoteReferralResult;
  onAdjust: (request: {
    adjustedPremium: number;
    adjustedRetention: number;
    updatedSubjectivities: string;
    reason: string;
    notes: string;
  }) => void;
  onApprove: (request: { reason: string; notes: string }) => void;
  onDecline: (request: { reason: string; notes: string }) => void;
  onDismissActionResult: () => void;
  quote: QuoteReferral;
}) {
  const [approvalReason, setApprovalReason] = useState("");
  const [approvalNotes, setApprovalNotes] = useState("");
  const [declineReason, setDeclineReason] = useState("");
  const [declineNotes, setDeclineNotes] = useState("");
  const [adjustedPremium, setAdjustedPremium] = useState(String(quote.premium));
  const [adjustedRetention, setAdjustedRetention] = useState(
    String(quote.retention),
  );
  const [updatedSubjectivities, setUpdatedSubjectivities] = useState(
    quote.subjectivities.join("\n"),
  );
  const [adjustmentReason, setAdjustmentReason] = useState("");
  const [adjustmentNotes, setAdjustmentNotes] = useState("");

  function handleApprove(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    onApprove({
      reason: approvalReason.trim(),
      notes: approvalNotes.trim(),
    });
  }

  function handleDecline(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    onDecline({
      reason: declineReason.trim(),
      notes: declineNotes.trim(),
    });
  }

  function handleAdjust(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    onAdjust({
      adjustedPremium: Number(adjustedPremium),
      adjustedRetention: Number(adjustedRetention),
      updatedSubjectivities: updatedSubjectivities.trim(),
      reason: adjustmentReason.trim(),
      notes: adjustmentNotes.trim(),
    });
  }

  return (
    <section className="rounded-lg border border-slate-800 bg-slate-900 p-5">
      <p className="text-sm font-semibold uppercase tracking-wide text-emerald-400">
        Manual underwriter decision
      </p>
      <h2 className="mt-2 text-xl font-semibold text-white">
        Human authority actions
      </h2>
      <p className="mt-2 text-sm text-slate-300">
        These forms call the existing backend underwriting endpoints. Advisory
        AI output is not submitted as a decision.
      </p>

      {actionResult && (
        <TransientStatusMessage
          className="mt-4 text-sm font-semibold"
          onDismiss={onDismissActionResult}
        >
          Manual action saved: {actionResult.status}
        </TransientStatusMessage>
      )}

      {actionError !== null && actionError !== undefined && (
        <p className="mt-4 whitespace-pre-wrap rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">
          {getErrorMessage(actionError, "Unable to save manual decision.")}
        </p>
      )}

      <div className="mt-5 grid gap-5 xl:grid-cols-3">
        <form
          aria-label="Approve referral"
          className="rounded-lg border border-slate-800 bg-slate-950 p-4"
          onSubmit={handleApprove}
        >
          <h3 className="text-base font-semibold text-white">Approve</h3>
          <div className="mt-4 space-y-4">
            <ReviewTextField
              label="Approval reason"
              required
              value={approvalReason}
              onChange={setApprovalReason}
            />
            <ReviewTextField
              label="Approval notes"
              value={approvalNotes}
              onChange={setApprovalNotes}
            />
            <button
              type="submit"
              className="rounded-lg bg-emerald-400 px-4 py-3 text-sm font-semibold text-slate-950 hover:bg-emerald-300"
            >
              Approve referral
            </button>
          </div>
        </form>

        <form
          aria-label="Decline referral"
          className="rounded-lg border border-slate-800 bg-slate-950 p-4"
          onSubmit={handleDecline}
        >
          <h3 className="text-base font-semibold text-white">Decline</h3>
          <div className="mt-4 space-y-4">
            <ReviewTextField
              label="Decline reason"
              required
              value={declineReason}
              onChange={setDeclineReason}
            />
            <ReviewTextField
              label="Decline notes"
              value={declineNotes}
              onChange={setDeclineNotes}
            />
            <button
              type="submit"
              className="rounded-lg border border-red-500 px-4 py-3 text-sm font-semibold text-red-100 hover:bg-red-950"
            >
              Decline referral
            </button>
          </div>
        </form>

        <form
          aria-label="Adjust referral terms"
          className="rounded-lg border border-slate-800 bg-slate-950 p-4"
          onSubmit={handleAdjust}
          role="region"
        >
          <h3 className="text-base font-semibold text-white">Adjust terms</h3>
          <div className="mt-4 space-y-4">
            <label className="block text-sm font-medium text-slate-200">
              Adjusted premium
              <input
                required
                min="1"
                type="number"
                value={adjustedPremium}
                onChange={(event) => setAdjustedPremium(event.target.value)}
                className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400"
              />
            </label>
            <label className="block text-sm font-medium text-slate-200">
              Adjusted retention
              <input
                required
                min="1"
                type="number"
                value={adjustedRetention}
                onChange={(event) => setAdjustedRetention(event.target.value)}
                className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400"
              />
            </label>
            <ReviewTextField
              label="Updated subjectivities"
              value={updatedSubjectivities}
              onChange={setUpdatedSubjectivities}
            />
            <ReviewTextField
              label="Adjustment reason"
              required
              value={adjustmentReason}
              onChange={setAdjustmentReason}
            />
            <ReviewTextField
              label="Adjustment notes"
              value={adjustmentNotes}
              onChange={setAdjustmentNotes}
            />
            <button
              type="submit"
              className="rounded-lg bg-amber-300 px-4 py-3 text-sm font-semibold text-slate-950 hover:bg-amber-200"
            >
              Adjust terms
            </button>
          </div>
        </form>
      </div>
    </section>
  );
}

function ReferralCard({
  isSelected,
  onSelect,
  referral,
}: {
  isSelected: boolean;
  onSelect: () => void;
  referral: QuoteReferral;
}) {
  return (
    <article
      className={`rounded-lg border p-4 ${
        isSelected
          ? "border-emerald-400 bg-emerald-950/30"
          : "border-slate-800 bg-slate-900"
      }`}
    >
      <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div>
          <h2 className="text-lg font-semibold text-white">{referral.quoteId}</h2>
          <p className="mt-1 text-sm text-slate-400">
            Submission {referral.submissionId}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <span className="rounded-md border border-red-800 px-3 py-1 text-xs font-semibold text-red-200">
            {referral.riskTier} risk
          </span>
          <span className="rounded-md border border-amber-800 px-3 py-1 text-xs font-semibold text-amber-200">
            {getExpiryLabel(referral)}
          </span>
        </div>
      </div>

      <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-3">
        <div>
          <dt className="text-slate-400">Premium</dt>
          <dd className="font-semibold text-white">
            {formatCurrency(referral.premium)}
          </dd>
        </div>
        <div>
          <dt className="text-slate-400">Limit</dt>
          <dd className="font-semibold text-white">
            {formatCurrency(referral.requestedLimit)}
          </dd>
        </div>
        <div>
          <dt className="text-slate-400">Retention</dt>
          <dd className="font-semibold text-white">
            {formatCurrency(referral.retention)}
          </dd>
        </div>
      </dl>

      <OperationsSummary operations={referral.operations} />

      <EvidenceSummary quote={referral} />

      <div className="mt-4 grid gap-4 lg:grid-cols-2">
        <section>
          <h3 className="text-sm font-semibold text-white">Referral reasons</h3>
          <TextList
            emptyLabel="No referral reasons were returned."
            items={referral.referralReasons}
          />
        </section>
        <section>
          <h3 className="text-sm font-semibold text-white">Subjectivities</h3>
          <TextList
            emptyLabel="No subjectivities were returned."
            items={referral.subjectivities}
          />
        </section>
      </div>

      <button
        type="button"
        onClick={onSelect}
        className="mt-4 rounded-lg border border-slate-700 px-4 py-2 text-sm font-semibold text-slate-100 hover:border-emerald-400"
      >
        {isSelected ? "Selected for decision" : "Review this referral"}
      </button>
    </article>
  );
}

export function UnderwritingQuoteReferralsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [search, setSearch] = useState("");
  const [appliedSearch, setAppliedSearch] = useState("");
  const [riskTier, setRiskTier] = useState("");
  const [priority, setPriority] = useState("");
  const [assignment, setAssignment] = useState("");
  const [evidenceState, setEvidenceState] = useState("");
  const referralsQuery = useQuoteReferrals({
    search: appliedSearch || undefined,
    riskTier: riskTier || undefined,
    priority: priority || undefined,
    assignment: assignment || undefined,
    evidenceState: evidenceState || undefined,
  });
  const referrals = useMemo(
    () => referralsQuery.data?.quoteReferrals ?? [],
    [referralsQuery.data?.quoteReferrals],
  );
  const [evidenceSearch, setEvidenceSearch] = useState("");
  const [appliedEvidenceSearch, setAppliedEvidenceSearch] = useState("");
  const [evidenceStatus, setEvidenceStatus] = useState("");
  const [evidenceUnreadOnly, setEvidenceUnreadOnly] = useState(false);
  const [evidenceCursor, setEvidenceCursor] = useState<string>();
  const evidenceQueueQuery = useEvidenceQueue({
    search: appliedEvidenceSearch || undefined,
    status: evidenceStatus || undefined,
    unreadFollowUps: evidenceUnreadOnly || undefined,
    cursor: evidenceCursor,
    pageSize: 12,
  });
  const evidenceQueue = evidenceQueueQuery.data?.evidenceRequests ?? [];
  const deepLinkedQuoteId = searchParams.get("quoteId") ?? undefined;
  const deepLinkedEvidenceRequestId = searchParams.get("evidenceRequestId") ?? undefined;
  const selectedEvidence = evidenceQueue.find((item) =>
    item.quoteId === deepLinkedQuoteId
      && item.evidenceRequestId === deepLinkedEvidenceRequestId);
  const [queueFilter, setQueueFilter] = useState<QueueFilter>("all");
  const [selectedQuoteId, setSelectedQuoteId] = useState<string>();
  const [aiReview, setAiReview] = useState<AiUnderwritingReviewResponse>();
  const [actionResult, setActionResult] =
    useState<UnderwriteQuoteReferralResult>();

  const generateAiReview = useGenerateAiUnderwritingReview();
  const approveReferral = useApproveQuoteReferral();
  const declineReferral = useDeclineQuoteReferral();
  const adjustReferral = useAdjustQuoteReferral();

  const visibleReferrals = useMemo(
    () => sortForTriage(filterReferrals(referrals, queueFilter)),
    [queueFilter, referrals],
  );
  const selectedReferral =
    visibleReferrals.find((referral) => referral.quoteId === selectedQuoteId) ??
    visibleReferrals[0];

  async function handleGenerateAiReview() {
    if (!selectedReferral) return;

    const result = await generateAiReview.mutateAsync(selectedReferral.quoteId);
    setAiReview(result);
  }

  async function handleApprove(request: { reason: string; notes: string }) {
    if (!selectedReferral) return;

    const result = await approveReferral.mutateAsync({
      quoteId: selectedReferral.quoteId,
      request,
    });
    setActionResult(result);
  }

  async function handleDecline(request: { reason: string; notes: string }) {
    if (!selectedReferral) return;

    const result = await declineReferral.mutateAsync({
      quoteId: selectedReferral.quoteId,
      request,
    });
    setActionResult(result);
  }

  async function handleAdjust(request: {
    adjustedPremium: number;
    adjustedRetention: number;
    updatedSubjectivities: string;
    reason: string;
    notes: string;
  }) {
    if (!selectedReferral) return;

    const result = await adjustReferral.mutateAsync({
      quoteId: selectedReferral.quoteId,
      request,
    });
    setActionResult(result);
  }

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
      <section className="mx-auto max-w-7xl">
        <Breadcrumbs items={[{ label: "Dashboard", to: "/dashboard" }, { label: "Underwriting" }]} />

        <div className="mt-8 flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <p className="text-sm font-semibold uppercase tracking-wide text-emerald-400">
              Underwriting
            </p>
            <h1 className="mt-4 text-4xl font-bold tracking-tight">
              Underwriting workbench
            </h1>
            <p className="mt-4 max-w-3xl text-slate-300">
              Triage referred cyber quotes, review evidence, request advisory AI
              support, and record the human underwriting decision.
            </p>
          </div>

          <label className="w-full max-w-xs text-sm font-medium text-slate-200">
            Queue filter
            <select
              value={queueFilter}
              onChange={(event) => setQueueFilter(event.target.value as QueueFilter)}
              className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400"
            >
              <option value="all">All referred quotes</option>
              <option value="high">High and severe risk</option>
              <option value="expiring">Expiring within 7 days</option>
            </select>
          </label>
        </div>

        <ReassessmentReviewPanel />

        <section className="mt-6 rounded-lg border border-cyan-800 bg-slate-900 p-5" aria-labelledby="evidence-review-queue-title">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <h2 id="evidence-review-queue-title" className="text-xl font-semibold">Evidence review queue</h2>
              <p className="mt-2 text-sm text-slate-300">Current evidence work remains available even when its quote was not referred.</p>
            </div>
            <span className="rounded-full bg-cyan-300 px-3 py-1 text-sm font-semibold text-slate-950">
              {evidenceQueue.length} on this page
            </span>
          </div>
          <form className="mt-4 grid gap-3 md:grid-cols-[1fr_220px_auto]" onSubmit={(event) => { event.preventDefault(); setEvidenceCursor(undefined); setAppliedEvidenceSearch(evidenceSearch.trim()); }}>
            <label className="text-sm font-semibold text-slate-200">Search evidence<input value={evidenceSearch} onChange={(event) => setEvidenceSearch(event.target.value)} placeholder="Company, submission, quote, or request" className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" /></label>
            <label className="text-sm font-semibold text-slate-200">Status<select value={evidenceStatus} onChange={(event) => { setEvidenceStatus(event.target.value); setEvidenceCursor(undefined); }} className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2"><option value="">All active statuses</option><option value="Open">Open</option><option value="Responded">Responded</option></select></label>
            <div className="flex items-end gap-3"><button type="submit" className="rounded-md bg-cyan-300 px-4 py-2 font-semibold text-slate-950">Search</button><button type="button" onClick={() => { setEvidenceSearch(""); setAppliedEvidenceSearch(""); setEvidenceStatus(""); setEvidenceUnreadOnly(false); setEvidenceCursor(undefined); }} className="rounded-md border border-slate-600 px-4 py-2 font-semibold">Clear</button></div>
            <label className="flex items-center gap-2 text-sm text-slate-200 md:col-span-3"><input type="checkbox" checked={evidenceUnreadOnly} onChange={(event) => { setEvidenceUnreadOnly(event.target.checked); setEvidenceCursor(undefined); }} />Unread customer follow-ups only</label>
          </form>
          {evidenceQueueQuery.isPending && <p role="status" className="mt-4 text-sm text-slate-300">Loading evidence review work...</p>}
          {evidenceQueueQuery.isError && <p role="alert" className="mt-4 rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">{getErrorMessage(evidenceQueueQuery.error, "Unable to load evidence review work.")}</p>}
          {evidenceQueueQuery.isSuccess && evidenceQueue.length === 0 && !deepLinkedEvidenceRequestId && <p className="mt-4 text-sm text-slate-300">No current evidence requests match these filters.</p>}
          {evidenceQueue.length > 0 && (
            <ul className="mt-4 grid gap-3 lg:grid-cols-2">
              {evidenceQueue.map((item) => (
                <li key={item.evidenceRequestId} className="rounded-md border border-slate-700 bg-slate-950 p-4">
                  <div className="flex items-start justify-between gap-3"><div><p className="font-semibold">{item.title}</p><p className="mt-1 text-sm text-slate-300">{item.companyName} · {item.submissionReference}</p></div>{item.pendingFollowUpCount > 0 && <span className="rounded-full bg-amber-300 px-2 py-1 text-xs font-semibold text-slate-950">{item.pendingFollowUpCount} unread</span>}</div>
                  <p className="mt-2 text-xs text-slate-400">Quote version {item.quoteVersion} · {item.category} · {item.status}/{item.reviewDecision}</p>
                  <p className="mt-1 text-xs text-slate-400">{item.documentCount} documents · {item.downloadableDocumentCount} ready · {item.isOverdue ? "Overdue" : `Due ${formatDate(item.dueAtUtc)}`}</p>
                  <button type="button" onClick={() => setSearchParams((current) => { const next = new URLSearchParams(current); next.set("quoteId", item.quoteId); next.set("evidenceRequestId", item.evidenceRequestId); return next; })} className="mt-3 rounded-md border border-cyan-400 px-3 py-2 text-sm font-semibold text-cyan-100 hover:bg-cyan-300 hover:text-slate-950">Review evidence request</button>
                </li>
              ))}
            </ul>
          )}
          {evidenceQueueQuery.data?.nextCursor && <button type="button" onClick={() => setEvidenceCursor(evidenceQueueQuery.data.nextCursor ?? undefined)} className="mt-4 rounded-md border border-slate-600 px-4 py-2 text-sm font-semibold">Next page</button>}
        </section>

        {deepLinkedQuoteId && deepLinkedEvidenceRequestId && (
          <div className="mt-6">
            <EvidencePanel
              key={`${deepLinkedQuoteId}:${deepLinkedEvidenceRequestId}`}
              initialEvidenceRequestId={deepLinkedEvidenceRequestId}
              quote={{
                quoteId: deepLinkedQuoteId,
                companyName: selectedEvidence?.companyName,
                operations: null,
                evidence: {
                  openRequestCount: 0,
                  respondedRequestCount: 0,
                  unreviewedRespondedRequestCount: 0,
                  satisfiedRequestCount: 0,
                  needsAttentionRequestCount: 0,
                  overdueRequestCount: 0,
                  nextOpenDueAtUtc: null,
                  isWaitingForInformation: true,
                  latestEvidenceActivityAtUtc: selectedEvidence?.latestActivityAtUtc ?? null,
                },
              }}
            />
          </div>
        )}

        <form className="mt-6 grid gap-4 rounded-lg border border-slate-800 bg-slate-900 p-4 md:grid-cols-5" onSubmit={(event: FormEvent) => { event.preventDefault(); setAppliedSearch(search.trim()); }}>
          <label className="text-sm font-semibold text-slate-200">Search referrals<input className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" placeholder="Quote, submission, or owner" value={search} onChange={(event) => setSearch(event.target.value)} /></label>
          <label className="text-sm font-semibold text-slate-200">Risk tier<select className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" value={riskTier} onChange={(event) => setRiskTier(event.target.value)}><option value="">All risk tiers</option>{['Low', 'Moderate', 'High', 'Severe'].map((value) => <option key={value}>{value}</option>)}</select></label>
          <label className="text-sm font-semibold text-slate-200">Priority<select className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" value={priority} onChange={(event) => setPriority(event.target.value)}><option value="">All priorities</option>{['Low', 'Normal', 'High', 'Urgent'].map((value) => <option key={value}>{value}</option>)}</select></label>
          <label className="text-sm font-semibold text-slate-200">Assignment<select className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" value={assignment} onChange={(event) => setAssignment(event.target.value)}><option value="">Any assignment</option><option value="assigned">Assigned</option><option value="unassigned">Unassigned</option></select></label>
          <label className="text-sm font-semibold text-slate-200">Evidence<select className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" value={evidenceState} onChange={(event) => setEvidenceState(event.target.value)}><option value="">Any evidence state</option><option value="waiting">Waiting for information</option><option value="attention">Needs attention</option><option value="overdue">Overdue</option><option value="satisfied">Satisfied</option></select></label>
          <div className="flex gap-3 md:col-span-5"><button type="submit" className="rounded-md bg-emerald-400 px-4 py-2 font-semibold text-slate-950">Search</button><button type="button" className="rounded-md border border-slate-600 px-4 py-2 font-semibold" onClick={() => { setSearch(""); setAppliedSearch(""); setRiskTier(""); setPriority(""); setAssignment(""); setEvidenceState(""); }}>Clear</button></div>
        </form>

        {referralsQuery.isPending && (
          <p className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-300">
            Loading referred quotes...
          </p>
        )}

        {referralsQuery.isError && (
          <p className="mt-8 whitespace-pre-wrap rounded-lg border border-red-900 bg-red-950 p-4 text-sm text-red-200">
            {getErrorMessage(
              referralsQuery.error,
              "Unable to load underwriting referrals.",
            )}
          </p>
        )}

        {referralsQuery.isSuccess && referrals.length === 0 && (
          <section className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-lg font-semibold text-white">
              No referred quotes are waiting for review.
            </h2>
            <p className="mt-2 text-sm text-slate-300">
              Quotes appear here after the rating workflow refers them to
              underwriting.
            </p>
          </section>
        )}

        {visibleReferrals.length > 0 && (
          <div className="mt-8 grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(420px,0.9fr)]">
            <section className="space-y-4">
              {visibleReferrals.map((referral) => (
                <ReferralCard
                  key={referral.quoteId}
                  referral={referral}
                  isSelected={referral.quoteId === selectedReferral?.quoteId}
                  onSelect={() => {
                    setSelectedQuoteId(referral.quoteId);
                    setAiReview(undefined);
                    setActionResult(undefined);
                  }}
                />
              ))}
            </section>

            {selectedReferral && (
              <aside className="space-y-5">
                <section className="rounded-lg border border-slate-800 bg-slate-900 p-5">
                  <p className="text-sm font-semibold uppercase tracking-wide text-emerald-400">
                    Selected quote
                  </p>
                  <h2 className="mt-2 text-xl font-semibold text-white">
                    {selectedReferral.quoteId}
                  </h2>
                  <dl className="mt-4 grid gap-3 text-sm md:grid-cols-2">
                    <div>
                      <dt className="text-slate-400">Status</dt>
                      <dd className="font-medium text-white">
                        {selectedReferral.status}
                      </dd>
                    </div>
                    <div>
                      <dt className="text-slate-400">Expires</dt>
                      <dd className="font-medium text-white">
                        {formatDate(selectedReferral.expiresAtUtc)}
                      </dd>
                    </div>
                    <div>
                      <dt className="text-slate-400">Owner user id</dt>
                      <dd className="break-all font-mono text-xs text-slate-200">
                        {selectedReferral.ownerUserId}
                      </dd>
                    </div>
                    <div>
                      <dt className="text-slate-400">Created</dt>
                      <dd className="font-medium text-white">
                        {formatDate(selectedReferral.createdAtUtc)}
                      </dd>
                    </div>
                  </dl>
                </section>

                <AiReviewPanel
                  aiReview={aiReview}
                  error={generateAiReview.error}
                  isPending={generateAiReview.isPending}
                  onGenerate={handleGenerateAiReview}
                />

                <OperationsPanel key={`${selectedReferral.quoteId}-operations`} quote={selectedReferral} />

                <EvidencePanel key={`${selectedReferral.quoteId}-evidence`} quote={selectedReferral} />

                <ManualActionsPanel
                  key={selectedReferral.quoteId}
                  quote={selectedReferral}
                  actionResult={actionResult}
                  actionError={
                    approveReferral.error ??
                    declineReferral.error ??
                    adjustReferral.error
                  }
                  onApprove={handleApprove}
                  onDecline={handleDecline}
                  onDismissActionResult={() => setActionResult(undefined)}
                  onAdjust={handleAdjust}
                />
              </aside>
            )}
          </div>
        )}
      </section>
    </main>
  );
}
