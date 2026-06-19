import { useAuth0 } from "@auth0/auth0-react";
import { useQuery } from "@tanstack/react-query";

import { getSubmissionDetail } from "../api/getSubmissionDetail";

export function useSubmissionDetail(submissionId: string | undefined) {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    enabled: Boolean(submissionId),
    queryKey: ["submissions", submissionId],
    queryFn: async () => {
      if (!submissionId) {
        throw new Error("Submission ID is required.");
      }

      const accessToken = await getAccessTokenSilently();

      return getSubmissionDetail(accessToken, submissionId);
    },
  });
}
