import { useAuth0 } from "@auth0/auth0-react";
import { Link } from "react-router";

import { useNotifications } from "../features/notifications/hooks/useNotifications";
import { useCurrentUser } from "../hooks/useCurrentUser";
import { auth0Config } from "../lib/auth0Config";
import {
  getNotificationScopeLabel,
  hasAnyRole,
  roleGroups,
} from "../lib/roleAccess";

type DashboardAction = {
  label: string;
  to: string;
  variant?: "primary" | "secondary";
};

type DashboardSection = {
  title: string;
  eyebrow: string;
  description: string;
  allowedRoles: readonly string[];
  actions: DashboardAction[];
  metric: string;
  status: string;
};

const dashboardSections: DashboardSection[] = [
  {
    title: "Policies",
    eyebrow: "Customer workspace",
    description: "Review active, scheduled, and expired coverage as contracts separate from submission history.",
    allowedRoles: roleGroups.policyWork,
    actions: [{ label: "View policies", to: "/policies", variant: "primary" }],
    metric: "Coverage contracts",
    status: "Owner-scoped",
  },
  {
    title: "Submission intake",
    eyebrow: "Customer workspace",
    description:
      "Create cyber liability submissions, continue drafts, and move complete risks toward quote generation.",
    allowedRoles: roleGroups.customerWork,
    actions: [
      { label: "View submissions", to: "/submissions", variant: "primary" },
      { label: "Create submission", to: "/submissions/new" },
    ],
    metric: "Drafts and quotes",
    status: "Owner-scoped",
  },
  {
    title: "Evidence requests",
    eyebrow: "Customer workspace",
    description:
      "Respond to underwriting evidence requests and upload safe supporting document metadata.",
    allowedRoles: roleGroups.customerWork,
    actions: [
      {
        label: "View evidence requests",
        to: "/evidence-requests",
        variant: "primary",
      },
    ],
    metric: "Open requests",
    status: "Action required",
  },
  {
    title: "Claims",
    eyebrow: "Customer workspace",
    description:
      "File a claim against a bound policy, track progress, answer adjuster questions, and review decisions.",
    allowedRoles: roleGroups.customerWork,
    actions: [
      { label: "View my claims", to: "/claims", variant: "primary" },
      { label: "File a claim", to: "/claims/new" },
    ],
    metric: "Claim files",
    status: "Policy-linked",
  },
  {
    title: "Underwriting workbench",
    eyebrow: "Staff workspace",
    description:
      "Triage referred quotes, inspect evidence, request advisory AI support, and record human decisions.",
    allowedRoles: roleGroups.underwritingWork,
    actions: [
      {
        label: "Open underwriting workbench",
        to: "/underwriting/quote-referrals",
        variant: "primary",
      },
    ],
    metric: "Referral queue",
    status: "Team-owned",
  },
  {
    title: "Claims adjudication",
    eyebrow: "Staff workspace",
    description:
      "Work the claims queue, claim files, request information, manage reserves, and close decisions.",
    allowedRoles: roleGroups.claimsAdjudication,
    actions: [
      {
        label: "Open claims workbench",
        to: "/claims/adjudication",
        variant: "primary",
      },
    ],
    metric: "Adjuster queue",
    status: "Role-restricted",
  },
];

function actionClassName(variant: DashboardAction["variant"]) {
  return variant === "primary"
    ? "inline-flex min-h-10 items-center rounded-md bg-emerald-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-emerald-200"
    : "inline-flex min-h-10 items-center rounded-md border border-slate-700 px-4 py-2 text-sm font-semibold text-slate-200 hover:border-slate-500 hover:text-white";
}

function UnreadNotificationBadge({ unreadCount }: { unreadCount: number }) {
  if (unreadCount <= 0) {
    return null;
  }

  return (
    <span
      aria-label={`${unreadCount} unread notifications`}
      className="absolute -right-2 -top-2 inline-flex min-h-6 min-w-6 items-center justify-center rounded-full bg-amber-300 px-1.5 text-xs font-bold text-slate-950 ring-2 ring-slate-900"
    >
      {unreadCount > 99 ? "99+" : unreadCount}
    </span>
  );
}

function getAuth0InteractionError(error: unknown) {
  if (!error || typeof error !== "object") {
    return undefined;
  }

  const auth0Error = error as { error?: unknown; error_description?: unknown };
  return typeof auth0Error.error === "string" ? auth0Error.error : undefined;
}

function needsInteractiveAuth0Authorization(error: unknown) {
  return [
    "consent_required",
    "interaction_required",
    "login_required",
  ].includes(getAuth0InteractionError(error) ?? "");
}

