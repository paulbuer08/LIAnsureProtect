import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  acceptClaim,
  addClaimWorkNote,
  assignClaimToMe,
  closeClaim,
  denyClaim,
  getAdjudicationDetail,
  listAdjudicationQueue,
  releaseClaimAssignment,
  requestClaimInformation,
  setClaimReserve,
} from "../api/claimsApi";
import type { ClaimAdjudicationDetail } from "../types";
import { ClaimsAdjudicationPage } from "./ClaimsAdjudicationPage";
import { ApiError } from "../../../lib/apiClient";

const getAccessTokenSilently = vi.fn();

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => ({
    getAccessTokenSilently,
    user: { sub: "auth0|adjuster-1" },
  }),
}));

vi.mock("../api/claimsApi", () => ({
  acceptClaim: vi.fn(),
  addClaimWorkNote: vi.fn(),
  assignClaimToMe: vi.fn(),
  closeClaim: vi.fn(),
  denyClaim: vi.fn(),
  downloadAdjudicationClaimDocument: vi.fn(),
  getAdjudicationDetail: vi.fn(),
  listAdjudicationQueue: vi.fn(),
  releaseClaimAssignment: vi.fn(),
  requestClaimInformation: vi.fn(),
  setClaimReserve: vi.fn(),
}));

function renderWorkbench(initialEntries = ["/claims/adjudication"]) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={initialEntries}>
        <ClaimsAdjudicationPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

const queueItem = {
  claimId: "claim-1",
  claimNumber: "CLM-CYB-20260401-AAAAAAAA",
  policyId: "policy-1",
  policyNumber: "LIP-CYB-20260101-BBBBBBBB",
  incidentType: "RansomwareExtortion",
  incidentAtUtc: "2026-03-10T08:00:00Z",
  status: "Filed",
  assignedAdjusterUserId: null,
  openInformationRequestCount: 0,
  filedAtUtc: "2026-03-13T10:00:00Z",
  updatedAtUtc: "2026-03-13T10:00:00Z",
};

const detail: ClaimAdjudicationDetail = {
  claimId: "claim-1",
  claimNumber: "CLM-CYB-20260401-AAAAAAAA",
  policyId: "policy-1",
  policyNumber: "LIP-CYB-20260101-BBBBBBBB",
  ownerUserId: "auth0|customer-1",
  incidentType: "RansomwareExtortion",
  incidentAtUtc: "2026-03-10T08:00:00Z",
  discoveredAtUtc: "2026-03-12T09:30:00Z",
  description: "Ransomware encrypted the file server.",
  status: "UnderReview",
  assignedAdjusterUserId: "auth0|adjuster-1",
  claimedAmount: 250000,
  reserveAmount: 150000,
  paidAmount: 0,
  settlementAmount: null,
  denialReason: null,
  denialNarrative: null,
  decidedAtUtc: null,
  closedAtUtc: null,
  policyLimitAtFiling: 1000000,
  policyRetentionAtFiling: 25000,
  policyEffectiveAtFiling: "2026-01-01T00:00:00Z",
  policyExpirationAtFiling: "2027-01-01T00:00:00Z",
  filedAtUtc: "2026-03-13T10:00:00Z",
  updatedAtUtc: "2026-03-14T10:00:00Z",
  reserveHistory: [
    {
      changeId: "change-1",
      oldAmount: 0,
      newAmount: 150000,
      reason: "Initial estimate.",
      changedByUserId: "auth0|adjuster-1",
      changedAtUtc: "2026-03-14T10:00:00Z",
    },
  ],
  decisions: [],
  workNotes: [],
  informationRequests: [],
  documents: [],
  timeline: [
    {
      entryId: "entry-1",
      entryType: "ClaimFiled",
      summary: "Claim filed for RansomwareExtortion.",
      createdByUserId: "auth0|customer-1",
      createdAtUtc: "2026-03-13T10:00:00Z",
    },
  ],
};

