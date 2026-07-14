import { useState } from "react";
import { Link } from "react-router";

import { Breadcrumbs } from "../../../components/Breadcrumbs";
import { getUserErrorMessage } from "../../../lib/apiClient";
import { formatCurrency } from "../../../lib/currency";
import { useClaimablePolicies, useFileClaim } from "../hooks/useClaims";
import { claimIncidentTypes, type ClaimablePolicy } from "../types";

function getErrorMessage(error: unknown) {
  return getUserErrorMessage(error, "Unable to file the claim.");
}

export function NewClaimPage() {
  const policiesQuery = useClaimablePolicies();
  const fileClaimMutation = useFileClaim();
  const [selectedPolicy, setSelectedPolicy] = useState<ClaimablePolicy | null>(
    null,
  );
  const [incidentType, setIncidentType] = useState<string>(
    claimIncidentTypes[0],
  );
  const [incidentDate, setIncidentDate] = useState("");
  const [discoveryDate, setDiscoveryDate] = useState("");
  const [description, setDescription] = useState("");

  const policies = policiesQuery.data?.policies ?? [];
  const filedClaim = fileClaimMutation.data;

  function handleFileClaim(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedPolicy) {
      return;
    }

    fileClaimMutation.mutate({
      policyId: selectedPolicy.policyId,
      incidentType,
      incidentAtUtc: new Date(`${incidentDate}T00:00:00Z`).toISOString(),
      discoveredAtUtc: new Date(`${discoveryDate}T00:00:00Z`).toISOString(),
      description,
    });
  }

  if (filedClaim) {
    return (
      <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
        <section className="mx-auto max-w-3xl">
          <p className="text-sm font-semibold uppercase tracking-wide text-emerald-400">
            Claim filed
          </p>
          <h1 className="mt-4 text-4xl font-bold tracking-tight">
            {filedClaim.claimNumber}
          </h1>
          <p className="mt-4 text-slate-300">
            Your claim has been filed against policy {filedClaim.policyNumber}{" "}
            and is now waiting for a claims adjuster. You can follow its
            progress, answer questions, and upload supporting documents from
            the claim page.
          </p>
          <div className="mt-8 flex gap-4">
            <Link
              to={`/claims/${filedClaim.claimId}`}
              className="inline-flex rounded-lg bg-emerald-400 px-5 py-3 text-sm font-semibold text-slate-950 shadow-sm hover:bg-emerald-300"
            >
              Open the claim
            </Link>
            <Link
              to="/claims"
              className="inline-flex rounded-lg border border-slate-700 px-5 py-3 text-sm font-semibold text-slate-200 hover:border-emerald-400"
            >
              Back to my claims
            </Link>
          </div>
        </section>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
      <section className="mx-auto max-w-3xl">
        <Breadcrumbs items={[{ label: "Dashboard", to: "/dashboard" }, { label: "Claims", to: "/claims" }, { label: "File a claim" }]} />

        <p className="mt-8 text-sm font-semibold uppercase tracking-wide text-emerald-400">
          File a claim
        </p>
        <h1 className="mt-4 text-4xl font-bold tracking-tight">
          {selectedPolicy
            ? "Step 2 of 2 — Describe the incident"
            : "Step 1 of 2 — Choose the policy"}
        </h1>

        {!selectedPolicy && (
          <>
            <p className="mt-4 max-w-2xl text-slate-300">
              A claim is filed against one of your bound cyber policies. Pick
              the policy the incident falls under.
            </p>

            {policiesQuery.isPending && (
              <p className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-300">
                Loading your bound policies...
              </p>
            )}

            {policiesQuery.isError && (
              <p className="mt-8 rounded-lg border border-red-900 bg-red-950 p-4 text-sm text-red-200">
                {getErrorMessage(policiesQuery.error)}
              </p>
            )}

            {policiesQuery.isSuccess && policies.length === 0 && (
              <section className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-6">
                <h2 className="text-lg font-semibold text-white">
                  No bound policies to claim against.
                </h2>
                <p className="mt-2 text-sm text-slate-300">
                  Claims can only be filed once a quote has been accepted and
                  bound into a policy.
                </p>
              </section>
            )}

            {policies.length > 0 && (
              <div className="mt-8 grid grid-cols-1 gap-4">
                {policies.map((policy) => (
                  <button
                    type="button"
                    key={policy.policyId}
                    onClick={() => setSelectedPolicy(policy)}
                    aria-label={`Select policy ${policy.policyNumber}`}
                    className="rounded-lg border border-slate-800 bg-slate-900 p-5 text-left hover:border-emerald-400"
                  >
                    <h2 className="text-lg font-semibold text-white">
                      {policy.policyNumber}
                    </h2>
                    <p className="mt-1 text-sm text-slate-300">
                      Coverage{" "}
                      {new Date(policy.effectiveAtUtc).toLocaleDateString()} —{" "}
                      {new Date(policy.expirationAtUtc).toLocaleDateString()}
                    </p>
                    <p className="mt-1 text-sm text-slate-400">
                      Limit {formatCurrency(policy.limit)} · Retention{" "}
                      {formatCurrency(policy.retention)}
                    </p>
                  </button>
                ))}
              </div>
            )}
          </>
        )}

        {selectedPolicy && (
          <form className="mt-8" onSubmit={handleFileClaim}>
            <div className="rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-300">
              Filing against{" "}
              <span className="font-semibold text-white">
                {selectedPolicy.policyNumber}
              </span>{" "}
              <button
                type="button"
                onClick={() => setSelectedPolicy(null)}
                className="ml-3 font-semibold text-emerald-300 hover:text-emerald-200"
              >
                Change policy
              </button>
            </div>

            <label
              className="mt-6 block text-sm font-semibold text-slate-200"
              htmlFor="incident-type"
            >
              Incident type
            </label>
            <select
              id="incident-type"
              value={incidentType}
              onChange={(event) => setIncidentType(event.target.value)}
              className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400"
            >
              {claimIncidentTypes.map((type) => (
                <option key={type} value={type}>
                  {type}
                </option>
              ))}
            </select>

            <label
              className="mt-6 block text-sm font-semibold text-slate-200"
              htmlFor="incident-date"
            >
              Incident date
            </label>
            <input
              id="incident-date"
              type="date"
              required
              value={incidentDate}
              onChange={(event) => setIncidentDate(event.target.value)}
              className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400"
            />

            <label
              className="mt-6 block text-sm font-semibold text-slate-200"
              htmlFor="discovery-date"
            >
              Discovery date
            </label>
            <input
              id="discovery-date"
              type="date"
              required
              value={discoveryDate}
              onChange={(event) => setDiscoveryDate(event.target.value)}
              className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400"
            />

            <label
              className="mt-6 block text-sm font-semibold text-slate-200"
              htmlFor="description"
            >
              Description
            </label>
            <textarea
              id="description"
              required
              rows={5}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder="What happened, when it was discovered, and the impact so far."
              className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400"
            />

            {fileClaimMutation.isError && (
              <p className="mt-6 whitespace-pre-wrap rounded-lg border border-red-900 bg-red-950 p-4 text-sm text-red-200">
                {getErrorMessage(fileClaimMutation.error)}
              </p>
            )}

            <button
              type="submit"
              disabled={fileClaimMutation.isPending}
              className="mt-8 inline-flex rounded-lg bg-emerald-400 px-5 py-3 text-sm font-semibold text-slate-950 shadow-sm hover:bg-emerald-300 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {fileClaimMutation.isPending ? "Filing claim..." : "File claim"}
            </button>
          </form>
        )}
      </section>
    </main>
  );
}
