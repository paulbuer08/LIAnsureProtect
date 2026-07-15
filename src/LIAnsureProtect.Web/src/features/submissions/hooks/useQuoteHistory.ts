import { useAuth0 } from "@auth0/auth0-react";
import { useQuery } from "@tanstack/react-query";

import { listQuoteHistory } from "../api/listQuoteHistory";

export const quoteHistoryQueryKey = (submissionId?: string) => [
  "submissions",
  submissionId,
  "quote-history",
] as const;

export function useQuoteHistory(submissionId?: string) {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    enabled: Boolean(submissionId),
    queryKey: quoteHistoryQueryKey(submissionId),
    queryFn: async () => {
      const accessToken = await getAccessTokenSilently();
      return listQuoteHistory(accessToken, submissionId!);
    },
  });
}
