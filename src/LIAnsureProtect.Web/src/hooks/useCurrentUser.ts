import { useAuth0 } from "@auth0/auth0-react";
import { useQuery } from "@tanstack/react-query";

import { fetchCurrentUser } from "../lib/currentUserApi";
import { auth0Config } from "../lib/auth0Config";

export const currentUserQueryKey = ["current-user"];

/**
 * Loads the signed-in caller's identity and roles from the API once per session (roles rarely
 * change mid-session, so the result is cached with a long stale time). This is the single source
 * of truth the SPA uses for role-based UX — it replaces reading roles from the ID token.
 */
export function useCurrentUser() {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    queryKey: currentUserQueryKey,
    queryFn: async () =>
      fetchCurrentUser(
        await getAccessTokenSilently({
          authorizationParams: {
            audience: auth0Config.audience,
            redirect_uri: auth0Config.callbackUrl,
          },
        }),
      ),
    retry: false,
    staleTime: 5 * 60 * 1000,
  });
}
