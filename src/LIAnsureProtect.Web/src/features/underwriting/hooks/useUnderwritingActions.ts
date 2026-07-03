import { useAuth0 } from "@auth0/auth0-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  addQuoteReferralNote,
  addQuoteReferralTask,
  adjustQuoteReferral,
  assignQuoteReferralToMe,
  approveQuoteReferral,
  acceptQuoteEvidenceRequest,
  cancelQuoteEvidenceRequest,
  completeQuoteReferralTask,
  createQuoteEvidenceRequest,
  declineQuoteReferral,
  followUpQuoteEvidenceRequest,
  generateAiUnderwritingReview,
  listQuoteReferralTimeline,
  recordQuoteEvidenceReviewDecision,
  releaseQuoteReferralAssignment,
  triageQuoteReferralOperation,
} from "../api/underwritingApi";
import type {
  AdjustQuoteReferralRequest,
  CreateQuoteEvidenceRequest,
  QuoteReferralNoteRequest,
  QuoteReferralReviewRequest,
  QuoteReferralTaskRequest,
  QuoteReferralTriageRequest,
  RecordQuoteEvidenceReviewDecisionRequest,
  ReviewQuoteEvidenceRequest,
} from "../types";
import { quoteReferralsQueryKey } from "./useQuoteReferrals";

export const quoteReferralTimelineQueryKey = (quoteId?: string) => [
  "underwriting",
  "quote-referrals",
  quoteId,
  "timeline",
];

export function useQuoteReferralTimeline(quoteId?: string) {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    enabled: quoteId !== undefined,
    queryKey: quoteReferralTimelineQueryKey(quoteId),
    queryFn: async () => {
      const accessToken = await getAccessTokenSilently();

      return listQuoteReferralTimeline(accessToken, quoteId ?? "");
    },
  });
}

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

export function useAssignQuoteReferralToMe() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (quoteId: string) => {
      const accessToken = await getAccessTokenSilently();

      return assignQuoteReferralToMe(accessToken, quoteId);
    },
    onSuccess: (_result, quoteId) => invalidateOperationsQueries(queryClient, quoteId),
    // A failed claim usually means another underwriter just won the race (409): refetch so the
    // loser immediately sees the current assignee instead of a stale "unassigned" row.
    onError: (_error, quoteId) => invalidateOperationsQueries(queryClient, quoteId),
  });
}

export function useReleaseQuoteReferralAssignment() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (quoteId: string) => {
      const accessToken = await getAccessTokenSilently();

      return releaseQuoteReferralAssignment(accessToken, quoteId);
    },
    onSuccess: (_result, quoteId) => invalidateOperationsQueries(queryClient, quoteId),
    // Concurrency conflicts (409) mean the row changed under us — refetch the truth.
    onError: (_error, quoteId) => invalidateOperationsQueries(queryClient, quoteId),
  });
}

export function useTriageQuoteReferralOperation() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      quoteId,
      request,
    }: {
      quoteId: string;
      request: QuoteReferralTriageRequest;
    }) => {
      const accessToken = await getAccessTokenSilently();

      return triageQuoteReferralOperation(accessToken, quoteId, request);
    },
    onSuccess: (_result, variables) =>
      invalidateOperationsQueries(queryClient, variables.quoteId),
    // Concurrency conflicts (409) mean the row changed under us — refetch the truth.
    onError: (_error, variables) =>
      invalidateOperationsQueries(queryClient, variables.quoteId),
  });
}

export function useAddQuoteReferralNote() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      quoteId,
      request,
    }: {
      quoteId: string;
      request: QuoteReferralNoteRequest;
    }) => {
      const accessToken = await getAccessTokenSilently();

      return addQuoteReferralNote(accessToken, quoteId, request);
    },
    onSuccess: (_result, variables) =>
      invalidateOperationsQueries(queryClient, variables.quoteId),
  });
}

export function useAddQuoteReferralTask() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      quoteId,
      request,
    }: {
      quoteId: string;
      request: QuoteReferralTaskRequest;
    }) => {
      const accessToken = await getAccessTokenSilently();

      return addQuoteReferralTask(accessToken, quoteId, request);
    },
    onSuccess: (_result, variables) =>
      invalidateOperationsQueries(queryClient, variables.quoteId),
  });
}

export function useCompleteQuoteReferralTask() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      quoteId,
      taskId,
    }: {
      quoteId: string;
      taskId: string;
    }) => {
      const accessToken = await getAccessTokenSilently();

      return completeQuoteReferralTask(accessToken, quoteId, taskId);
    },
    onSuccess: (_result, variables) =>
      invalidateOperationsQueries(queryClient, variables.quoteId),
  });
}

export function useCreateQuoteEvidenceRequest() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      quoteId,
      request,
    }: {
      quoteId: string;
      request: CreateQuoteEvidenceRequest;
    }) => {
      const accessToken = await getAccessTokenSilently();

      return createQuoteEvidenceRequest(accessToken, quoteId, request);
    },
    onSuccess: (_result, variables) =>
      invalidateOperationsQueries(queryClient, variables.quoteId),
  });
}

export function useAcceptQuoteEvidenceRequest() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      quoteId,
      evidenceRequestId,
      request,
    }: {
      quoteId: string;
      evidenceRequestId: string;
      request: ReviewQuoteEvidenceRequest;
    }) => {
      const accessToken = await getAccessTokenSilently();

      return acceptQuoteEvidenceRequest(
        accessToken,
        quoteId,
        evidenceRequestId,
        request,
      );
    },
    onSuccess: (_result, variables) =>
      invalidateOperationsQueries(queryClient, variables.quoteId),
  });
}

export function useCancelQuoteEvidenceRequest() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      quoteId,
      evidenceRequestId,
      request,
    }: {
      quoteId: string;
      evidenceRequestId: string;
      request: ReviewQuoteEvidenceRequest;
    }) => {
      const accessToken = await getAccessTokenSilently();

      return cancelQuoteEvidenceRequest(
        accessToken,
        quoteId,
        evidenceRequestId,
        request,
      );
    },
    onSuccess: (_result, variables) =>
      invalidateOperationsQueries(queryClient, variables.quoteId),
  });
}

export function useFollowUpQuoteEvidenceRequest() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      quoteId,
      evidenceRequestId,
    }: {
      quoteId: string;
      evidenceRequestId: string;
    }) => {
      const accessToken = await getAccessTokenSilently();

      return followUpQuoteEvidenceRequest(
        accessToken,
        quoteId,
        evidenceRequestId,
      );
    },
    onSuccess: (_result, variables) =>
      invalidateOperationsQueries(queryClient, variables.quoteId),
  });
}

export function useRecordQuoteEvidenceReviewDecision() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      quoteId,
      evidenceRequestId,
      request,
    }: {
      quoteId: string;
      evidenceRequestId: string;
      request: RecordQuoteEvidenceReviewDecisionRequest;
    }) => {
      const accessToken = await getAccessTokenSilently();

      return recordQuoteEvidenceReviewDecision(
        accessToken,
        quoteId,
        evidenceRequestId,
        request,
      );
    },
    onSuccess: (_result, variables) =>
      invalidateOperationsQueries(queryClient, variables.quoteId),
  });
}

function invalidateOperationsQueries(
  queryClient: ReturnType<typeof useQueryClient>,
  quoteId: string,
) {
  void queryClient.invalidateQueries({ queryKey: quoteReferralsQueryKey });
  void queryClient.invalidateQueries({
    queryKey: quoteReferralTimelineQueryKey(quoteId),
  });
}
