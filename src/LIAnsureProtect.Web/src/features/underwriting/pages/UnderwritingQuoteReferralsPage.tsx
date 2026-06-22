import { useMemo, useState } from "react";
import type { FormEvent } from "react";
import { Link } from "react-router";

import {
  useAdjustQuoteReferral,
  useApproveQuoteReferral,
  useDeclineQuoteReferral,
  useGenerateAiUnderwritingReview,
} from "../hooks/useUnderwritingActions";
import { useQuoteReferrals } from "../hooks/useQuoteReferrals";
import type {
  AiUnderwritingReviewResponse,
  QuoteReferral,
  UnderwriteQuoteReferralResult,
} from "../types";

type QueueFilter = "all" | "high" | "expiring";

const currencyFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});

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
  return error instanceof Error ? error.message : fallback;
}

function formatCurrency(value: number) {
  return currencyFormatter.format(value);
}

function formatDate(value: string) {
  return dateFormatter.format(new Date(value));
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
        <p className="mt-4 rounded-md border border-emerald-800 bg-emerald-950 p-3 text-sm font-semibold text-emerald-100">
          Manual action saved: {actionResult.status}
        </p>
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
  const referralsQuery = useQuoteReferrals();
  const referrals = useMemo(
    () => referralsQuery.data?.quoteReferrals ?? [],
    [referralsQuery.data?.quoteReferrals],
  );
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
        <Link
          to="/dashboard"
          className="inline-flex text-sm font-semibold text-emerald-300 hover:text-emerald-200"
        >
          Back to dashboard
        </Link>

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
