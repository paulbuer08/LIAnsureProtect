import type {
  AdjustQuoteReferralRequest,
  AiUnderwritingReviewResponse,
  CreateQuoteEvidenceRequest,
  ListQuoteReferralsResponse,
  QuoteEvidenceRequest,
  QuoteReferralNoteRequest,
  QuoteReferralNoteResult,
  QuoteReferralOperationResult,
  QuoteReferralReviewRequest,
  QuoteReferralTaskRequest,
  QuoteReferralTaskResult,
  QuoteReferralTimelineResponse,
  QuoteReferralTriageRequest,
  ReviewQuoteEvidenceRequest,
  UnderwriteQuoteReferralResult,
} from "../types";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

async function parseJsonResponse<T>(
  response: Response,
  notFoundMessage: string,
) {
  if (response.status === 404) {
    throw new Error(notFoundMessage);
  }

  if (!response.ok) {
    const errorBody = await response.text();

    throw new Error(
      `API request failed with ${response.status} ${response.statusText}: ${errorBody}`,
    );
  }

  return (await response.json()) as T;
}

function authHeaders(accessToken: string) {
  return {
    Authorization: `Bearer ${accessToken}`,
  };
}

function jsonHeaders(accessToken: string) {
  return {
    ...authHeaders(accessToken),
    "Content-Type": "application/json",
  };
}

export async function listQuoteReferrals(accessToken: string) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/underwriting/quote-referrals`,
    {
      headers: authHeaders(accessToken),
    },
  );

  return parseJsonResponse<ListQuoteReferralsResponse>(
    response,
    "Underwriting referral queue was not found.",
  );
}

export async function generateAiUnderwritingReview(
  accessToken: string,
  quoteId: string,
) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/underwriting/quote-referrals/${quoteId}/ai-review`,
    {
      method: "POST",
      headers: authHeaders(accessToken),
    },
  );

  return parseJsonResponse<AiUnderwritingReviewResponse>(
    response,
    "Quote referral was not found.",
  );
}

export async function approveQuoteReferral(
  accessToken: string,
  quoteId: string,
  request: QuoteReferralReviewRequest,
) {
  return postUnderwritingAction(
    accessToken,
    quoteId,
    "approve",
    request,
  );
}

export async function declineQuoteReferral(
  accessToken: string,
  quoteId: string,
  request: QuoteReferralReviewRequest,
) {
  return postUnderwritingAction(
    accessToken,
    quoteId,
    "decline",
    request,
  );
}

export async function adjustQuoteReferral(
  accessToken: string,
  quoteId: string,
  request: AdjustQuoteReferralRequest,
) {
  return postUnderwritingAction(accessToken, quoteId, "adjust", request);
}

export async function assignQuoteReferralToMe(
  accessToken: string,
  quoteId: string,
) {
  return postOperationAction(accessToken, quoteId, "assign-to-me");
}

export async function releaseQuoteReferralAssignment(
  accessToken: string,
  quoteId: string,
) {
  return postOperationAction(accessToken, quoteId, "release-assignment");
}

export async function triageQuoteReferralOperation(
  accessToken: string,
  quoteId: string,
  request: QuoteReferralTriageRequest,
) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/underwriting/quote-referrals/${quoteId}/operations/triage`,
    {
      method: "POST",
      headers: jsonHeaders(accessToken),
      body: JSON.stringify(request),
    },
  );

  return parseJsonResponse<QuoteReferralOperationResult>(
    response,
    "Quote referral operations were not found.",
  );
}

export async function addQuoteReferralNote(
  accessToken: string,
  quoteId: string,
  request: QuoteReferralNoteRequest,
) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/underwriting/quote-referrals/${quoteId}/operations/notes`,
    {
      method: "POST",
      headers: jsonHeaders(accessToken),
      body: JSON.stringify(request),
    },
  );

  return parseJsonResponse<QuoteReferralNoteResult>(
    response,
    "Quote referral operations were not found.",
  );
}

