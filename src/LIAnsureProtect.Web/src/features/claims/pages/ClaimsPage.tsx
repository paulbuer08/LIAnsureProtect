import { useState, type FormEvent } from "react";
import { Link } from "react-router";

import { Breadcrumbs } from "../../../components/Breadcrumbs";
import { getUserErrorMessage } from "../../../lib/apiClient";
import { useMyClaims } from "../hooks/useClaims";

function getErrorMessage(error: unknown) {
  return getUserErrorMessage(error, "Unable to load claims.");
}

export function ClaimsPage() {
  const [search, setSearch] = useState("");
  const [appliedSearch, setAppliedSearch] = useState("");
  const [status, setStatus] = useState("");
  const [incidentType, setIncidentType] = useState("");
  const claimsQuery = useMyClaims({
    search: appliedSearch || undefined,
    status: status || undefined,
    incidentType: incidentType || undefined,
  });
  const claims = claimsQuery.data?.claims ?? [];

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
      <section className="mx-auto max-w-5xl">
        <Breadcrumbs
          items={[{ label: "Dashboard", to: "/dashboard" }, { label: "Claims" }]}
        />

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

        <form
          className="mt-6 grid gap-4 rounded-lg border border-slate-800 bg-slate-900 p-4 md:grid-cols-3"
          onSubmit={(event: FormEvent) => {
            event.preventDefault();
            setAppliedSearch(search.trim());
          }}
        >
          <label className="text-sm font-semibold text-slate-200">
            Search your claims
            <input
              className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2"
              placeholder="Claim or policy number"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </label>
          <label className="text-sm font-semibold text-slate-200">
            Status
            <select className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" value={status} onChange={(event) => setStatus(event.target.value)}>
              <option value="">All statuses</option>
              {['Filed', 'UnderReview', 'InformationRequested', 'Accepted', 'Denied', 'Closed'].map((value) => <option key={value}>{value}</option>)}
            </select>
          </label>
          <label className="text-sm font-semibold text-slate-200">
            Incident type
            <select className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" value={incidentType} onChange={(event) => setIncidentType(event.target.value)}>
              <option value="">All incident types</option>
              {['RansomwareExtortion', 'BusinessEmailCompromise', 'DataBreachPrivacy', 'NetworkInterruption', 'FundsTransferFraud', 'Other'].map((value) => <option key={value}>{value}</option>)}
            </select>
          </label>
          <div className="flex gap-3 md:col-span-3">
            <button type="submit" className="rounded-md bg-emerald-400 px-4 py-2 font-semibold text-slate-950">Search</button>
            <button type="button" className="rounded-md border border-slate-600 px-4 py-2 font-semibold" onClick={() => { setSearch(""); setAppliedSearch(""); setStatus(""); setIncidentType(""); }}>Clear</button>
          </div>
        </form>

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
