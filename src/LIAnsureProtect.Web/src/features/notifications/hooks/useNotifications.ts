import { useAuth0 } from "@auth0/auth0-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  listMyNotifications,
  markNotificationRead,
} from "../api/notificationsApi";
import type { NotificationFilters } from "../types";

export const notificationsQueryKey = ["notifications"];

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

export function useMarkNotificationRead() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (notificationId: string) => {
      const accessToken = await getAccessTokenSilently();

      return markNotificationRead(accessToken, notificationId);
    },
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: notificationsQueryKey }),
  });
}
