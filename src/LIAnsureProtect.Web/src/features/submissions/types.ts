export type CreateSubmissionRequest = {
  applicantName: string;
  applicantEmail: string;
  companyName: string;
};

export type CreateSubmissionResponse = {
  submissionId: string;
  status: string;
  possibleDuplicate: boolean;
};

export type SubmitSubmissionResponse = {
  submissionId: string;
  status: string;
};

export type UpdateSubmissionRequest = CreateSubmissionRequest;

export type CyberIndustryClass =
  | "ProfessionalServices"
  | "Technology"
  | "Retail"
  | "Healthcare"
  | "FinancialServices";

export type AnnualRevenueBand =
  | "Under1M"
  | "From1MTo10M"
  | "From10MTo50M"
  | "From50MTo250M";

export type CyberSecurityControlStatus =
  | "NotImplemented"
  | "Partial"
  | "Implemented";

export type BackupMaturity = "Weak" | "Partial" | "Mature";

export type SensitiveDataExposure = "Low" | "Moderate" | "High";

export type CreateQuoteRequest = {
  industryClass: CyberIndustryClass;
  annualRevenueBand: AnnualRevenueBand;
  requestedLimit: number;
  retention: number;
  mfaStatus: CyberSecurityControlStatus;
  edrStatus: CyberSecurityControlStatus;
  backupMaturity: BackupMaturity;
  hasIncidentResponsePlan: boolean;
  priorCyberIncidents: number;
  sensitiveDataExposure: SensitiveDataExposure;
  otherIndustryDescription?: string | null;
  priorCyberIncidentTypes?: string[] | null;
  priorCyberIncidentDetails?: string | null;
};

export type RatingProviderIndication = {
  providerName: string;
  status: string;
  marketDisposition: string;
  providerReference?: string | null;
  providerQuoteNumber?: string | null;
  indicatedPremium?: number | null;
  indicatedLimit?: number | null;
  indicatedRetention?: number | null;
  httpStatusCode?: number | null;
  failureCategory: string;
  failureReason?: string | null;
  attemptCount: number;
  durationMs: number;
};

export type CreateQuoteResponse = {
  quoteId: string;
  submissionId: string;
  premium: number;
  requestedLimit: number;
  retention: number;
  riskTier: string;
  status: string;
  subjectivities: string[];
  referralReasons: string[];
  expiresAtUtc: string;
  providerIndication: RatingProviderIndication;
};

export type SubmissionQuoteSummary = Omit<
  CreateQuoteResponse,
  "submissionId" | "providerIndication"
>;

export type AcceptQuoteRequest = {
  acceptedByName: string;
  acceptedByTitle: string;
  subjectivitiesAcknowledged: boolean;
};

export type AcceptQuoteResponse = {
  quoteId: string;
  submissionId: string;
  status: string;
  premium: number;
  requestedLimit: number;
  retention: number;
  subjectivities: string;
  expiresAtUtc: string;
  acceptedByUserId: string;
  acceptedByName: string;
  acceptedByTitle: string;
  subjectivitiesAcknowledged: boolean;
  acceptedAtUtc: string;
};

export type BindPolicyRequest = {
  effectiveDateUtc: string;
};

export type BindPolicyResponse = {
  policyId: string;
  policyNumber: string;
  quoteId: string;
  submissionId: string;
  status: string;
  premium: number;
  requestedLimit: number;
  retention: number;
  effectiveDateUtc: string;
  expirationDateUtc: string;
  boundByUserId: string;
  boundAtUtc: string;
  bindingProviderName: string;
  bindingReference: string;
};

export type SubmissionListItem = {
  submissionId: string;
  applicantName: string;
  applicantEmail: string;
  companyName: string;
  status: string;
  createdAtUtc: string;
};

export type SubmissionDetailResponse = SubmissionListItem & {
  latestQuote?: SubmissionQuoteSummary | null;
  relatedPolicy?: {
    policyId: string;
    policyNumber: string;
    contractualStatus: string;
    coverageState: string;
    effectiveDateUtc: string;
    expirationDateUtc: string;
  } | null;
};

export type WithdrawSubmissionResponse = SubmitSubmissionResponse;

export type ListSubmissionsResponse = {
  submissions: SubmissionListItem[];
};
