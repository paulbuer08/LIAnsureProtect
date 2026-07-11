import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router";
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
      <MemoryRouter initialEntries={["/submissions/new"]}>
        <Routes>
          <Route path="/submissions/new" element={<NewSubmissionPage />} />
          <Route
            path="/submissions/:submissionId"
            element={<p>Draft detail destination</p>}
          />
        </Routes>
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
      screen.getByRole("heading", { name: "Create draft submission" }),
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

  it("creates a draft with an idempotency key and navigates to its detail", async () => {
    const user = userEvent.setup();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(createSubmission).mockResolvedValue({
      submissionId: "submission-456",
      status: "Draft",
      possibleDuplicate: false,
      existingDraft: false,
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
    expect(createSubmission).toHaveBeenCalledWith(
      "api-access-token",
      {
        applicantName: "Jane Applicant",
        applicantEmail: "jane@example.com",
        companyName: "Example Company",
        createAnotherDraft: false,
      },
      expect.any(String),
    );
    expect(await screen.findByText("Draft detail destination")).toBeInTheDocument();
  });

  it("offers the matching draft before explicitly creating another one", async () => {
    const user = userEvent.setup();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(createSubmission)
      .mockResolvedValueOnce({
        submissionId: "submission-existing",
        status: "Draft",
        possibleDuplicate: true,
        existingDraft: true,
      })
      .mockResolvedValueOnce({
        submissionId: "submission-new",
        status: "Draft",
        possibleDuplicate: true,
        existingDraft: false,
      });
    renderNewSubmissionPage();

    await user.type(screen.getByLabelText("Applicant name"), "Jane Applicant");
    await user.type(screen.getByLabelText("Applicant email"), "jane@example.com");
    await user.type(screen.getByLabelText("Company name"), "Example Company");
    await user.click(screen.getByRole("button", { name: "Create draft submission" }));

    expect(
      await screen.findByRole("heading", { name: "A matching draft already exists" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Continue existing draft" }),
    ).toBeInTheDocument();

    await user.click(
      screen.getByRole("button", { name: "Create another draft anyway" }),
    );

    expect(createSubmission).toHaveBeenLastCalledWith(
      "api-access-token",
      {
        applicantName: "Jane Applicant",
        applicantEmail: "jane@example.com",
        companyName: "Example Company",
        createAnotherDraft: true,
      },
      expect.any(String),
    );
    expect(await screen.findByText("Draft detail destination")).toBeInTheDocument();
  });

  it("reuses the idempotency key when the same failed form attempt is retried", async () => {
    const user = userEvent.setup();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(createSubmission)
      .mockRejectedValueOnce(new Error("Temporary network failure"))
      .mockResolvedValueOnce({
        submissionId: "submission-retry",
        status: "Draft",
        possibleDuplicate: false,
        existingDraft: false,
      });

    renderNewSubmissionPage();
    await user.type(screen.getByLabelText("Applicant name"), "Jane Applicant");
    await user.type(screen.getByLabelText("Applicant email"), "jane@example.com");
    await user.type(screen.getByLabelText("Company name"), "Example Company");
    await user.click(screen.getByRole("button", { name: "Create draft submission" }));
    expect(await screen.findByText("Temporary network failure")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Create draft submission" }));

    const firstKey = vi.mocked(createSubmission).mock.calls[0][2];
    const secondKey = vi.mocked(createSubmission).mock.calls[1][2];
    expect(firstKey).toBe(secondKey);
    expect(await screen.findByText("Draft detail destination")).toBeInTheDocument();
  });
});
