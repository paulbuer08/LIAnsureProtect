import { useAuth0 } from "@auth0/auth0-react";
import { useQuery } from "@tanstack/react-query";

import { getQuoteDetail } from "../api/getQuoteDetail";

export function useQuoteDetail(submissionId?: string, quoteId?: string) {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    queryKey: ["submission-quote", submissionId, quoteId],
    enabled: Boolean(submissionId && quoteId),
    queryFn: async () => {
      const accessToken = await getAccessTokenSilently();
      return getQuoteDetail(accessToken, submissionId!, quoteId!);
    },
  });
}
