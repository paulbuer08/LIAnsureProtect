import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  addQuoteReferralNote,
  addQuoteReferralTask,
  adjustQuoteReferral,
  assignQuoteReferralToMe,
  approveQuoteReferral,
  acceptQuoteEvidenceRequest,
  completeQuoteReferralTask,
  createQuoteEvidenceRequest,
  declineQuoteReferral,
  followUpQuoteEvidenceRequest,
  generateAiUnderwritingReview,
  listQuoteReferralTimeline,
  listQuoteReferrals,
  releaseQuoteReferralAssignment,
  cancelQuoteEvidenceRequest,
  triageQuoteReferralOperation,
} from "../api/underwritingApi";
import { UnderwritingQuoteReferralsPage } from "./UnderwritingQuoteReferralsPage";

const getAccessTokenSilently = vi.fn();

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => ({
    getAccessTokenSilently,
  }),
}));

vi.mock("../api/underwritingApi", () => ({
  adjustQuoteReferral: vi.fn(),
  approveQuoteReferral: vi.fn(),
  acceptQuoteEvidenceRequest: vi.fn(),
  cancelQuoteEvidenceRequest: vi.fn(),
  declineQuoteReferral: vi.fn(),
  createQuoteEvidenceRequest: vi.fn(),
  generateAiUnderwritingReview: vi.fn(),
  getUnderwritingEvidenceDocumentDownloadUrl: (
    quoteId: string,
    evidenceRequestId: string,
    documentId: string,
  ) =>
    `http://localhost:5223/api/v1/underwriting/quote-referrals/${quoteId}/evidence-requests/${evidenceRequestId}/documents/${documentId}/download`,
  addQuoteReferralNote: vi.fn(),
  addQuoteReferralTask: vi.fn(),
  assignQuoteReferralToMe: vi.fn(),
  completeQuoteReferralTask: vi.fn(),
  listQuoteReferralTimeline: vi.fn(),
  listQuoteReferrals: vi.fn(),
  followUpQuoteEvidenceRequest: vi.fn(),
  releaseQuoteReferralAssignment: vi.fn(),
  triageQuoteReferralOperation: vi.fn(),
}));

