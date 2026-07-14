import { Link, useParams } from "react-router";
import { Breadcrumbs } from "../../../components/Breadcrumbs";
import { getUserErrorMessage } from "../../../lib/apiClient";
import { formatCurrency } from "../../../lib/currency";
import { useClaimablePolicies } from "../../claims/hooks/useClaims";
import { usePolicy } from "../hooks/usePolicies";

export function PolicyDetailPage() {
  const { policyId } = useParams();
  const query = usePolicy(policyId);
  const claimableQuery = useClaimablePolicies();
  const policy = query.data;
  const canFileClaim = Boolean(policy && claimableQuery.data?.policies.some((item) => item.policyId === policy.policyId));

  return <main className="min-h-screen bg-slate-950 px-6 py-12 text-white"><section className="mx-auto max-w-5xl">
    <Breadcrumbs items={[{ label: "Dashboard", to: "/dashboard" }, { label: "Policies", to: "/policies" }, { label: policy?.policyNumber ?? "Policy detail" }]} />
    {query.isPending && <p className="mt-8">Loading policy...</p>}
    {query.isError && <p className="mt-8 rounded-lg border border-red-900 bg-red-950 p-5 text-red-200">{getUserErrorMessage(query.error, "Unable to load policy.")}</p>}
    {policy && <>
      <section className="mt-8 rounded-lg border border-emerald-500/40 bg-slate-900 p-7">
        <p className="text-sm font-semibold uppercase text-emerald-400">Policy contract</p><div className="mt-3 flex flex-col gap-3 sm:flex-row sm:justify-between"><div><h1 className="text-3xl font-bold">{policy.policyNumber}</h1><p className="mt-2 text-slate-300">{policy.companyName} · {policy.applicantName}</p></div><div><p className="font-semibold text-emerald-300">Coverage {policy.coverageState}</p><p className="text-sm text-slate-400">Contractual status: {policy.contractualStatus}</p></div></div>
        <dl className="mt-7 grid gap-5 sm:grid-cols-2 lg:grid-cols-4"><div><dt className="text-sm text-slate-400">Effective</dt><dd>{new Date(policy.effectiveDateUtc).toLocaleDateString()}</dd></div><div><dt className="text-sm text-slate-400">Expires</dt><dd>{new Date(policy.expirationDateUtc).toLocaleDateString()}</dd></div><div><dt className="text-sm text-slate-400">Premium</dt><dd>{formatCurrency(policy.premium)}</dd></div><div><dt className="text-sm text-slate-400">Limit / retention</dt><dd>{formatCurrency(policy.requestedLimit)} / {formatCurrency(policy.retention)}</dd></div></dl>
        {canFileClaim && <Link to={`/claims/new?policyId=${policy.policyId}`} className="mt-7 inline-flex rounded-md bg-emerald-300 px-4 py-2 font-semibold text-slate-950">File claim</Link>}
      </section>
      <section className="mt-6 rounded-lg border border-slate-800 bg-slate-900 p-6"><h2 className="text-xl font-semibold">Source and quote history</h2><p className="mt-3 text-slate-300">The policy is the contract. Its submission and quote remain separate audit records.</p><div className="mt-4 flex flex-wrap gap-3"><Link to={`/submissions/${policy.submissionId}`} className="font-semibold text-emerald-300">Open source submission</Link><span className="text-slate-500">Quote {policy.quoteId} · {policy.quoteStatusAtBind} at bind · {policy.quoteRiskTierAtBind} risk</span></div></section>
    </>}
  </section></main>;
}
