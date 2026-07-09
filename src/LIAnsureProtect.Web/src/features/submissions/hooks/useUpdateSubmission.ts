import { useAuth0 } from "@auth0/auth0-react";
import { useMutation, useQueryClient } from "@tanstack/react-query";

import { updateSubmission } from "../api/updateSubmission";
import type { SubmissionDetailResponse, UpdateSubmissionRequest } from "../types";
import { submissionDetailQueryKey } from "./useSubmissionDetail";

type UpdateSubmissionVariables = {
  submissionId: string;
  request: UpdateSubmissionRequest;
};

export function useUpdateSubmission() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ submissionId, request }: UpdateSubmissionVariables) => {
      const accessToken = await getAccessTokenSilently();

      return updateSubmission(accessToken, submissionId, request);
    },
    onSuccess: (result) => {
      queryClient.setQueryData<SubmissionDetailResponse>(
        submissionDetailQueryKey(result.submissionId),
        result,
      );
      void queryClient.invalidateQueries({ queryKey: ["submissions"] });
    },
  });
}
