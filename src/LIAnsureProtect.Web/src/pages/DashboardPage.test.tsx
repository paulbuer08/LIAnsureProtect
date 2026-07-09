import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useNotifications } from "../features/notifications/hooks/useNotifications";
import { useCurrentUser } from "../hooks/useCurrentUser";
import { DashboardPage } from "./DashboardPage";

const getAccessTokenSilently = vi.fn();
const logout = vi.fn();

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => ({
    getAccessTokenSilently,
    isAuthenticated: true,
    isLoading: false,
    logout,
    user: {
      email: "customer@example.com",
    },
  }),
}));

vi.mock("../hooks/useCurrentUser", () => ({
  useCurrentUser: vi.fn(),
}));

vi.mock("../features/notifications/hooks/useNotifications", () => ({
  useNotifications: vi.fn(),
}));

function mockCurrentUser(roles: string[]) {
  vi.mocked(useCurrentUser).mockReturnValue({
    data: { userId: "user-1", email: "customer@example.com", roles },
    isPending: false,
    isError: false,
  } as unknown as ReturnType<typeof useCurrentUser>);
}

function mockNotifications(unreadCount = 0) {
  vi.mocked(useNotifications).mockReturnValue({
    data: {
      notifications: [],
      unreadCount,
    },
    isPending: false,
    isError: false,
  } as unknown as ReturnType<typeof useNotifications>);
}

describe("DashboardPage", () => {
  beforeEach(() => {
    getAccessTokenSilently.mockReset();
    logout.mockReset();
    mockCurrentUser(["Customer"]);
    mockNotifications();
  });

  it("shows a shortened access token preview instead of the full token", async () => {
    const user = userEvent.setup();
    const accessToken = "header.payload.signature-with-long-test-value";
    getAccessTokenSilently.mockResolvedValue(accessToken);

    render(
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>,
    );

    await user.click(
      screen.getByRole("button", { name: "Get API access token" }),
    );

    expect(getAccessTokenSilently).toHaveBeenCalledTimes(1);
    expect(
      await screen.findByText(/header\.payload\.s\.\.\.-long-test-value/),
    ).toBeInTheDocument();
    expect(screen.queryByText(accessToken)).not.toBeInTheDocument();
  });

  it("links signed-in users to the real submission intake workflow", () => {
    render(
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>,
    );

    expect(
      screen.getByRole("link", { name: "Create submission" }),
    ).toHaveAttribute("href", "/submissions/new");
    expect(
      screen.getByRole("link", { name: "View submissions" }),
    ).toHaveAttribute("href", "/submissions");
    expect(
      screen.queryByRole("button", { name: "Create draft submission" }),
    ).not.toBeInTheDocument();
  });

  it("links underwriters to the underwriting referral workbench", () => {
    mockCurrentUser(["Underwriter"]);

    render(
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>,
    );

    expect(
      screen.getByRole("link", { name: "Open underwriting workbench" }),
    ).toHaveAttribute("href", "/underwriting/quote-referrals");
  });

  it("hides staff-only dashboard areas from customer users", () => {
    mockCurrentUser(["Customer"]);
    mockNotifications(3);

    render(
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>,
    );

    expect(
      screen.getByRole("link", { name: "Create submission" }),
    ).toHaveAttribute("href", "/submissions/new");
    expect(
      screen.getByRole("link", { name: "View notifications" }),
    ).toHaveAttribute("href", "/notifications");
    expect(
      screen.getByLabelText("3 unread notifications"),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: "Open underwriting workbench" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: "Open claims workbench" }),
    ).not.toBeInTheDocument();
  });

  it("shows underwriters their workbench and team notification context", () => {
    mockCurrentUser(["Underwriter"]);
    mockNotifications(2);

    render(
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>,
    );

    expect(
      screen.getByRole("link", { name: "Open underwriting workbench" }),
    ).toHaveAttribute("href", "/underwriting/quote-referrals");
    expect(
      screen.getByText(/personal and team notifications/i),
    ).toBeInTheDocument();
    expect(
      screen.getByLabelText("2 unread notifications"),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: "Create submission" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: "File a claim" }),
    ).not.toBeInTheDocument();
  });
});
