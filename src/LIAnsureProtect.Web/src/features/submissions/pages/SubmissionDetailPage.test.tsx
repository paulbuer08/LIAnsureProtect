import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { getSubmissionDetail } from "../api/getSubmissionDetail";
import { submitSubmission } from "../api/submitSubmission";
import { SubmissionDetailPage } from "./SubmissionDetailPage";

const getAccessTokenSilently = vi.fn();

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => ({
    getAccessTokenSilently,
  }),
}));

vi.mock("../api/getSubmissionDetail", () => ({
  getSubmissionDetail: vi.fn(),
}));

vi.mock("../api/submitSubmission", () => ({
  submitSubmission: vi.fn(),
}));

function renderSubmissionDetailPage(submissionId = "submission-456") {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[`/submissions/${submissionId}`]}>
        <Routes>
          <Route
            path="/submissions/:submissionId"
            element={<SubmissionDetailPage />}
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("SubmissionDetailPage", () => {
  beforeEach(() => {
    getAccessTokenSilently.mockReset();
    vi.mocked(getSubmissionDetail).mockReset();
    vi.mocked(submitSubmission).mockReset();
  });

  it("shows a loading state while the submission detail is loading", () => {
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(getSubmissionDetail).mockReturnValue(new Promise(() => {}));

    renderSubmissionDetailPage();

    expect(screen.getByText("Loading submission...")).toBeInTheDocument();
  });

  it("shows a not-found state when the submission does not exist", async () => {
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(getSubmissionDetail).mockRejectedValue(
      new Error("Submission was not found."),
    );

    renderSubmissionDetailPage("missing-submission");

    expect(
      await screen.findByText("Submission was not found."),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "Back to submissions" }),
    ).toHaveAttribute("href", "/submissions");
  });

  it("shows an error state when the detail cannot be loaded", async () => {
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(getSubmissionDetail).mockRejectedValue(
      new Error("API request failed with 500 Internal Server Error"),
    );

    renderSubmissionDetailPage();

    expect(
      await screen.findByText("API request failed with 500 Internal Server Error"),
    ).toBeInTheDocument();
  });

  it("renders submission detail from the protected API", async () => {
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(getSubmissionDetail).mockResolvedValue({
      submissionId: "submission-456",
      applicantName: "Jane Applicant",
      applicantEmail: "jane@example.com",
      companyName: "Example Company",
      status: "Draft",
      createdAtUtc: "2026-06-19T08:30:00Z",
    });

    renderSubmissionDetailPage();

    expect(await screen.findByText("submission-456")).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: "Submission detail" }),
    ).toBeInTheDocument();
    expect(screen.getByText("Jane Applicant")).toBeInTheDocument();
    expect(screen.getByText("jane@example.com")).toBeInTheDocument();
    expect(screen.getByText("Example Company")).toBeInTheDocument();
    expect(screen.getByText("Draft")).toBeInTheDocument();
    expect(getAccessTokenSilently).toHaveBeenCalledTimes(1);
    expect(getSubmissionDetail).toHaveBeenCalledWith(
      "api-access-token",
      "submission-456",
    );
  });

  it("submits a draft submission and updates the displayed status", async () => {
    const user = userEvent.setup();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(getSubmissionDetail).mockResolvedValue({
      submissionId: "submission-456",
      applicantName: "Jane Applicant",
      applicantEmail: "jane@example.com",
      companyName: "Example Company",
      status: "Draft",
      createdAtUtc: "2026-06-19T08:30:00Z",
    });
    vi.mocked(submitSubmission).mockResolvedValue({
      submissionId: "submission-456",
      status: "Submitted",
    });

    renderSubmissionDetailPage();

    await user.click(
      await screen.findByRole("button", { name: "Submit submission" }),
    );

    expect(submitSubmission).toHaveBeenCalledWith(
      "api-access-token",
      "submission-456",
    );
    expect(
      await screen.findByText("Submission submitted successfully."),
    ).toBeInTheDocument();
    expect(await screen.findByText("Submitted")).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Submit submission" }),
    ).not.toBeInTheDocument();
  });

  it("does not show the submit action after a submission is already submitted", async () => {
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(getSubmissionDetail).mockResolvedValue({
      submissionId: "submission-456",
      applicantName: "Jane Applicant",
      applicantEmail: "jane@example.com",
      companyName: "Example Company",
      status: "Submitted",
      createdAtUtc: "2026-06-19T08:30:00Z",
    });

    renderSubmissionDetailPage();

    expect(await screen.findByText("Submitted")).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Submit submission" }),
    ).not.toBeInTheDocument();
  });
});
