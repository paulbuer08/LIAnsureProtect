import { useAuth0 } from "@auth0/auth0-react";
import { useMutation } from "@tanstack/react-query";

import { bindPolicy } from "../api/bindPolicy";
import type { BindPolicyRequest } from "../types";

export function useBindPolicy() {
  const { getAccessTokenSilently } = useAuth0();

  return useMutation({
    mutationFn: async ({
      quoteId,
      request,
    }: {
      quoteId: string;
      request: BindPolicyRequest;
    }) => {
      const accessToken = await getAccessTokenSilently();

      return bindPolicy(accessToken, quoteId, request);
    },
  });
}
