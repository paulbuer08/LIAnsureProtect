import { useState, type FormEvent } from "react";
import { Link } from "react-router";

import { Breadcrumbs } from "../../../components/Breadcrumbs";
import { useCurrentUser } from "../../../hooks/useCurrentUser";
import { hasTeamNotificationAccess } from "../../../lib/roleAccess";
import { formatCurrency } from "../../../lib/currency";
import { getUserErrorMessage } from "../../../lib/apiClient";
import { useNotifications } from "../hooks/useNotifications";
import type { NotificationInboxItem, NotificationScope } from "../types";
import { resolveNotificationAction } from "../notificationActionResolver";

type NotificationFilter = "all" | NotificationScope;

const filterTabs: { value: NotificationFilter; label: string }[] = [
  { value: "all", label: "All" },
  { value: "personal", label: "Personal" },
  { value: "team", label: "Team" },
];

function getErrorMessage(error: unknown) {
  return getUserErrorMessage(error, "Unable to load notifications.");
}

function NotificationDetails({ attributes }: { attributes: Record<string, string> }) {
  const details: string[] = [];
  if (attributes.category) details.push(`Control: ${attributes.category}`);
  if (attributes.quoteVersion) details.push(`Quote version ${attributes.quoteVersion}`);
  if (attributes.version) details.push(`Quote version ${attributes.version}`);
  if (attributes.premium && Number.isFinite(Number(attributes.premium))) {
    details.push(`Premium ${formatCurrency(Number(attributes.premium))}`);
  }
  if (attributes.dueAtUtc) {
    details.push(`Due ${new Date(attributes.dueAtUtc).toLocaleDateString()}`);
  }
  if (attributes.expiresAtUtc) {
    details.push(`Expires ${new Date(attributes.expiresAtUtc).toLocaleDateString()}`);
  }

  return details.length > 0 ? (
    <p className="mt-2 text-sm text-slate-300">{details.join(" · ")}</p>
  ) : null;
}

function groupNotifications(notifications: NotificationInboxItem[]) {
  const groups = new Map<
    string,
    { key: string; companyName: string; submissionReference: string; notifications: NotificationInboxItem[] }
  >();

  for (const notification of notifications) {
    const submissionReference =
      notification.attributes.submissionReference ??
      notification.attributes.submissionId ??
      "Other updates";
    const companyName = notification.attributes.companyName ?? "";
    const key = `${companyName}|${submissionReference}`;
    const group = groups.get(key) ?? {
      key,
      companyName,
      submissionReference,
      notifications: [],
    };
    group.notifications.push(notification);
    groups.set(key, group);
  }

  return Array.from(groups.values());
}

