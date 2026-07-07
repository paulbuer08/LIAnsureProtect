import type { ReactNode } from "react";
import { Link } from "react-router";

import { useCurrentUser } from "../hooks/useCurrentUser";

type RequireRoleProps = {
  allowedRoles: string[];
  children: ReactNode;
};

function GuardMessage({
  heading,
  body,
}: {
  heading: string;
  body: string;
}) {
  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-950 px-6 text-white">
      <section className="max-w-xl text-center">
        <p className="text-sm font-semibold uppercase tracking-wide text-red-400">
          Access restricted
        </p>
        <h1 className="mt-4 text-3xl font-bold tracking-tight">{heading}</h1>
        <p className="mt-4 text-slate-300">{body}</p>
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

/**
 * Role-based route guard (UX only — the API's authorization policies are the real enforcement
 * point). Roles are read from the API's `GET /api/v1/me` via {@link useCurrentUser}, so the guard
 * always agrees with what the API will actually allow, and the SPA never parses a token.
 */
export function RequireRole({ allowedRoles, children }: RequireRoleProps) {
  const { data, isPending, isError } = useCurrentUser();

  if (isPending) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-slate-950 px-6 text-white">
        <p className="text-slate-300">Checking access...</p>
      </main>
    );
  }

  if (isError || !data) {
    return (
      <GuardMessage
        heading="We could not verify your access"
        body="Your roles could not be loaded. Refresh the page, and if the problem persists contact your administrator."
      />
    );
  }

  if (data.roles.length === 0) {
    return (
      <GuardMessage
        heading="No roles assigned to your account"
        body="Your account has no roles yet, so no protected areas are available. Contact your administrator to be granted the appropriate role."
      />
    );
  }

  const isAllowed = data.roles.some((role) => allowedRoles.includes(role));
  if (!isAllowed) {
    return (
      <GuardMessage
        heading="You do not have access to this page"
        body={`This area is limited to the ${allowedRoles.join(", ")} role${
          allowedRoles.length > 1 ? "s" : ""
        }.`}
      />
    );
  }

  return children;
}