export function DashboardPage() {
  const { loginWithRedirect, user } = useAuth0();
  const currentUserQuery = useCurrentUser();

  const roles = currentUserQuery.data?.roles ?? [];
  const canReadNotifications = hasAnyRole(roles, roleGroups.notifications);
  const notificationsQuery = useNotifications({
    enabled: currentUserQuery.isSuccess && canReadNotifications,
  });
  const visibleSections = dashboardSections.filter((section) =>
    hasAnyRole(roles, section.allowedRoles),
  );
  const unreadCount = notificationsQuery.data?.unreadCount ?? 0;
  const notificationScope = getNotificationScopeLabel(roles);
  const roleSummary = currentUserQuery.isPending
    ? "Loading roles"
    : currentUserQuery.isError
      ? "Roles unavailable"
    : roles.length > 0
      ? roles.join(", ")
      : "No roles assigned";
  const roleLookupError =
    currentUserQuery.error instanceof Error
      ? currentUserQuery.error.message
      : "The API could not load your roles.";
  const roleLookupNeedsAuth0Consent = needsInteractiveAuth0Authorization(
    currentUserQuery.error,
  );

  async function handleAuthorizeApiAccess() {
    await loginWithRedirect({
      appState: {
        returnTo: window.location.pathname,
      },
      authorizationParams: {
        audience: auth0Config.audience,
        redirect_uri: auth0Config.callbackUrl,
        prompt: "consent",
      },
    });
  }

  return (
    <main className="bg-slate-950 px-4 py-8 text-white sm:px-6 lg:py-10">
      <section className="mx-auto max-w-7xl">
        <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_360px] lg:items-start">
          <section className="rounded-lg border border-slate-800 bg-slate-900 p-6">
            <p className="text-sm font-semibold uppercase text-emerald-300">
              Dashboard
            </p>
            <div className="mt-4 flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
              <div>
                <h1 className="text-3xl font-bold tracking-tight sm:text-4xl">
                  Workbench for your assigned role
                </h1>
                <p className="mt-3 max-w-3xl text-sm leading-6 text-slate-300 sm:text-base">
                  Signed in as {user?.email ?? "the current user"}. The actions
                  below are filtered by the same roles the API uses, so
                  restricted workspaces do not appear for users who cannot use
                  them.
                </p>
              </div>
              <div className="rounded-md border border-slate-700 bg-slate-950 px-4 py-3 text-sm">
                <p className="text-slate-400">Active roles</p>
                <p className="mt-1 font-semibold text-white">{roleSummary}</p>
              </div>
            </div>
          </section>

          <section className="rounded-lg border border-slate-800 bg-slate-900 p-6">
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-sm font-semibold uppercase text-sky-300">
                  Notifications
                </p>
                <h2 className="mt-2 text-xl font-semibold">
                  {notificationScope}
                </h2>
              </div>
              <Link
                to="/notifications"
                aria-label="View notifications"
                className="relative inline-flex h-11 min-w-11 items-center justify-center rounded-md border border-slate-700 px-3 text-sm font-semibold text-slate-100 hover:border-slate-500"
              >
                <span className="sm:hidden" aria-hidden="true">
                  N
                </span>
                <span className="hidden sm:inline">View notifications</span>
                <UnreadNotificationBadge unreadCount={unreadCount} />
              </Link>
            </div>
            <p className="mt-4 text-sm leading-6 text-slate-300">
              Customers and brokers only see personal messages. Underwriters,
              claims adjusters, and admins can also see team inbox entries when
              their queue needs attention.
            </p>
          </section>
        </div>

        {currentUserQuery.isError ? (
          <section className="mt-6 rounded-lg border border-red-500/50 bg-red-950/30 p-6">
            <h2 className="text-lg font-semibold text-white">
              We could not load your assigned roles.
            </h2>
            <p className="mt-2 text-sm leading-6 text-red-100">
              The dashboard cannot show Customer, Underwriter, Adjuster, or
              Admin workspaces until the API returns your roles from{" "}
              <code className="rounded bg-red-950 px-1.5 py-0.5 text-red-100">
                GET /api/v1/me
              </code>
              . Check that the API is running with the same Auth0 authority,
              audience, and role-claim type as the frontend.
            </p>
            <p className="mt-3 break-all rounded-md border border-red-500/30 bg-red-950 p-3 text-xs text-red-100">
              {roleLookupError}
            </p>
            {roleLookupNeedsAuth0Consent && (
              <div className="mt-4">
                <p className="text-sm leading-6 text-red-100">
                  Auth0 needs one interactive authorization step before it can
                  issue an access token for the LIAnsureProtect API.
                </p>
                <button
                  type="button"
                  onClick={handleAuthorizeApiAccess}
                  className="mt-3 inline-flex min-h-10 items-center rounded-md bg-red-100 px-4 py-2 text-sm font-semibold text-red-950 hover:bg-white"
                >
                  Continue with Auth0
                </button>
              </div>
            )}
          </section>
        ) : visibleSections.length === 0 ? (
          <section className="mt-6 rounded-lg border border-amber-400/40 bg-amber-950/20 p-6">
            <h2 className="text-lg font-semibold text-white">
              No application workspace is available yet.
            </h2>
            <p className="mt-2 text-sm leading-6 text-amber-100">
              Your account is authenticated, but it has no product role. Ask an
              administrator to assign the correct Auth0 role before using the
              protected workflows.
            </p>
          </section>
        ) : (
          <section className="mt-6 grid gap-4 lg:grid-cols-2">
            {visibleSections.map((section) => (
              <article
                key={section.title}
                className="rounded-lg border border-slate-800 bg-slate-900 p-5"
              >
                <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                  <div>
                    <p className="text-xs font-semibold uppercase text-slate-400">
                      {section.eyebrow}
                    </p>
                    <h2 className="mt-2 text-xl font-semibold text-white">
                      {section.title}
                    </h2>
                  </div>
                  <div className="rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-xs text-slate-300">
                    <p className="font-semibold text-white">{section.metric}</p>
                    <p className="mt-1">{section.status}</p>
                  </div>
                </div>
                <p className="mt-4 text-sm leading-6 text-slate-300">
                  {section.description}
                </p>
                <div className="mt-5 flex flex-wrap gap-3">
                  {section.actions.map((action) => (
                    <Link
                      key={action.to}
                      to={action.to}
                      className={actionClassName(action.variant)}
                    >
                      {action.label}
                    </Link>
                  ))}
                </div>
              </article>
            ))}
          </section>
        )}

      </section>
    </main>
  );
}
