import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { listSubmissions } from "../api/listSubmissions";
import { SubmissionsPage } from "./SubmissionsPage";

const getAccessTokenSilently = vi.fn();

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => ({
    getAccessTokenSilently,
  }),
}));

vi.mock("../api/listSubmissions", () => ({
  listSubmissions: vi.fn(),
}));

function renderSubmissionsPage() {
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
        <SubmissionsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("SubmissionsPage", () => {
  beforeEach(() => {
    getAccessTokenSilently.mockReset();
    vi.mocked(listSubmissions).mockReset();
  });

  it("shows a loading state while submissions are loading", () => {
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(listSubmissions).mockReturnValue(new Promise(() => {}));

    renderSubmissionsPage();

    expect(screen.getByText("Loading submissions...")).toBeInTheDocument();
  });

  it("shows an empty state when there are no submissions", async () => {
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(listSubmissions).mockResolvedValue({
      submissions: [],
    });

    renderSubmissionsPage();

    expect(
      await screen.findByText("No submissions yet."),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "Create submission" }),
    ).toHaveAttribute("href", "/submissions/new");
  });

  it("shows an error state when submissions cannot be loaded", async () => {
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(listSubmissions).mockRejectedValue(
      new Error("API request failed with 500 Internal Server Error"),
    );

    renderSubmissionsPage();

    expect(
      await screen.findByText("API request failed with 500 Internal Server Error"),
    ).toBeInTheDocument();
  });

  it("renders submission rows from the protected API", async () => {
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(listSubmissions).mockResolvedValue({
      submissions: [
        {
          submissionId: "submission-456",
          applicantName: "Jane Applicant",
          applicantEmail: "jane@example.com",
          companyName: "Example Company",
          status: "Draft",
          createdAtUtc: "2026-06-19T08:30:00Z",
        },
      ],
    });

    renderSubmissionsPage();

    expect(await screen.findByText("Jane Applicant")).toBeInTheDocument();
    expect(screen.getByText("jane@example.com")).toBeInTheDocument();
    expect(screen.getByText("Example Company")).toBeInTheDocument();
    expect(screen.getByText("Draft")).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "View details for Jane Applicant" }),
    ).toHaveAttribute("href", "/submissions/submission-456");
    expect(getAccessTokenSilently).toHaveBeenCalledTimes(1);
    expect(listSubmissions).toHaveBeenCalledWith("api-access-token");
  });
});
