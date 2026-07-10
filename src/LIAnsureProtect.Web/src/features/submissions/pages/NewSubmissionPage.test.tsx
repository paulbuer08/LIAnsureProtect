import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createSubmission } from "../api/createSubmission";
import { NewSubmissionPage } from "./NewSubmissionPage";

const getAccessTokenSilently = vi.fn();

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => ({
    getAccessTokenSilently,
    user: {
      email: "customer@example.com",
    },
  }),
}));

vi.mock("../api/createSubmission", () => ({
  createSubmission: vi.fn(),
}));

function renderNewSubmissionPage() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
      mutations: {
        retry: false,
      },
    },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <NewSubmissionPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("NewSubmissionPage", () => {
  beforeEach(() => {
    getAccessTokenSilently.mockReset();
    vi.mocked(createSubmission).mockReset();
  });

  it("renders the protected submission intake form", () => {
    renderNewSubmissionPage();

    expect(
      screen.getByRole("heading", { name: "Create submission" }),
    ).toBeInTheDocument();
    expect(screen.getByLabelText("Applicant name")).toBeInTheDocument();
    expect(screen.getByLabelText("Applicant email")).toBeInTheDocument();
    expect(screen.getByLabelText("Company name")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Create draft submission" }),
    ).toBeInTheDocument();
  });

  it("shows friendly inline validation errors for required fields and invalid email", async () => {
    const user = userEvent.setup();

    renderNewSubmissionPage();

    await user.type(screen.getByLabelText("Applicant email"), "not-an-email");
    await user.click(
      screen.getByRole("button", { name: "Create draft submission" }),
    );

    expect(
      await screen.findByText("Applicant name is required."),
    ).toBeInTheDocument();
    expect(screen.getByText("Enter a valid email address.")).toBeInTheDocument();
    expect(screen.getByText("Company name is required.")).toBeInTheDocument();
    expect(createSubmission).not.toHaveBeenCalled();
  });

  it("submits the form with the current Auth0 access token and shows the draft result", async () => {
    const user = userEvent.setup();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(createSubmission).mockResolvedValue({
      submissionId: "submission-456",
      status: "Draft",
      possibleDuplicate: false,
    });

    renderNewSubmissionPage();

    await user.type(screen.getByLabelText("Applicant name"), "Jane Applicant");
    await user.type(
      screen.getByLabelText("Applicant email"),
      "jane@example.com",
    );
    await user.type(screen.getByLabelText("Company name"), "Example Company");
    await user.click(
      screen.getByRole("button", { name: "Create draft submission" }),
    );

    expect(getAccessTokenSilently).toHaveBeenCalledTimes(1);
    expect(createSubmission).toHaveBeenCalledWith("api-access-token", {
      applicantName: "Jane Applicant",
      applicantEmail: "jane@example.com",
      companyName: "Example Company",
    });
    expect(await screen.findByText("submission-456")).toBeInTheDocument();
    expect(screen.getByText("Draft")).toBeInTheDocument();
  });

  it("warns about a possible duplicate without blocking the new draft", async () => {
    const user = userEvent.setup();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(createSubmission).mockResolvedValue({
      submissionId: "submission-duplicate",
      status: "Draft",
      possibleDuplicate: true,
    });
    renderNewSubmissionPage();

    await user.type(screen.getByLabelText("Applicant name"), "Jane Applicant");
    await user.type(screen.getByLabelText("Applicant email"), "jane@example.com");
    await user.type(screen.getByLabelText("Company name"), "Example Company");
    await user.click(screen.getByRole("button", { name: "Create draft submission" }));

    expect(await screen.findByText(/multiple legitimate submissions are allowed/i)).toBeInTheDocument();
    expect(screen.getByText("submission-duplicate")).toBeInTheDocument();
  });
});
