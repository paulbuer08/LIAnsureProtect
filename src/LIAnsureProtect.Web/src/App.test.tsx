import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import App from "./App";
import { listQuoteReferrals } from "./features/underwriting/api/underwritingApi";

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
  adjustQuoteReferral: vi.fn(),
  approveQuoteReferral: vi.fn(),
  declineQuoteReferral: vi.fn(),
  generateAiUnderwritingReview: vi.fn(),
  listQuoteReferrals: vi.fn(),
}));

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
  });

  it("registers the protected underwriting quote referrals route", async () => {
    vi.mocked(listQuoteReferrals).mockResolvedValue({
      quoteReferrals: [],
    });

    renderAppAt("/underwriting/quote-referrals");

    expect(
      await screen.findByRole("heading", { name: "Underwriting workbench" }),
    ).toBeInTheDocument();
    expect(listQuoteReferrals).toHaveBeenCalledWith("underwriter-token");
  });
});
