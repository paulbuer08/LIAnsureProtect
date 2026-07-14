import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { acceptQuote } from "../api/acceptQuote";
import { bindPolicy } from "../api/bindPolicy";
import { createQuote } from "../api/createQuote";
import { deleteDraftSubmission } from "../api/deleteDraftSubmission";
import { getSubmissionDetail } from "../api/getSubmissionDetail";
import { submitSubmission } from "../api/submitSubmission";
import { updateSubmission } from "../api/updateSubmission";
import { withdrawSubmission } from "../api/withdrawSubmission";
import { SubmissionDetailPage } from "./SubmissionDetailPage";

const getAccessTokenSilently = vi.fn();

const expectedStrongControlDetails = {
  mfaCoversPrivilegedAccess: true,
  mfaCoversEmail: true,
  mfaCoversRemoteAccess: true,
  mfaCoversWorkforce: true,
  mfaPhishingResistant: false,
  edrCoveragePercent: 98,
  edrCoversServers: true,
  edrActivelyMonitored: true,
  edrTamperProtection: true,
  backupsImmutableOrOffline: true,
  backupCredentialsSeparated: true,
  restoreTestedLast12Months: true,
  recoveryPointObjectiveHours: 4,
  recoveryTimeObjectiveHours: 8,
  incidentPlanApproved: true,
  incidentPlanUpdatedLast12Months: true,
  incidentPlanTestedLast12Months: true,
  incidentRolesNamed: true,
  sensitiveDataInventoryMaintained: true,
  sensitiveDataEncrypted: true,
  sensitiveDataTypes: ["Personal data", "Credentials"],
  sensitiveDataVolume: "Moderate",
};

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

vi.mock("../api/updateSubmission", () => ({
  updateSubmission: vi.fn(),
}));

vi.mock("../api/createQuote", () => ({
  createQuote: vi.fn(),
}));

vi.mock("../api/acceptQuote", () => ({
  acceptQuote: vi.fn(),
}));

vi.mock("../api/bindPolicy", () => ({
  bindPolicy: vi.fn(),
}));