function renderWorkbench() {
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
        <UnderwritingQuoteReferralsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

const severeReferral = {
  quoteId: "quote-severe",
  submissionId: "submission-severe",
  ownerUserId: "auth0|customer-1",
  premium: 42000,
  requestedLimit: 5000000,
  retention: 100000,
  riskTier: "Severe",
  status: "Referred",
  subjectivities: ["MFA evidence required.", "EDR rollout evidence required."],
  referralReasons: ["Severe cyber risk tier.", "Requested limit is high."],
  createdAtUtc: "2026-06-20T08:00:00Z",
  expiresAtUtc: "2026-06-25T08:00:00Z",
  operations: {
    assignedUnderwriterUserId: "auth0|underwriter",
    priority: "High",
    dueAtUtc: "2026-06-24T08:00:00Z",
    isSlaBreached: false,
    status: "InReview",
    openTaskCount: 1,
    latestTimelineAtUtc: "2026-06-22T09:00:00Z",
  },
  evidence: {
    openRequestCount: 1,
    respondedRequestCount: 1,
    overdueRequestCount: 1,
    nextOpenDueAtUtc: "2026-06-20T09:00:00Z",
    isWaitingForInformation: true,
    latestEvidenceActivityAtUtc: "2026-06-22T12:00:00Z",
  },
};

const moderateReferral = {
  quoteId: "quote-moderate",
  submissionId: "submission-moderate",
  ownerUserId: "auth0|customer-2",
  premium: 12000,
  requestedLimit: 1000000,
  retention: 25000,
  riskTier: "Moderate",
  status: "Referred",
  subjectivities: ["Backup evidence required."],
  referralReasons: ["Prior incident requires review."],
  createdAtUtc: "2026-06-21T08:00:00Z",
  expiresAtUtc: "2026-07-20T08:00:00Z",
  operations: {
    assignedUnderwriterUserId: null,
    priority: "Normal",
    dueAtUtc: "2026-06-26T08:00:00Z",
    isSlaBreached: false,
    status: "New",
    openTaskCount: 0,
    latestTimelineAtUtc: "2026-06-21T08:00:00Z",
  },
  evidence: {
    openRequestCount: 0,
    respondedRequestCount: 0,
    overdueRequestCount: 0,
    nextOpenDueAtUtc: null,
    isWaitingForInformation: false,
    latestEvidenceActivityAtUtc: null,
  },
};

describe("UnderwritingQuoteReferralsPage", () => {
  beforeEach(() => {
    getAccessTokenSilently.mockReset();
    getAccessTokenSilently.mockResolvedValue("underwriter-token");
    vi.mocked(adjustQuoteReferral).mockReset();
    vi.mocked(approveQuoteReferral).mockReset();
    vi.mocked(acceptQuoteEvidenceRequest).mockReset();
    vi.mocked(cancelQuoteEvidenceRequest).mockReset();
    vi.mocked(createQuoteEvidenceRequest).mockReset();
    vi.mocked(declineQuoteReferral).mockReset();
    vi.mocked(generateAiUnderwritingReview).mockReset();
    vi.mocked(followUpQuoteEvidenceRequest).mockReset();
    vi.mocked(addQuoteReferralNote).mockReset();
    vi.mocked(addQuoteReferralTask).mockReset();
    vi.mocked(assignQuoteReferralToMe).mockReset();
    vi.mocked(completeQuoteReferralTask).mockReset();
    vi.mocked(listQuoteReferralTimeline).mockReset();
    vi.mocked(listQuoteReferralTimeline).mockResolvedValue({
      quoteId: "quote-severe",
      entries: [],
    });
    vi.mocked(listQuoteReferrals).mockReset();
    vi.mocked(releaseQuoteReferralAssignment).mockReset();
    vi.mocked(triageQuoteReferralOperation).mockReset();
  });

  it("shows a loading state while referrals are loading", () => {
    vi.mocked(listQuoteReferrals).mockReturnValue(new Promise(() => {}));

    renderWorkbench();

    expect(screen.getByText("Loading referred quotes...")).toBeInTheDocument();
  });

  it("shows an empty state when there are no referred quotes", async () => {
    vi.mocked(listQuoteReferrals).mockResolvedValue({
      quoteReferrals: [],
    });

    renderWorkbench();

    expect(
      await screen.findByText("No referred quotes are waiting for review."),
    ).toBeInTheDocument();
  });

  it("shows an error state when referrals cannot be loaded", async () => {
    vi.mocked(listQuoteReferrals).mockRejectedValue(
      new Error("API request failed with 500 Internal Server Error"),
    );

    renderWorkbench();

    expect(
      await screen.findByText("API request failed with 500 Internal Server Error"),
    ).toBeInTheDocument();
  });

  it("renders referred quotes with risk, expiry, referral reasons, and subjectivities", async () => {
    vi.mocked(listQuoteReferrals).mockResolvedValue({
      quoteReferrals: [moderateReferral, severeReferral],
    });

    renderWorkbench();

    expect(await screen.findAllByText("quote-severe")).toHaveLength(2);
    expect(screen.getByText("Severe risk")).toBeInTheDocument();
    expect(screen.getByText("Expires in 2 days")).toBeInTheDocument();
    expect(screen.getByText("$42,000")).toBeInTheDocument();
    expect(screen.getByText("$5,000,000")).toBeInTheDocument();
    expect(screen.getAllByText("High priority")).toHaveLength(2);
    expect(screen.getAllByText("InReview")).toHaveLength(2);
    expect(screen.getAllByText("Assigned to auth0|underwriter")).toHaveLength(2);
    expect(screen.getAllByText("1 open task")).toHaveLength(2);
    expect(screen.getByText("Severe cyber risk tier.")).toBeInTheDocument();
    expect(screen.getByText("MFA evidence required.")).toBeInTheDocument();

    const filter = screen.getByLabelText("Queue filter");
    await userEvent.selectOptions(filter, "high");

    expect(screen.queryByText("quote-moderate")).not.toBeInTheDocument();
    expect(screen.getAllByText("quote-severe")).toHaveLength(2);
  });

  it("submits operations updates and displays the referral timeline", async () => {
    const user = userEvent.setup();
    vi.mocked(listQuoteReferrals).mockResolvedValue({
      quoteReferrals: [severeReferral],
    });
    vi.mocked(listQuoteReferralTimeline).mockResolvedValue({
      quoteId: "quote-severe",
      entries: [
        {
          entryType: "OperationCreated",
          summary: "Referral operations created with High priority.",
          createdByUserId: "system",
          createdAtUtc: "2026-06-20T08:00:00Z",
        },
      ],
    });
    vi.mocked(assignQuoteReferralToMe).mockResolvedValue({
      quoteId: "quote-severe",
      assignedUnderwriterUserId: "auth0|underwriter",
      priority: "High",
      dueAtUtc: "2026-06-24T08:00:00Z",
      isSlaBreached: false,
      status: "InReview",
      openTaskCount: 1,
      latestTimelineAtUtc: "2026-06-22T09:00:00Z",
    });
    vi.mocked(triageQuoteReferralOperation).mockResolvedValue({
      quoteId: "quote-severe",
      assignedUnderwriterUserId: "auth0|underwriter",
      priority: "Urgent",
      dueAtUtc: "2026-06-23T08:00:00Z",
      isSlaBreached: false,
      status: "WaitingForInformation",
      openTaskCount: 1,
      latestTimelineAtUtc: "2026-06-22T09:10:00Z",
    });
    vi.mocked(addQuoteReferralNote).mockResolvedValue({
      noteId: "note-1",
      quoteId: "quote-severe",
      note: "Asked broker team to confirm MFA rollout evidence.",
      createdByUserId: "auth0|underwriter",
      createdAtUtc: "2026-06-22T09:15:00Z",
    });
    vi.mocked(addQuoteReferralTask).mockResolvedValue({
      taskId: "task-1",
      quoteId: "quote-severe",
      title: "Verify MFA evidence.",
      dueAtUtc: "2026-06-23T12:00:00Z",
      isCompleted: false,
      createdByUserId: "auth0|underwriter",
      createdAtUtc: "2026-06-22T09:20:00Z",
      completedByUserId: null,
      completedAtUtc: null,
    });
    vi.mocked(completeQuoteReferralTask).mockResolvedValue({
      taskId: "task-1",
      quoteId: "quote-severe",
      title: "Verify MFA evidence.",
      dueAtUtc: "2026-06-23T12:00:00Z",
      isCompleted: true,
      createdByUserId: "auth0|underwriter",
      createdAtUtc: "2026-06-22T09:20:00Z",
      completedByUserId: "auth0|underwriter",
      completedAtUtc: "2026-06-22T09:30:00Z",
    });

    renderWorkbench();

    await screen.findAllByText("quote-severe");
    expect(await screen.findByText("Referral operations created with High priority.")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Assign to me" }));
    expect(assignQuoteReferralToMe).toHaveBeenCalledWith(
      "underwriter-token",
      "quote-severe",
    );

    await user.selectOptions(screen.getByLabelText("Operations priority"), "Urgent");
    await user.selectOptions(screen.getByLabelText("Operations status"), "WaitingForInformation");
    await user.clear(screen.getByLabelText("Operations due date"));
    await user.type(screen.getByLabelText("Operations due date"), "2026-06-23T08:00");
    await user.click(screen.getByRole("button", { name: "Save triage" }));
    expect(triageQuoteReferralOperation).toHaveBeenCalledWith(
      "underwriter-token",
      "quote-severe",
      {
        priority: "Urgent",
        status: "WaitingForInformation",
        dueAtUtc: "2026-06-23T08:00:00.000Z",
      },
    );

    await user.type(
      screen.getByLabelText("Internal work note"),
      "Asked broker team to confirm MFA rollout evidence.",
    );
    await user.click(screen.getByRole("button", { name: "Add note" }));
    expect(addQuoteReferralNote).toHaveBeenCalledWith(
      "underwriter-token",
      "quote-severe",
      { note: "Asked broker team to confirm MFA rollout evidence." },
    );

    await user.type(screen.getByLabelText("Follow-up task title"), "Verify MFA evidence.");
    await user.clear(screen.getByLabelText("Follow-up task due date"));
    await user.type(screen.getByLabelText("Follow-up task due date"), "2026-06-23T12:00");
    await user.click(screen.getByRole("button", { name: "Add task" }));
    expect(addQuoteReferralTask).toHaveBeenCalledWith(
      "underwriter-token",
      "quote-severe",
      {
        title: "Verify MFA evidence.",
        dueAtUtc: "2026-06-23T12:00:00.000Z",
      },
    );

    await user.type(screen.getByLabelText("Complete task id"), "task-1");
    await user.click(screen.getByRole("button", { name: "Complete task" }));
    expect(completeQuoteReferralTask).toHaveBeenCalledWith(
      "underwriter-token",
      "quote-severe",
      "task-1",
    );
  });

  it("creates and reviews evidence requests from the underwriting workbench", async () => {
    const user = userEvent.setup();
    vi.mocked(listQuoteReferrals).mockResolvedValue({
      quoteReferrals: [severeReferral],
    });
    vi.mocked(listQuoteReferralTimeline).mockResolvedValue({
      quoteId: "quote-severe",
      entries: [
        {
          entryType: "EvidenceRequestCreated",
          summary: "Evidence request evidence-1 created.",
          createdByUserId: "auth0|underwriter",
          createdAtUtc: "2026-06-22T09:00:00Z",
        },
      ],
    });
    vi.mocked(createQuoteEvidenceRequest).mockResolvedValue({
      evidenceRequestId: "evidence-1",
      quoteId: "quote-severe",
      submissionId: "submission-severe",
      category: "MultiFactorAuthentication",
      title: "Confirm MFA rollout",
      description: "Please provide current MFA rollout evidence.",
      dueAtUtc: "2026-06-25T09:00:00Z",
      status: "Open",
      isOverdue: false,
      daysUntilDue: 3,
      requestedByUserId: "auth0|underwriter",
      requestedAtUtc: "2026-06-22T09:00:00Z",
      respondedByUserId: null,
      respondentName: null,
      respondentTitle: null,
      responseText: null,
      attachmentFileName: null,
      attachmentContentType: null,
      attachmentSizeBytes: null,
      respondedAtUtc: null,
      acceptedByUserId: null,
      acceptedAtUtc: null,
      cancelledByUserId: null,
      cancelledAtUtc: null,
      reviewNotes: null,
      updatedAtUtc: "2026-06-22T09:00:00Z",
    });
    vi.mocked(acceptQuoteEvidenceRequest).mockResolvedValue({
      evidenceRequestId: "evidence-1",
      quoteId: "quote-severe",
      submissionId: "submission-severe",
      category: "MultiFactorAuthentication",
      title: "Confirm MFA rollout",
      description: "Please provide current MFA rollout evidence.",
      dueAtUtc: "2026-06-25T09:00:00Z",
      status: "Accepted",
      isOverdue: false,
      daysUntilDue: 3,
      requestedByUserId: "auth0|underwriter",
      requestedAtUtc: "2026-06-22T09:00:00Z",
      respondedByUserId: "auth0|customer",
      respondentName: "Jane Applicant",
      respondentTitle: "CISO",
      responseText: "MFA evidence uploaded as placeholder metadata.",
      attachmentFileName: "mfa-attestation.pdf",
      attachmentContentType: "application/pdf",
      attachmentSizeBytes: 124000,
      respondedAtUtc: "2026-06-22T12:00:00Z",
      acceptedByUserId: "auth0|underwriter",
      acceptedAtUtc: "2026-06-22T13:00:00Z",
      cancelledByUserId: null,
      cancelledAtUtc: null,
      reviewNotes: "MFA evidence is sufficient.",
      updatedAtUtc: "2026-06-22T13:00:00Z",
      documents: [
        {
          documentId: "document-1",
          originalFileName: "mfa-attestation.pdf",
          contentType: "application/pdf",
          sizeBytes: 124000,
          uploadedByUserId: "auth0|customer",
          uploadedAtUtc: "2026-06-22T12:00:00Z",
        },
      ],
    });
    vi.mocked(followUpQuoteEvidenceRequest).mockResolvedValue({
      evidenceRequestId: "evidence-1",
      quoteId: "quote-severe",
      submissionId: "submission-severe",
      category: "MultiFactorAuthentication",
      title: "Confirm MFA rollout",
      description: "Please provide current MFA rollout evidence.",
      dueAtUtc: "2026-06-20T09:00:00Z",
      status: "Open",
      isOverdue: true,
      daysUntilDue: -2,
      requestedByUserId: "auth0|underwriter",
      requestedAtUtc: "2026-06-18T09:00:00Z",
      respondedByUserId: null,
      respondentName: null,
      respondentTitle: null,
      responseText: null,
      attachmentFileName: null,
      attachmentContentType: null,
      attachmentSizeBytes: null,
      respondedAtUtc: null,
      acceptedByUserId: null,
      acceptedAtUtc: null,
      cancelledByUserId: null,
      cancelledAtUtc: null,
      reviewNotes: null,
      updatedAtUtc: "2026-06-22T09:00:00Z",
    });
    renderWorkbench();

    expect(await screen.findAllByText("1 open evidence request")).toHaveLength(2);
    expect(screen.getAllByText("1 response awaiting review")).toHaveLength(2);
    expect(screen.getAllByText("1 overdue evidence request")).toHaveLength(2);
    expect(screen.getAllByText("Next evidence due Jun 20, 2026")).toHaveLength(2);
    expect(screen.getAllByText("Waiting for information")).not.toHaveLength(0);

    await user.selectOptions(screen.getByLabelText("Evidence category"), "MultiFactorAuthentication");
    await user.type(screen.getByLabelText("Evidence request title"), "Confirm MFA rollout");
    await user.type(
      screen.getByLabelText("Evidence request description"),
      "Please provide current MFA rollout evidence.",
    );
    await user.clear(screen.getByLabelText("Evidence due date"));
    await user.type(screen.getByLabelText("Evidence due date"), "2026-06-25T09:00");
    await user.click(screen.getByRole("button", { name: "Create evidence request" }));

    expect(createQuoteEvidenceRequest).toHaveBeenCalledWith(
      "underwriter-token",
      "quote-severe",
      {
        category: "MultiFactorAuthentication",
        title: "Confirm MFA rollout",
        description: "Please provide current MFA rollout evidence.",
        dueAtUtc: "2026-06-25T09:00:00.000Z",
      },
    );
    expect(await screen.findByText("Evidence request saved: Open")).toBeInTheDocument();

    await user.type(screen.getByLabelText("Evidence request id"), "evidence-1");
    await user.type(screen.getByLabelText("Evidence review notes"), "MFA evidence is sufficient.");
    await user.click(screen.getByRole("button", { name: "Accept evidence" }));

    expect(acceptQuoteEvidenceRequest).toHaveBeenCalledWith(
      "underwriter-token",
      "quote-severe",
      "evidence-1",
      { reviewNotes: "MFA evidence is sufficient." },
    );
    const evidenceDocumentLink = await screen.findByRole("link", {
      name: "Download mfa-attestation.pdf",
    });
    expect(evidenceDocumentLink).toHaveAttribute(
      "href",
      "http://localhost:5223/api/v1/underwriting/quote-referrals/quote-severe/evidence-requests/evidence-1/documents/document-1/download",
    );

    await user.click(screen.getByRole("button", { name: "Send evidence follow-up" }));

    expect(followUpQuoteEvidenceRequest).toHaveBeenCalledWith(
      "underwriter-token",
      "quote-severe",
      "evidence-1",
    );
    expect(await screen.findByText("Evidence request saved: Open")).toBeInTheDocument();
  });

  it("requests advisory AI review and displays advisory-only output", async () => {
    const user = userEvent.setup();
    vi.mocked(listQuoteReferrals).mockResolvedValue({
      quoteReferrals: [severeReferral],
    });
    vi.mocked(generateAiUnderwritingReview).mockResolvedValue({
      reviewId: "review-123",
      quoteId: "quote-severe",
      submissionId: "submission-severe",
      status: "Succeeded",
      providerName: "LocalSimulatedAiReviewService",
      promptVersion: "cyber-underwriting-review-v1",
      outputSchemaVersion: "ai-underwriting-review-result-v1",
      inputSnapshotHash: "hash-abc",
      executiveSummary: "Advisory review for a severe cyber referral.",
      positiveRiskSignals: ["Detailed cyber application received."],
      negativeRiskSignals: ["Prior incident history needs confirmation."],
      controlGaps: ["MFA evidence is missing."],
      suggestedUnderwritingQuestions: ["Can the insured provide MFA evidence?"],
      suggestedSubjectivityCandidates: ["MFA evidence required before bind."],
      citations: ["quote.referralReasons[0]"],
      limitations: ["No document review was performed."],
      advisoryDisclaimer:
        "This AI review is advisory only and does not approve, decline, price, accept, or bind coverage.",
      failureReason: null,
      createdAtUtc: "2026-06-22T01:00:00Z",
      completedAtUtc: "2026-06-22T01:00:01Z",
    });

    renderWorkbench();

    await user.click(
      await screen.findByRole("button", { name: "Request advisory AI review" }),
    );

    expect(generateAiUnderwritingReview).toHaveBeenCalledWith(
      "underwriter-token",
      "quote-severe",
    );
    expect(
      await screen.findByText("Advisory review for a severe cyber referral."),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        "This AI review is advisory only and does not approve, decline, price, accept, or bind coverage.",
      ),
    ).toBeInTheDocument();
    expect(screen.getByText("hash-abc")).toBeInTheDocument();
  });

  it("submits approve, decline, and adjust manual decisions with the current token", async () => {
    const user = userEvent.setup();
    vi.mocked(listQuoteReferrals).mockResolvedValue({
      quoteReferrals: [severeReferral],
    });
    vi.mocked(approveQuoteReferral).mockResolvedValue({
      quoteId: "quote-severe",
      submissionId: "submission-severe",
      status: "Approved",
      premium: 42000,
      requestedLimit: 5000000,
      retention: 100000,
      reviewedByUserId: "auth0|underwriter",
      reviewedAtUtc: "2026-06-22T01:00:00Z",
      underwritingDecisionReason: "Controls acceptable after evidence review.",
      underwritingDecisionNotes: "MFA screenshots supplied.",
    });
    vi.mocked(declineQuoteReferral).mockResolvedValue({
      quoteId: "quote-severe",
      submissionId: "submission-severe",
      status: "Declined",
      premium: 42000,
      requestedLimit: 5000000,
      retention: 100000,
      reviewedByUserId: "auth0|underwriter",
      reviewedAtUtc: "2026-06-22T01:00:00Z",
      underwritingDecisionReason: "Risk is outside appetite.",
      underwritingDecisionNotes: "Prior incident severity is too high.",
    });
    vi.mocked(adjustQuoteReferral).mockResolvedValue({
      quoteId: "quote-severe",
      submissionId: "submission-severe",
      status: "Approved",
      premium: 50000,
      requestedLimit: 5000000,
      retention: 150000,
      reviewedByUserId: "auth0|underwriter",
      reviewedAtUtc: "2026-06-22T01:00:00Z",
      underwritingDecisionReason: "Adjusted terms for control maturity.",
      underwritingDecisionNotes: "Higher retention required.",
    });

    renderWorkbench();

    await screen.findAllByText("quote-severe");

    await user.type(
      screen.getByLabelText("Approval reason"),
      "Controls acceptable after evidence review.",
    );
    await user.type(screen.getByLabelText("Approval notes"), "MFA screenshots supplied.");
    await user.click(screen.getByRole("button", { name: "Approve referral" }));

    expect(approveQuoteReferral).toHaveBeenCalledWith(
      "underwriter-token",
      "quote-severe",
      {
        reason: "Controls acceptable after evidence review.",
        notes: "MFA screenshots supplied.",
      },
    );
    expect(await screen.findByText("Manual action saved: Approved")).toBeInTheDocument();
    await waitFor(() => expect(listQuoteReferrals).toHaveBeenCalledTimes(2));

    await user.type(screen.getByLabelText("Decline reason"), "Risk is outside appetite.");
    await user.type(screen.getByLabelText("Decline notes"), "Prior incident severity is too high.");
    await user.click(screen.getByRole("button", { name: "Decline referral" }));

    expect(declineQuoteReferral).toHaveBeenCalledWith(
      "underwriter-token",
      "quote-severe",
      {
        reason: "Risk is outside appetite.",
        notes: "Prior incident severity is too high.",
      },
    );

    const adjustPanel = screen.getByRole("region", { name: "Adjust referral terms" });
    await user.clear(within(adjustPanel).getByLabelText("Adjusted premium"));
    await user.type(within(adjustPanel).getByLabelText("Adjusted premium"), "50000");
    await user.clear(within(adjustPanel).getByLabelText("Adjusted retention"));
    await user.type(within(adjustPanel).getByLabelText("Adjusted retention"), "150000");
    await user.clear(within(adjustPanel).getByLabelText("Updated subjectivities"));
    await user.type(
      within(adjustPanel).getByLabelText("Updated subjectivities"),
      "Evidence of MFA and EDR required before bind.",
    );
    await user.type(
      within(adjustPanel).getByLabelText("Adjustment reason"),
      "Adjusted terms for control maturity.",
    );
    await user.type(
      within(adjustPanel).getByLabelText("Adjustment notes"),
      "Higher retention required.",
    );
    await user.click(within(adjustPanel).getByRole("button", { name: "Adjust terms" }));

    expect(adjustQuoteReferral).toHaveBeenCalledWith(
      "underwriter-token",
      "quote-severe",
      {
        adjustedPremium: 50000,
        adjustedRetention: 150000,
        updatedSubjectivities: "Evidence of MFA and EDR required before bind.",
        reason: "Adjusted terms for control maturity.",
        notes: "Higher retention required.",
      },
    );
  });
});
