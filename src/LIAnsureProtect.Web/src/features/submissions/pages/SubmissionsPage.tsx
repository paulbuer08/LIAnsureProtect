import { Link } from "react-router";

import { useSubmissions } from "../hooks/useSubmissions";

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Unable to load submissions.";
}

export function SubmissionsPage() {
  const submissionsQuery = useSubmissions();
  const submissions = submissionsQuery.data?.submissions ?? [];

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
      <section className="mx-auto max-w-5xl">
        <Link
          to="/dashboard"
          className="inline-flex text-sm font-semibold text-emerald-300 hover:text-emerald-200"
        >
          Back to dashboard
        </Link>

        <div className="mt-8 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <p className="text-sm font-semibold uppercase tracking-wide text-emerald-400">
              Submissions
            </p>
            <h1 className="mt-4 text-4xl font-bold tracking-tight">
              Submission list
            </h1>
            <p className="mt-4 max-w-2xl text-slate-300">
              Review draft submissions created through the protected intake
              workflow.
            </p>
          </div>

          <Link
            to="/submissions/new"
            className="inline-flex rounded-lg bg-emerald-400 px-5 py-3 text-sm font-semibold text-slate-950 shadow-sm hover:bg-emerald-300"
          >
            Create submission
          </Link>
        </div>

        {submissionsQuery.isPending && (
          <p className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-300">
            Loading submissions...
          </p>
        )}

        {submissionsQuery.isError && (
          <p className="mt-8 whitespace-pre-wrap rounded-lg border border-red-900 bg-red-950 p-4 text-sm text-red-200">
            {getErrorMessage(submissionsQuery.error)}
          </p>
        )}

        {submissionsQuery.isSuccess && submissions.length === 0 && (
          <section className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-lg font-semibold text-white">
              No submissions yet.
            </h2>
            <p className="mt-2 text-sm text-slate-300">
              Create a draft submission to see it listed here.
            </p>
          </section>
        )}

        {submissions.length > 0 && (
          <div className="mt-8 overflow-hidden rounded-lg border border-slate-800">
            <div className="grid grid-cols-1 gap-0 bg-slate-900 text-sm text-slate-200">
              {submissions.map((submission) => (
                <article
                  className="border-b border-slate-800 p-5 last:border-b-0"
                  key={submission.submissionId}
                >
                  <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                    <div>
                      <h2 className="text-lg font-semibold text-white">
                        {submission.applicantName}
                      </h2>
                      <p className="mt-1 text-slate-300">
                        {submission.companyName}
                      </p>
                      <p className="mt-1 text-slate-400">
                        {submission.applicantEmail}
                      </p>
                    </div>

                    <div className="flex flex-col gap-3 md:items-end">
                      <span className="inline-flex w-fit rounded-md border border-emerald-800 px-3 py-1 text-xs font-semibold uppercase tracking-wide text-emerald-300">
                        {submission.status}
                      </span>
                      <Link
                        to={`/submissions/${submission.submissionId}`}
                        className="inline-flex text-sm font-semibold text-emerald-300 hover:text-emerald-200"
                      >
                        View details for {submission.applicantName}
                      </Link>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          </div>
        )}
      </section>
    </main>
  );
}
