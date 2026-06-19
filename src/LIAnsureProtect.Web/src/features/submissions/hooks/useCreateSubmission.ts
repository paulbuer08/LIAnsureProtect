import { useAuth0 } from "@auth0/auth0-react";
import { useMutation } from "@tanstack/react-query";

import { createSubmission } from "../api/createSubmission";
import type { CreateSubmissionRequest } from "../types";

export function useCreateSubmission() {
  const { getAccessTokenSilently } = useAuth0();

  return useMutation({
    mutationFn: async (request: CreateSubmissionRequest) => {
      const accessToken = await getAccessTokenSilently();

      return createSubmission(accessToken, request);
    },
  });
}
