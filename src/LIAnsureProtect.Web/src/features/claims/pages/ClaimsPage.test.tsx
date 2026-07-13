import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { listMyClaims } from "../api/claimsApi";
import { ClaimsPage } from "./ClaimsPage";

const getAccessTokenSilently = vi.fn();

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => ({
    getAccessTokenSilently,
  }),
}));

vi.mock("../api/claimsApi", () => ({
  listMyClaims: vi.fn(),
}));

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <ClaimsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("ClaimsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getAccessTokenSilently.mockResolvedValue("token-1");
  });

  it("lists the caller's claims with status and claim number", async () => {
    vi.mocked(listMyClaims).mockResolvedValue({
      claims: [
        {
          claimId: "claim-1",
          claimNumber: "CLM-CYB-20260401-AAAAAAAA",
          policyId: "policy-1",
          policyNumber: "LIP-CYB-20260101-BBBBBBBB",
          incidentType: "RansomwareExtortion",
          incidentAtUtc: "2026-03-10T08:00:00Z",
          discoveredAtUtc: "2026-03-12T09:30:00Z",
          status: "UnderReview",
          filedAtUtc: "2026-03-13T10:00:00Z",
          updatedAtUtc: "2026-03-14T10:00:00Z",
        },
      ],
    });

    renderPage();

    expect(
      await screen.findByText("CLM-CYB-20260401-AAAAAAAA"),
    ).toBeInTheDocument();
    expect(screen.getByText("UnderReview")).toBeInTheDocument();
    expect(screen.getByText(/LIP-CYB-20260101-BBBBBBBB/)).toBeInTheDocument();
  });

  it("shows an empty state when there are no claims", async () => {
    vi.mocked(listMyClaims).mockResolvedValue({ claims: [] });

    renderPage();

    expect(await screen.findByText(/no claims yet/i)).toBeInTheDocument();
  });

  it("shows a safe error message when loading fails", async () => {
    vi.mocked(listMyClaims).mockRejectedValue(new Error("Claims exploded."));

    renderPage();

    expect(await screen.findByText("Unable to load claims.")).toBeInTheDocument();
    expect(screen.queryByText("Claims exploded.")).not.toBeInTheDocument();
  });
});
