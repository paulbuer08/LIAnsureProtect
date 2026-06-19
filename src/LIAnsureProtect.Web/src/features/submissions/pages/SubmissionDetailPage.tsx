import { Link, useParams } from "react-router";

import { useSubmissionDetail } from "../hooks/useSubmissionDetail";

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Unable to load submission.";
}

export function SubmissionDetailPage() {
  const { submissionId } = useParams();
  const submissionQuery = useSubmissionDetail(submissionId);
  const submission = submissionQuery.data;

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

        {submission && (
          <section className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-6 text-sm text-slate-200">
            <dl className="grid gap-5 sm:grid-cols-2">
              <div>
                <dt className="font-semibold text-slate-400">Submission ID</dt>
                <dd className="mt-1 break-all text-white">
                  {submission.submissionId}
                </dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-400">Status</dt>
                <dd className="mt-1 text-white">{submission.status}</dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-400">Applicant</dt>
                <dd className="mt-1 text-white">{submission.applicantName}</dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-400">
                  Applicant email
                </dt>
                <dd className="mt-1 text-white">{submission.applicantEmail}</dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-400">Company</dt>
                <dd className="mt-1 text-white">{submission.companyName}</dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-400">Created UTC</dt>
                <dd className="mt-1 text-white">
                  <time dateTime={submission.createdAtUtc}>
                    {submission.createdAtUtc}
                  </time>
                </dd>
              </div>
            </dl>
          </section>
        )}
      </section>
    </main>
  );
}
