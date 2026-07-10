import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router";
import { describe, expect, it, vi } from "vitest";

import { useClaimablePolicies } from "../../claims/hooks/useClaims";
import { usePolicy } from "../hooks/usePolicies";
import { PolicyDetailPage } from "./PolicyDetailPage";

vi.mock("../hooks/usePolicies", () => ({ usePolicy: vi.fn() }));
vi.mock("../../claims/hooks/useClaims", () => ({ useClaimablePolicies: vi.fn() }));

const policy = {
  policyId: "policy-1", policyNumber: "LAP-2026-001", contractualStatus: "Bound", coverageState: "Active",
  effectiveDateUtc: "2026-07-01T00:00:00Z", expirationDateUtc: "2027-07-01T00:00:00Z", premium: 12000,
  requestedLimit: 1000000, retention: 10000, quoteId: "quote-1", submissionId: "submission-1",
  quoteStatusAtBind: "Accepted", quoteRiskTierAtBind: "Moderate", quoteSubjectivitiesAtBind: ["Maintain MFA."],
  applicantName: "Jane Applicant", companyName: "Example Company",
};

describe("PolicyDetailPage", () => {
  it("shows the contract first and only offers filing when Claims says it is eligible", () => {
    vi.mocked(usePolicy).mockReturnValue({ data: policy, isPending: false, isError: false } as ReturnType<typeof usePolicy>);
    vi.mocked(useClaimablePolicies).mockReturnValue({ data: { policies: [{ policyId: "policy-1", policyNumber: "LAP-2026-001", effectiveAtUtc: policy.effectiveDateUtc, expirationAtUtc: policy.expirationDateUtc, limit: policy.requestedLimit, retention: policy.retention }] } } as ReturnType<typeof useClaimablePolicies>);

    render(<MemoryRouter initialEntries={["/policies/policy-1"]}><Routes><Route path="/policies/:policyId" element={<PolicyDetailPage />} /></Routes></MemoryRouter>);

    expect(screen.getByRole("heading", { name: "LAP-2026-001" })).toBeInTheDocument();
    expect(screen.getByText("Contractual status: Bound")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "File claim" })).toHaveAttribute("href", "/claims/new?policyId=policy-1");
    expect(screen.getByRole("link", { name: "Open source submission" })).toHaveAttribute("href", "/submissions/submission-1");
  });
});