vi.mock("../api/deleteDraftSubmission", () => ({ deleteDraftSubmission: vi.fn() }));
vi.mock("../api/withdrawSubmission", () => ({ withdrawSubmission: vi.fn() }));

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
          <Route path="/submissions" element={<p>Submission list destination</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

async function completeQuoteAttestation(user: ReturnType<typeof userEvent.setup>) {
  await user.type(
    await screen.findByLabelText("Attesting person"),
    "Jane Applicant",
  );
  await user.type(screen.getByLabelText("Title or role"), "CFO");
  await user.click(screen.getByLabelText("I confirm this attestation."));
}

describe("SubmissionDetailPage", () => {
  beforeEach(() => {
    getAccessTokenSilently.mockReset();
    vi.mocked(getSubmissionDetail).mockReset();
    vi.mocked(submitSubmission).mockReset();
    vi.mocked(updateSubmission).mockReset();
    vi.mocked(createQuote).mockReset();
    vi.mocked(acceptQuote).mockReset();
    vi.mocked(bindPolicy).mockReset();
    vi.mocked(deleteDraftSubmission).mockReset();
    vi.mocked(withdrawSubmission).mockReset();
    vi.restoreAllMocks();
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
      await screen.findByText("Unable to load submission."),
    ).toBeInTheDocument();
    expect(screen.queryByText("Submission was not found.")).not.toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "Submissions" }),
    ).toHaveAttribute("href", "/submissions");
  });

  it("shows an error state when the detail cannot be loaded", async () => {
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(getSubmissionDetail).mockRejectedValue(
      new Error("API request failed with 500 Internal Server Error"),
    );

    renderSubmissionDetailPage();

    expect(
      await screen.findByText("Unable to load submission."),
    ).toBeInTheDocument();
    expect(
      screen.queryByText("API request failed with 500 Internal Server Error"),
    ).not.toBeInTheDocument();
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

  it("uses an accessible confirmation dialog before deleting an owned draft", async () => {
    const user = userEvent.setup();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(deleteDraftSubmission).mockResolvedValue(undefined);
    vi.mocked(getSubmissionDetail).mockResolvedValue({
      submissionId: "submission-456", applicantName: "Jane Applicant", applicantEmail: "jane@example.com",
      companyName: "Example Company", status: "Draft", createdAtUtc: "2026-06-19T08:30:00Z",
    });

    renderSubmissionDetailPage();
    await user.click(await screen.findByRole("button", { name: "Delete draft" }));

    const dialog = screen.getByRole("dialog", { name: "Delete this draft?" });
    expect(dialog).toHaveTextContent(
      "This permanently deletes the draft for Example Company.",
    );
    expect(dialog).toHaveTextContent("Why can this draft be deleted?");
    expect(dialog).toHaveTextContent(
      /after you select Submit submission and the system accepts it/i,
    );
    expect(dialog).toHaveTextContent(/may only be withdrawn/i);
    expect(deleteDraftSubmission).not.toHaveBeenCalled();

    await user.click(within(dialog).getByRole("button", { name: "Delete draft" }));

    expect(deleteDraftSubmission).toHaveBeenCalledWith("api-access-token", "submission-456");
  });

  it("cancels draft deletion without calling the API", async () => {
    const user = userEvent.setup();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(getSubmissionDetail).mockResolvedValue({
      submissionId: "submission-456", applicantName: "Jane Applicant", applicantEmail: "jane@example.com",
      companyName: "Example Company", status: "Draft", createdAtUtc: "2026-06-19T08:30:00Z",
    });

    renderSubmissionDetailPage();
    await user.click(await screen.findByRole("button", { name: "Delete draft" }));
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(deleteDraftSubmission).not.toHaveBeenCalled();
  });

  it("closes the dialog and shows the API error when draft deletion fails", async () => {
    const user = userEvent.setup();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(deleteDraftSubmission).mockRejectedValue(
      new Error("Draft could not be deleted."),
    );
    vi.mocked(getSubmissionDetail).mockResolvedValue({
      submissionId: "submission-456", applicantName: "Jane Applicant", applicantEmail: "jane@example.com",
      companyName: "Example Company", status: "Draft", createdAtUtc: "2026-06-19T08:30:00Z",
    });

    renderSubmissionDetailPage();
    await user.click(await screen.findByRole("button", { name: "Delete draft" }));
    const dialog = screen.getByRole("dialog", { name: "Delete this draft?" });
    await user.click(within(dialog).getByRole("button", { name: "Delete draft" }));

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(await screen.findByText("Unable to load submission.")).toBeInTheDocument();
    expect(screen.queryByText("Draft could not be deleted.")).not.toBeInTheDocument();
  });

  it("offers withdrawal for an eligible submitted application", async () => {
    const user = userEvent.setup();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(withdrawSubmission).mockResolvedValue({ submissionId: "submission-456", status: "Withdrawn" });
    vi.mocked(getSubmissionDetail).mockResolvedValue({
      submissionId: "submission-456", applicantName: "Jane Applicant", applicantEmail: "jane@example.com",
      companyName: "Example Company", status: "Submitted", createdAtUtc: "2026-06-19T08:30:00Z",
    });

    renderSubmissionDetailPage();
    await user.click(await screen.findByRole("button", { name: "Withdraw submission" }));

    const dialog = screen.getByRole("dialog", {
      name: "Withdraw this application?",
    });
    expect(dialog).toHaveTextContent(
      "Why is withdrawal different from deletion?",
    );
    expect(dialog).toHaveTextContent(/without erasing the record/i);
    expect(withdrawSubmission).not.toHaveBeenCalled();

    await user.click(
      within(dialog).getByRole("button", { name: "Withdraw application" }),
    );

    expect(withdrawSubmission).toHaveBeenCalledWith("api-access-token", "submission-456");
    expect(await screen.findByText(/record remains available as audit history/i)).toBeInTheDocument();
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

  it("lets a customer edit draft details before submitting", async () => {
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
    vi.mocked(updateSubmission).mockResolvedValue({
      submissionId: "submission-456",
      applicantName: "Updated Applicant",
      applicantEmail: "updated@example.com",
      companyName: "Updated Company",
      status: "Draft",
      createdAtUtc: "2026-06-19T08:30:00Z",
    });

    renderSubmissionDetailPage();

    await user.click(
      await screen.findByRole("button", { name: "Edit draft details" }),
    );
    await user.clear(screen.getByLabelText("Applicant name"));
    await user.type(screen.getByLabelText("Applicant name"), "Updated Applicant");
    await user.clear(screen.getByLabelText("Applicant email"));
    await user.type(
      screen.getByLabelText("Applicant email"),
      "updated@example.com",
    );
    await user.clear(screen.getByLabelText("Company name"));
    await user.type(screen.getByLabelText("Company name"), "Updated Company");
    await user.click(screen.getByRole("button", { name: "Save changes" }));

    expect(updateSubmission).toHaveBeenCalledWith(
      "api-access-token",
      "submission-456",
      {
        applicantName: "Updated Applicant",
        applicantEmail: "updated@example.com",
        companyName: "Updated Company",
      },
    );
    expect(await screen.findByText("Draft details updated.")).toBeInTheDocument();
    expect(screen.getByText("Updated Applicant")).toBeInTheDocument();
    expect(screen.getByText("updated@example.com")).toBeInTheDocument();
    expect(screen.getByText("Updated Company")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Submit submission" }),
    ).toBeInTheDocument();
  });

  it("keeps the draft unchanged when edit mode is canceled", async () => {
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

    renderSubmissionDetailPage();

    await user.click(
      await screen.findByRole("button", { name: "Edit draft details" }),
    );
    await user.clear(screen.getByLabelText("Applicant name"));
    await user.type(screen.getByLabelText("Applicant name"), "Discarded Name");
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    expect(updateSubmission).not.toHaveBeenCalled();
    expect(screen.getByText("Jane Applicant")).toBeInTheDocument();
    expect(
      screen.queryByDisplayValue("Discarded Name"),
    ).not.toBeInTheDocument();
  });

  it("shows quote generation after a submission is already submitted", async () => {
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
    expect(
      screen.queryByRole("button", { name: "Edit draft details" }),
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Generate quote" }),
    ).toBeInTheDocument();
  });

  it("generates a quote for a submitted submission", async () => {
    const user = userEvent.setup();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(getSubmissionDetail).mockResolvedValue({
      submissionId: "submission-456",
      applicantName: "Jane Applicant",
      applicantEmail: "jane@example.com",
      companyName: "Example Company",
      status: "Submitted",
      createdAtUtc: "2026-06-19T08:30:00Z",
    });
    vi.mocked(createQuote).mockResolvedValue({
      quoteId: "quote-123",
      submissionId: "submission-456",
      premium: 6500,
      requestedLimit: 1000000,
      retention: 10000,
      riskTier: "Low",
      status: "Quoted",
      subjectivities: ["Maintain MFA for privileged accounts."],
      referralReasons: [],
      expiresAtUtc: "2026-07-19T08:30:00Z",
      providerIndication: {
        providerName: "LocalSimulatedRatingProvider",
        status: "Succeeded",
        marketDisposition: "Quoted",
        providerReference: "provider-reference",
        providerQuoteNumber: "provider-quote",
        indicatedPremium: 6500,
        indicatedLimit: 1000000,
        indicatedRetention: 10000,
        httpStatusCode: 200,
        failureCategory: "None",
        failureReason: null,
        attemptCount: 1,
        durationMs: 12,
      },
    });

    renderSubmissionDetailPage();

    await completeQuoteAttestation(user);

    await user.click(
      await screen.findByRole("button", { name: "Generate quote" }),
    );

    expect(createQuote).toHaveBeenCalledWith(
      "api-access-token",
      "submission-456",
      {
        industryClass: "ProfessionalServices",
        annualRevenueBand: "From10MTo50M",
        requestedLimit: 1000000,
        retention: 10000,
        mfaStatus: "Implemented",
        edrStatus: "Implemented",
        backupMaturity: "Mature",
        hasIncidentResponsePlan: true,
        priorCyberIncidents: 0,
        sensitiveDataExposure: "Moderate",
        otherIndustryDescription: null,
        priorCyberIncidentTypes: null,
        priorCyberIncidentDetails: null,
        attestationAccepted: true,
        attestedByName: "Jane Applicant",
        attestedByTitle: "CFO",
        isReassessment: false,
        controlDetails: expectedStrongControlDetails,
      },
    );
    expect(await screen.findByText("quote-123")).toBeInTheDocument();
    expect(screen.getByText("₱6,500")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Accept quote" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Generate quote" }),
    ).not.toBeInTheDocument();
  });

  it("shows an existing latest quote from the submission detail without regenerating it", async () => {
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(getSubmissionDetail).mockResolvedValue({
      submissionId: "submission-456",
      applicantName: "Jane Applicant",
      applicantEmail: "jane@example.com",
      companyName: "Example Company",
      status: "Submitted",
      createdAtUtc: "2026-06-19T08:30:00Z",
      latestQuote: {
        quoteId: "quote-existing",
        premium: 6500,
        requestedLimit: 1000000,
        retention: 10000,
        riskTier: "Low",
        status: "Quoted",
        subjectivities: ["Maintain MFA for privileged accounts."],
        referralReasons: [],
        expiresAtUtc: "2026-07-19T08:30:00Z",
      },
    });

    renderSubmissionDetailPage();

    expect(await screen.findByText("quote-existing")).toBeInTheDocument();
    expect(screen.getByText("₱6,500")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Accept quote" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Generate quote" }),
    ).not.toBeInTheDocument();
    expect(createQuote).not.toHaveBeenCalled();
  });

  it("offers a versioned reassessment before quote acceptance", async () => {
    const user = userEvent.setup();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(getSubmissionDetail).mockResolvedValue({
      submissionId: "submission-456",
      applicantName: "Jane Applicant",
      applicantEmail: "jane@example.com",
      companyName: "Example Company",
      status: "Submitted",
      createdAtUtc: "2026-06-19T08:30:00Z",
      latestQuote: {
        quoteId: "quote-existing",
        premium: 6500,
        requestedLimit: 1000000,
        retention: 10000,
        riskTier: "Low",
        status: "Quoted",
        subjectivities: [],
        referralReasons: [],
        expiresAtUtc: "2026-07-19T08:30:00Z",
        version: 1,
        assuranceStatus: "EvidenceRequired",
        evidenceRequiredCount: 1,
        evidenceSatisfiedCount: 0,
      },
    });

    renderSubmissionDetailPage();
    await user.click(
      await screen.findByRole("button", { name: "Reassess controls" }),
    );

    expect(
      screen.getByRole("heading", { name: "Reassess quote" }),
    ).toBeInTheDocument();
    expect(screen.getByText(/new quote version will preserve/i)).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Create reassessment" }),
    ).toBeDisabled();

    await user.click(screen.getByRole("button", { name: "Cancel reassessment" }));

    expect(
      screen.queryByRole("heading", { name: "Reassess quote" }),
    ).not.toBeInTheDocument();
    expect(screen.getByText("quote-existing")).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("confirms before discarding changed reassessment answers", async () => {
    const user = userEvent.setup();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(getSubmissionDetail).mockResolvedValue({
      submissionId: "submission-456",
      applicantName: "Jane Applicant",
      applicantEmail: "jane@example.com",
      companyName: "Example Company",
      status: "Submitted",
      createdAtUtc: "2026-06-19T08:30:00Z",
      latestQuote: {
        quoteId: "quote-existing",
        premium: 6500,
        requestedLimit: 1000000,
        retention: 10000,
        riskTier: "Low",
        status: "Quoted",
        subjectivities: [],
        referralReasons: [],
        expiresAtUtc: "2026-07-19T08:30:00Z",
        version: 1,
        assuranceStatus: "EvidenceRequired",
        evidenceRequiredCount: 1,
        evidenceSatisfiedCount: 0,
      },
    });

    renderSubmissionDetailPage();
    await user.click(
      await screen.findByRole("button", { name: "Reassess controls" }),
    );
    await user.selectOptions(screen.getByLabelText("MFA status"), "Partial");
    await completeQuoteAttestation(user);

    expect(screen.getByRole("button", { name: "Create reassessment" })).toBeEnabled();

    await user.click(screen.getByRole("button", { name: "Cancel reassessment" }));
    const dialog = screen.getByRole("dialog", {
      name: "Discard reassessment changes?",
    });
    expect(dialog).toHaveTextContent("It does not change or delete the current quote.");
    expect(dialog).toHaveTextContent("Your current quote stays unchanged");

    await user.click(within(dialog).getByRole("button", { name: "Discard changes" }));

    expect(
      screen.queryByRole("heading", { name: "Reassess quote" }),
    ).not.toBeInTheDocument();
    expect(createQuote).not.toHaveBeenCalled();
    expect(screen.getByText("quote-existing")).toBeInTheDocument();
  });

  it("accepts a generated quote and then shows the bind action", async () => {
    const user = userEvent.setup();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(getSubmissionDetail).mockResolvedValue({
      submissionId: "submission-456",
      applicantName: "Jane Applicant",
      applicantEmail: "jane@example.com",
      companyName: "Example Company",
      status: "Submitted",
      createdAtUtc: "2026-06-19T08:30:00Z",
    });
    vi.mocked(createQuote).mockResolvedValue({
      quoteId: "quote-123",
      submissionId: "submission-456",
      premium: 6500,
      requestedLimit: 1000000,
      retention: 10000,
      riskTier: "Low",
      status: "Quoted",
      subjectivities: ["Maintain MFA for privileged accounts."],
      referralReasons: [],
      expiresAtUtc: "2026-07-19T08:30:00Z",
      providerIndication: {
        providerName: "LocalSimulatedRatingProvider",
        status: "Succeeded",
        marketDisposition: "Quoted",
        providerReference: "provider-reference",
        providerQuoteNumber: "provider-quote",
        indicatedPremium: 6500,
        indicatedLimit: 1000000,
        indicatedRetention: 10000,
        httpStatusCode: 200,
        failureCategory: "None",
        failureReason: null,
        attemptCount: 1,
        durationMs: 12,
      },
    });
    vi.mocked(acceptQuote).mockResolvedValue({
      quoteId: "quote-123",
      submissionId: "submission-456",
      status: "Accepted",
      premium: 6500,
      requestedLimit: 1000000,
      retention: 10000,
      subjectivities: "Maintain MFA for privileged accounts.",
      expiresAtUtc: "2026-07-19T08:30:00Z",
      acceptedByUserId: "customer-1",
      acceptedByName: "Jane Applicant",
      acceptedByTitle: "CFO",
      subjectivitiesAcknowledged: true,
      acceptedAtUtc: "2026-06-19T08:40:00Z",
    });

    renderSubmissionDetailPage();

    await completeQuoteAttestation(user);

    await user.click(
      await screen.findByRole("button", { name: "Generate quote" }),
    );
    await user.click(
      await screen.findByLabelText(
        "I acknowledge the quote subjectivities and understand they must be satisfied before binding.",
      ),
    );
    await user.click(await screen.findByRole("button", { name: "Accept quote" }));

    expect(acceptQuote).toHaveBeenCalledWith("api-access-token", "quote-123", {
      acceptedByName: "Jane Applicant",
      acceptedByTitle: "CFO",
      subjectivitiesAcknowledged: true,
    });
    expect(
      await screen.findByText("Quote accepted successfully."),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Bind policy" }),
    ).toBeInTheDocument();
  });

  it("binds an accepted quote and displays the policy number", async () => {
    const user = userEvent.setup();
    getAccessTokenSilently.mockResolvedValue("api-access-token");
    vi.mocked(getSubmissionDetail).mockResolvedValue({
      submissionId: "submission-456",
      applicantName: "Jane Applicant",
      applicantEmail: "jane@example.com",
      companyName: "Example Company",
      status: "Submitted",
      createdAtUtc: "2026-06-19T08:30:00Z",
    });
    vi.mocked(createQuote).mockResolvedValue({
      quoteId: "quote-123",
      submissionId: "submission-456",
      premium: 6500,
      requestedLimit: 1000000,
      retention: 10000,
      riskTier: "Low",
      status: "Quoted",
      subjectivities: ["Maintain MFA for privileged accounts."],
      referralReasons: [],
      expiresAtUtc: "2026-07-19T08:30:00Z",
      providerIndication: {
        providerName: "LocalSimulatedRatingProvider",
        status: "Succeeded",
        marketDisposition: "Quoted",
        providerReference: "provider-reference",
        providerQuoteNumber: "provider-quote",
        indicatedPremium: 6500,
        indicatedLimit: 1000000,
        indicatedRetention: 10000,
        httpStatusCode: 200,
        failureCategory: "None",
        failureReason: null,
        attemptCount: 1,
        durationMs: 12,
      },
    });
    vi.mocked(acceptQuote).mockResolvedValue({
      quoteId: "quote-123",
      submissionId: "submission-456",
      status: "Accepted",
      premium: 6500,
      requestedLimit: 1000000,
      retention: 10000,
      subjectivities: "Maintain MFA for privileged accounts.",
      expiresAtUtc: "2026-07-19T08:30:00Z",
      acceptedByUserId: "customer-1",
      acceptedByName: "Jane Applicant",
      acceptedByTitle: "CFO",
      subjectivitiesAcknowledged: true,
      acceptedAtUtc: "2026-06-19T08:40:00Z",
    });
    vi.mocked(bindPolicy).mockResolvedValue({
      policyId: "policy-789",
      policyNumber: "LIA-CYB-2026-0001",
      quoteId: "quote-123",
      submissionId: "submission-456",
      status: "Bound",
      premium: 6500,
      requestedLimit: 1000000,
      retention: 10000,
      effectiveDateUtc: "2026-06-20T00:00:00Z",
      expirationDateUtc: "2027-06-20T00:00:00Z",
      boundByUserId: "customer-1",
      boundAtUtc: "2026-06-19T08:45:00Z",
      bindingProviderName: "LocalSimulatedPolicyBindingProvider",
      bindingReference: "BIND-123",
    });

    renderSubmissionDetailPage();

    await completeQuoteAttestation(user);

    await user.click(
      await screen.findByRole("button", { name: "Generate quote" }),
    );
    await user.click(
      await screen.findByLabelText(
        "I acknowledge the quote subjectivities and understand they must be satisfied before binding.",
      ),
    );
    await user.click(await screen.findByRole("button", { name: "Accept quote" }));
    await user.click(await screen.findByRole("button", { name: "Bind policy" }));

    expect(bindPolicy).toHaveBeenCalledWith(
      "api-access-token",
      "quote-123",
      {
        effectiveDateUtc: expect.any(String),
      },
    );
    expect(await screen.findByText("Policy bound")).toBeInTheDocument();
    expect(screen.getByText("LIA-CYB-2026-0001")).toBeInTheDocument();
  });
});
