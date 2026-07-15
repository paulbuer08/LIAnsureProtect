import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  downloadOwnerEvidenceDocument,
  getEvidenceRequest,
  respondToEvidenceRequest,
  uploadReplacementEvidenceDocuments,
} from "../api/evidenceRequestsApi";
import { useEvidenceRequest } from "../hooks/useEvidenceRequests";
import type { QuoteEvidenceRequest } from "../types";
import { EvidenceRequestCard } from "./EvidenceRequestsPage";

const getAccessTokenSilently = vi.fn();

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => ({
    getAccessTokenSilently,
  }),
}));

vi.mock("../api/evidenceRequestsApi", () => ({
  downloadOwnerEvidenceDocument: vi.fn(),
  getEvidenceRequest: vi.fn(),
  respondToEvidenceRequest: vi.fn(),
  uploadReplacementEvidenceDocuments: vi.fn(),
}));

const notReviewedEvidence = {
  reviewDecision: "NotReviewed",
  reviewReason: null,
  remediationGuidance: null,
  reviewedByUserId: null,
  reviewedAtUtc: null,
};

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
        <EvidenceRequestCardHarness />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

function EvidenceRequestCardHarness() {
  const query = useEvidenceRequest("evidence-request-under-test");
  const request = query.data;
  return request ? <EvidenceRequestCard request={request} /> : <p>Loading evidence request...</p>;
}

