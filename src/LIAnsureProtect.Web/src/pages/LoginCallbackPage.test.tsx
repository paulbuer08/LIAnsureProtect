import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { LoginCallbackPage } from "./LoginCallbackPage";

const navigate = vi.fn();
const auth0State = vi.hoisted(() => ({
  error: undefined as Error | undefined,
  isAuthenticated: true,
  isLoading: false,
  user: {
    email: "user@example.com",
  },
}));

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => auth0State,
}));

vi.mock("react-router", async () => {
  const actual = await vi.importActual<typeof import("react-router")>(
    "react-router",
  );

  return {
    ...actual,
    useNavigate: () => navigate,
  };
});

describe("LoginCallbackPage", () => {
  beforeEach(() => {
    navigate.mockReset();
    auth0State.error = undefined;
    auth0State.isAuthenticated = true;
    auth0State.isLoading = false;
    auth0State.user = {
      email: "user@example.com",
    };
  });

  it("continues to the dashboard automatically after Auth0 loads an authenticated session", async () => {
    render(
      <MemoryRouter>
        <LoginCallbackPage />
      </MemoryRouter>,
    );

    await waitFor(() =>
      expect(navigate).toHaveBeenCalledWith("/dashboard", { replace: true }),
    );
  });

  it("keeps the fallback dashboard link visible while Auth0 is still loading", () => {
    auth0State.isLoading = true;
    auth0State.isAuthenticated = false;

    render(
      <MemoryRouter>
        <LoginCallbackPage />
      </MemoryRouter>,
    );

    expect(
      screen.getByRole("link", { name: "Continue to dashboard" }),
    ).toHaveAttribute("href", "/dashboard");
    expect(navigate).not.toHaveBeenCalled();
  });

  it("shows the Auth0 callback error instead of navigating when the SDK reports an error", () => {
    auth0State.error = new Error("Access denied");

    render(
      <MemoryRouter>
        <LoginCallbackPage />
      </MemoryRouter>,
    );

    expect(screen.getByText("Auth0 callback error")).toBeInTheDocument();
    expect(screen.getByText("Access denied")).toBeInTheDocument();
    expect(navigate).not.toHaveBeenCalled();
  });
});
