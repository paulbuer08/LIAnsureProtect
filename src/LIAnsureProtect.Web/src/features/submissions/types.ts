export type CreateSubmissionRequest = {
  applicantName: string;
  applicantEmail: string;
  companyName: string;
  createAnotherDraft?: boolean;
};

export type CreateSubmissionResponse = {
  submissionId: string;
  submissionReference?: string;
  status: string;
  possibleDuplicate: boolean;
  existingDraft: boolean;
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

export type SensitiveDataExposure = "Unknown" | "Low" | "Moderate" | "High";

export type CyberControlDetails = {
  mfaCoversPrivilegedAccess: boolean;
  mfaCoversEmail: boolean;
  mfaCoversRemoteAccess: boolean;
  mfaCoversWorkforce: boolean;
  mfaPhishingResistant: boolean;
  edrCoveragePercent: number;
  edrCoversServers: boolean;
  edrActivelyMonitored: boolean;
  edrTamperProtection: boolean;
  backupsImmutableOrOffline: boolean;
  backupCredentialsSeparated: boolean;
  restoreTestedLast12Months: boolean;
  recoveryPointObjectiveHours: number;
  recoveryTimeObjectiveHours: number;
  incidentPlanApproved: boolean;
  incidentPlanUpdatedLast12Months: boolean;
  incidentPlanTestedLast12Months: boolean;
  incidentRolesNamed: boolean;
  sensitiveDataInventoryMaintained: boolean;
  sensitiveDataEncrypted: boolean;
  sensitiveDataTypes: string[];
  sensitiveDataVolume: string;
};

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
  attestationAccepted: boolean;
  attestedByName: string;
  attestedByTitle: string;
  isReassessment?: boolean;
  controlDetails: CyberControlDetails;
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
  version?: number;
  supersedesQuoteId?: string | null;
  assuranceStatus?: string;
  evidenceRequiredCount?: number;
  evidenceSatisfiedCount?: number;
  controlAssertions?: Array<{
    controlType: string;
    claimedState: string;
    assuranceState: string;
    evidenceRequired: boolean;
    evidenceReason: string;
    detailsJson: string;
  }>;
};

export type SubmissionQuoteSummary = Omit<
  CreateQuoteResponse,
  "submissionId" | "providerIndication"
>;

export type OwnedQuoteDetail = SubmissionQuoteSummary & {
  quoteId: string;
  submissionId: string;
  createdAtUtc: string;
};

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
  submissionReference?: string;
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
  nextCursor?: string | null;
};

export type SubmissionListFilters = {
  search?: string;
  status?: string;
  createdFromUtc?: string;
  createdToUtc?: string;
  cursor?: string;
  pageSize?: number;
};
