import { useAuth0 } from "@auth0/auth0-react";
import { useQuery } from "@tanstack/react-query";

import { getPolicy, listPolicies } from "../api/policiesApi";
import type { PolicyFilters } from "../types";

export function usePolicies(filters: PolicyFilters = {}) {
  const { getAccessTokenSilently } = useAuth0();
  return useQuery({
    queryKey: ["policies", filters],
    queryFn: async () => listPolicies(await getAccessTokenSilently(), filters),
  });
}

export function usePolicy(policyId: string | undefined) {
  const { getAccessTokenSilently } = useAuth0();
  return useQuery({
    enabled: Boolean(policyId),
    queryKey: ["policies", policyId],
    queryFn: async () => {
      if (!policyId) throw new Error("Policy ID is required.");
      return getPolicy(await getAccessTokenSilently(), policyId);
    },
  });
}
