import { useEffect } from "react";
import { useAuth0 } from "@auth0/auth0-react";
import {
  HubConnectionBuilder,
  LogLevel,
} from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";

import {
  notificationsQueryKey,
  notificationUnreadCountQueryKey,
} from "./useNotifications";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

/**
 * Receives payload-free invalidation hints. PostgreSQL-backed HTTP queries remain authoritative.
 */
export function useNotificationRealtime(enabled: boolean) {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!enabled) return;

    let disposed = false;
    const invalidateNotifications = () => {
      if (disposed) return;
      void queryClient.invalidateQueries({ queryKey: notificationsQueryKey });
      void queryClient.invalidateQueries({
        queryKey: notificationUnreadCountQueryKey,
      });
    };
    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBaseUrl}/hubs/notifications`, {
        accessTokenFactory: getAccessTokenSilently,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on("NotificationsChanged", invalidateNotifications);
    connection.onreconnected(invalidateNotifications);
    void connection.start().then(invalidateNotifications).catch(() => {
      // Focus/navigation refetch remains the safety net while reconnect is unavailable.
    });

    return () => {
      disposed = true;
      connection.off("NotificationsChanged", invalidateNotifications);
      void connection.stop();
    };
  }, [enabled, getAccessTokenSilently, queryClient]);
}
