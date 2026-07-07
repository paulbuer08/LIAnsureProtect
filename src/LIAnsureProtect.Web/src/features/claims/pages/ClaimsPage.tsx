import { Link } from "react-router";

import { useMyClaims } from "../hooks/useClaims";

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Unable to load claims.";
}

export function ClaimsPage() {
  const claimsQuery = useMyClaims();
  const claims = claimsQuery.data?.claims ?? [];

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
              Claims
            </p>
            <h1 className="mt-4 text-4xl font-bold tracking-tight">
              My claims
            </h1>
            <p className="mt-4 max-w-2xl text-slate-300">
              Track the cyber claims you have filed against your bound
              policies.
            </p>
          </div>

          <Link
            to="/claims/new"
            className="inline-flex rounded-lg bg-emerald-400 px-5 py-3 text-sm font-semibold text-slate-950 shadow-sm hover:bg-emerald-300"
          >
            File a claim
          </Link>
        </div>

        {claimsQuery.isPending && (
          <p className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-300">
            Loading claims...
          </p>
        )}

        {claimsQuery.isError && (
          <p className="mt-8 whitespace-pre-wrap rounded-lg border border-red-900 bg-red-950 p-4 text-sm text-red-200">
            {getErrorMessage(claimsQuery.error)}
          </p>
        )}

        {claimsQuery.isSuccess && claims.length === 0 && (
          <section className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-lg font-semibold text-white">No claims yet.</h2>
            <p className="mt-2 text-sm text-slate-300">
              File a claim against one of your bound policies to see it here.
            </p>
          </section>
        )}

        {claims.length > 0 && (
          <div className="mt-8 overflow-hidden rounded-lg border border-slate-800">
            <div className="grid grid-cols-1 gap-0 bg-slate-900 text-sm text-slate-200">
              {claims.map((claim) => (
                <article
                  className="border-b border-slate-800 p-5 last:border-b-0"
                  key={claim.claimId}
                >
                  <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                    <div>
                      <h2 className="text-lg font-semibold text-white">
                        {claim.claimNumber}
                      </h2>
                      <p className="mt-1 text-slate-300">
                        {claim.incidentType} on policy {claim.policyNumber}
                      </p>
                      <p className="mt-1 text-slate-400">
                        Filed {new Date(claim.filedAtUtc).toLocaleDateString()}
                      </p>
                    </div>

                    <div className="flex flex-col gap-3 md:items-end">
                      <span className="inline-flex w-fit rounded-md border border-emerald-800 px-3 py-1 text-xs font-semibold uppercase tracking-wide text-emerald-300">
                        {claim.status}
                      </span>
                      <Link
                        to={`/claims/${claim.claimId}`}
                        className="inline-flex text-sm font-semibold text-emerald-300 hover:text-emerald-200"
                      >
                        View claim {claim.claimNumber}
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
