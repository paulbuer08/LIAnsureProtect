import { useAuth0 } from "@auth0/auth0-react";
import { useQuery } from "@tanstack/react-query";

import { listUnderwritingEvidenceQueue } from "../api/underwritingApi";
import type { UnderwritingEvidenceQueueFilters } from "../types";

export const evidenceQueueQueryKey = ["underwriting", "evidence-requests"];

export function useEvidenceQueue(filters: UnderwritingEvidenceQueueFilters = {}) {
  const { getAccessTokenSilently } = useAuth0();
  return useQuery({
    queryKey: [...evidenceQueueQueryKey, filters],
    queryFn: async () => {
      const accessToken = await getAccessTokenSilently();
      return listUnderwritingEvidenceQueue(accessToken, filters);
    },
  });
}
