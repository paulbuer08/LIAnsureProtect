export type QuoteReferral = {
  quoteId: string;
  submissionId: string;
  submissionReference?: string;
  companyName?: string;
  ownerUserId: string;
  premium: number;
  requestedLimit: number;
  retention: number;
  riskTier: string;
  status: string;
  subjectivities: string[];
  referralReasons: string[];
  createdAtUtc: string;
  expiresAtUtc: string;
  operations: QuoteReferralOperationsSummary | null;
  evidence: QuoteReferralEvidenceSummary;
};

export type ListQuoteReferralsResponse = {
  quoteReferrals: QuoteReferral[];
};

export type QuoteReferralFilters = {
  search?: string;
  riskTier?: string;
  priority?: string;
  assignment?: string;
  evidenceState?: string;
};

export type QuoteReferralOperationsSummary = {
  assignedUnderwriterUserId: string | null;
  priority: string;
  dueAtUtc: string;
  isSlaBreached: boolean;
  status: string;
  openTaskCount: number;
  latestTimelineAtUtc: string | null;
};

export type QuoteReferralEvidenceSummary = {
  openRequestCount: number;
  respondedRequestCount: number;
  unreviewedRespondedRequestCount: number;
  satisfiedRequestCount: number;
  needsAttentionRequestCount: number;
  overdueRequestCount: number;
  nextOpenDueAtUtc: string | null;
  isWaitingForInformation: boolean;
  latestEvidenceActivityAtUtc: string | null;
};

export type QuoteReferralOperationResult = QuoteReferralOperationsSummary & {
  quoteId: string;
};

export type QuoteReferralTimelineEntry = {
  entryType: string;
  summary: string;
  createdByUserId: string;
  createdAtUtc: string;
};

export type QuoteReferralTimelineResponse = {
  quoteId: string;
  entries: QuoteReferralTimelineEntry[];
};

export type QuoteReferralTriageRequest = {
  priority: string;
  status: string;
  dueAtUtc: string;
};

export type QuoteReferralNoteRequest = {
  note: string;
};

export type QuoteReferralNoteResult = {
  noteId: string;
  quoteId: string;
  note: string;
  createdByUserId: string;
  createdAtUtc: string;
};

export type QuoteReferralTaskRequest = {
  title: string;
  dueAtUtc: string;
};

export type QuoteReferralTaskResult = {
  taskId: string;
  quoteId: string;
  title: string;
  dueAtUtc: string;
  isCompleted: boolean;
  createdByUserId: string;
  createdAtUtc: string;
  completedByUserId: string | null;
  completedAtUtc: string | null;
};

export type QuoteEvidenceRequest = {
  evidenceRequestId: string;
  quoteId: string;
  submissionId: string;
  submissionReference?: string;
  companyName?: string;
  documentRequirement?: "Required" | "Optional" | "NarrativeOnly";
  category: string;
  title: string;
  description: string;
  dueAtUtc: string;
  status: string;
  isOverdue: boolean;
  daysUntilDue: number;
  requestedByUserId: string;
  requestedAtUtc: string;
  respondedByUserId: string | null;
  respondentName: string | null;
  respondentTitle: string | null;
  respondentEmail?: string | null;
  respondentPhone?: string | null;
  respondentMobileNumber?: string | null;
  respondentTelephoneNumber?: string | null;
  responseText: string | null;
  otherConcerns?: string | null;
  attachmentFileName: string | null;
  attachmentContentType: string | null;
  attachmentSizeBytes: number | null;
  respondedAtUtc: string | null;
  acceptedByUserId: string | null;
  acceptedAtUtc: string | null;
  cancelledByUserId: string | null;
  cancelledAtUtc: string | null;
  reviewDecision: string;
  reviewReason: string | null;
  remediationGuidance: string | null;
  reviewedByUserId: string | null;
  reviewedAtUtc: string | null;
  reviewNotes: string | null;
  updatedAtUtc: string;
  documents?: QuoteEvidenceDocument[];
  responses?: QuoteEvidenceResponse[];
  pendingFollowUpCount?: number;
  maxPendingFollowUps?: number;
};

export type QuoteEvidenceResponse = {
  responseId: string;
  respondedByUserId: string;
  respondentName: string;
  respondentTitle: string;
  respondentEmail: string;
  respondentPhone: string | null;
  respondentMobileNumber?: string | null;
  respondentTelephoneNumber?: string | null;
  responseText: string | null;
  otherConcerns: string | null;
  kind: "Initial" | "FollowUp" | "Remediation";
  respondedAtUtc: string;
  viewedByUserId?: string | null;
  viewedAtUtc?: string | null;
};

export type QuoteEvidenceDocument = {
  documentId: string;
  evidenceResponseId?: string | null;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedByUserId: string;
  uploadedAtUtc: string;
  scanStatus: string;
  scannerProviderName: string | null;
  scanResultCode: string | null;
  scanResultReason: string | null;
  scannedAtUtc: string | null;
  sha256: string | null;
  isDownloadAvailable: boolean;
  assessmentVersion?: string | null;
  plausibilityStatus?: string | null;
  claimConsistencyStatus?: string | null;
  advisoryFindings?: string[];
};

export type CreateQuoteEvidenceRequest = {
  category: string;
  title: string;
  description: string;
  dueAtUtc: string;
  documentRequirement?: "Required" | "Optional" | "NarrativeOnly";
};

export type ReviewQuoteEvidenceRequest = {
  reviewNotes?: string | null;
};

export type RecordQuoteEvidenceReviewDecisionRequest = {
  decision: string;
  reason: string;
  remediationGuidance?: string | null;
};

export type AiUnderwritingReviewResponse = {
  reviewId: string;
  quoteId: string;
  submissionId: string;
  status: string;
  providerName: string;
  promptVersion: string;
  outputSchemaVersion: string;
  inputSnapshotHash: string;
  executiveSummary: string | null;
  positiveRiskSignals: string[];
  negativeRiskSignals: string[];
  controlGaps: string[];
  suggestedUnderwritingQuestions: string[];
  suggestedSubjectivityCandidates: string[];
  citations: string[];
  limitations: string[];
  advisoryDisclaimer: string | null;
  failureReason: string | null;
  createdAtUtc: string;
  completedAtUtc: string;
};

export type QuoteReferralReviewRequest = {
  reason: string;
  notes?: string | null;
};

export type AdjustQuoteReferralRequest = QuoteReferralReviewRequest & {
  adjustedPremium: number;
  adjustedRetention: number;
  updatedSubjectivities?: string | null;
};

export type UnderwriteQuoteReferralResult = {
  quoteId: string;
  submissionId: string;
  status: string;
  premium: number;
  requestedLimit: number;
  retention: number;
  reviewedByUserId: string;
  reviewedAtUtc: string;
  underwritingDecisionReason: string;
  underwritingDecisionNotes: string | null;
};
