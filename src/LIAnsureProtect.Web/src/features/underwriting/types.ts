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
};

export type ListQuoteReferralsResponse = {
  quoteReferrals: QuoteReferral[];
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
