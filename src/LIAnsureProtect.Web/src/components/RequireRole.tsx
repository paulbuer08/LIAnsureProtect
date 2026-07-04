import { useAuth0 } from "@auth0/auth0-react";
import type { ReactNode } from "react";
import { Link } from "react-router";

import { getUserRoles } from "../lib/userRoles";

type RequireRoleProps = {
  allowedRoles: string[];
  children: ReactNode;
};

/**
 * Role-based route guard (UX only — the API's authorization policies are the enforcement
 * point). Renders children only when the signed-in user holds one of the allowed roles.
 */
export function RequireRole({ allowedRoles, children }: RequireRoleProps) {
  const { user } = useAuth0();
  const roles = getUserRoles(user);
  const isAllowed = roles.some((role) => allowedRoles.includes(role));

  if (!isAllowed) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-slate-950 px-6 text-white">
        <section className="max-w-xl text-center">
          <p className="text-sm font-semibold uppercase tracking-wide text-red-400">
            Access restricted
          </p>

          <h1 className="mt-4 text-3xl font-bold tracking-tight">
            You do not have access to this page
          </h1>

          <p className="mt-4 text-slate-300">
            This area is limited to the {allowedRoles.join(", ")} role
            {allowedRoles.length > 1 ? "s" : ""}.
          </p>

          <Link
            to="/dashboard"
            className="mt-8 inline-flex rounded-lg bg-emerald-400 px-5 py-3 text-sm font-semibold text-slate-950 shadow-sm hover:bg-emerald-300"
          >
            Back to dashboard
          </Link>
        </section>
      </main>
    );
  }

  return children;
}
