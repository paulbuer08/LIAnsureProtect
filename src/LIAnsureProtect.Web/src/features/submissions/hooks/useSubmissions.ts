import { useAuth0 } from "@auth0/auth0-react";
import { useQuery } from "@tanstack/react-query";

import { listSubmissions } from "../api/listSubmissions";

export function useSubmissions() {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    queryKey: ["submissions"],
    queryFn: async () => {
      const accessToken = await getAccessTokenSilently();

      return listSubmissions(accessToken);
    },
  });
}
