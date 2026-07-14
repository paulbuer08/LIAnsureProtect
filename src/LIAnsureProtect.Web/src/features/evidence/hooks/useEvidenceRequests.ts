import { useAuth0 } from "@auth0/auth0-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  listEvidenceRequests,
  getEvidenceRequest,
  respondToEvidenceRequest,
  uploadReplacementEvidenceDocuments,
} from "../api/evidenceRequestsApi";
import type { EvidenceRequestFilters, RespondToEvidenceRequest } from "../types";

export const evidenceRequestsQueryKey = ["evidence-requests"];

export function useEvidenceRequests(filters: EvidenceRequestFilters = {}) {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    queryKey: [...evidenceRequestsQueryKey, filters],
    queryFn: async () => {
      const accessToken = await getAccessTokenSilently();

      return listEvidenceRequests(accessToken, filters);
    },
  });
}

export function useEvidenceRequest(evidenceRequestId?: string) {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    queryKey: [...evidenceRequestsQueryKey, "detail", evidenceRequestId],
    enabled: Boolean(evidenceRequestId),
    queryFn: async () => {
      const accessToken = await getAccessTokenSilently();
      return getEvidenceRequest(accessToken, evidenceRequestId!);
    },
  });
}

export function useRespondToEvidenceRequest() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      evidenceRequestId,
      request,
    }: {
      evidenceRequestId: string;
      request: RespondToEvidenceRequest;
    }) => {
      const accessToken = await getAccessTokenSilently();

      return respondToEvidenceRequest(accessToken, evidenceRequestId, request);
    },
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: evidenceRequestsQueryKey }),
  });
}

export function useUploadReplacementEvidenceDocuments() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      evidenceRequestId,
      attachments,
    }: {
      evidenceRequestId: string;
      attachments: File[];
    }) => {
      const accessToken = await getAccessTokenSilently();

      return uploadReplacementEvidenceDocuments(
        accessToken,
        evidenceRequestId,
        attachments,
      );
    },
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: evidenceRequestsQueryKey }),
  });
}
