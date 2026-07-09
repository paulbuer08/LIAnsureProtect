import { Link, useParams } from "react-router";

import { useSubmissionDetail } from "../hooks/useSubmissionDetail";
import { useSubmitSubmission } from "../hooks/useSubmitSubmission";

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Unable to load submission.";
}

export function SubmissionDetailPage() {
  const { submissionId } = useParams();
  const submissionQuery = useSubmissionDetail(submissionId);
  const submitSubmissionMutation = useSubmitSubmission();
  const submission = submissionQuery.data;
  const displayedSubmission =
    submission &&
    submitSubmissionMutation.data?.submissionId === submission.submissionId
      ? {
          ...submission,
          status: submitSubmissionMutation.data.status,
        }
      : submission;
  const canSubmit = displayedSubmission?.status === "Draft";

  function handleSubmitSubmission() {
    if (!submission) {
      return;
    }

    submitSubmissionMutation.mutate(submission.submissionId);
  }

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
      <section className="mx-auto max-w-4xl">
        <Link
          to="/submissions"
          className="inline-flex text-sm font-semibold text-emerald-300 hover:text-emerald-200"
        >
          Back to submissions
        </Link>

        <p className="mt-8 text-sm font-semibold uppercase tracking-wide text-emerald-400">
          Submission
        </p>
        <h1 className="mt-4 text-4xl font-bold tracking-tight">
          Submission detail
        </h1>

        {submissionQuery.isPending && (
          <p className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-300">
            Loading submission...
          </p>
        )}

        {submissionQuery.isError && (
          <p className="mt-8 whitespace-pre-wrap rounded-lg border border-red-900 bg-red-950 p-4 text-sm text-red-200">
            {getErrorMessage(submissionQuery.error)}
          </p>
        )}

        {displayedSubmission && (
          <section className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-6 text-sm text-slate-200">
            <dl className="grid gap-5 sm:grid-cols-2">
              <div>
                <dt className="font-semibold text-slate-400">Submission ID</dt>
                <dd className="mt-1 break-all text-white">
                  {displayedSubmission.submissionId}
                </dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-400">Status</dt>
                <dd className="mt-1 text-white">{displayedSubmission.status}</dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-400">Applicant</dt>
                <dd className="mt-1 text-white">
                  {displayedSubmission.applicantName}
                </dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-400">
                  Applicant email
                </dt>
                <dd className="mt-1 text-white">
                  {displayedSubmission.applicantEmail}
                </dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-400">Company</dt>
                <dd className="mt-1 text-white">
                  {displayedSubmission.companyName}
                </dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-400">Created UTC</dt>
                <dd className="mt-1 text-white">
                  <time dateTime={displayedSubmission.createdAtUtc}>
                    {displayedSubmission.createdAtUtc}
                  </time>
                </dd>
              </div>
            </dl>

            {canSubmit && (
              <div className="mt-6 border-t border-slate-800 pt-5">
                <h2 className="text-base font-semibold text-white">
                  Submit this draft
                </h2>
                <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-300">
                  Submit the completed intake so quote generation and downstream
                  underwriting steps can start.
                </p>
                <button
                  type="button"
                  onClick={handleSubmitSubmission}
                  disabled={submitSubmissionMutation.isPending}
                  className="mt-4 inline-flex min-h-10 items-center rounded-md bg-emerald-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-emerald-200 disabled:cursor-not-allowed disabled:bg-slate-600 disabled:text-slate-300"
                >
                  {submitSubmissionMutation.isPending
                    ? "Submitting..."
                    : "Submit submission"}
                </button>
              </div>
            )}

            {submitSubmissionMutation.isSuccess && (
              <p className="mt-5 rounded-md border border-emerald-500/40 bg-emerald-950/30 p-3 text-sm text-emerald-100">
                Submission submitted successfully.
              </p>
            )}

            {submitSubmissionMutation.isError && (
              <p className="mt-5 whitespace-pre-wrap rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">
                {getErrorMessage(submitSubmissionMutation.error)}
              </p>
            )}
          </section>
        )}
      </section>
    </main>
  );
}
