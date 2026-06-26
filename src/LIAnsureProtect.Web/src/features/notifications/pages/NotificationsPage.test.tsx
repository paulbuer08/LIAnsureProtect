import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  listMyNotifications,
  markNotificationRead,
} from "../api/notificationsApi";
import { NotificationsPage } from "./NotificationsPage";

const getAccessTokenSilently = vi.fn();

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => ({
    getAccessTokenSilently,
  }),
}));

vi.mock("../api/notificationsApi", () => ({
  listMyNotifications: vi.fn(),
  markNotificationRead: vi.fn(),
}));

function renderNotificationsPage() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <NotificationsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("NotificationsPage", () => {
  beforeEach(() => {
    getAccessTokenSilently.mockReset();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(listMyNotifications).mockReset();
    vi.mocked(markNotificationRead).mockReset();
  });

  it("shows an empty state when there are no notifications", async () => {
    vi.mocked(listMyNotifications).mockResolvedValue({
      notifications: [],
      unreadCount: 0,
    });

    renderNotificationsPage();

    expect(await screen.findByText("No notifications yet.")).toBeInTheDocument();
  });

  it("renders notifications with the unread count and remediation guidance", async () => {
    vi.mocked(listMyNotifications).mockResolvedValue({
      notifications: [
        {
          notificationId: "n-1",
          type: "evidence_request.remediation_required",
          title: "Action needed on your evidence",
          subjectReferenceType: "evidence-request",
          subjectReferenceId: "er-1",
          attributes: { remediationGuidance: "Please confirm MFA scope." },
          occurredAtUtc: "2026-06-22T09:00:00Z",
          isRead: false,
          readAtUtc: null,
        },
      ],
      unreadCount: 1,
    });

    renderNotificationsPage();

    expect(
      await screen.findByText("Action needed on your evidence"),
    ).toBeInTheDocument();
    expect(screen.getByText("Please confirm MFA scope.")).toBeInTheDocument();
    expect(screen.getByText("1 unread")).toBeInTheDocument();
  });

  it("marks a notification as read through the protected API", async () => {
    const user = userEvent.setup();
    vi.mocked(listMyNotifications).mockResolvedValue({
      notifications: [
        {
          notificationId: "n-1",
          type: "quote.ready",
          title: "Your quote is ready",
          subjectReferenceType: "quote",
          subjectReferenceId: "q-1",
          attributes: {},
          occurredAtUtc: "2026-06-22T09:00:00Z",
          isRead: false,
          readAtUtc: null,
        },
      ],
      unreadCount: 1,
    });
    vi.mocked(markNotificationRead).mockResolvedValue(undefined);

    renderNotificationsPage();

    const markReadButton = await screen.findByRole("button", {
      name: "Mark as read",
    });
    await user.click(markReadButton);

    expect(markNotificationRead).toHaveBeenCalledWith("api-access-token", "n-1");
  });
});
