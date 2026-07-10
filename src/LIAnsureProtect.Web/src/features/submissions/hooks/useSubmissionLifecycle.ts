import { useAuth0 } from "@auth0/auth0-react";
import { useMutation, useQueryClient } from "@tanstack/react-query";

import { deleteDraftSubmission } from "../api/deleteDraftSubmission";
import { withdrawSubmission } from "../api/withdrawSubmission";

export function useDeleteDraftSubmission() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (submissionId: string) => deleteDraftSubmission(await getAccessTokenSilently(), submissionId),
    onSuccess: async () => queryClient.invalidateQueries({ queryKey: ["submissions"] }),
  });
}

export function useWithdrawSubmission() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (submissionId: string) => withdrawSubmission(await getAccessTokenSilently(), submissionId),
    onSuccess: async (_result, submissionId) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["submissions"] }),
        queryClient.invalidateQueries({ queryKey: ["submissions", submissionId] }),
      ]);
    },
  });
}
