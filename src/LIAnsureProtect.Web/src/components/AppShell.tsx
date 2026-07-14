import type { ReactNode } from "react";
import { useAuth0 } from "@auth0/auth0-react";
import { Link, NavLink } from "react-router";

import { useUnreadNotificationCount } from "../features/notifications/hooks/useNotifications";
import { useCurrentUser } from "../hooks/useCurrentUser";
import {
  getNotificationScopeLabel,
  hasAnyRole,
  roleGroups,
} from "../lib/roleAccess";

type AppShellProps = {
  children: ReactNode;
};

type NavItem = {
  label: string;
  compactLabel: string;
  to: string;
  allowedRoles?: readonly string[];
  hasBadge?: boolean;
};

const navItems: NavItem[] = [
  { label: "Dashboard", compactLabel: "D", to: "/dashboard" },
  {
    label: "Submissions",
    compactLabel: "S",
    to: "/submissions",
    allowedRoles: roleGroups.customerWork,
  },
  {
    label: "Evidence",
    compactLabel: "E",
    to: "/evidence-requests",
    allowedRoles: roleGroups.customerWork,
  },
  {
    label: "Policies",
    compactLabel: "P",
    to: "/policies",
    allowedRoles: roleGroups.policyWork,
  },
  {
    label: "Claims",
    compactLabel: "C",
    to: "/claims",
    allowedRoles: roleGroups.customerWork,
  },
  {
    label: "Underwriting",
    compactLabel: "U",
    to: "/underwriting/quote-referrals",
    allowedRoles: roleGroups.underwritingWork,
  },
  {
    label: "Claims queue",
    compactLabel: "Q",
    to: "/claims/adjudication",
    allowedRoles: roleGroups.claimsAdjudication,
  },
  {
    label: "Notifications",
    compactLabel: "N",
    to: "/notifications",
    allowedRoles: roleGroups.notifications,
    hasBadge: true,
  },
];

function UnreadBadge({ unreadCount }: { unreadCount: number }) {
  if (unreadCount <= 0) {
    return null;
  }

  return (
    <span
      aria-label={`${unreadCount} unread notifications`}
      className="absolute -right-2 -top-2 inline-flex min-h-5 min-w-5 items-center justify-center rounded-full bg-amber-300 px-1.5 text-[11px] font-bold leading-none text-slate-950 ring-2 ring-slate-950"
    >
      {unreadCount > 99 ? "99+" : unreadCount}
    </span>
  );
}

function ShellNavLink({
  item,
  unreadCount,
}: {
  item: NavItem;
  unreadCount: number;
}) {
  return (
    <NavLink
      to={item.to}
      aria-label={item.label}
      className={({ isActive }) =>
        `relative inline-flex h-10 items-center justify-center rounded-md border px-3 text-sm font-semibold transition ${
          isActive
            ? "border-emerald-300 bg-emerald-300 text-slate-950"
            : "border-slate-700 bg-slate-900 text-slate-200 hover:border-slate-500 hover:text-white"
        }`
      }
    >
      <span className="sm:hidden" aria-hidden="true">
        {item.compactLabel}
      </span>
      <span className="hidden sm:inline">{item.label}</span>
      {item.hasBadge && <UnreadBadge unreadCount={unreadCount} />}
    </NavLink>
  );
}

export function AppShell({ children }: AppShellProps) {
  const { logout, user } = useAuth0();
  const currentUserQuery = useCurrentUser();

  const roles = currentUserQuery.data?.roles ?? [];
  const canReadNotifications = hasAnyRole(roles, roleGroups.notifications);
  const notificationsQuery = useUnreadNotificationCount({
    enabled: currentUserQuery.isSuccess && canReadNotifications,
  });
  const visibleNavItems = navItems.filter(
    (item) => !item.allowedRoles || hasAnyRole(roles, item.allowedRoles),
  );
  const unreadCount = notificationsQuery.data?.unreadCount ?? 0;
  const headerContext = currentUserQuery.isPending
    ? "Loading access"
    : currentUserQuery.isError
      ? "Roles unavailable"
      : getNotificationScopeLabel(roles);

  return (
    <div className="min-h-screen bg-slate-950 text-white">
      <header className="sticky top-0 z-20 border-b border-slate-800 bg-slate-950/95 px-4 py-3 backdrop-blur sm:px-6">
        <div className="mx-auto flex max-w-7xl flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <div className="flex items-center justify-between gap-4">
            <Link
              to="/dashboard"
              className="text-base font-bold tracking-tight text-white"
            >
              LIAnsureProtect
            </Link>
            <p className="hidden text-xs text-slate-400 md:block">
              {headerContext}
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <nav
              aria-label="Primary"
              className="flex min-w-0 flex-1 flex-wrap items-center gap-2"
            >
              {visibleNavItems.map((item) => (
                <ShellNavLink
                  key={item.to}
                  item={item}
                  unreadCount={unreadCount}
                />
              ))}
            </nav>

            <div className="ml-auto flex items-center gap-2">
              <span className="hidden max-w-48 truncate text-xs text-slate-400 xl:inline">
                {user?.email ?? currentUserQuery.data?.email ?? "Signed in"}
              </span>
              <button
                type="button"
                onClick={() =>
                  logout({
                    logoutParams: {
                      returnTo: window.location.origin,
                    },
                  })
                }
                className="inline-flex h-10 items-center rounded-md border border-slate-700 px-3 text-sm font-semibold text-slate-200 hover:border-slate-500"
              >
                Log out
              </button>
            </div>
          </div>
        </div>
      </header>

      {children}
    </div>
  );
}
