import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  downloadOwnerClaimDocument,
  getClaimDetail,
  respondToInformationRequest,
  setClaimedAmount,
  uploadClaimDocuments,
} from "../api/claimsApi";
import type { ClaimDetail } from "../types";
import { ClaimDetailPage } from "./ClaimDetailPage";

const getAccessTokenSilently = vi.fn();

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => ({
    getAccessTokenSilently,
  }),
}));

vi.mock("../api/claimsApi", () => ({
  getClaimDetail: vi.fn(),
  downloadOwnerClaimDocument: vi.fn(),
  respondToInformationRequest: vi.fn(),
  setClaimedAmount: vi.fn(),
  uploadClaimDocuments: vi.fn(),
}));

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={["/claims/claim-1"]}>
        <Routes>
          <Route path="/claims/:claimId" element={<ClaimDetailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

const baseDetail: ClaimDetail = {
  claimId: "claim-1",
  claimNumber: "CLM-CYB-20260401-AAAAAAAA",
  policyId: "policy-1",
  policyNumber: "LIP-CYB-20260101-BBBBBBBB",
  incidentType: "RansomwareExtortion",
  incidentAtUtc: "2026-03-10T08:00:00Z",
  discoveredAtUtc: "2026-03-12T09:30:00Z",
  description: "Ransomware encrypted the file server.",
  status: "InformationRequested",
  claimedAmount: 250000,
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
  timeline: [
    {
      entryId: "entry-1",
      entryType: "ClaimFiled",
      summary: "Claim filed for RansomwareExtortion.",
      createdByUserId: "auth0|customer-1",
      createdAtUtc: "2026-03-13T10:00:00Z",
    },
  ],
  informationRequests: [
    {
      informationRequestId: "request-1",
      claimId: "claim-1",
      title: "Proof of loss",
      message: "Please provide the forensic report.",
      requestedByUserId: "auth0|adjuster-1",
      requestedAtUtc: "2026-03-14T10:00:00Z",
      isAnswered: false,
      responseText: null,
      respondedByUserId: null,
      respondedAtUtc: null,
    },
  ],
  documents: [
    {
      documentId: "document-1",
      claimId: "claim-1",
      kind: "ForensicReport",
      originalFileName: "forensic-report.pdf",
      contentType: "application/pdf",
      sizeBytes: 1024,
      scanStatus: "Clean",
      scanResultReason: "No local test threat markers were found.",
      isDownloadAvailable: true,
      uploadedByUserId: "auth0|customer-1",
      uploadedAtUtc: "2026-03-14T11:00:00Z",
    },
  ],
};

describe("ClaimDetailPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getAccessTokenSilently.mockResolvedValue("token-1");
    vi.mocked(getClaimDetail).mockResolvedValue(baseDetail);
  });

  it("shows the claim, its timeline, open questions, and documents", async () => {
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "CLM-CYB-20260401-AAAAAAAA" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Claim filed for RansomwareExtortion."),
    ).toBeInTheDocument();
    expect(screen.getByText("Proof of loss")).toBeInTheDocument();
    expect(screen.getByText("forensic-report.pdf")).toBeInTheDocument();
    // Downloads are authenticated fetches (no bare links): clicking calls the API with the token.
    const user = userEvent.setup();
    await user.click(
      screen.getByRole("button", { name: /download forensic-report.pdf/i }),
    );
    await waitFor(() => {
      expect(downloadOwnerClaimDocument).toHaveBeenCalledWith(
        "token-1",
        "claim-1",
        "document-1",
        "forensic-report.pdf",
      );
    });
  });

  it("answers an open information request", async () => {
    const user = userEvent.setup();
    vi.mocked(respondToInformationRequest).mockResolvedValue({
      informationRequestId: "request-1",
      claimId: "claim-1",
      title: "Proof of loss",
      message: "Please provide the forensic report.",
      requestedByUserId: "auth0|adjuster-1",
      requestedAtUtc: "2026-03-14T10:00:00Z",
      isAnswered: true,
      responseText: "Report attached.",
      respondedByUserId: "auth0|customer-1",
      respondedAtUtc: "2026-03-15T10:00:00Z",
    });

    renderPage();

    await user.type(
      await screen.findByLabelText(/your response/i),
      "Report attached.",
    );
    await user.click(screen.getByRole("button", { name: /send response/i }));

    await waitFor(() => {
      expect(respondToInformationRequest).toHaveBeenCalledWith(
        "token-1",
        "claim-1",
        "request-1",
        { responseText: "Report attached." },
      );
    });
  });

  it("declares the claimed amount", async () => {
    const user = userEvent.setup();
    vi.mocked(setClaimedAmount).mockResolvedValue({
      claimId: "claim-1",
      claimedAmount: 300000,
      reserveAmount: 0,
      paidAmount: 0,
      policyLimitAtFiling: 1000000,
      policyRetentionAtFiling: 25000,
    });

    renderPage();

    const amountInput = await screen.findByLabelText(/claimed amount/i);
    await user.clear(amountInput);
    await user.type(amountInput, "300000");
    await user.click(
      screen.getByRole("button", { name: /update claimed amount/i }),
    );

    await waitFor(() => {
      expect(setClaimedAmount).toHaveBeenCalledWith("token-1", "claim-1", {
        amount: 300000,
      });
    });
  });

  it("uploads supporting documents", async () => {
    const user = userEvent.setup();
    vi.mocked(uploadClaimDocuments).mockResolvedValue({
      claimId: "claim-1",
      documents: [],
    });

    renderPage();

    const fileInput = await screen.findByLabelText(/supporting documents/i);
    await user.upload(
      fileInput,
      new File(["invoice bytes"], "invoice.pdf", { type: "application/pdf" }),
    );
    await user.selectOptions(screen.getByLabelText(/document kind/i), "Invoice");
    await user.click(screen.getByRole("button", { name: /upload documents/i }));

    await waitFor(() => {
      expect(uploadClaimDocuments).toHaveBeenCalledTimes(1);
    });
    const [, claimId, kind, files] =
      vi.mocked(uploadClaimDocuments).mock.calls[0];
    expect(claimId).toBe("claim-1");
    expect(kind).toBe("Invoice");
    expect(files).toHaveLength(1);
  });

  it("shows the verdict when the claim is decided", async () => {
    vi.mocked(getClaimDetail).mockResolvedValue({
      ...baseDetail,
      status: "Accepted",
      settlementAmount: 300000,
      paidAmount: 300000,
      decidedAtUtc: "2026-03-20T10:00:00Z",
      informationRequests: [],
    });

    renderPage();

    expect(await screen.findByText(/settlement/i)).toBeInTheDocument();
    expect(screen.getAllByText(/300,000/).length).toBeGreaterThan(0);
  });
});
