const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

/**
 * The caller's identity and roles as reported by the API's `GET /api/v1/me`. Roles come from the
 * same `ICurrentUser` source the authorization policies use, so the UI and the API can never
 * disagree — and the SPA never parses a token to discover roles.
 */
export type CurrentUser = {
  userId: string;
  email: string | null;
  roles: string[];
};

export async function fetchCurrentUser(accessToken: string): Promise<CurrentUser> {
  const response = await fetch(`${apiBaseUrl}/api/v1/me`, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
  });

  if (!response.ok) {
    throw new Error(`Current-user lookup failed with ${response.status}.`);
  }

  return (await response.json()) as CurrentUser;
}
