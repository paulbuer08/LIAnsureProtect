import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

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

describe("DashboardPage", () => {
  beforeEach(() => {
    getAccessTokenSilently.mockReset();
    logout.mockReset();
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
});
