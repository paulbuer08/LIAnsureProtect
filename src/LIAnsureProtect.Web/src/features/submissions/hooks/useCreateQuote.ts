import { useAuth0 } from "@auth0/auth0-react";
import { useMutation } from "@tanstack/react-query";

import { createQuote } from "../api/createQuote";
import type { CreateQuoteRequest } from "../types";

export function useCreateQuote() {
  const { getAccessTokenSilently } = useAuth0();

  return useMutation({
    mutationFn: async ({
      submissionId,
      request,
    }: {
      submissionId: string;
      request: CreateQuoteRequest;
    }) => {
      const accessToken = await getAccessTokenSilently();

      return createQuote(accessToken, submissionId, request);
    },
  });
}
