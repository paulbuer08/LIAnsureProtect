import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { RequireAuth } from "./RequireAuth";

const loginWithRedirect = vi.fn();
let authState = {
  isAuthenticated: false,
  isLoading: false,
  loginWithRedirect,
};

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => authState,
}));

describe("RequireAuth", () => {
  beforeEach(() => {
    loginWithRedirect.mockReset();
    authState = {
      isAuthenticated: false,
      isLoading: false,
      loginWithRedirect,
    };
  });

  it("shows a loading message while Auth0 checks the browser session", () => {
    authState = {
      ...authState,
      isLoading: true,
    };

    render(
      <RequireAuth>
        <p>Private dashboard</p>
      </RequireAuth>,
    );

    expect(screen.getByText("Checking authentication...")).toBeInTheDocument();
    expect(screen.queryByText("Private dashboard")).not.toBeInTheDocument();
  });

  it("asks signed-out users to log in before showing private content", async () => {
    const user = userEvent.setup();

    render(
      <RequireAuth>
        <p>Private dashboard</p>
      </RequireAuth>,
    );

    expect(screen.getByText("Authentication required")).toBeInTheDocument();
    expect(screen.queryByText("Private dashboard")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Log in with Auth0" }));

    expect(loginWithRedirect).toHaveBeenCalledTimes(1);
  });

  it("renders private content when Auth0 says the user is authenticated", () => {
    authState = {
      ...authState,
      isAuthenticated: true,
    };

    render(
      <RequireAuth>
        <p>Private dashboard</p>
      </RequireAuth>,
    );

    expect(screen.getByText("Private dashboard")).toBeInTheDocument();
  });
});
