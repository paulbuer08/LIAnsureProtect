import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  listEvidenceRequests,
  respondToEvidenceRequest,
} from "../api/evidenceRequestsApi";
import { EvidenceRequestsPage } from "./EvidenceRequestsPage";

const getAccessTokenSilently = vi.fn();

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => ({
    getAccessTokenSilently,
  }),
}));

vi.mock("../api/evidenceRequestsApi", () => ({
  getOwnerEvidenceDocumentDownloadUrl: (evidenceRequestId: string, documentId: string) =>
    `http://localhost:5223/api/v1/evidence-requests/${evidenceRequestId}/documents/${documentId}/download`,
  listEvidenceRequests: vi.fn(),
  respondToEvidenceRequest: vi.fn(),
}));

function renderEvidenceRequestsPage() {
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
        <EvidenceRequestsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("EvidenceRequestsPage", () => {
  beforeEach(() => {
    getAccessTokenSilently.mockReset();
    getAccessTokenSilently.mockResolvedValue("owner-token");
    vi.mocked(listEvidenceRequests).mockReset();
    vi.mocked(respondToEvidenceRequest).mockReset();
  });

  it("lists owner evidence requests and submits a text response with evidence documents", async () => {
    const user = userEvent.setup();
    const mfaFile = new File(["mfa rollout evidence"], "mfa-attestation.pdf", {
      type: "application/pdf",
    });
    const edrFile = new File(["edr deployment evidence"], "edr-rollout.txt", {
      type: "text/plain",
    });
    vi.mocked(listEvidenceRequests).mockResolvedValue({
      evidenceRequests: [
        {
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
          documents: [],
        },
      ],
    });
    vi.mocked(respondToEvidenceRequest).mockResolvedValue({
      evidenceRequestId: "evidence-1",
      quoteId: "quote-severe",
      submissionId: "submission-severe",
      category: "MultiFactorAuthentication",
      title: "Confirm MFA rollout",
      description: "Please provide current MFA rollout evidence.",
      dueAtUtc: "2026-06-25T09:00:00Z",
      status: "Responded",
      isOverdue: false,
      daysUntilDue: 3,
      requestedByUserId: "auth0|underwriter",
      requestedAtUtc: "2026-06-22T09:00:00Z",
      respondedByUserId: "auth0|customer",
      respondentName: "Jane Applicant",
      respondentTitle: "CISO",
      responseText: "MFA is enforced for all email and privileged accounts.",
      attachmentFileName: "mfa-attestation.pdf",
      attachmentContentType: "application/pdf",
      attachmentSizeBytes: 124000,
      respondedAtUtc: "2026-06-22T12:00:00Z",
      acceptedByUserId: null,
      acceptedAtUtc: null,
      cancelledByUserId: null,
      cancelledAtUtc: null,
      reviewNotes: null,
      updatedAtUtc: "2026-06-22T12:00:00Z",
      documents: [
        {
          documentId: "document-1",
          originalFileName: "mfa-attestation.pdf",
          contentType: "application/pdf",
          sizeBytes: 124000,
          uploadedByUserId: "auth0|customer",
          uploadedAtUtc: "2026-06-22T12:00:00Z",
        },
        {
          documentId: "document-2",
          originalFileName: "edr-rollout.txt",
          contentType: "text/plain",
          sizeBytes: 92000,
          uploadedByUserId: "auth0|customer",
          uploadedAtUtc: "2026-06-22T12:00:01Z",
        },
      ],
    });

    renderEvidenceRequestsPage();

    expect(await screen.findByText("Confirm MFA rollout")).toBeInTheDocument();
    expect(screen.getByText("MultiFactorAuthentication")).toBeInTheDocument();
    expect(screen.getByText("Due in 3 days")).toBeInTheDocument();

    await user.type(screen.getByLabelText("Respondent name"), "Jane Applicant");
    await user.type(screen.getByLabelText("Respondent title"), "CISO");
    await user.type(
      screen.getByLabelText("Evidence response"),
      "MFA is enforced for all email and privileged accounts.",
    );
    await user.upload(screen.getByLabelText("Evidence files"), [mfaFile, edrFile]);
    await user.click(screen.getByRole("button", { name: "Submit evidence response" }));

    expect(respondToEvidenceRequest).toHaveBeenCalledWith(
      "owner-token",
      "evidence-1",
      {
        respondentName: "Jane Applicant",
        respondentTitle: "CISO",
        responseText: "MFA is enforced for all email and privileged accounts.",
        attachments: [mfaFile, edrFile],
      },
    );
    expect(await screen.findByText("Evidence response saved: Responded")).toBeInTheDocument();
    expect(screen.getByText("mfa-attestation.pdf")).toBeInTheDocument();
    expect(screen.getByText("edr-rollout.txt")).toBeInTheDocument();
  });

  it("shows overdue evidence requests clearly for the owner", async () => {
    vi.mocked(listEvidenceRequests).mockResolvedValue({
      evidenceRequests: [
        {
          evidenceRequestId: "evidence-overdue",
          quoteId: "quote-severe",
          submissionId: "submission-severe",
          category: "BackupRecovery",
          title: "Confirm backup testing",
          description: "Please provide latest backup test date.",
          dueAtUtc: "2020-06-20T09:00:00Z",
          status: "Open",
          isOverdue: true,
          daysUntilDue: -3,
          requestedByUserId: "auth0|underwriter",
          requestedAtUtc: "2020-06-18T09:00:00Z",
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
          updatedAtUtc: "2020-06-18T09:00:00Z",
        },
      ],
    });

    renderEvidenceRequestsPage();

    expect(await screen.findByText("Confirm backup testing")).toBeInTheDocument();
    expect(screen.getByText("Overdue by 3 days")).toBeInTheDocument();
  });
});
