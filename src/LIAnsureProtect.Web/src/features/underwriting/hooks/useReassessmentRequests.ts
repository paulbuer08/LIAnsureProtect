import { useAuth0 } from "@auth0/auth0-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { listReassessmentRequests, reviewReassessmentRequest } from "../api/underwritingApi";
import { quoteReferralsQueryKey } from "./useQuoteReferrals";

export const reassessmentRequestsQueryKey = ["underwriting", "reassessment-requests"] as const;

export function useReassessmentRequests(status = "Pending") {
  const { getAccessTokenSilently } = useAuth0();
  return useQuery({
    queryKey: [...reassessmentRequestsQueryKey, status],
    queryFn: async () => listReassessmentRequests(await getAccessTokenSilently(), status),
  });
}

export function useReviewReassessmentRequest() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ reassessmentRequestId, decision, reason }: { reassessmentRequestId: string; decision: "approve" | "decline"; reason: string }) =>
      reviewReassessmentRequest(await getAccessTokenSilently(), reassessmentRequestId, decision, reason),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: reassessmentRequestsQueryKey });
      void queryClient.invalidateQueries({ queryKey: quoteReferralsQueryKey });
    },
  });
}
