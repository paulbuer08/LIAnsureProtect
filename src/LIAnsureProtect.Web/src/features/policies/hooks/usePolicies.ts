import { useAuth0 } from "@auth0/auth0-react";
import { useQuery } from "@tanstack/react-query";

import { getPolicy, listPolicies } from "../api/policiesApi";

export function usePolicies() {
  const { getAccessTokenSilently } = useAuth0();
  return useQuery({
    queryKey: ["policies"],
    queryFn: async () => listPolicies(await getAccessTokenSilently()),
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