describe("ClaimsAdjudicationPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getAccessTokenSilently.mockResolvedValue("token-1");
    vi.mocked(listAdjudicationQueue).mockResolvedValue({
      claims: [queueItem],
    });
    vi.mocked(getAdjudicationDetail).mockResolvedValue(detail);
  });

  it("lists the queue and opens a claim's working file", async () => {
    const user = userEvent.setup();

    renderWorkbench();

    await user.click(
      await screen.findByRole("button", {
        name: /open claim CLM-CYB-20260401-AAAAAAAA/i,
      }),
    );

    expect(
      await screen.findByText("Ransomware encrypted the file server."),
    ).toBeInTheDocument();
    expect(screen.getByText("Initial estimate.")).toBeInTheDocument();
  });

  it("opens an exact claim from the notification deep-link query", async () => {
    renderWorkbench(["/claims/adjudication?claimId=claim-1"]);

    expect(
      await screen.findByText("Ransomware encrypted the file server."),
    ).toBeInTheDocument();
    expect(getAdjudicationDetail).toHaveBeenCalledWith("token-1", "claim-1");
  });

  it("claims the file with assign-to-me", async () => {
    const user = userEvent.setup();
    vi.mocked(assignClaimToMe).mockResolvedValue({
      claimId: "claim-1",
      claimNumber: "CLM-CYB-20260401-AAAAAAAA",
      policyId: "policy-1",
      policyNumber: "LIP-CYB-20260101-BBBBBBBB",
      incidentType: "RansomwareExtortion",
      incidentAtUtc: "2026-03-10T08:00:00Z",
      status: "UnderReview",
      assignedAdjusterUserId: "auth0|adjuster-1",
      openInformationRequestCount: 0,
      filedAtUtc: "2026-03-13T10:00:00Z",
      updatedAtUtc: "2026-03-14T10:00:00Z",
    });

    renderWorkbench();

    await user.click(
      await screen.findByRole("button", {
        name: /open claim CLM-CYB-20260401-AAAAAAAA/i,
      }),
    );
    await user.click(
      await screen.findByRole("button", { name: /assign to me/i }),
    );

    await waitFor(() => {
      expect(assignClaimToMe).toHaveBeenCalledWith("token-1", "claim-1");
    });
  });

  it("shows the losing adjuster the conflict and refetches the truth", async () => {
    const user = userEvent.setup();
    vi.mocked(assignClaimToMe).mockRejectedValue(
      new ApiError(
        "This claim is already assigned to another adjuster.",
        409,
        "claim.assignment.conflict",
      ),
    );

    renderWorkbench();

    await user.click(
      await screen.findByRole("button", {
        name: /open claim CLM-CYB-20260401-AAAAAAAA/i,
      }),
    );
    await user.click(
      await screen.findByRole("button", { name: /assign to me/i }),
    );

    expect(
      await screen.findByText(/already assigned to another adjuster/i),
    ).toBeInTheDocument();
    // The queue is refetched so the loser sees the real assignee (M44.5 UX).
    await waitFor(() => {
      expect(listAdjudicationQueue).toHaveBeenCalledTimes(2);
    });
  });

  it("sets the reserve with a reason", async () => {
    const user = userEvent.setup();
    vi.mocked(setClaimReserve).mockResolvedValue({
      claimId: "claim-1",
      claimedAmount: 250000,
      reserveAmount: 90000,
      paidAmount: 0,
      policyLimitAtFiling: 1000000,
      policyRetentionAtFiling: 25000,
    });

    renderWorkbench();

    await user.click(
      await screen.findByRole("button", {
        name: /open claim CLM-CYB-20260401-AAAAAAAA/i,
      }),
    );
    const amountInput = await screen.findByLabelText(/reserve amount/i);
    await user.clear(amountInput);
    await user.type(amountInput, "90000");
    await user.type(
      screen.getByLabelText(/reserve reason/i),
      "Backups recovered; exposure reduced.",
    );
    await user.click(screen.getByRole("button", { name: /update reserve/i }));

    await waitFor(() => {
      expect(setClaimReserve).toHaveBeenCalledWith("token-1", "claim-1", {
        amount: 90000,
        reason: "Backups recovered; exposure reduced.",
      });
    });
  });

  it("requests information from the claimant", async () => {
    const user = userEvent.setup();
    vi.mocked(requestClaimInformation).mockResolvedValue({
      informationRequestId: "request-1",
      claimId: "claim-1",
      title: "Proof of loss",
      message: "Please provide the forensic report.",
      requestedByUserId: "auth0|adjuster-1",
      requestedAtUtc: "2026-03-15T10:00:00Z",
      isAnswered: false,
      responseText: null,
      respondedByUserId: null,
      respondedAtUtc: null,
    });

    renderWorkbench();

    await user.click(
      await screen.findByRole("button", {
        name: /open claim CLM-CYB-20260401-AAAAAAAA/i,
      }),
    );
    await user.type(
      await screen.findByLabelText(/information request title/i),
      "Proof of loss",
    );
    await user.type(
      screen.getByLabelText(/information request message/i),
      "Please provide the forensic report.",
    );
    await user.click(
      screen.getByRole("button", { name: /send information request/i }),
    );

    await waitFor(() => {
      expect(requestClaimInformation).toHaveBeenCalledWith(
        "token-1",
        "claim-1",
        {
          title: "Proof of loss",
          message: "Please provide the forensic report.",
        },
      );
    });
  });

  it("accepts the claim with a settlement", async () => {
    const user = userEvent.setup();
    vi.mocked(acceptClaim).mockResolvedValue({
      claimId: "claim-1",
      claimNumber: "CLM-CYB-20260401-AAAAAAAA",
      status: "Accepted",
      outcome: "Accepted",
      settlementAmount: 300000,
      paidAmount: 300000,
      denialReason: null,
      reason: "Covered loss.",
      notes: null,
      claimedAmountAtDecision: 250000,
      reserveAmountAtDecision: 150000,
      decidedByUserId: "auth0|adjuster-1",
      decidedAtUtc: "2026-03-20T10:00:00Z",
    });

    renderWorkbench();

    await user.click(
      await screen.findByRole("button", {
        name: /open claim CLM-CYB-20260401-AAAAAAAA/i,
      }),
    );
    const settlementInput = await screen.findByLabelText(/settlement amount/i);
    await user.type(settlementInput, "300000");
    await user.type(
      screen.getByLabelText(/acceptance reason/i),
      "Covered loss.",
    );
    await user.click(screen.getByRole("button", { name: /accept claim/i }));

    await waitFor(() => {
      expect(acceptClaim).toHaveBeenCalledWith("token-1", "claim-1", {
        settlementAmount: 300000,
        reason: "Covered loss.",
        notes: null,
      });
    });
  });

  it("denies the claim with a category and narrative", async () => {
    const user = userEvent.setup();
    vi.mocked(denyClaim).mockResolvedValue({
      claimId: "claim-1",
      claimNumber: "CLM-CYB-20260401-AAAAAAAA",
      status: "Denied",
      outcome: "Denied",
      settlementAmount: null,
      paidAmount: 0,
      denialReason: "PolicyExclusion",
      reason: "War exclusion applies.",
      notes: null,
      claimedAmountAtDecision: 250000,
      reserveAmountAtDecision: 150000,
      decidedByUserId: "auth0|adjuster-1",
      decidedAtUtc: "2026-03-20T10:00:00Z",
    });

    renderWorkbench();

    await user.click(
      await screen.findByRole("button", {
        name: /open claim CLM-CYB-20260401-AAAAAAAA/i,
      }),
    );
    await user.selectOptions(
      await screen.findByLabelText(/denial reason/i),
      "PolicyExclusion",
    );
    await user.type(
      screen.getByLabelText(/denial narrative/i),
      "War exclusion applies.",
    );
    await user.click(screen.getByRole("button", { name: /deny claim/i }));

    await waitFor(() => {
      expect(denyClaim).toHaveBeenCalledWith("token-1", "claim-1", {
        reasonCategory: "PolicyExclusion",
        narrative: "War exclusion applies.",
      });
    });
  });

  it("closes a decided claim", async () => {
    const user = userEvent.setup();
    vi.mocked(getAdjudicationDetail).mockResolvedValue({
      ...detail,
      status: "Accepted",
      settlementAmount: 300000,
      paidAmount: 300000,
    });
    vi.mocked(closeClaim).mockResolvedValue({
      claimId: "claim-1",
      claimNumber: "CLM-CYB-20260401-AAAAAAAA",
      status: "Closed",
      outcome: "Closed",
      settlementAmount: null,
      paidAmount: 300000,
      denialReason: null,
      reason: "Claim closed after Accepted.",
      notes: null,
      claimedAmountAtDecision: 250000,
      reserveAmountAtDecision: 150000,
      decidedByUserId: "auth0|adjuster-1",
      decidedAtUtc: "2026-03-21T10:00:00Z",
    });

    renderWorkbench();

    await user.click(
      await screen.findByRole("button", {
        name: /open claim CLM-CYB-20260401-AAAAAAAA/i,
      }),
    );
    await user.click(await screen.findByRole("button", { name: /close claim/i }));

    await waitFor(() => {
      expect(closeClaim).toHaveBeenCalledWith("token-1", "claim-1");
    });
  });

  it("adds an internal work note", async () => {
    const user = userEvent.setup();
    vi.mocked(addClaimWorkNote).mockResolvedValue({
      noteId: "note-1",
      claimId: "claim-1",
      note: "Called the insured.",
      createdByUserId: "auth0|adjuster-1",
      createdAtUtc: "2026-03-15T10:00:00Z",
    });

    renderWorkbench();

    await user.click(
      await screen.findByRole("button", {
        name: /open claim CLM-CYB-20260401-AAAAAAAA/i,
      }),
    );
    await user.type(
      await screen.findByLabelText(/work note/i),
      "Called the insured.",
    );
    await user.click(screen.getByRole("button", { name: /add note/i }));

    await waitFor(() => {
      expect(addClaimWorkNote).toHaveBeenCalledWith("token-1", "claim-1", {
        note: "Called the insured.",
      });
    });
  });

  it("releases the assignment", async () => {
    const user = userEvent.setup();
    vi.mocked(releaseClaimAssignment).mockResolvedValue({
      claimId: "claim-1",
      claimNumber: "CLM-CYB-20260401-AAAAAAAA",
      policyId: "policy-1",
      policyNumber: "LIP-CYB-20260101-BBBBBBBB",
      incidentType: "RansomwareExtortion",
      incidentAtUtc: "2026-03-10T08:00:00Z",
      status: "UnderReview",
      assignedAdjusterUserId: null,
      openInformationRequestCount: 0,
      filedAtUtc: "2026-03-13T10:00:00Z",
      updatedAtUtc: "2026-03-15T10:00:00Z",
    });

    renderWorkbench();

    await user.click(
      await screen.findByRole("button", {
        name: /open claim CLM-CYB-20260401-AAAAAAAA/i,
      }),
    );
    await user.click(
      await screen.findByRole("button", { name: /release assignment/i }),
    );

    await waitFor(() => {
      expect(releaseClaimAssignment).toHaveBeenCalledWith("token-1", "claim-1");
    });
  });
});