describe("EvidenceRequestsPage", () => {
  beforeEach(() => {
    getAccessTokenSilently.mockReset();
    getAccessTokenSilently.mockResolvedValue("owner-token");
    vi.mocked(getEvidenceRequest).mockReset();
    vi.mocked(respondToEvidenceRequest).mockReset();
    vi.mocked(uploadReplacementEvidenceDocuments).mockReset();
  });

  it("submits a text response with evidence documents from request detail", async () => {
    const user = userEvent.setup();
    const mfaFile = new File(["mfa rollout evidence"], "mfa-attestation.pdf", {
      type: "application/pdf",
    });
    const edrFile = new File(["edr deployment evidence"], "edr-rollout.txt", {
      type: "text/plain",
    });
    vi.mocked(getEvidenceRequest).mockResolvedValue(
        {
          evidenceRequestId: "evidence-1",
          quoteId: "quote-severe",
          submissionId: "submission-severe",
          submissionReference: "SUB-2026-1234567890ABCDEF",
          companyName: "Example Company",
          documentRequirement: "Required",
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
          ...notReviewedEvidence,
          reviewNotes: null,
          updatedAtUtc: "2026-06-22T09:00:00Z",
          documents: [],
        },
    );
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
      ...notReviewedEvidence,
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
          scanStatus: "Clean",
          scannerProviderName: "LocalDeterministicEvidenceDocumentScanner",
          scanResultCode: "NO_THREATS_FOUND",
          scanResultReason: "No local test threat markers were found.",
          scannedAtUtc: "2026-06-22T12:00:01Z",
          sha256: "hash-clean-1",
          isDownloadAvailable: true,
        },
        {
          documentId: "document-2",
          originalFileName: "edr-rollout.txt",
          contentType: "text/plain",
          sizeBytes: 92000,
          uploadedByUserId: "auth0|customer",
          uploadedAtUtc: "2026-06-22T12:00:01Z",
          scanStatus: "Clean",
          scannerProviderName: "LocalDeterministicEvidenceDocumentScanner",
          scanResultCode: "NO_THREATS_FOUND",
          scanResultReason: "No local test threat markers were found.",
          scannedAtUtc: "2026-06-22T12:00:02Z",
          sha256: "hash-clean-2",
          isDownloadAvailable: true,
        },
      ],
    });

    renderEvidenceRequestsPage();

    expect(await screen.findByText("Confirm MFA rollout")).toBeInTheDocument();
    expect(screen.getByText("MultiFactorAuthentication")).toBeInTheDocument();
    expect(screen.getByText("Due in 3 days")).toBeInTheDocument();
    expect(screen.getByText(/SUB-2026-1234567890ABCDEF/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Submit evidence response" })).toBeDisabled();

    await user.type(screen.getByLabelText("Respondent name"), "Jane Applicant");
    await user.type(screen.getByLabelText("Respondent title"), "CISO");
    await user.type(screen.getByLabelText(/Respondent email/), "jane@example.com");
    await user.type(
      screen.getByLabelText(/^Evidence response/),
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
        respondentEmail: "jane@example.com",
        respondentMobileNumber: null,
        respondentTelephoneNumber: null,
        responseText: "MFA is enforced for all email and privileged accounts.",
        otherConcerns: null,
        attachments: [mfaFile, edrFile],
      },
    );
    expect(await screen.findByText("Evidence response saved: Responded")).toBeInTheDocument();
    expect(screen.getAllByText("Clean")).toHaveLength(2);
    // Downloads are authenticated fetches (no bare links): clicking calls the API with the token.
    await user.click(screen.getByRole("button", { name: "Download mfa-attestation.pdf" }));
    await waitFor(() => {
      expect(downloadOwnerEvidenceDocument).toHaveBeenCalledWith(
        "owner-token",
        "evidence-1",
        "document-1",
        "mfa-attestation.pdf",
      );
    });
    expect(
      screen.getByRole("button", { name: "Download edr-rollout.txt" }),
    ).toBeInTheDocument();
  });

  it("asks before discarding an unsent evidence response", async () => {
    const user = userEvent.setup();
    vi.mocked(getEvidenceRequest).mockResolvedValue({
      evidenceRequestId: "evidence-cancel",
      quoteId: "quote-1",
      submissionId: "submission-1",
      submissionReference: "SUB-2026-1111111111111111",
      companyName: "Example Company",
      documentRequirement: "Optional",
      category: "Other",
      title: "Clarify the control",
      description: "Provide a short explanation.",
      dueAtUtc: "2026-07-28T09:00:00Z",
      status: "Open",
      isOverdue: false,
      daysUntilDue: 14,
      requestedByUserId: "underwriter-1",
      requestedAtUtc: "2026-07-14T09:00:00Z",
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
      ...notReviewedEvidence,
      reviewNotes: null,
      updatedAtUtc: "2026-07-14T09:00:00Z",
      documents: [],
    });
    renderEvidenceRequestsPage();
    await user.type(await screen.findByLabelText("Respondent name"), "Jane");
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    const dialog = screen.getByRole("dialog", { name: "Cancel this evidence response?" });
    expect(dialog).toBeInTheDocument();
    await user.click(within(dialog).getByRole("button", { name: "Cancel" }));
    expect(screen.getByLabelText("Respondent name")).toHaveValue("Jane");
  });

  it("shows rejected document status and lets the owner upload replacement evidence", async () => {
    const user = userEvent.setup();
    const replacementFile = new File(["replacement clean evidence"], "replacement-evidence.txt", {
      type: "text/plain",
    });
    vi.mocked(getEvidenceRequest).mockResolvedValue(
        {
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
          responseText: "MFA evidence uploaded.",
          attachmentFileName: "rejected-evidence.txt",
          attachmentContentType: "text/plain",
          attachmentSizeBytes: 64,
          respondedAtUtc: "2026-06-22T12:00:00Z",
          acceptedByUserId: null,
          acceptedAtUtc: null,
          cancelledByUserId: null,
          cancelledAtUtc: null,
          ...notReviewedEvidence,
          reviewNotes: null,
          updatedAtUtc: "2026-06-22T12:00:00Z",
          documents: [
            {
              documentId: "document-1",
              originalFileName: "rejected-evidence.txt",
              contentType: "text/plain",
              sizeBytes: 64,
              uploadedByUserId: "auth0|customer",
              uploadedAtUtc: "2026-06-22T12:00:00Z",
              scanStatus: "Rejected",
              scannerProviderName: "LocalDeterministicEvidenceDocumentScanner",
              scanResultCode: "THREATS_FOUND",
              scanResultReason: "Local deterministic scanner found a test threat marker.",
              scannedAtUtc: "2026-06-22T12:00:01Z",
              sha256: "hash-rejected",
              isDownloadAvailable: false,
            },
          ],
        },
    );
    vi.mocked(uploadReplacementEvidenceDocuments).mockResolvedValue({
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
      responseText: "MFA evidence uploaded.",
      attachmentFileName: "rejected-evidence.txt",
      attachmentContentType: "text/plain",
      attachmentSizeBytes: 64,
      respondedAtUtc: "2026-06-22T12:00:00Z",
      acceptedByUserId: null,
      acceptedAtUtc: null,
      cancelledByUserId: null,
      cancelledAtUtc: null,
      ...notReviewedEvidence,
      reviewNotes: null,
      updatedAtUtc: "2026-06-22T12:00:00Z",
      documents: [
        {
          documentId: "document-1",
          originalFileName: "rejected-evidence.txt",
          contentType: "text/plain",
          sizeBytes: 64,
          uploadedByUserId: "auth0|customer",
          uploadedAtUtc: "2026-06-22T12:00:00Z",
          scanStatus: "Rejected",
          scannerProviderName: "LocalDeterministicEvidenceDocumentScanner",
          scanResultCode: "THREATS_FOUND",
          scanResultReason: "Local deterministic scanner found a test threat marker.",
          scannedAtUtc: "2026-06-22T12:00:01Z",
          sha256: "hash-rejected",
          isDownloadAvailable: false,
        },
        {
          documentId: "document-2",
          originalFileName: "replacement-evidence.txt",
          contentType: "text/plain",
          sizeBytes: 26,
          uploadedByUserId: "auth0|customer",
          uploadedAtUtc: "2026-06-22T12:10:00Z",
          scanStatus: "Clean",
          scannerProviderName: "LocalDeterministicEvidenceDocumentScanner",
          scanResultCode: "NO_THREATS_FOUND",
          scanResultReason: "No local test threat markers were found.",
          scannedAtUtc: "2026-06-22T12:10:01Z",
          sha256: "hash-clean-replacement",
          isDownloadAvailable: true,
        },
      ],
    });

    renderEvidenceRequestsPage();

    expect(await screen.findByText("rejected-evidence.txt")).toBeInTheDocument();
    expect(screen.getByText("Rejected")).toBeInTheDocument();
    expect(screen.getByText("Local deterministic scanner found a test threat marker.")).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Download rejected-evidence.txt" }),
    ).not.toBeInTheDocument();
    expect(screen.getByText("Download unavailable until security screening is clean.")).toBeInTheDocument();

    await user.upload(screen.getByLabelText("Replacement evidence files"), [replacementFile]);
    await user.click(screen.getByRole("button", { name: "Upload replacement evidence" }));

    await waitFor(() =>
      expect(uploadReplacementEvidenceDocuments).toHaveBeenCalledWith(
        "owner-token",
        "evidence-1",
        [replacementFile],
      ),
    );
    const replacementDownload = await screen.findByRole("button", {
      name: "Download replacement-evidence.txt",
    });
    await user.click(replacementDownload);
    await waitFor(() =>
      expect(downloadOwnerEvidenceDocument).toHaveBeenCalledWith(
        "owner-token",
        "evidence-1",
        "document-2",
        "replacement-evidence.txt",
      ),
    );
  });

  it("shows overdue evidence requests clearly for the owner", async () => {
    vi.mocked(getEvidenceRequest).mockResolvedValue(
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
          ...notReviewedEvidence,
          reviewNotes: null,
          updatedAtUtc: "2020-06-18T09:00:00Z",
        },
    );

    renderEvidenceRequestsPage();

    expect(await screen.findByText("Confirm backup testing")).toBeInTheDocument();
    expect(screen.getByText("Overdue by 3 days")).toBeInTheDocument();
  });

  it("shows underwriter remediation guidance and allows supplemental response", async () => {
    const user = userEvent.setup();
    vi.mocked(getEvidenceRequest).mockResolvedValue(
        {
          evidenceRequestId: "evidence-clarification",
          quoteId: "quote-severe",
          submissionId: "submission-severe",
          category: "MultiFactorAuthentication",
          title: "Confirm MFA rollout",
          description: "Please provide current MFA rollout evidence.",
          dueAtUtc: "2026-06-25T09:00:00Z",
          status: "Responded",
          reviewDecision: "NeedsClarification",
          reviewReason: "The response does not confirm privileged account MFA scope.",
          remediationGuidance:
            "Please confirm whether MFA applies to all administrator and service-owner accounts.",
          reviewedByUserId: "auth0|underwriter",
          reviewedAtUtc: "2026-06-22T13:00:00Z",
          isOverdue: false,
          daysUntilDue: 3,
          requestedByUserId: "auth0|underwriter",
          requestedAtUtc: "2026-06-22T09:00:00Z",
          respondedByUserId: "auth0|customer",
          respondentName: "Jane Applicant",
          respondentTitle: "CISO",
          responseText: "MFA is enforced for email accounts.",
          attachmentFileName: null,
          attachmentContentType: null,
          attachmentSizeBytes: null,
          respondedAtUtc: "2026-06-22T12:00:00Z",
          acceptedByUserId: null,
          acceptedAtUtc: null,
          cancelledByUserId: null,
          cancelledAtUtc: null,
          reviewNotes: null,
          updatedAtUtc: "2026-06-22T13:00:00Z",
          documents: [],
        },
    );
    vi.mocked(respondToEvidenceRequest).mockResolvedValue({
      evidenceRequestId: "evidence-clarification",
      quoteId: "quote-severe",
      submissionId: "submission-severe",
      category: "MultiFactorAuthentication",
      title: "Confirm MFA rollout",
      description: "Please provide current MFA rollout evidence.",
      dueAtUtc: "2026-06-25T09:00:00Z",
      status: "Responded",
      reviewDecision: "NotReviewed",
      reviewReason: null,
      remediationGuidance: null,
      reviewedByUserId: null,
      reviewedAtUtc: null,
      isOverdue: false,
      daysUntilDue: 3,
      requestedByUserId: "auth0|underwriter",
      requestedAtUtc: "2026-06-22T09:00:00Z",
      respondedByUserId: "auth0|customer",
      respondentName: "Jane Applicant",
      respondentTitle: "CISO",
      responseText: "Supplemental response: MFA applies to email and privileged accounts.",
      attachmentFileName: null,
      attachmentContentType: null,
      attachmentSizeBytes: null,
      respondedAtUtc: "2026-06-22T14:00:00Z",
      acceptedByUserId: null,
      acceptedAtUtc: null,
      cancelledByUserId: null,
      cancelledAtUtc: null,
      reviewNotes: null,
      updatedAtUtc: "2026-06-22T14:00:00Z",
      documents: [],
    });

    renderEvidenceRequestsPage();

    expect(await screen.findByText("NeedsClarification")).toBeInTheDocument();
    expect(
      screen.getByText(
        "Please confirm whether MFA applies to all administrator and service-owner accounts.",
      ),
    ).toBeInTheDocument();

    await user.type(screen.getByLabelText("Respondent name"), "Jane Applicant");
    await user.type(screen.getByLabelText("Respondent title"), "CISO");
    await user.type(screen.getByLabelText(/Respondent email/), "jane@example.com");
    await user.type(
      screen.getByLabelText(/^Evidence response/),
      "Supplemental response: MFA applies to email and privileged accounts.",
    );
    await user.click(screen.getByRole("button", { name: "Submit remediation evidence" }));

    expect(respondToEvidenceRequest).toHaveBeenCalledWith(
      "owner-token",
      "evidence-clarification",
      {
        respondentName: "Jane Applicant",
        respondentTitle: "CISO",
        respondentEmail: "jane@example.com",
        respondentMobileNumber: null,
        respondentTelephoneNumber: null,
        responseText: "Supplemental response: MFA applies to email and privileged accounts.",
        otherConcerns: null,
        attachments: [],
      },
    );
    expect(await screen.findByText("Evidence response saved: Responded")).toBeInTheDocument();
  });

  it("allows an auditable mobile-number-only follow-up before underwriting review", async () => {
    const user = userEvent.setup();
    const respondedRequest = {
      evidenceRequestId: "evidence-follow-up",
      quoteId: "quote-1",
      submissionId: "submission-1",
      submissionReference: "SUB-2026-ABCDEF1234567890",
      companyName: "Example Company",
      documentRequirement: "Required" as const,
      category: "BackupRecovery",
      title: "Verify backup and recovery controls",
      description: "Provide current backup evidence.",
      dueAtUtc: "2026-07-28T09:00:00Z",
      status: "Responded",
      isOverdue: false,
      daysUntilDue: 14,
      requestedByUserId: "system-assurance-policy",
      requestedAtUtc: "2026-07-14T09:00:00Z",
      respondedByUserId: "customer-1",
      respondentName: "Jane Applicant",
      respondentTitle: "CISO",
      respondentEmail: "jane@example.com",
      respondentPhone: null,
      respondentMobileNumber: null,
      respondentTelephoneNumber: null,
      responseText: "Backups are tested quarterly.",
      otherConcerns: null,
      attachmentFileName: "backup-report.pdf",
      attachmentContentType: "application/pdf",
      attachmentSizeBytes: 1024,
      respondedAtUtc: "2026-07-14T10:00:00Z",
      acceptedByUserId: null,
      acceptedAtUtc: null,
      cancelledByUserId: null,
      cancelledAtUtc: null,
      ...notReviewedEvidence,
      reviewNotes: null,
      updatedAtUtc: "2026-07-14T10:00:00Z",
      documents: [],
      responses: [
        {
          responseId: "response-1",
          respondedByUserId: "customer-1",
          respondentName: "Jane Applicant",
          respondentTitle: "CISO",
          respondentEmail: "jane@example.com",
          respondentPhone: null,
          respondentMobileNumber: null,
          respondentTelephoneNumber: null,
          responseText: "Backups are tested quarterly.",
          otherConcerns: null,
          kind: "Initial" as const,
          respondedAtUtc: "2026-07-14T10:00:00Z",
        },
      ],
    };
    vi.mocked(getEvidenceRequest).mockResolvedValue(respondedRequest);
    vi.mocked(respondToEvidenceRequest).mockResolvedValue({
      ...respondedRequest,
      respondentMobileNumber: "+639175550101",
      pendingFollowUpCount: 1,
      responses: [
        ...respondedRequest.responses,
        {
          responseId: "response-2",
          respondedByUserId: "customer-1",
          respondentName: "Jane Applicant",
          respondentTitle: "CISO",
          respondentEmail: "jane@example.com",
          respondentPhone: null,
          respondentMobileNumber: "+639175550101",
          respondentTelephoneNumber: null,
          responseText: null,
          otherConcerns: null,
          kind: "FollowUp",
          respondedAtUtc: "2026-07-14T11:00:00Z",
        },
      ],
    });

    renderEvidenceRequestsPage();

    expect(await screen.findByRole("button", { name: "Send follow-up" })).toBeDisabled();
    expect(screen.getByText(/original response remains unchanged/i)).toBeInTheDocument();
    await user.type(
      screen.getByLabelText(/^Respondent mobile number/),
      "+63 917 555 0101",
    );
    expect(screen.getByRole("button", { name: "Send follow-up" })).toBeEnabled();
    await user.click(screen.getByRole("button", { name: "Send follow-up" }));

    expect(respondToEvidenceRequest).toHaveBeenCalledWith(
      "owner-token",
      "evidence-follow-up",
      {
        respondentName: "Jane Applicant",
        respondentTitle: "CISO",
        respondentEmail: "jane@example.com",
        respondentMobileNumber: "+63 917 555 0101",
        respondentTelephoneNumber: null,
        responseText: null,
        otherConcerns: null,
        attachments: [],
      },
    );
    expect(await screen.findByText("FollowUp response")).toBeInTheDocument();
  });

  it("lets a Required evidence follow-up use new narrative without another file", async () => {
    const user = userEvent.setup();
    const respondedRequest: QuoteEvidenceRequest = {
      evidenceRequestId: "evidence-required-follow-up",
      quoteId: "quote-1",
      submissionId: "submission-1",
      submissionReference: "SUB-2026-ABCDEF1234567890",
      companyName: "Example Company",
      documentRequirement: "Required" as const,
      category: "MultiFactorAuthentication",
      title: "Verify multi-factor authentication",
      description: "Provide current MFA evidence.",
      dueAtUtc: "2026-07-28T09:00:00Z",
      status: "Responded",
      isOverdue: false,
      daysUntilDue: 14,
      requestedByUserId: "system-assurance-policy",
      requestedAtUtc: "2026-07-14T09:00:00Z",
      respondedByUserId: "customer-1",
      respondentName: "Jane Applicant",
      respondentTitle: "CISO",
      respondentEmail: "jane@example.com",
      respondentPhone: null,
      responseText: "The original response included the required document.",
      otherConcerns: null,
      attachmentFileName: null,
      attachmentContentType: null,
      attachmentSizeBytes: null,
      respondedAtUtc: "2026-07-14T10:00:00Z",
      acceptedByUserId: null,
      acceptedAtUtc: null,
      cancelledByUserId: null,
      cancelledAtUtc: null,
      ...notReviewedEvidence,
      reviewNotes: null,
      updatedAtUtc: "2026-07-14T10:00:00Z",
      documents: [],
      responses: [],
    };
    vi.mocked(getEvidenceRequest).mockResolvedValue(respondedRequest);
    vi.mocked(respondToEvidenceRequest).mockResolvedValue(respondedRequest);

    renderEvidenceRequestsPage();

    const button = await screen.findByRole("button", { name: "Send follow-up" });
    await user.type(
      screen.getByLabelText(/^Evidence response/),
      "MFA coverage now includes the remaining service accounts.",
    );
    expect(button).toBeEnabled();
    await user.click(button);

    expect(respondToEvidenceRequest).toHaveBeenCalledWith(
      "owner-token",
      "evidence-required-follow-up",
      expect.objectContaining({
        responseText: "MFA coverage now includes the remaining service accounts.",
        attachments: [],
      }),
    );
  });
});
