import { useState } from "react";
import { Link } from "react-router";

import { useCurrentUser } from "../../../hooks/useCurrentUser";
import { hasTeamNotificationAccess } from "../../../lib/roleAccess";
import { formatCurrency } from "../../../lib/currency";
import { getUserErrorMessage } from "../../../lib/apiClient";
import {
  useMarkNotificationRead,
  useNotifications,
} from "../hooks/useNotifications";
import type { NotificationScope } from "../types";

type NotificationFilter = "all" | NotificationScope;

const filterTabs: { value: NotificationFilter; label: string }[] = [
  { value: "all", label: "All" },
  { value: "personal", label: "Personal" },
  { value: "team", label: "Team" },
];

function getErrorMessage(error: unknown) {
  return getUserErrorMessage(error, "Unable to load notifications.");
}

function getNotificationAction(
  subjectReferenceType: string,
  subjectReferenceId: string,
  attributes: Record<string, string>,
) {
  if (subjectReferenceType === "policy") {
    const policyId = attributes.policyId ?? subjectReferenceId;
    return policyId
      ? { label: "View policy", to: `/policies/${policyId}` }
      : null;
  }

  if (subjectReferenceType === "evidence-request") {
    const evidenceRequestId = attributes.evidenceRequestId ?? subjectReferenceId;
    return evidenceRequestId
      ? {
          label: "Open evidence request",
          to: `/evidence-requests/${evidenceRequestId}`,
        }
      : null;
  }

  if (subjectReferenceType === "quote") {
    const submissionId = attributes.submissionId;
    const quoteId = attributes.quoteId ?? subjectReferenceId;
    return submissionId && quoteId
      ? {
          label: "View quote",
          to: `/submissions/${submissionId}/quotes/${quoteId}`,
        }
      : null;
  }

  if (subjectReferenceType === "submission") {
    const submissionId =
      attributes.submissionId ??
      subjectReferenceId;
    return submissionId
      ? { label: "Open submission", to: `/submissions/${submissionId}` }
      : null;
  }

  return null;
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

export function NotificationsPage() {
  const currentUserQuery = useCurrentUser();
  const notificationsQuery = useNotifications();
  const markReadMutation = useMarkNotificationRead();
  const [filter, setFilter] = useState<NotificationFilter>("all");
  const canFilterByTeam = hasTeamNotificationAccess(
    currentUserQuery.data?.roles,
  );
  const [previousCanFilterByTeam, setPreviousCanFilterByTeam] = useState(
    canFilterByTeam,
  );
  if (previousCanFilterByTeam !== canFilterByTeam) {
    setPreviousCanFilterByTeam(canFilterByTeam);
    setFilter(canFilterByTeam ? "all" : "personal");
  }

  const notifications = notificationsQuery.data?.notifications ?? [];
  const unreadCount = notificationsQuery.data?.unreadCount ?? 0;
  const visibleNotifications = notifications.filter(
    (notification) => filter === "all" || notification.scope === filter,
  );

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
      <section className="mx-auto max-w-3xl">
        <Link
          to="/dashboard"
          className="inline-flex text-sm font-semibold text-emerald-300 hover:text-emerald-200"
        >
          Back to dashboard
        </Link>

        <div className="mt-8">
          <p className="text-sm font-semibold uppercase tracking-wide text-emerald-400">
            Notifications
          </p>
          <h1 className="mt-4 text-4xl font-bold tracking-tight">
            Your notifications
          </h1>
          <p className="mt-4 text-slate-300">{unreadCount} unread</p>
        </div>

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
              No notifications yet.
            </h2>
            <p className="mt-2 text-sm text-slate-300">
              Updates about your submissions, quotes, policies, and evidence
              requests will appear here.
            </p>
          </section>
        )}

        {notificationsQuery.isSuccess && notifications.length > 0 && (
          <>
            {canFilterByTeam && (
              <div
                role="tablist"
                aria-label="Filter notifications"
                className="mt-8 inline-flex rounded-lg border border-slate-800 bg-slate-900 p-1"
              >
                {filterTabs.map((tab) => (
                  <button
                    key={tab.value}
                    type="button"
                    role="tab"
                    aria-selected={filter === tab.value}
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

            {visibleNotifications.length === 0 ? (
              <p className="mt-6 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-300">
                No notifications in this view.
              </p>
            ) : (
              <ul className="mt-6 space-y-3">
                {visibleNotifications.map((notification) => (
                  <li
                    key={notification.notificationId}
                    className={`rounded-lg border p-5 ${
                      notification.isRead
                        ? "border-slate-800 bg-slate-900"
                        : "border-emerald-500/40 bg-emerald-950/20"
                    }`}
                  >
                    {(() => {
                      const action = getNotificationAction(
                        notification.subjectReferenceType,
                        notification.subjectReferenceId,
                        notification.attributes,
                      );

                      return (
                    <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                      <div>
                        <div className="flex items-center gap-2">
                          <h2 className="text-base font-semibold text-white">
                            {notification.title}
                          </h2>
                          {notification.scope === "team" && (
                            <span className="inline-flex rounded-md border border-sky-500/40 bg-sky-950/40 px-2 py-0.5 text-xs font-semibold text-sky-300">
                              Team
                            </span>
                          )}
                        </div>
                        {notification.attributes.remediationGuidance && (
                          <p className="mt-1 text-sm text-slate-300">
                            {notification.attributes.remediationGuidance}
                          </p>
                        )}
                        <NotificationDetails attributes={notification.attributes} />
                        <p className="mt-2 text-xs text-slate-400">
                          {new Date(notification.occurredAtUtc).toLocaleString()}
                        </p>
                      </div>

                      <div className="flex flex-wrap gap-2 sm:justify-end">
                        {action && (
                          <Link
                            to={action.to}
                            className="inline-flex h-fit rounded-lg border border-emerald-400/60 px-4 py-2 text-xs font-semibold text-emerald-200 hover:bg-emerald-400 hover:text-slate-950"
                          >
                            {action.label}
                          </Link>
                        )}
                        {notification.isRead ? (
                          <span className="inline-flex h-fit rounded-md border border-slate-700 px-3 py-1 text-xs font-semibold text-slate-300">
                            Read
                          </span>
                        ) : (
                          <button
                            type="button"
                            onClick={() =>
                              markReadMutation.mutate(notification.notificationId)
                            }
                            disabled={markReadMutation.isPending}
                            className="inline-flex h-fit rounded-lg bg-emerald-400 px-4 py-2 text-xs font-semibold text-slate-950 hover:bg-emerald-300 disabled:cursor-not-allowed disabled:bg-slate-600 disabled:text-slate-300"
                          >
                            Mark as read
                          </button>
                        )}
                      </div>
                    </div>
                      );
                    })()}
                  </li>
                ))}
              </ul>
            )}
          </>
        )}
      </section>
    </main>
  );
}
