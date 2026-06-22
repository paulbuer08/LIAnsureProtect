import { useAuth0 } from "@auth0/auth0-react";
import { useMutation, useQueryClient } from "@tanstack/react-query";

import {
  adjustQuoteReferral,
  approveQuoteReferral,
  declineQuoteReferral,
  generateAiUnderwritingReview,
} from "../api/underwritingApi";
import type {
  AdjustQuoteReferralRequest,
  QuoteReferralReviewRequest,
} from "../types";
import { quoteReferralsQueryKey } from "./useQuoteReferrals";

export function useGenerateAiUnderwritingReview() {
  const { getAccessTokenSilently } = useAuth0();

  return useMutation({
    mutationFn: async (quoteId: string) => {
      const accessToken = await getAccessTokenSilently();

      return generateAiUnderwritingReview(accessToken, quoteId);
    },
  });
}

export function useApproveQuoteReferral() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      quoteId,
      request,
    }: {
      quoteId: string;
      request: QuoteReferralReviewRequest;
    }) => {
      const accessToken = await getAccessTokenSilently();

      return approveQuoteReferral(accessToken, quoteId, request);
    },
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: quoteReferralsQueryKey }),
  });
}

export function useDeclineQuoteReferral() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      quoteId,
      request,
    }: {
      quoteId: string;
      request: QuoteReferralReviewRequest;
    }) => {
      const accessToken = await getAccessTokenSilently();

      return declineQuoteReferral(accessToken, quoteId, request);
    },
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: quoteReferralsQueryKey }),
  });
}

export function useAdjustQuoteReferral() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      quoteId,
      request,
    }: {
      quoteId: string;
      request: AdjustQuoteReferralRequest;
    }) => {
      const accessToken = await getAccessTokenSilently();

      return adjustQuoteReferral(accessToken, quoteId, request);
    },
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: quoteReferralsQueryKey }),
  });
}
