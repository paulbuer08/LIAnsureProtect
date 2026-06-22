import type {
  AdjustQuoteReferralRequest,
  AiUnderwritingReviewResponse,
  ListQuoteReferralsResponse,
  QuoteReferralReviewRequest,
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
