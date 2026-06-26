import type { ListMyNotificationsResponse } from "../types";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

async function ensureOk(response: Response, notFoundMessage: string) {
  if (response.status === 404) {
    throw new Error(notFoundMessage);
  }

  if (!response.ok) {
    const errorBody = await response.text();

    throw new Error(
      `API request failed with ${response.status} ${response.statusText}: ${errorBody}`,
    );
  }
}

function authHeaders(accessToken: string) {
  return {
    Authorization: `Bearer ${accessToken}`,
  };
}

export async function listMyNotifications(accessToken: string) {
  const response = await fetch(`${apiBaseUrl}/api/v1/notifications`, {
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
