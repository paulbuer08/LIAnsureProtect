export type QuoteReferral = {
  quoteId: string;
  submissionId: string;
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
  responseText: string | null;
  attachmentFileName: string | null;
  attachmentContentType: string | null;
  attachmentSizeBytes: number | null;
  respondedAtUtc: string | null;
  acceptedByUserId: string | null;
  acceptedAtUtc: string | null;
  cancelledByUserId: string | null;
  cancelledAtUtc: string | null;
  reviewNotes: string | null;
  updatedAtUtc: string;
};

export type CreateQuoteEvidenceRequest = {
  category: string;
  title: string;
  description: string;
  dueAtUtc: string;
};

export type ReviewQuoteEvidenceRequest = {
  reviewNotes?: string | null;
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
