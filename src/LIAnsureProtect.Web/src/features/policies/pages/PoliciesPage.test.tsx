import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { describe, expect, it, vi } from "vitest";

import { usePolicies } from "../hooks/usePolicies";
import { PoliciesPage } from "./PoliciesPage";

vi.mock("../hooks/usePolicies", () => ({ usePolicies: vi.fn() }));

describe("PoliciesPage", () => {
  it("presents coverage independently from the source submission", () => {
    vi.mocked(usePolicies).mockReturnValue({
      data: {
        policies: [{
          policyId: "policy-1",
          policyNumber: "LAP-2026-001",
          contractualStatus: "Bound",
          coverageState: "Active",
          effectiveDateUtc: "2026-07-01T00:00:00Z",
          expirationDateUtc: "2027-07-01T00:00:00Z",
          premium: 12000,
          requestedLimit: 1000000,
          retention: 10000,
          quoteId: "quote-1",
          submissionId: "submission-1",
          quoteStatusAtBind: "Accepted",
          quoteRiskTierAtBind: "Moderate",
          quoteSubjectivitiesAtBind: ["Maintain MFA."],
          applicantName: "Jane Applicant",
          companyName: "Example Company",
        }],
      },
      isPending: false,
      isError: false,
      isSuccess: true,
    } as unknown as ReturnType<typeof usePolicies>);

    render(<MemoryRouter><PoliciesPage /></MemoryRouter>);

    expect(screen.getByText("LAP-2026-001")).toBeInTheDocument();
    expect(screen.getByText("Active")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "View policy" })).toHaveAttribute("href", "/policies/policy-1");
  });
});
