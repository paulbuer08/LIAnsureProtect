import { useAuth0 } from "@auth0/auth0-react";
import { useMutation, useQueryClient } from "@tanstack/react-query";

import { createQuote } from "../api/createQuote";
import type { CreateQuoteRequest } from "../types";
import { quoteHistoryQueryKey } from "./useQuoteHistory";
import { submissionDetailQueryKey } from "./useSubmissionDetail";

export function useCreateQuote() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

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
    onSuccess: (_result, variables) => {
      void queryClient.invalidateQueries({
        queryKey: submissionDetailQueryKey(variables.submissionId),
      });
      void queryClient.invalidateQueries({
        queryKey: quoteHistoryQueryKey(variables.submissionId),
      });
    },
  });
}
