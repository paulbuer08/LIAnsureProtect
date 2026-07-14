import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  listMyNotifications,
  markNotificationRead,
} from "../api/notificationsApi";
import { NotificationsPage } from "./NotificationsPage";
import { useCurrentUser } from "../../../hooks/useCurrentUser";

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

vi.mock("../../../hooks/useCurrentUser", () => ({
  useCurrentUser: vi.fn(),
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
    vi.mocked(useCurrentUser).mockReturnValue({
      data: { userId: "customer-1", email: null, roles: ["Customer"] },
      isPending: false,
      isError: false,
      isSuccess: true,
    } as unknown as ReturnType<typeof useCurrentUser>);
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
          scope: "personal",
          audience: "customer-or-broker",
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

  it("does not expose a standalone mark-as-read control", async () => {
    vi.mocked(listMyNotifications).mockResolvedValue({
      notifications: [
        {
          notificationId: "n-1",
          scope: "personal",
          audience: "customer-or-broker",
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
    renderNotificationsPage();

    expect(await screen.findByText("Your quote is ready")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Mark as read" })).not.toBeInTheDocument();
    expect(markNotificationRead).not.toHaveBeenCalled();
  });

  it("links quote notifications to the exact immutable quote", async () => {
    const user = userEvent.setup();
    vi.mocked(listMyNotifications).mockResolvedValue({
      notifications: [
        {
          notificationId: "n-1",
          scope: "personal",
          audience: "customer-or-broker",
          type: "quote.ready",
          title: "Your quote is ready",
          subjectReferenceType: "quote",
          subjectReferenceId: "q-1",
          attributes: {
            quoteId: "q-1",
            submissionId: "submission-456",
            status: "Quoted",
          },
          occurredAtUtc: "2026-06-22T09:00:00Z",
          isRead: false,
          readAtUtc: null,
        },
      ],
      unreadCount: 1,
    });

    renderNotificationsPage();

    const quoteLink = await screen.findByRole("link", { name: "View quote" });
    expect(quoteLink).toHaveAttribute("href", "/submissions/submission-456/quotes/q-1");
    await user.click(quoteLink);
    await waitFor(() => {
      expect(markNotificationRead).toHaveBeenCalledWith("api-access-token", "n-1");
    });
    expect(screen.queryByRole("button", { name: "Mark as read" })).not.toBeInTheDocument();
  });

  it("shows no filter tabs for a personal-only customer", async () => {
    vi.mocked(listMyNotifications).mockResolvedValue({
      notifications: [
        {
          notificationId: "personal-1",
          scope: "personal",
          audience: "customer-or-broker",
          type: "quote.ready",
          title: "Your quote is ready",
          subjectReferenceType: "quote",
          subjectReferenceId: "q-1",
          attributes: {
            submissionId: "submission-1",
            submissionReference: "SUB-2026-AAAA1111BBBB2222",
            companyName: "Example Company",
          },
          occurredAtUtc: "2026-06-22T10:00:00Z",
          isRead: false,
          readAtUtc: null,
        },
      ],
      unreadCount: 1,
    });

    renderNotificationsPage();

    expect(await screen.findByText("Your quote is ready")).toBeInTheDocument();
    expect(screen.queryByRole("tablist")).not.toBeInTheDocument();
  });

  it("links policy and evidence subjects to their own workspaces", async () => {
    vi.mocked(listMyNotifications).mockResolvedValue({
      notifications: [
        {
          notificationId: "policy-1",
          scope: "personal",
          audience: "customer-or-broker",
          type: "policy.bound",
          title: "Your policy is bound",
          subjectReferenceType: "policy",
          subjectReferenceId: "policy-123",
          attributes: {
            submissionId: "submission-1",
            submissionReference: "SUB-2026-AAAA1111BBBB2222",
            companyName: "Example Company",
          },
          occurredAtUtc: "2026-06-22T10:00:00Z",
          isRead: false,
          readAtUtc: null,
        },
        {
          notificationId: "evidence-1",
          scope: "personal",
          audience: "customer-or-broker",
          type: "evidence_request.remediation_required",
          title: "Evidence needs attention",
          subjectReferenceType: "evidence-request",
          subjectReferenceId: "evidence-123",
          attributes: { submissionId: "submission-1" },
          occurredAtUtc: "2026-06-22T09:00:00Z",
          isRead: false,
          readAtUtc: null,
        },
      ],
      unreadCount: 2,
    });

    renderNotificationsPage();

    expect(await screen.findByText("Example Company · SUB-2026-AAAA1111BBBB2222")).toBeInTheDocument();
    expect(await screen.findByRole("link", { name: "View policy" })).toHaveAttribute(
      "href",
      "/policies/policy-123",
    );
    expect(screen.getByRole("link", { name: "Open evidence request" })).toHaveAttribute(
      "href",
      "/evidence-requests/evidence-123",
    );
  });

  it("badges team notifications and filters them with the Team tab", async () => {
    const user = userEvent.setup();
    vi.mocked(useCurrentUser).mockReturnValue({
      data: { userId: "underwriter-1", email: null, roles: ["Underwriter"] },
      isPending: false,
      isError: false,
      isSuccess: true,
    } as unknown as ReturnType<typeof useCurrentUser>);
    vi.mocked(listMyNotifications).mockResolvedValue({
      notifications: [
        {
          notificationId: "personal-1",
          scope: "personal",
          audience: "customer-or-broker",
          type: "quote.ready",
          title: "Your quote is ready",
          subjectReferenceType: "quote",
          subjectReferenceId: "q-1",
          attributes: {},
          occurredAtUtc: "2026-06-22T10:00:00Z",
          isRead: false,
          readAtUtc: null,
        },
        {
          notificationId: "team-1",
          scope: "team",
          audience: "underwriting-operations",
          type: "quote.referred_for_underwriting",
          title: "Quote referred for underwriting",
          subjectReferenceType: "quote",
          subjectReferenceId: "q-2",
          attributes: {},
          occurredAtUtc: "2026-06-22T09:00:00Z",
          isRead: false,
          readAtUtc: null,
        },
      ],
      unreadCount: 2,
    });

    renderNotificationsPage();

    // Both items show under "All", and the team item carries a Team badge.
    expect(
      await screen.findByText("Quote referred for underwriting"),
    ).toBeInTheDocument();
    expect(screen.getByText("Your quote is ready")).toBeInTheDocument();
    expect(screen.getByText("Team", { selector: "span" })).toBeInTheDocument();

    // The Team tab hides the personal item.
    await user.click(screen.getByRole("tab", { name: "Team" }));
    expect(screen.getByText("Quote referred for underwriting")).toBeInTheDocument();
    expect(screen.queryByText("Your quote is ready")).not.toBeInTheDocument();

    // The Personal tab hides the team item.
    await user.click(screen.getByRole("tab", { name: "Personal" }));
    expect(screen.getByText("Your quote is ready")).toBeInTheDocument();
    expect(
      screen.queryByText("Quote referred for underwriting"),
    ).not.toBeInTheDocument();
  });
});
