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

  it("lists owner evidence requests and submits a text response with attachment metadata", async () => {
    const user = userEvent.setup();
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
    });

    renderEvidenceRequestsPage();

    expect(await screen.findByText("Confirm MFA rollout")).toBeInTheDocument();
    expect(screen.getByText("MultiFactorAuthentication")).toBeInTheDocument();

    await user.type(screen.getByLabelText("Respondent name"), "Jane Applicant");
    await user.type(screen.getByLabelText("Respondent title"), "CISO");
    await user.type(
      screen.getByLabelText("Evidence response"),
      "MFA is enforced for all email and privileged accounts.",
    );
    await user.type(screen.getByLabelText("Attachment file name"), "mfa-attestation.pdf");
    await user.type(screen.getByLabelText("Attachment content type"), "application/pdf");
    await user.type(screen.getByLabelText("Attachment size bytes"), "124000");
    await user.click(screen.getByRole("button", { name: "Submit evidence response" }));

    expect(respondToEvidenceRequest).toHaveBeenCalledWith(
      "owner-token",
      "evidence-1",
      {
        respondentName: "Jane Applicant",
        respondentTitle: "CISO",
        responseText: "MFA is enforced for all email and privileged accounts.",
        attachmentFileName: "mfa-attestation.pdf",
        attachmentContentType: "application/pdf",
        attachmentSizeBytes: 124000,
      },
    );
    expect(await screen.findByText("Evidence response saved: Responded")).toBeInTheDocument();
  });
});
