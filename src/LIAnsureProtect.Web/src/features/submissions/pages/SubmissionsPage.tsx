import { useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router";

import { Breadcrumbs } from "../../../components/Breadcrumbs";
import { getUserErrorMessage } from "../../../lib/apiClient";
import { formatLocalDateTime } from "../../../lib/dateTime";
import { useSubmissions } from "../hooks/useSubmissions";

function getErrorMessage(error: unknown) {
  return getUserErrorMessage(error, "Unable to load submissions.");
}

export function SubmissionsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [search, setSearch] = useState(searchParams.get("search") ?? "");
  const [cursorHistory, setCursorHistory] = useState<string[]>([]);
  const filters = {
    search: searchParams.get("search") || undefined,
    status: searchParams.get("status") || undefined,
    createdFromUtc: searchParams.get("createdFromUtc") || undefined,
    createdToUtc: searchParams.get("createdToUtc") || undefined,
    cursor: searchParams.get("cursor") || undefined,
    pageSize: 20,
  };
  const submissionsQuery = useSubmissions(filters);
  const submissions = submissionsQuery.data?.submissions ?? [];

  function updateFilter(name: string, value: string) {
    setSearchParams((current) => {
      const next = new URLSearchParams(current);
      if (value) next.set(name, value);
      else next.delete(name);
      next.delete("cursor");
      return next;
    });
    setCursorHistory([]);
  }

  function submitSearch(event: FormEvent) {
    event.preventDefault();
    updateFilter("search", search.trim());
  }

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
      <section className="mx-auto max-w-5xl">
        <Breadcrumbs items={[{ label: "Dashboard", to: "/dashboard" }, { label: "Submissions" }]} />

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
            Create draft submission
          </Link>
        </div>

        <form className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-5" onSubmit={submitSearch}>
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
            <label className="text-sm font-semibold text-slate-200 lg:col-span-2">
              Search your submissions
              <input
                className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white"
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Reference, ID, applicant, email, or company"
                value={search}
              />
            </label>
            <label className="text-sm font-semibold text-slate-200">
              Status
              <select className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" onChange={(event) => updateFilter("status", event.target.value)} value={filters.status ?? ""}>
                <option value="">All statuses</option>
                <option value="Draft">Draft</option>
                <option value="Submitted">Submitted</option>
                <option value="Withdrawn">Withdrawn</option>
              </select>
            </label>
            <div className="flex items-end gap-3">
              <button className="rounded-md bg-emerald-400 px-4 py-2 font-semibold text-slate-950" type="submit">Search</button>
              <button className="rounded-md border border-slate-600 px-4 py-2 font-semibold" onClick={() => { setSearch(""); setSearchParams({}); setCursorHistory([]); }} type="button">Clear</button>
            </div>
          </div>
          <div className="mt-4 grid gap-4 sm:grid-cols-2">
            <label className="text-sm text-slate-300">Created from<input className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" onChange={(event) => updateFilter("createdFromUtc", event.target.value ? new Date(`${event.target.value}T00:00:00`).toISOString() : "")} type="date" /></label>
            <label className="text-sm text-slate-300">Created through<input className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" onChange={(event) => updateFilter("createdToUtc", event.target.value ? new Date(`${event.target.value}T23:59:59.999`).toISOString() : "")} type="date" /></label>
          </div>
        </form>

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
                      <p className="mt-1 font-mono text-sm text-emerald-300">{submission.submissionReference ?? submission.submissionId}</p>
                      <p className="mt-1 text-slate-300">
                        {submission.companyName}
                      </p>
                      <p className="mt-1 text-slate-400">
                        {submission.applicantEmail}
                      </p>
                      <time className="mt-2 block text-xs text-slate-500" dateTime={submission.createdAtUtc} title={submission.createdAtUtc}>
                        Created {formatLocalDateTime(submission.createdAtUtc)}
                      </time>
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
        {(cursorHistory.length > 0 || submissionsQuery.data?.nextCursor) && (
          <nav aria-label="Submission pages" className="mt-6 flex gap-3">
            <button className="rounded-md border border-slate-600 px-4 py-2 font-semibold disabled:text-slate-600" disabled={cursorHistory.length === 0} onClick={() => {
              const previous = cursorHistory.at(-1);
              setCursorHistory((items) => items.slice(0, -1));
              setSearchParams((current) => { const next = new URLSearchParams(current); if (previous) next.set("cursor", previous); else next.delete("cursor"); return next; });
            }} type="button">Previous</button>
            <button className="rounded-md border border-emerald-600 px-4 py-2 font-semibold text-emerald-300 disabled:border-slate-700 disabled:text-slate-600" disabled={!submissionsQuery.data?.nextCursor} onClick={() => {
              setCursorHistory((items) => [...items, filters.cursor ?? ""]);
              setSearchParams((current) => { const next = new URLSearchParams(current); next.set("cursor", submissionsQuery.data?.nextCursor ?? ""); return next; });
            }} type="button">Next</button>
          </nav>
        )}
      </section>
    </main>
  );
}
