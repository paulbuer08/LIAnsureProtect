import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useNotifications } from "../features/notifications/hooks/useNotifications";
import { useCurrentUser } from "../hooks/useCurrentUser";
import { DashboardPage } from "./DashboardPage";

const loginWithRedirect = vi.fn();

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => ({
    isAuthenticated: true,
    isLoading: false,
    loginWithRedirect,
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

function mockCurrentUserLookupFailure() {
  vi.mocked(useCurrentUser).mockReturnValue({
    data: undefined,
    error: new Error("Current-user lookup failed with 401."),
    isPending: false,
    isError: true,
  } as unknown as ReturnType<typeof useCurrentUser>);
}

function mockCurrentUserConsentRequired() {
  vi.mocked(useCurrentUser).mockReturnValue({
    data: undefined,
    error: Object.assign(new Error("Consent required"), {
      error: "consent_required",
    }),
    isPending: false,
    isError: true,
  } as unknown as ReturnType<typeof useCurrentUser>);
}

describe("DashboardPage", () => {
  beforeEach(() => {
    loginWithRedirect.mockReset();
    mockCurrentUser(["Customer"]);
    mockNotifications();
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

  it("shows a role lookup diagnostic instead of treating API errors as no assigned roles", () => {
    mockCurrentUserLookupFailure();

    render(
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>,
    );

    expect(screen.getByText("Roles unavailable")).toBeInTheDocument();
    expect(
      screen.getByRole("heading", {
        name: "We could not load your assigned roles.",
      }),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Current-user lookup failed with 401."),
    ).toBeInTheDocument();
    expect(
      screen.queryByText("No application workspace is available yet."),
    ).not.toBeInTheDocument();
  });

  it("offers an Auth0 consent action when API token consent is required", async () => {
    const user = userEvent.setup();
    mockCurrentUserConsentRequired();

    render(
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>,
    );

    expect(screen.getByText("Consent required")).toBeInTheDocument();

    await user.click(
      screen.getByRole("button", { name: "Continue with Auth0" }),
    );

    expect(loginWithRedirect).toHaveBeenCalledWith({
      appState: {
        returnTo: "/",
      },
      authorizationParams: expect.objectContaining({
        audience: "https://api.liansureprotect.local",
        prompt: "consent",
      }),
    });
  });
});
