import { useAuth0 } from "@auth0/auth0-react";
import { useMutation } from "@tanstack/react-query";

import { acceptQuote } from "../api/acceptQuote";
import type { AcceptQuoteRequest } from "../types";

export function useAcceptQuote() {
  const { getAccessTokenSilently } = useAuth0();

  return useMutation({
    mutationFn: async ({
      quoteId,
      request,
    }: {
      quoteId: string;
      request: AcceptQuoteRequest;
    }) => {
      const accessToken = await getAccessTokenSilently();

      return acceptQuote(accessToken, quoteId, request);
    },
  });
}
