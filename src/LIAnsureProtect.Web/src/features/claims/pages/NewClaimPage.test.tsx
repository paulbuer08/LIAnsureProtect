import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { fileClaim, listClaimablePolicies } from "../api/claimsApi";
import { NewClaimPage } from "./NewClaimPage";
import { ApiError } from "../../../lib/apiClient";

const getAccessTokenSilently = vi.fn();

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => ({
    getAccessTokenSilently,
  }),
}));

vi.mock("../api/claimsApi", () => ({
  fileClaim: vi.fn(),
  listClaimablePolicies: vi.fn(),
}));

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <NewClaimPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

const boundPolicy = {
  policyId: "policy-1",
  policyNumber: "LIP-CYB-20260101-BBBBBBBB",
  effectiveAtUtc: "2026-01-01T00:00:00Z",
  expirationAtUtc: "2027-01-01T00:00:00Z",
  limit: 1000000,
  retention: 25000,
};

describe("NewClaimPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getAccessTokenSilently.mockResolvedValue("token-1");
    vi.mocked(listClaimablePolicies).mockResolvedValue({
      policies: [boundPolicy],
    });
  });

  it("walks the wizard: pick a bound policy, describe the incident, file", async () => {
    const user = userEvent.setup();
    vi.mocked(fileClaim).mockResolvedValue({
      claimId: "claim-1",
      claimNumber: "CLM-CYB-20260401-AAAAAAAA",
      policyId: "policy-1",
      policyNumber: "LIP-CYB-20260101-BBBBBBBB",
      incidentType: "RansomwareExtortion",
      incidentAtUtc: "2026-03-10T08:00:00Z",
      discoveredAtUtc: "2026-03-12T09:30:00Z",
      status: "Filed",
      filedAtUtc: "2026-03-13T10:00:00Z",
      updatedAtUtc: "2026-03-13T10:00:00Z",
    });

    renderPage();

    // Step 1: the bound policy is offered and selected.
    await user.click(
      await screen.findByRole("button", {
        name: /select policy LIP-CYB-20260101-BBBBBBBB/i,
      }),
    );

    // Step 2: incident details.
    await user.selectOptions(
      screen.getByLabelText(/incident type/i),
      "RansomwareExtortion",
    );
    await user.type(screen.getByLabelText(/incident date/i), "2026-03-10");
    await user.type(screen.getByLabelText(/discovery date/i), "2026-03-12");
    await user.type(
      screen.getByLabelText(/description/i),
      "Ransomware encrypted the file server; extortion note received.",
    );

    await user.click(screen.getByRole("button", { name: /file claim/i }));

    await waitFor(() => {
      expect(fileClaim).toHaveBeenCalledTimes(1);
    });
    const [, request] = vi.mocked(fileClaim).mock.calls[0];
    expect(request.policyId).toBe("policy-1");
    expect(request.incidentType).toBe("RansomwareExtortion");

    expect(
      await screen.findByText(/CLM-CYB-20260401-AAAAAAAA/),
    ).toBeInTheDocument();
  }, 10_000);

  it("shows an empty state when the caller has no bound policies", async () => {
    vi.mocked(listClaimablePolicies).mockResolvedValue({ policies: [] });

    renderPage();

    expect(
      await screen.findByText(/no bound policies to claim against/i),
    ).toBeInTheDocument();
  });

  it("surfaces filing rejections from the API", async () => {
    const user = userEvent.setup();
    vi.mocked(fileClaim).mockRejectedValue(
      new ApiError(
        "Claims can only be filed against a bound policy.",
        409,
        "claim.policy.not_bound",
      ),
    );

    renderPage();

    await user.click(
      await screen.findByRole("button", {
        name: /select policy LIP-CYB-20260101-BBBBBBBB/i,
      }),
    );
    await user.selectOptions(
      screen.getByLabelText(/incident type/i),
      "Other",
    );
    await user.type(screen.getByLabelText(/incident date/i), "2026-03-10");
    await user.type(screen.getByLabelText(/discovery date/i), "2026-03-12");
    await user.type(screen.getByLabelText(/description/i), "Incident text.");
    await user.click(screen.getByRole("button", { name: /file claim/i }));

    expect(
      await screen.findByText(/Claims can only be filed against a bound policy./),
    ).toBeInTheDocument();
  });
});
