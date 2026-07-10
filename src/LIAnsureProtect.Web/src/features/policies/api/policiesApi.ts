import type { ListPoliciesResponse, Policy } from "../types";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";
const policiesPath = `${apiBaseUrl}/api/v1/policies`;

async function parseResponse<T>(response: Response) {
  if (response.status === 404) throw new Error("Policy was not found.");
  if (!response.ok) throw new Error((await response.text()) || "Unable to load policies.");
  return (await response.json()) as T;
}

export async function listPolicies(accessToken: string) {
  return parseResponse<ListPoliciesResponse>(await fetch(policiesPath, {
    headers: { Authorization: `Bearer ${accessToken}` },
  }));
}

export async function getPolicy(accessToken: string, policyId: string) {
  return parseResponse<Policy>(await fetch(`${policiesPath}/${policyId}`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  }));
}
