import type { ListPoliciesResponse, Policy } from "../types";
import { parseJsonResponse } from "../../../lib/apiClient";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";
const policiesPath = `${apiBaseUrl}/api/v1/policies`;

export async function listPolicies(accessToken: string) {
  return parseJsonResponse<ListPoliciesResponse>(await fetch(policiesPath, {
    headers: { Authorization: `Bearer ${accessToken}` },
  }), { notFoundMessage: "Policies were not found." });
}

export async function getPolicy(accessToken: string, policyId: string) {
  return parseJsonResponse<Policy>(await fetch(`${policiesPath}/${policyId}`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  }), { notFoundMessage: "Policy was not found." });
}
