export type Policy = {
  policyId: string;
  policyNumber: string;
  contractualStatus: string;
  coverageState: string;
  effectiveDateUtc: string;
  expirationDateUtc: string;
  premium: number;
  requestedLimit: number;
  retention: number;
  quoteId: string;
  submissionId: string;
  submissionReference?: string;
  quoteStatusAtBind: string;
  quoteRiskTierAtBind: string;
  quoteSubjectivitiesAtBind: string[];
  applicantName: string;
  companyName: string;
};

export type ListPoliciesResponse = { policies: Policy[] };

export type PolicyFilters = { search?: string; contractualStatus?: string; coverageState?: string };