export async function listQuoteReferralTimeline(
  accessToken: string,
  quoteId: string,
) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/underwriting/quote-referrals/${quoteId}/operations/timeline`,
    {
      headers: authHeaders(accessToken),
    },
  );

  return parseJsonResponse<QuoteReferralTimelineResponse>(
    response,
    "Quote referral operations were not found.",
  );
}

export async function addQuoteReferralTask(
  accessToken: string,
  quoteId: string,
  request: QuoteReferralTaskRequest,
) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/underwriting/quote-referrals/${quoteId}/operations/tasks`,
    {
      method: "POST",
      headers: jsonHeaders(accessToken),
      body: JSON.stringify(request),
    },
  );

  return parseJsonResponse<QuoteReferralTaskResult>(
    response,
    "Quote referral operations were not found.",
  );
}

export async function completeQuoteReferralTask(
  accessToken: string,
  quoteId: string,
  taskId: string,
) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/underwriting/quote-referrals/${quoteId}/operations/tasks/${taskId}/complete`,
    {
      method: "POST",
      headers: authHeaders(accessToken),
    },
  );

  return parseJsonResponse<QuoteReferralTaskResult>(
    response,
    "Quote referral operations were not found.",
  );
}

export async function createQuoteEvidenceRequest(
  accessToken: string,
  quoteId: string,
  request: CreateQuoteEvidenceRequest,
) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/underwriting/quote-referrals/${quoteId}/evidence-requests`,
    {
      method: "POST",
      headers: jsonHeaders(accessToken),
      body: JSON.stringify(request),
    },
  );

  return parseJsonResponse<QuoteEvidenceRequest>(
    response,
    "Quote referral was not found.",
  );
}

export async function acceptQuoteEvidenceRequest(
  accessToken: string,
  quoteId: string,
  evidenceRequestId: string,
  request: ReviewQuoteEvidenceRequest,
) {
  return postEvidenceReviewAction(
    accessToken,
    quoteId,
    evidenceRequestId,
    "accept",
    request,
  );
}

export async function cancelQuoteEvidenceRequest(
  accessToken: string,
  quoteId: string,
  evidenceRequestId: string,
  request: ReviewQuoteEvidenceRequest,
) {
  return postEvidenceReviewAction(
    accessToken,
    quoteId,
    evidenceRequestId,
    "cancel",
    request,
  );
}

export async function followUpQuoteEvidenceRequest(
  accessToken: string,
  quoteId: string,
  evidenceRequestId: string,
) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/underwriting/quote-referrals/${quoteId}/evidence-requests/${evidenceRequestId}/follow-up`,
    {
      method: "POST",
      headers: authHeaders(accessToken),
    },
  );

  return parseJsonResponse<QuoteEvidenceRequest>(
    response,
    "Evidence request was not found.",
  );
}

async function postUnderwritingAction(
  accessToken: string,
  quoteId: string,
  action: "approve" | "decline" | "adjust",
  request: QuoteReferralReviewRequest | AdjustQuoteReferralRequest,
) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/underwriting/quote-referrals/${quoteId}/${action}`,
    {
      method: "POST",
      headers: jsonHeaders(accessToken),
      body: JSON.stringify(request),
    },
  );

  return parseJsonResponse<UnderwriteQuoteReferralResult>(
    response,
    "Quote referral was not found.",
  );
}

async function postOperationAction(
  accessToken: string,
  quoteId: string,
  action: "assign-to-me" | "release-assignment",
) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/underwriting/quote-referrals/${quoteId}/operations/${action}`,
    {
      method: "POST",
      headers: authHeaders(accessToken),
    },
  );

  return parseJsonResponse<QuoteReferralOperationResult>(
    response,
    "Quote referral operations were not found.",
  );
}

async function postEvidenceReviewAction(
  accessToken: string,
  quoteId: string,
  evidenceRequestId: string,
  action: "accept" | "cancel",
  request: ReviewQuoteEvidenceRequest,
) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/underwriting/quote-referrals/${quoteId}/evidence-requests/${evidenceRequestId}/${action}`,
    {
      method: "POST",
      headers: jsonHeaders(accessToken),
      body: JSON.stringify(request),
    },
  );

  return parseJsonResponse<QuoteEvidenceRequest>(
    response,
    "Evidence request was not found.",
  );
}
