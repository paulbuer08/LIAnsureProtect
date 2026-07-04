export type ClaimSummary = {
  claimId: string;
  claimNumber: string;
  policyId: string;
  policyNumber: string;
  incidentType: string;
  incidentAtUtc: string;
  discoveredAtUtc: string;
  status: string;
  filedAtUtc: string;
  updatedAtUtc: string;
};

export type ListMyClaimsResponse = {
  claims: ClaimSummary[];
};

export type ClaimTimelineEntry = {
  entryId: string;
  entryType: string;
  summary: string;
  createdByUserId: string;
  createdAtUtc: string;
};

export type ClaimInformationRequest = {
  informationRequestId: string;
  claimId: string;
  title: string;
  message: string;
  requestedByUserId: string;
  requestedAtUtc: string;
  isAnswered: boolean;
  responseText: string | null;
  respondedByUserId: string | null;
  respondedAtUtc: string | null;
};

export type ClaimDocument = {
  documentId: string;
  claimId: string;
  kind: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  scanStatus: string;
  scanResultReason: string | null;
  isDownloadAvailable: boolean;
  uploadedByUserId: string;
  uploadedAtUtc: string;
};

export type ClaimDetail = ClaimSummary & {
  description: string;
  claimedAmount: number | null;
  paidAmount: number;
  settlementAmount: number | null;
  denialReason: string | null;
  denialNarrative: string | null;
  decidedAtUtc: string | null;
  closedAtUtc: string | null;
  policyLimitAtFiling: number;
  policyRetentionAtFiling: number;
  policyEffectiveAtFiling: string;
  policyExpirationAtFiling: string;
  timeline: ClaimTimelineEntry[];
  informationRequests: ClaimInformationRequest[];
  documents: ClaimDocument[];
};

export type ClaimablePolicy = {
  policyId: string;
  policyNumber: string;
  effectiveAtUtc: string;
  expirationAtUtc: string;
  limit: number;
  retention: number;
};

export type ListClaimablePoliciesResponse = {
  policies: ClaimablePolicy[];
};

export type FileClaimRequest = {
  policyId: string;
  incidentType: string;
  incidentAtUtc: string;
  discoveredAtUtc: string;
  description: string;
};

export type RespondToInformationRequestRequest = {
  responseText: string;
};

export type SetClaimedAmountRequest = {
  amount: number;
};

export type ClaimFinancials = {
  claimId: string;
  claimedAmount: number | null;
  reserveAmount: number;
  paidAmount: number;
  policyLimitAtFiling: number;
  policyRetentionAtFiling: number;
};

export type UploadClaimDocumentsResponse = {
  claimId: string;
  documents: ClaimDocument[];
};

export type ClaimAdjudicationSummary = {
  claimId: string;
  claimNumber: string;
  policyId: string;
  policyNumber: string;
  incidentType: string;
  incidentAtUtc: string;
  status: string;
  assignedAdjusterUserId: string | null;
  openInformationRequestCount: number;
  filedAtUtc: string;
  updatedAtUtc: string;
};

export type ListAdjudicationQueueResponse = {
  claims: ClaimAdjudicationSummary[];
};

export type ClaimWorkNote = {
  noteId: string;
  claimId: string;
  note: string;
  createdByUserId: string;
  createdAtUtc: string;
};

export type ClaimReserveChange = {
  changeId: string;
  oldAmount: number;
  newAmount: number;
  reason: string;
  changedByUserId: string;
  changedAtUtc: string;
};

export type ClaimDecision = {
  claimId: string;
  claimNumber: string;
  status: string;
  outcome: string;
  settlementAmount: number | null;
  paidAmount: number;
  denialReason: string | null;
  reason: string;
  notes: string | null;
  claimedAmountAtDecision: number | null;
  reserveAmountAtDecision: number;
  decidedByUserId: string;
  decidedAtUtc: string;
};

export type ClaimAdjudicationDetail = {
  claimId: string;
  claimNumber: string;
  policyId: string;
  policyNumber: string;
  ownerUserId: string;
  incidentType: string;
  incidentAtUtc: string;
  discoveredAtUtc: string;
  description: string;
  status: string;
  assignedAdjusterUserId: string | null;
  claimedAmount: number | null;
  reserveAmount: number;
  paidAmount: number;
  settlementAmount: number | null;
  denialReason: string | null;
  denialNarrative: string | null;
  decidedAtUtc: string | null;
  closedAtUtc: string | null;
  policyLimitAtFiling: number;
  policyRetentionAtFiling: number;
  policyEffectiveAtFiling: string;
  policyExpirationAtFiling: string;
  filedAtUtc: string;
  updatedAtUtc: string;
  reserveHistory: ClaimReserveChange[];
  decisions: ClaimDecision[];
  workNotes: ClaimWorkNote[];
  informationRequests: ClaimInformationRequest[];
  documents: ClaimDocument[];
  timeline: ClaimTimelineEntry[];
};

export type AddWorkNoteRequest = {
  note: string;
};

export type RequestInformationRequest = {
  title: string;
  message: string;
};

export type SetReserveRequest = {
  amount: number;
  reason: string;
};

export type AcceptClaimRequest = {
  settlementAmount: number;
  reason: string;
  notes: string | null;
};

export type DenyClaimRequest = {
  reasonCategory: string;
  narrative: string;
};

export const claimIncidentTypes = [
  "RansomwareExtortion",
  "BusinessEmailCompromise",
  "DataBreachPrivacy",
  "NetworkInterruption",
  "FundsTransferFraud",
  "Other",
] as const;

export const claimDocumentKinds = [
  "ProofOfLoss",
  "Invoice",
  "ForensicReport",
  "Correspondence",
  "Other",
] as const;

export const claimDenialReasons = [
  "NotCovered",
  "PolicyExclusion",
  "OutsidePolicyPeriod",
  "InsufficientEvidence",
  "MisrepresentationFraud",
  "Other",
] as const;
