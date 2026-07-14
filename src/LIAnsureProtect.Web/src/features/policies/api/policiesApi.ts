import type { ListPoliciesResponse, Policy, PolicyFilters } from "../types";
import { parseJsonResponse } from "../../../lib/apiClient";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";
const policiesPath = `${apiBaseUrl}/api/v1/policies`;

export async function listPolicies(accessToken: string, filters: PolicyFilters = {}) {
  const parameters = new URLSearchParams();
  Object.entries(filters).forEach(([key, value]) => { if (value) parameters.set(key, value); });
  const query = parameters.size ? `?${parameters}` : "";
  return parseJsonResponse<ListPoliciesResponse>(await fetch(`${policiesPath}${query}`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  }), { notFoundMessage: "Policies were not found." });
}

export async function getPolicy(accessToken: string, policyId: string) {
  return parseJsonResponse<Policy>(await fetch(`${policiesPath}/${policyId}`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  }), { notFoundMessage: "Policy was not found." });
}
