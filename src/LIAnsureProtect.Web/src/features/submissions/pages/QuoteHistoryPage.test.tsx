import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useQuoteHistory } from "../hooks/useQuoteHistory";
import { QuoteHistoryPage } from "./QuoteHistoryPage";

vi.mock("../hooks/useQuoteHistory", () => ({ useQuoteHistory: vi.fn() }));

describe("QuoteHistoryPage", () => {
  beforeEach(() => vi.mocked(useQuoteHistory).mockReset());

  it("shows one current quote and keeps its superseded predecessor navigable", () => {
    vi.mocked(useQuoteHistory).mockReturnValue({
      data: {
        quotes: [
          {
            quoteId: "quote-2",
            submissionId: "submission-1",
            version: 2,
            status: "Quoted",
            premium: 7_200,
            requestedLimit: 1_000_000,
            retention: 10_000,
            riskTier: "Moderate",
            assuranceStatus: "EvidenceRequired",
            evidenceRequiredCount: 2,
            evidenceSatisfiedCount: 0,
            createdAtUtc: "2026-07-16T02:00:00Z",
            expiresAtUtc: "2026-08-16T02:00:00Z",
            supersedesQuoteId: "quote-1",
          },
          {
            quoteId: "quote-1",
            submissionId: "submission-1",
            version: 1,
            status: "Superseded",
            premium: 8_000,
            requestedLimit: 1_000_000,
            retention: 10_000,
            riskTier: "High",
            assuranceStatus: "EvidenceRequired",
            evidenceRequiredCount: 4,
            evidenceSatisfiedCount: 0,
            createdAtUtc: "2026-07-15T02:00:00Z",
            expiresAtUtc: "2026-08-15T02:00:00Z",
            supersededAtUtc: "2026-07-16T02:00:00Z",
          },
        ],
      },
      isPending: false,
      isError: false,
    } as ReturnType<typeof useQuoteHistory>);

    render(
      <MemoryRouter initialEntries={["/submissions/submission-1/quotes"]}>
        <Routes>
          <Route path="/submissions/:submissionId/quotes" element={<QuoteHistoryPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByRole("heading", { name: "All quote versions" })).toBeInTheDocument();
    expect(screen.getByText("Current")).toBeInTheDocument();
    expect(screen.getByText("Superseded")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "View quote version 1" })).toHaveAttribute(
      "href",
      "/submissions/submission-1/quotes/quote-1",
    );
  });
});
