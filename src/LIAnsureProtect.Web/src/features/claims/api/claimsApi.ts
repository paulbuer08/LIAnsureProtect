import { downloadDocumentWithToken } from "../../../lib/documentDownload";
import { parseJsonResponse as parseApiJsonResponse } from "../../../lib/apiClient";
import type {
  AcceptClaimRequest,
  AddWorkNoteRequest,
  ClaimAdjudicationDetail,
  ClaimAdjudicationSummary,
  ClaimDecision,
  ClaimDetail,
  ClaimFinancials,
  ClaimInformationRequest,
  ClaimSummary,
  ClaimWorkNote,
  DenyClaimRequest,
  FileClaimRequest,
  ListAdjudicationQueueResponse,
  ListClaimablePoliciesResponse,
  ListMyClaimsResponse,
  RequestInformationRequest,
  RespondToInformationRequestRequest,
  SetClaimedAmountRequest,
  SetReserveRequest,
  UploadClaimDocumentsResponse,
} from "../types";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";
const claimsPath = `${apiBaseUrl}/api/v1/claims`;
const adjudicationPath = `${apiBaseUrl}/api/v1/claims/adjudication`;

function parseJsonResponse<T>(response: Response, notFoundMessage: string) {
  return parseApiJsonResponse<T>(response, { notFoundMessage });
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

function idempotentJsonHeaders(accessToken: string) {
  return {
    ...jsonHeaders(accessToken),
    "Idempotency-Key": crypto.randomUUID(),
  };
}

// --- Claimant ---

export async function listMyClaims(accessToken: string) {
  const response = await fetch(claimsPath, {
    headers: authHeaders(accessToken),
  });

  return parseJsonResponse<ListMyClaimsResponse>(
    response,
    "Claims were not found.",
  );
}

export async function getClaimDetail(accessToken: string, claimId: string) {
  const response = await fetch(`${claimsPath}/${claimId}`, {
    headers: authHeaders(accessToken),
  });

  return parseJsonResponse<ClaimDetail>(response, "Claim was not found.");
}

export async function listClaimablePolicies(accessToken: string) {
  const response = await fetch(`${claimsPath}/policy-options`, {
    headers: authHeaders(accessToken),
  });

  return parseJsonResponse<ListClaimablePoliciesResponse>(
    response,
    "Policies were not found.",
  );
}

export async function fileClaim(accessToken: string, request: FileClaimRequest) {
  const response = await fetch(claimsPath, {
    method: "POST",
    headers: idempotentJsonHeaders(accessToken),
    body: JSON.stringify(request),
  });

  return parseJsonResponse<ClaimSummary>(response, "Policy was not found.");
}

export async function respondToInformationRequest(
  accessToken: string,
  claimId: string,
  informationRequestId: string,
  request: RespondToInformationRequestRequest,
) {
  const response = await fetch(
    `${claimsPath}/${claimId}/information-requests/${informationRequestId}/respond`,
    {
      method: "POST",
      headers: jsonHeaders(accessToken),
      body: JSON.stringify(request),
    },
  );

  return parseJsonResponse<ClaimInformationRequest>(
    response,
    "Information request was not found.",
  );
}

export async function setClaimedAmount(
  accessToken: string,
  claimId: string,
  request: SetClaimedAmountRequest,
) {
  const response = await fetch(`${claimsPath}/${claimId}/claimed-amount`, {
    method: "POST",
    headers: jsonHeaders(accessToken),
    body: JSON.stringify(request),
  });

  return parseJsonResponse<ClaimFinancials>(response, "Claim was not found.");
}

export async function uploadClaimDocuments(
  accessToken: string,
  claimId: string,
  kind: string,
  files: File[],
) {
  const formData = new FormData();
  formData.append("kind", kind);
  for (const file of files) {
    formData.append("attachments", file);
  }

  const response = await fetch(`${claimsPath}/${claimId}/documents`, {
    method: "POST",
    headers: authHeaders(accessToken),
    body: formData,
  });

  return parseJsonResponse<UploadClaimDocumentsResponse>(
    response,
    "Claim was not found.",
  );
}

export async function downloadOwnerClaimDocument(
  accessToken: string,
  claimId: string,
  documentId: string,
  fileName: string,
) {
  await downloadDocumentWithToken(
    `${claimsPath}/${claimId}/documents/${documentId}/download`,
    accessToken,
    fileName,
  );
}

// --- Adjudication ---

export async function listAdjudicationQueue(accessToken: string) {
  const response = await fetch(adjudicationPath, {
    headers: authHeaders(accessToken),
  });

  return parseJsonResponse<ListAdjudicationQueueResponse>(
    response,
    "Adjudication queue was not found.",
  );
}

export async function getAdjudicationDetail(
  accessToken: string,
  claimId: string,
) {
  const response = await fetch(`${adjudicationPath}/${claimId}`, {
    headers: authHeaders(accessToken),
  });

  return parseJsonResponse<ClaimAdjudicationDetail>(
    response,
    "Claim was not found.",
  );
}

export async function assignClaimToMe(accessToken: string, claimId: string) {
  const response = await fetch(`${adjudicationPath}/${claimId}/assign-to-me`, {
    method: "POST",
    headers: authHeaders(accessToken),
  });

  return parseJsonResponse<ClaimAdjudicationSummary>(
    response,
    "Claim was not found.",
  );
}

export async function releaseClaimAssignment(
  accessToken: string,
  claimId: string,
) {
  const response = await fetch(
    `${adjudicationPath}/${claimId}/release-assignment`,
    {
      method: "POST",
      headers: authHeaders(accessToken),
    },
  );

  return parseJsonResponse<ClaimAdjudicationSummary>(
    response,
    "Claim was not found.",
  );
}

export async function addClaimWorkNote(
  accessToken: string,
  claimId: string,
  request: AddWorkNoteRequest,
) {
  const response = await fetch(`${adjudicationPath}/${claimId}/notes`, {
    method: "POST",
    headers: jsonHeaders(accessToken),
    body: JSON.stringify(request),
  });

  return parseJsonResponse<ClaimWorkNote>(response, "Claim was not found.");
}

export async function requestClaimInformation(
  accessToken: string,
  claimId: string,
  request: RequestInformationRequest,
) {
  const response = await fetch(
    `${adjudicationPath}/${claimId}/information-requests`,
    {
      method: "POST",
      headers: jsonHeaders(accessToken),
      body: JSON.stringify(request),
    },
  );

  return parseJsonResponse<ClaimInformationRequest>(
    response,
    "Claim was not found.",
  );
}

export async function setClaimReserve(
  accessToken: string,
  claimId: string,
  request: SetReserveRequest,
) {
  const response = await fetch(`${adjudicationPath}/${claimId}/reserve`, {
    method: "POST",
    headers: jsonHeaders(accessToken),
    body: JSON.stringify(request),
  });

  return parseJsonResponse<ClaimFinancials>(response, "Claim was not found.");
}

export async function acceptClaim(
  accessToken: string,
  claimId: string,
  request: AcceptClaimRequest,
) {
  const response = await fetch(`${adjudicationPath}/${claimId}/accept`, {
    method: "POST",
    headers: idempotentJsonHeaders(accessToken),
    body: JSON.stringify(request),
  });

  return parseJsonResponse<ClaimDecision>(response, "Claim was not found.");
}

export async function denyClaim(
  accessToken: string,
  claimId: string,
  request: DenyClaimRequest,
) {
  const response = await fetch(`${adjudicationPath}/${claimId}/deny`, {
    method: "POST",
    headers: idempotentJsonHeaders(accessToken),
    body: JSON.stringify(request),
  });

  return parseJsonResponse<ClaimDecision>(response, "Claim was not found.");
}

export async function closeClaim(accessToken: string, claimId: string) {
  const response = await fetch(`${adjudicationPath}/${claimId}/close`, {
    method: "POST",
    headers: authHeaders(accessToken),
  });

  return parseJsonResponse<ClaimDecision>(response, "Claim was not found.");
}

export async function downloadAdjudicationClaimDocument(
  accessToken: string,
  claimId: string,
  documentId: string,
  fileName: string,
) {
  await downloadDocumentWithToken(
    `${adjudicationPath}/${claimId}/documents/${documentId}/download`,
    accessToken,
    fileName,
  );
}
