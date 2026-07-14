import { useAuth0 } from "@auth0/auth0-react";
import { useQuery } from "@tanstack/react-query";

import { listSubmissions } from "../api/listSubmissions";
import type { SubmissionListFilters } from "../types";

export function useSubmissions(filters: SubmissionListFilters = {}) {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    queryKey: ["submissions", filters],
    queryFn: async () => {
      const accessToken = await getAccessTokenSilently();

      return listSubmissions(accessToken, filters);
    },
  });
}
