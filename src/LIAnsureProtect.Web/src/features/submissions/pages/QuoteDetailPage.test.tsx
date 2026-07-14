import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useQuoteDetail } from "../hooks/useQuoteDetail";
import { QuoteDetailPage } from "./QuoteDetailPage";

vi.mock("../hooks/useQuoteDetail", () => ({ useQuoteDetail: vi.fn() }));

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/submissions/submission-1/quotes/quote-2"]}>
      <Routes>
        <Route
          path="/submissions/:submissionId/quotes/:quoteId"
          element={<QuoteDetailPage />}
        />
      </Routes>
    </MemoryRouter>,
  );
}

describe("QuoteDetailPage", () => {
  beforeEach(() => vi.mocked(useQuoteDetail).mockReset());

  it("renders the exact immutable quote version reached from a notification", () => {
    vi.mocked(useQuoteDetail).mockReturnValue({
      data: {
        quoteId: "quote-2",
        submissionId: "submission-1",
        version: 2,
        premium: 12_500,
        requestedLimit: 1_000_000,
        retention: 25_000,
        riskTier: "Moderate",
        status: "Superseded",
        assuranceStatus: "Satisfied",
        expiresAtUtc: "2026-08-01T00:00:00Z",
        createdAtUtc: "2026-07-01T00:00:00Z",
      },
      isPending: false,
      isError: false,
    } as ReturnType<typeof useQuoteDetail>);

    renderPage();

    expect(screen.getByRole("heading", { name: "Quote version 2" })).toBeInTheDocument();
    expect(screen.getByText("₱12,500")).toBeInTheDocument();
    expect(screen.getByText("Superseded")).toBeInTheDocument();
    expect(screen.getByText(/immutable historical quote/i)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Back to submission" })).toHaveAttribute(
      "href",
      "/submissions/submission-1",
    );
    expect(useQuoteDetail).toHaveBeenCalledWith("submission-1", "quote-2");
  });

  it("hides a raw local error behind safe quote guidance", () => {
    vi.mocked(useQuoteDetail).mockReturnValue({
      data: undefined,
      isPending: false,
      isError: true,
      error: new Error("Database connection details"),
    } as ReturnType<typeof useQuoteDetail>);

    renderPage();

    expect(screen.getByText("Unable to load this quote.")).toBeInTheDocument();
    expect(screen.queryByText("Database connection details")).not.toBeInTheDocument();
  });
});