export function NotificationsPage() {
  const currentUserQuery = useCurrentUser();
  const [filter, setFilter] = useState<NotificationFilter>("all");
  const [search, setSearch] = useState("");
  const [appliedSearch, setAppliedSearch] = useState("");
  const [readState, setReadState] = useState("");
  const canFilterByTeam = currentUserQuery.data?.capabilities
    ? currentUserQuery.data.capabilities.includes("Notifications.ReadTeam")
    : hasTeamNotificationAccess(currentUserQuery.data?.roles);

  const notificationsQuery = useNotifications({
    filters: {
      search: appliedSearch || undefined,
      isUnread:
        readState === "unread" ? true : readState === "read" ? false : undefined,
      scope:
        filter === "all" ? undefined : filter,
    },
  });

  const notifications = notificationsQuery.data?.notifications ?? [];
  const unreadCount = notificationsQuery.data?.unreadCount ?? 0;
  const visibleNotifications = notifications.filter(
    (notification) => filter === "all" || notification.scope === filter,
  );
  const notificationGroups = groupNotifications(visibleNotifications);
  const hasActiveResultFilters = Boolean(appliedSearch || readState);
  const emptyHeading = hasActiveResultFilters
    ? "No notifications match these filters."
    : filter === "personal"
      ? "No personal notifications."
      : filter === "team"
        ? "No team notifications."
        : "No notifications yet.";
  const emptyDescription = hasActiveResultFilters
    ? "Change or clear the search and read-state filters to see other updates."
    : filter === "personal"
      ? "Personal updates addressed directly to you will appear here."
      : filter === "team"
        ? "Updates for your operational teams will appear here."
        : "Updates about your submissions, quotes, policies, and evidence requests will appear here.";

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
      <section className="mx-auto max-w-3xl">
        <Breadcrumbs items={[{ label: "Dashboard", to: "/dashboard" }, { label: "Notifications" }]} />

        <div className="mt-8">
          <p className="text-sm font-semibold uppercase tracking-wide text-emerald-400">
            Notifications
          </p>
          <h1 className="mt-4 text-4xl font-bold tracking-tight">
            Your notifications
          </h1>
          <p className="mt-4 text-slate-300">{unreadCount} unread</p>
        </div>

        <form className="mt-6 grid gap-4 rounded-lg border border-slate-800 bg-slate-900 p-4 sm:grid-cols-3" onSubmit={(event: FormEvent) => { event.preventDefault(); setAppliedSearch(search.trim()); }}>
          <label className="text-sm font-semibold text-slate-200 sm:col-span-2">
            Search notifications
            <input className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" placeholder="Reference, update type, or subject" value={search} onChange={(event) => setSearch(event.target.value)} />
          </label>
          <label className="text-sm font-semibold text-slate-200">
            Read state
            <select className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2" value={readState} onChange={(event) => setReadState(event.target.value)}><option value="">All</option><option value="unread">Unread</option><option value="read">Read</option></select>
          </label>
          <div className="flex gap-3 sm:col-span-3"><button type="submit" className="rounded-md bg-emerald-400 px-4 py-2 font-semibold text-slate-950">Search</button><button type="button" className="rounded-md border border-slate-600 px-4 py-2 font-semibold" onClick={() => { setSearch(""); setAppliedSearch(""); setReadState(""); }}>Clear</button></div>
        </form>

        {canFilterByTeam && (
          <div
            role="tablist"
            aria-label="Filter notifications"
            className="mt-8 inline-flex rounded-lg border border-slate-800 bg-slate-900 p-1"
          >
            {filterTabs.map((tab) => (
              <button
                key={tab.value}
                id={`notification-scope-${tab.value}`}
                type="button"
                role="tab"
                aria-selected={filter === tab.value}
                aria-controls="notification-results"
                tabIndex={filter === tab.value ? 0 : -1}
                onClick={() => setFilter(tab.value)}
                className={`rounded-md px-4 py-1.5 text-sm font-semibold ${
                  filter === tab.value
                    ? "bg-emerald-400 text-slate-950"
                    : "text-slate-300 hover:text-white"
                }`}
              >
                {tab.label}
              </button>
            ))}
          </div>
        )}

        <div
          id="notification-results"
          role={canFilterByTeam ? "tabpanel" : "region"}
          aria-labelledby={canFilterByTeam ? `notification-scope-${filter}` : undefined}
          aria-label={canFilterByTeam ? undefined : "Notification results"}
        >
        {notificationsQuery.isPending && (
          <p className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-300">
            Loading notifications...
          </p>
        )}

        {notificationsQuery.isError && (
          <p className="mt-8 whitespace-pre-wrap rounded-lg border border-red-900 bg-red-950 p-4 text-sm text-red-200">
            {getErrorMessage(notificationsQuery.error)}
          </p>
        )}

        {notificationsQuery.isSuccess && notifications.length === 0 && (
          <section className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-lg font-semibold text-white">
              {emptyHeading}
            </h2>
            <p className="mt-2 text-sm text-slate-300">
              {emptyDescription}
            </p>
          </section>
        )}

        {notificationsQuery.isSuccess && notifications.length > 0 && (
          <>
            {visibleNotifications.length === 0 ? (
              <p className="mt-6 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-300">
                No notifications in this view.
              </p>
            ) : (
              <div className="mt-6 space-y-6">
                {notificationGroups.map((group) => (
                  <section key={group.key} className="overflow-hidden rounded-xl border border-slate-700 bg-slate-900/50">
                    <header className="border-b border-slate-700 bg-slate-900 px-5 py-4">
                      <p className="text-xs font-semibold uppercase tracking-wide text-emerald-300">Submission</p>
                      <h2 className="mt-1 text-lg font-semibold text-white">
                        {group.companyName ? `${group.companyName} · ` : ""}{group.submissionReference}
                      </h2>
                      <p className="mt-1 text-xs text-slate-400">
                        {group.notifications.length} {group.notifications.length === 1 ? "update" : "updates"}
                      </p>
                    </header>
                    <ul className="divide-y divide-slate-800">
                      {group.notifications.map((notification) => {
                        const isHistorical = notification.lifecycleState === "Historical";
                        const action = resolveNotificationAction(
                          notification,
                          currentUserQuery.data,
                        );

                        return (
                          <li
                            key={notification.notificationId}
                            className={`border-l-4 p-5 ${
                              isHistorical
                                ? "border-l-slate-600 bg-slate-950/50"
                                : notification.isRead
                                ? "border-l-transparent bg-slate-900"
                                : "border-l-amber-300 bg-amber-950/20"
                            }`}
                          >
                            <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                              <div>
                                <div className="flex items-center gap-2">
                                  <h3 className="text-base font-semibold text-white">{notification.title}</h3>
                                  {!isHistorical && !notification.isRead && (
                                    <span className="inline-flex rounded-full border border-amber-300/70 bg-amber-300 px-2 py-0.5 text-xs font-bold text-slate-950">
                                      Unread
                                    </span>
                                  )}
                                  {isHistorical && (
                                    <span className="inline-flex rounded-full border border-slate-600 bg-slate-800 px-2 py-0.5 text-xs font-bold text-slate-200">
                                      Superseded
                                    </span>
                                  )}
                                  {notification.scope === "team" && (
                                    <span className="inline-flex rounded-md border border-sky-500/40 bg-sky-950/40 px-2 py-0.5 text-xs font-semibold text-sky-300">Team</span>
                                  )}
                                </div>
                                {notification.attributes.remediationGuidance && (
                                  <p className="mt-1 text-sm text-slate-300">{notification.attributes.remediationGuidance}</p>
                                )}
                                {isHistorical && (
                                  <p className="mt-2 text-sm text-slate-400">
                                    {notification.historicalReason ?? "A later quote version replaced this update. It remains available as history."}
                                  </p>
                                )}
                                <NotificationDetails attributes={notification.attributes} />
                                <p className="mt-2 text-xs text-slate-400">{new Date(notification.occurredAtUtc).toLocaleString()}</p>
                              </div>
                              <div className="flex flex-wrap gap-2 sm:justify-end">
                                {action && (
                                  <Link
                                    to={action.to}
                                    className="inline-flex h-fit rounded-lg border border-emerald-400/60 px-4 py-2 text-xs font-semibold text-emerald-200 hover:bg-emerald-400 hover:text-slate-950 disabled:cursor-not-allowed disabled:border-slate-700 disabled:text-slate-500"
                                  >
                                    {isHistorical
                                      ? action.label.replace("Open", "View historical").replace("View quote", "View historical quote")
                                      : action.label}
                                  </Link>
                                )}
                                {!action && notification.isRead && (
                                  <span className="inline-flex h-fit rounded-md border border-slate-700 px-3 py-1 text-xs font-semibold text-slate-300">Read</span>
                                )}
                              </div>
                            </div>
                          </li>
                        );
                      })}
                    </ul>
                  </section>
                ))}
              </div>
            )}
          </>
        )}
        </div>
      </section>
    </main>
  );
}
