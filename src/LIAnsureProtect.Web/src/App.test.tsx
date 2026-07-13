import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import App from "./App";
import { listQuoteReferrals } from "./features/underwriting/api/underwritingApi";
import { useNotifications } from "./features/notifications/hooks/useNotifications";
import { useCurrentUser } from "./hooks/useCurrentUser";

const getAccessTokenSilently = vi.fn();

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => ({
    getAccessTokenSilently,
    isAuthenticated: true,
    isLoading: false,
    loginWithRedirect: vi.fn(),
  }),
}));

vi.mock("./features/underwriting/api/underwritingApi", () => ({
  acceptQuoteEvidenceRequest: vi.fn(),
  addQuoteReferralNote: vi.fn(),
  addQuoteReferralTask: vi.fn(),
  adjustQuoteReferral: vi.fn(),
  approveQuoteReferral: vi.fn(),
  assignQuoteReferralToMe: vi.fn(),
  cancelQuoteEvidenceRequest: vi.fn(),
  completeQuoteReferralTask: vi.fn(),
  createQuoteEvidenceRequest: vi.fn(),
  declineQuoteReferral: vi.fn(),
  followUpQuoteEvidenceRequest: vi.fn(),
  generateAiUnderwritingReview: vi.fn(),
  getUnderwritingEvidenceDocumentDownloadUrl: (
    quoteId: string,
    evidenceRequestId: string,
    documentId: string,
  ) =>
    `http://localhost:5223/api/v1/underwriting/quote-referrals/${quoteId}/evidence-requests/${evidenceRequestId}/documents/${documentId}/download`,
  listQuoteReferralTimeline: vi.fn(),
  listQuoteReferrals: vi.fn(),
  releaseQuoteReferralAssignment: vi.fn(),
  triageQuoteReferralOperation: vi.fn(),
}));

vi.mock("./hooks/useCurrentUser", () => ({
  useCurrentUser: vi.fn(),
}));

vi.mock("./features/notifications/hooks/useNotifications", () => ({
  useNotifications: vi.fn(),
}));

function mockCurrentUser(roles: string[]) {
  vi.mocked(useCurrentUser).mockReturnValue({
    data: { userId: "user-1", email: "user@example.com", roles },
    isPending: false,
    isError: false,
  } as unknown as ReturnType<typeof useCurrentUser>);
}

function renderAppAt(path: string) {
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
      <MemoryRouter initialEntries={[path]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("App routes", () => {
  beforeEach(() => {
    getAccessTokenSilently.mockReset();
    getAccessTokenSilently.mockResolvedValue("underwriter-token");
    vi.mocked(listQuoteReferrals).mockReset();
    mockCurrentUser(["Underwriter"]);
    vi.mocked(useNotifications).mockReturnValue({
      data: {
        notifications: [],
        unreadCount: 0,
      },
      isPending: false,
      isError: false,
    } as unknown as ReturnType<typeof useNotifications>);
  });

  it("registers the protected underwriting quote referrals route", async () => {
    vi.mocked(listQuoteReferrals).mockResolvedValue({
      quoteReferrals: [],
    });

    renderAppAt("/underwriting/quote-referrals");

    expect(
      await screen.findByRole(
        "heading",
        { name: "Underwriting workbench" },
        { timeout: 5_000 },
      ),
    ).toBeInTheDocument();
    await waitFor(() =>
      expect(listQuoteReferrals).toHaveBeenCalledWith("underwriter-token"),
    );
  });

  it("does not mount the underwriting route for a customer direct URL", async () => {
    mockCurrentUser(["Customer"]);

    renderAppAt("/underwriting/quote-referrals");

    expect(
      await screen.findByRole("heading", {
        name: "You do not have access to this page",
      }, { timeout: 5_000 }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("heading", { name: "Underwriting workbench" }),
    ).not.toBeInTheDocument();
    expect(listQuoteReferrals).not.toHaveBeenCalled();
  });
});
