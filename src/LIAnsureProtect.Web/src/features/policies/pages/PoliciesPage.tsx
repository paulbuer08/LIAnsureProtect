import { useState, type FormEvent } from "react";
import { Link } from "react-router";
import { Breadcrumbs } from "../../../components/Breadcrumbs";
import { getUserErrorMessage } from "../../../lib/apiClient";
import { formatCurrency } from "../../../lib/currency";
import { usePolicies } from "../hooks/usePolicies";

export function PoliciesPage() {
  const [search, setSearch] = useState("");
  const [appliedSearch, setAppliedSearch] = useState("");
  const [contractualStatus, setContractualStatus] = useState("");
  const [coverageState, setCoverageState] = useState("");
  const query = usePolicies({ search: appliedSearch || undefined, contractualStatus: contractualStatus || undefined, coverageState: coverageState || undefined });
  const policies = query.data?.policies ?? [];

  return <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
    <section className="mx-auto max-w-6xl">
      <Breadcrumbs items={[{ label: "Dashboard", to: "/dashboard" }, { label: "Policies" }]} />
      <p className="mt-8 text-sm font-semibold uppercase text-emerald-400">Coverage</p>
      <h1 className="mt-3 text-4xl font-bold">My policies</h1>
      <p className="mt-3 text-slate-300">Review bound contracts independently from their source submissions.</p>
      <form className="mt-6 grid gap-4 rounded-lg border border-slate-800 bg-slate-900 p-4 md:grid-cols-4" onSubmit={(event: FormEvent) => { event.preventDefault(); setAppliedSearch(search.trim()); }}>
        <label className="text-sm font-semibold text-slate-200 md:col-span-2">Search your policies<input className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" placeholder="Policy, submission, applicant, or company" value={search} onChange={(event) => setSearch(event.target.value)} /></label>
        <label className="text-sm font-semibold text-slate-200">Contract status<select className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" value={contractualStatus} onChange={(event) => setContractualStatus(event.target.value)}><option value="">All statuses</option><option value="Bound">Bound</option><option value="Cancelled">Cancelled</option></select></label>
        <label className="text-sm font-semibold text-slate-200">Coverage state<select className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" value={coverageState} onChange={(event) => setCoverageState(event.target.value)}><option value="">All coverage states</option><option value="Scheduled">Scheduled</option><option value="Active">Active</option><option value="Expired">Expired</option><option value="Inactive">Inactive</option></select></label>
        <div className="flex gap-3 md:col-span-4"><button type="submit" className="rounded-md bg-emerald-400 px-4 py-2 font-semibold text-slate-950">Search</button><button type="button" className="rounded-md border border-slate-600 px-4 py-2 font-semibold" onClick={() => { setSearch(""); setAppliedSearch(""); setContractualStatus(""); setCoverageState(""); }}>Clear</button></div>
      </form>
      {query.isPending && <p className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-5">Loading policies...</p>}
      {query.isError && <p className="mt-8 rounded-lg border border-red-900 bg-red-950 p-5 text-red-200">{getUserErrorMessage(query.error, "Unable to load policies.")}</p>}
      {query.isSuccess && policies.length === 0 && <p className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-6 text-slate-300">No policies are bound yet.</p>}
      <div className="mt-8 grid gap-4 lg:grid-cols-2">
        {policies.map((policy) => <article key={policy.policyId} className="rounded-lg border border-slate-800 bg-slate-900 p-6">
          <div className="flex justify-between gap-4"><div><p className="text-sm text-slate-400">{policy.companyName}</p><h2 className="mt-1 text-xl font-semibold">{policy.policyNumber}</h2><p className="mt-1 font-mono text-xs text-emerald-300">{policy.submissionReference ?? policy.submissionId}</p></div><span className="h-fit rounded-md border border-emerald-700 px-3 py-1 text-xs font-semibold text-emerald-300">{policy.coverageState}</span></div>
          <dl className="mt-5 grid grid-cols-2 gap-4 text-sm"><div><dt className="text-slate-400">Coverage dates</dt><dd>{new Date(policy.effectiveDateUtc).toLocaleDateString()} – {new Date(policy.expirationDateUtc).toLocaleDateString()}</dd></div><div><dt className="text-slate-400">Premium</dt><dd>{formatCurrency(policy.premium)}</dd></div><div><dt className="text-slate-400">Limit</dt><dd>{formatCurrency(policy.requestedLimit)}</dd></div><div><dt className="text-slate-400">Retention</dt><dd>{formatCurrency(policy.retention)}</dd></div></dl>
          <Link to={`/policies/${policy.policyId}`} className="mt-5 inline-flex font-semibold text-emerald-300">View policy</Link>
        </article>)}
      </div>
    </section>
  </main>;
}
