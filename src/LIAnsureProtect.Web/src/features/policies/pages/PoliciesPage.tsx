import { Link } from "react-router";
import { formatCurrency } from "../../../lib/currency";
import { usePolicies } from "../hooks/usePolicies";

export function PoliciesPage() {
  const query = usePolicies();
  const policies = query.data?.policies ?? [];

  return <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
    <section className="mx-auto max-w-6xl">
      <Link to="/dashboard" className="text-sm font-semibold text-emerald-300">Back to dashboard</Link>
      <p className="mt-8 text-sm font-semibold uppercase text-emerald-400">Coverage</p>
      <h1 className="mt-3 text-4xl font-bold">My policies</h1>
      <p className="mt-3 text-slate-300">Review bound contracts independently from their source submissions.</p>
      {query.isPending && <p className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-5">Loading policies...</p>}
      {query.isError && <p className="mt-8 rounded-lg border border-red-900 bg-red-950 p-5 text-red-200">{query.error instanceof Error ? query.error.message : "Unable to load policies."}</p>}
      {query.isSuccess && policies.length === 0 && <p className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-6 text-slate-300">No policies are bound yet.</p>}
      <div className="mt-8 grid gap-4 lg:grid-cols-2">
        {policies.map((policy) => <article key={policy.policyId} className="rounded-lg border border-slate-800 bg-slate-900 p-6">
          <div className="flex justify-between gap-4"><div><p className="text-sm text-slate-400">{policy.companyName}</p><h2 className="mt-1 text-xl font-semibold">{policy.policyNumber}</h2></div><span className="h-fit rounded-md border border-emerald-700 px-3 py-1 text-xs font-semibold text-emerald-300">{policy.coverageState}</span></div>
          <dl className="mt-5 grid grid-cols-2 gap-4 text-sm"><div><dt className="text-slate-400">Coverage dates</dt><dd>{new Date(policy.effectiveDateUtc).toLocaleDateString()} – {new Date(policy.expirationDateUtc).toLocaleDateString()}</dd></div><div><dt className="text-slate-400">Premium</dt><dd>{formatCurrency(policy.premium)}</dd></div><div><dt className="text-slate-400">Limit</dt><dd>{formatCurrency(policy.requestedLimit)}</dd></div><div><dt className="text-slate-400">Retention</dt><dd>{formatCurrency(policy.retention)}</dd></div></dl>
          <Link to={`/policies/${policy.policyId}`} className="mt-5 inline-flex font-semibold text-emerald-300">View policy</Link>
        </article>)}
      </div>
    </section>
  </main>;
}
