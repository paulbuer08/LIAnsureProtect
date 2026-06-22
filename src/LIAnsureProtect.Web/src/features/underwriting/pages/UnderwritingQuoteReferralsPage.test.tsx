import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  adjustQuoteReferral,
  approveQuoteReferral,
  declineQuoteReferral,
  generateAiUnderwritingReview,
  listQuoteReferrals,
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
  declineQuoteReferral: vi.fn(),
  generateAiUnderwritingReview: vi.fn(),
  listQuoteReferrals: vi.fn(),
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
  expiresAtUtc: "2026-06-24T08:00:00Z",
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
};

describe("UnderwritingQuoteReferralsPage", () => {
  beforeEach(() => {
    getAccessTokenSilently.mockReset();
    getAccessTokenSilently.mockResolvedValue("underwriter-token");
    vi.mocked(adjustQuoteReferral).mockReset();
    vi.mocked(approveQuoteReferral).mockReset();
    vi.mocked(declineQuoteReferral).mockReset();
    vi.mocked(generateAiUnderwritingReview).mockReset();
    vi.mocked(listQuoteReferrals).mockReset();
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
    expect(screen.getByText("Severe cyber risk tier.")).toBeInTheDocument();
    expect(screen.getByText("MFA evidence required.")).toBeInTheDocument();

    const filter = screen.getByLabelText("Queue filter");
    await userEvent.selectOptions(filter, "high");

    expect(screen.queryByText("quote-moderate")).not.toBeInTheDocument();
    expect(screen.getAllByText("quote-severe")).toHaveLength(2);
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
