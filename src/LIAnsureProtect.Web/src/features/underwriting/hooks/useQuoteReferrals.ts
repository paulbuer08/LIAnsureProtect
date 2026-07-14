import { useAuth0 } from "@auth0/auth0-react";
import { useQuery } from "@tanstack/react-query";

import { listQuoteReferrals } from "../api/underwritingApi";
import type { QuoteReferralFilters } from "../types";

export const quoteReferralsQueryKey = ["underwriting", "quote-referrals"];

export function useQuoteReferrals(filters: QuoteReferralFilters = {}) {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    queryKey: [...quoteReferralsQueryKey, filters],
    queryFn: async () => {
      const accessToken = await getAccessTokenSilently();

      return listQuoteReferrals(accessToken, filters);
    },
  });
}
