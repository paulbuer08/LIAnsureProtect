import type { User } from "@auth0/auth0-react";

/**
 * The namespaced role claim the identity provider puts on tokens — the same claim name the API
 * validates (Authentication:RoleClaimType). Frontend role checks are UX only; the API's
 * authorization policies remain the enforcement point.
 */
export const rolesClaim = "https://liansureprotect.local/roles";

export function getUserRoles(user: User | undefined): string[] {
  const claimValue = user?.[rolesClaim];

  if (Array.isArray(claimValue)) {
    return claimValue.filter((role): role is string => typeof role === "string");
  }

  if (typeof claimValue === "string") {
    return [claimValue];
  }

  return [];
}
