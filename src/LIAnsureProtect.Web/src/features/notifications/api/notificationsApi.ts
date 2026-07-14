import type { ListMyNotificationsResponse, NotificationFilters } from "../types";
import { ensureSuccess } from "../../../lib/apiClient";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

async function ensureOk(response: Response, notFoundMessage: string) {
  await ensureSuccess(response, { notFoundMessage });
}

function authHeaders(accessToken: string) {
  return {
    Authorization: `Bearer ${accessToken}`,
  };
}

export async function listMyNotifications(
  accessToken: string,
  filters: NotificationFilters = {},
) {
  const url = new URL(`${apiBaseUrl}/api/v1/notifications`);
  if (filters.search) url.searchParams.set("search", filters.search);
  if (filters.type) url.searchParams.set("type", filters.type);
  if (filters.isUnread !== undefined)
    url.searchParams.set("isUnread", String(filters.isUnread));
  if (filters.scope) url.searchParams.set("scope", filters.scope);
  const response = await fetch(url, {
    headers: authHeaders(accessToken),
  });

  await ensureOk(response, "Notifications were not found.");

  return (await response.json()) as ListMyNotificationsResponse;
}

export async function markNotificationRead(
  accessToken: string,
  notificationId: string,
) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/notifications/${notificationId}/read`,
    {
      method: "POST",
      headers: authHeaders(accessToken),
    },
  );

  await ensureOk(response, "Notification was not found.");
}
