import { useAuth0 } from "@auth0/auth0-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  listEvidenceRequests,
  respondToEvidenceRequest,
} from "../api/evidenceRequestsApi";
import type { RespondToEvidenceRequest } from "../types";

export const evidenceRequestsQueryKey = ["evidence-requests"];

export function useEvidenceRequests() {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    queryKey: evidenceRequestsQueryKey,
    queryFn: async () => {
      const accessToken = await getAccessTokenSilently();

      return listEvidenceRequests(accessToken);
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
