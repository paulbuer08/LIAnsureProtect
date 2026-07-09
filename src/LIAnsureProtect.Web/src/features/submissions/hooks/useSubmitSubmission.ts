import { useAuth0 } from "@auth0/auth0-react";
import { useMutation, useQueryClient } from "@tanstack/react-query";

import { submitSubmission } from "../api/submitSubmission";
import type { SubmissionDetailResponse } from "../types";
import { submissionDetailQueryKey } from "./useSubmissionDetail";

export function useSubmitSubmission() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (submissionId: string) => {
      const accessToken = await getAccessTokenSilently();

      return submitSubmission(accessToken, submissionId);
    },
    onSuccess: (result, submissionId) => {
      queryClient.setQueryData<SubmissionDetailResponse>(
        submissionDetailQueryKey(submissionId),
        (current) =>
          current
            ? {
                ...current,
                status: result.status,
              }
            : current,
      );
      void queryClient.invalidateQueries({
        queryKey: submissionDetailQueryKey(submissionId),
      });
      void queryClient.invalidateQueries({ queryKey: ["submissions"] });
    },
  });
}
