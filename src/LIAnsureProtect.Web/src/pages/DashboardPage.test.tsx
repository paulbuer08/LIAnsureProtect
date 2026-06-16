import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createSubmission } from "../lib/apiClient";
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

vi.mock("../lib/apiClient", () => ({
  createSubmission: vi.fn(),
}));

describe("DashboardPage", () => {
  beforeEach(() => {
    getAccessTokenSilently.mockReset();
    logout.mockReset();
    vi.mocked(createSubmission).mockReset();
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

  it("creates a draft submission with the current API access token", async () => {
    const user = userEvent.setup();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(createSubmission).mockResolvedValue({
      submissionId: "submission-123",
      status: "Draft",
    });

    render(
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>,
    );

    await user.click(
      screen.getByRole("button", { name: "Create draft submission" }),
    );

    expect(getAccessTokenSilently).toHaveBeenCalledTimes(1);
    expect(createSubmission).toHaveBeenCalledWith("api-access-token", {
      applicantName: "Frontend Smoke Test Applicant",
      applicantEmail: "frontend-smoke@example.com",
      companyName: "Frontend Smoke Test Company",
    });
    expect(await screen.findByText("submission-123")).toBeInTheDocument();
    expect(screen.getByText("Draft")).toBeInTheDocument();
  });
});
