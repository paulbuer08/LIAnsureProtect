import { useAuth0 } from "@auth0/auth0-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  getUnreadNotificationCount,
  listMyNotifications,
  markNotificationRead,
} from "../api/notificationsApi";
import type {
  ListMyNotificationsResponse,
  NotificationFilters,
  UnreadNotificationCountResponse,
} from "../types";

export const notificationsQueryKey = ["notifications"];
export const notificationUnreadCountQueryKey = ["notifications", "unread-count"];

export function useNotifications(options?: {
  enabled?: boolean;
  filters?: NotificationFilters;
}) {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    queryKey: [...notificationsQueryKey, options?.filters ?? {}],
    enabled: options?.enabled ?? true,
    queryFn: async () => {
      const accessToken = await getAccessTokenSilently();

      return listMyNotifications(accessToken, options?.filters);
    },
  });
}

export function useUnreadNotificationCount(options?: { enabled?: boolean }) {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    queryKey: notificationUnreadCountQueryKey,
    enabled: options?.enabled ?? true,
    queryFn: async () => {
      const accessToken = await getAccessTokenSilently();

      return getUnreadNotificationCount(accessToken);
    },
    refetchOnWindowFocus: true,
  });
}

export function useMarkNotificationRead() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (notificationId: string) => {
      const accessToken = await getAccessTokenSilently();

      return markNotificationRead(accessToken, notificationId);
    },
    onMutate: (notificationId) => {
      let changedUnreadToRead = false;
      const cachedLists = queryClient.getQueriesData<ListMyNotificationsResponse>({
        queryKey: notificationsQueryKey,
      });

      for (const [queryKey, current] of cachedLists) {
        if (!current || !Array.isArray(current.notifications)) continue;
        const target = current.notifications.find(
          (notification) => notification.notificationId === notificationId,
        );
        if (!target || target.isRead) continue;
        changedUnreadToRead = true;
        const filters = queryKey[1] as NotificationFilters | undefined;
        const notifications = filters?.isUnread === true
          ? current.notifications.filter(
              (notification) => notification.notificationId !== notificationId,
            )
          : current.notifications.map((notification) =>
              notification.notificationId === notificationId
                ? { ...notification, isRead: true, readAtUtc: new Date().toISOString() }
                : notification,
            );

        queryClient.setQueryData<ListMyNotificationsResponse>(queryKey, {
          ...current,
          unreadCount: Math.max(0, current.unreadCount - 1),
          notifications,
        });
      }

      if (changedUnreadToRead) {
        queryClient.setQueryData<UnreadNotificationCountResponse>(
          notificationUnreadCountQueryKey,
          (current) => current
            ? { unreadCount: Math.max(0, current.unreadCount - 1) }
            : current,
        );
      }
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: notificationsQueryKey });
      void queryClient.invalidateQueries({ queryKey: notificationUnreadCountQueryKey });
    },
  });
}
