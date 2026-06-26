import { Link } from "react-router";

import {
  useMarkNotificationRead,
  useNotifications,
} from "../hooks/useNotifications";

function getErrorMessage(error: unknown) {
  return error instanceof Error
    ? error.message
    : "Unable to load notifications.";
}

export function NotificationsPage() {
  const notificationsQuery = useNotifications();
  const markReadMutation = useMarkNotificationRead();
  const notifications = notificationsQuery.data?.notifications ?? [];
  const unreadCount = notificationsQuery.data?.unreadCount ?? 0;

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

        {notifications.length > 0 && (
          <ul className="mt-8 space-y-3">
            {notifications.map((notification) => (
              <li
                key={notification.notificationId}
                className={`rounded-lg border p-5 ${
                  notification.isRead
                    ? "border-slate-800 bg-slate-900"
                    : "border-emerald-500/40 bg-emerald-950/20"
                }`}
              >
                <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                  <div>
                    <h2 className="text-base font-semibold text-white">
                      {notification.title}
                    </h2>
                    {notification.attributes.remediationGuidance && (
                      <p className="mt-1 text-sm text-slate-300">
                        {notification.attributes.remediationGuidance}
                      </p>
                    )}
                    <p className="mt-2 text-xs text-slate-400">
                      {new Date(notification.occurredAtUtc).toLocaleString()}
                    </p>
                  </div>

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
              </li>
            ))}
          </ul>
        )}
      </section>
    </main>
  );
}
