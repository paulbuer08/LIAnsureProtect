import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { acknowledgeNotificationSubject, markNotificationRead } from "../api/notificationsApi";
import type { ListMyNotificationsResponse } from "../types";
import {
  notificationUnreadCountQueryKey,
  notificationsQueryKey,
  useMarkNotificationRead,
  useAcknowledgeNotificationSubject,
} from "./useNotifications";

const getAccessTokenSilently = vi.fn();

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => ({ getAccessTokenSilently }),
}));

vi.mock("../api/notificationsApi", () => ({
  acknowledgeNotificationSubject: vi.fn(),
  markNotificationRead: vi.fn(),
}));

describe("useMarkNotificationRead", () => {
  beforeEach(() => {
    getAccessTokenSilently.mockReset();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(markNotificationRead).mockReset();
    vi.mocked(markNotificationRead).mockResolvedValue(undefined);
  });

  it("removes an opened notification from unread-only cache and decrements the badge", async () => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    const unreadQueryKey = [...notificationsQueryKey, { isUnread: true }];
    const cached: ListMyNotificationsResponse = {
      unreadCount: 1,
      notifications: [
        {
          notificationId: "notification-1",
          scope: "personal",
          audience: "customer-or-broker",
          type: "quote.ready",
          title: "Your quote is ready",
          subjectReferenceType: "quote",
          subjectReferenceId: "quote-1",
          attributes: { submissionId: "submission-1" },
          occurredAtUtc: "2026-07-14T10:00:00Z",
          isRead: false,
          readAtUtc: null,
        },
      ],
    };
    queryClient.setQueryData(unreadQueryKey, cached);
    queryClient.setQueryData(notificationUnreadCountQueryKey, { unreadCount: 1 });

    function Wrapper({ children }: { children: ReactNode }) {
      return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
    }

    const { result } = renderHook(() => useMarkNotificationRead(), {
      wrapper: Wrapper,
    });

    await act(async () => {
      await result.current.mutateAsync("notification-1");
    });

    expect(markNotificationRead).toHaveBeenCalledWith(
      "api-access-token",
      "notification-1",
    );
    expect(
      queryClient.getQueryData<ListMyNotificationsResponse>(unreadQueryKey)?.notifications,
    ).toEqual([]);
    expect(queryClient.getQueryData(notificationUnreadCountQueryKey)).toEqual({
      unreadCount: 0,
    });
  });
});

describe("useAcknowledgeNotificationSubject", () => {
  beforeEach(() => {
    getAccessTokenSilently.mockReset();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(acknowledgeNotificationSubject).mockReset();
    vi.mocked(acknowledgeNotificationSubject).mockResolvedValue(undefined);
  });

  it("acknowledges the exact subject only after its detail query succeeds", async () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    function Wrapper({ children }: { children: ReactNode }) {
      return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
    }

    const { rerender } = renderHook(
      ({ enabled }) => useAcknowledgeNotificationSubject(
        "evidence-request",
        "evidence-123",
        { enabled },
      ),
      { wrapper: Wrapper, initialProps: { enabled: false } },
    );

    expect(acknowledgeNotificationSubject).not.toHaveBeenCalled();
    rerender({ enabled: true });

    await waitFor(() => {
      expect(acknowledgeNotificationSubject).toHaveBeenCalledWith(
        "api-access-token",
        "evidence-request",
        "evidence-123",
        "personal",
      );
    });
  });
});
