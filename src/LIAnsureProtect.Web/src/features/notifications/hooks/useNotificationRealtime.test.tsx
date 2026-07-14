import type { ReactNode } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

const signalR = vi.hoisted(() => ({
  eventHandler: undefined as (() => void) | undefined,
  reconnectHandler: undefined as (() => void) | undefined,
  start: vi.fn(() => Promise.resolve()),
  stop: vi.fn(() => Promise.resolve()),
  off: vi.fn(),
}));

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => ({ getAccessTokenSilently: vi.fn(() => Promise.resolve("token-1")) }),
}));

vi.mock("@microsoft/signalr", () => ({
  LogLevel: { Warning: 3 },
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() {
      return {
        on: (_name: string, handler: () => void) => { signalR.eventHandler = handler; },
        off: signalR.off,
        onreconnected: (handler: () => void) => { signalR.reconnectHandler = handler; },
        start: signalR.start,
        stop: signalR.stop,
      };
    }
  },
}));

import { useNotificationRealtime } from "./useNotificationRealtime";

describe("useNotificationRealtime", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    signalR.eventHandler = undefined;
    signalR.reconnectHandler = undefined;
  });

  it("invalidates the inbox and unread count on a hint and reconnect", async () => {
    const queryClient = new QueryClient();
    const invalidate = vi.spyOn(queryClient, "invalidateQueries");
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );

    const { unmount } = renderHook(() => useNotificationRealtime(true), { wrapper });

    await waitFor(() => expect(signalR.start).toHaveBeenCalledOnce());
    invalidate.mockClear();
    signalR.eventHandler?.();
    signalR.reconnectHandler?.();

    expect(invalidate).toHaveBeenCalledTimes(4);
    unmount();
    expect(signalR.stop).toHaveBeenCalledOnce();
  });
});
