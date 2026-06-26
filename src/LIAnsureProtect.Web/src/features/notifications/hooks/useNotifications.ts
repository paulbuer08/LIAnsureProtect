import { useAuth0 } from "@auth0/auth0-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  listMyNotifications,
  markNotificationRead,
} from "../api/notificationsApi";

export const notificationsQueryKey = ["notifications"];

export function useNotifications() {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    queryKey: notificationsQueryKey,
    queryFn: async () => {
      const accessToken = await getAccessTokenSilently();

      return listMyNotifications(accessToken);
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
