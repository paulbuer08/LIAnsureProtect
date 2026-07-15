import { useState } from "react";
import { useAuth0 } from "@auth0/auth0-react";
import type { FormEvent } from "react";
import { Link, useNavigate } from "react-router";

import { Breadcrumbs } from "../../../components/Breadcrumbs";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { FileDropzone } from "../../../components/FileDropzone";
import { TransientStatusMessage } from "../../../components/TransientStatusMessage";
import { getUserErrorMessage } from "../../../lib/apiClient";
import { downloadOwnerEvidenceDocument } from "../api/evidenceRequestsApi";
import {
  useEvidenceRequests,
  useRespondToEvidenceRequest,
  useUploadReplacementEvidenceDocuments,
} from "../hooks/useEvidenceRequests";
import type { EvidenceRequestSummary, QuoteEvidenceRequest } from "../types";

const dateFormatter = new Intl.DateTimeFormat("en-US", {
  month: "short",
  day: "numeric",
  year: "numeric",
});

const respondentNameMaxLength = 120;
const respondentTitleMaxLength = 120;
const respondentEmailMaxLength = 254;
const evidenceResponseMaxLength = 4000;
const otherConcernsMaxLength = 2000;

function compactPhone(value: string) {
  return value.trim().replace(/[\s()-]/g, "");
}

function getPhilippineMobileError(value: string) {
  const compact = compactPhone(value);
  if (compact.length === 0 || /^(?:09\d{9}|\+?639\d{9})$/.test(compact)) {
    return undefined;
  }

  return "Enter a Philippine mobile number such as 09171234567 or +639171234567.";
}

function getPhilippineTelephoneError(value: string) {
  const compact = compactPhone(value);
  if (compact.length === 0 || /^(?:0[2-8]\d{7,8}|\+?63[2-8]\d{7,8})$/.test(compact)) {
    return undefined;
  }

  return "Enter a Philippine landline with its area code, such as 02 8123 4567 or +63 2 8123 4567.";
}

function formatDate(value: string) {
  return dateFormatter.format(new Date(value));
}

function getErrorMessage(error: unknown, fallback: string) {
  return getUserErrorMessage(error, fallback);
}

function formatEvidenceDueLabel(
  request: Pick<QuoteEvidenceRequest, "isOverdue" | "daysUntilDue">,
) {
  if (request.isOverdue) {
    const overdueDays = Math.abs(request.daysUntilDue);

    return overdueDays === 1
      ? "Overdue by 1 day"
      : `Overdue by ${overdueDays} days`;
  }

  if (request.daysUntilDue === 0) return "Due today";
  if (request.daysUntilDue === 1) return "Due in 1 day";

  return `Due in ${request.daysUntilDue} days`;
}

export function EvidenceRequestCard({ request }: { request: QuoteEvidenceRequest }) {
  const navigate = useNavigate();
  const { getAccessTokenSilently } = useAuth0();
  const respondToEvidenceRequest = useRespondToEvidenceRequest();
  const uploadReplacementEvidenceDocuments = useUploadReplacementEvidenceDocuments();
  const isPreReviewResponse =
    request.status === "Responded" && request.reviewDecision === "NotReviewed";
  const [respondentName, setRespondentName] = useState(isPreReviewResponse ? request.respondentName ?? "" : "");
  const [respondentTitle, setRespondentTitle] = useState(isPreReviewResponse ? request.respondentTitle ?? "" : "");
  const [respondentEmail, setRespondentEmail] = useState(isPreReviewResponse ? request.respondentEmail ?? "" : "");
  const initialMobileNumber = isPreReviewResponse
    ? request.respondentMobileNumber ?? request.respondentPhone ?? ""
    : "";
  const initialTelephoneNumber = isPreReviewResponse
    ? request.respondentTelephoneNumber ?? ""
    : "";
  const [respondentMobileNumber, setRespondentMobileNumber] = useState(initialMobileNumber);
  const [respondentTelephoneNumber, setRespondentTelephoneNumber] = useState(initialTelephoneNumber);
  const [responseText, setResponseText] = useState("");
  const [otherConcerns, setOtherConcerns] = useState("");
  const [attachments, setAttachments] = useState<File[]>([]);
  const [replacementAttachments, setReplacementAttachments] = useState<File[]>([]);
  const [savedStatus, setSavedStatus] = useState<string>();
  const [savedDocuments, setSavedDocuments] = useState(request.documents ?? []);
  const [savedResponses, setSavedResponses] = useState(request.responses ?? []);
  const [pendingFollowUpCount, setPendingFollowUpCount] = useState(
    request.pendingFollowUpCount ?? 0,
  );
  const [showCancelConfirmation, setShowCancelConfirmation] = useState(false);
  const documents = savedDocuments.length > 0 ? savedDocuments : request.documents ?? [];
  const needsSupplementalEvidence =
    request.reviewDecision === "Insufficient" ||
    request.reviewDecision === "NeedsClarification";
  const isPreReviewFollowUp =
    request.status === "Responded" && request.reviewDecision === "NotReviewed";
  const canSubmitResponse =
    request.status === "Open" || isPreReviewFollowUp || needsSupplementalEvidence;
  const canUploadReplacement = documents.some(
    (document) => document.scanStatus === "Rejected" || document.scanStatus === "Failed",
  );

  const [downloadError, setDownloadError] = useState<string>();
  const documentRequirement = request.documentRequirement ?? "Optional";
  const requiresDocumentForThisResponse =
    documentRequirement === "Required" && !isPreReviewFollowUp;
  const hasContactChange =
    respondentName.trim() !== (request.respondentName ?? "").trim() ||
    respondentTitle.trim() !== (request.respondentTitle ?? "").trim() ||
    respondentEmail.trim().toLowerCase() !==
      (request.respondentEmail ?? "").trim().toLowerCase() ||
    respondentMobileNumber.trim() !== initialMobileNumber.trim() ||
    respondentTelephoneNumber.trim() !== initialTelephoneNumber.trim();
  const hasFollowUpContent =
    hasContactChange ||
    responseText.trim().length > 0 ||
    otherConcerns.trim().length > 0 ||
    attachments.length > 0;
  const respondentEmailError =
    respondentEmail.trim().length > 0 &&
    !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(respondentEmail.trim())
      ? "Enter a valid email address, such as name@example.com."
      : undefined;
  const respondentMobileError = getPhilippineMobileError(respondentMobileNumber);
  const respondentTelephoneError = getPhilippineTelephoneError(
    respondentTelephoneNumber,
  );
  const hasRequiredRespondentDetails =
    respondentName.trim().length > 0 &&
    respondentTitle.trim().length > 0 &&
    respondentEmail.trim().length > 0 &&
    respondentEmailError === undefined &&
    respondentMobileError === undefined &&
    respondentTelephoneError === undefined;
  const maxPendingFollowUps = request.maxPendingFollowUps ?? 5;
  const hasFollowUpCapacity = pendingFollowUpCount < maxPendingFollowUps;
  const hasRequiredNarrative =
    isPreReviewFollowUp || responseText.trim().length > 0;

  function discardResponseAndLeave() {
    setRespondentName("");
    setRespondentTitle("");
    setRespondentEmail("");
    setRespondentMobileNumber("");
    setRespondentTelephoneNumber("");
    setResponseText("");
    setOtherConcerns("");
    setAttachments([]);
    respondToEvidenceRequest.reset();
    setShowCancelConfirmation(false);
    void navigate("/evidence-requests", { replace: true });
  }

  function cancelResponse() {
    const hasUnsentWork = [
      respondentName,
      respondentTitle,
      respondentEmail,
      respondentMobileNumber,
      respondentTelephoneNumber,
      responseText,
      otherConcerns,
    ].some(
      (value) => value.trim().length > 0,
    ) || attachments.length > 0;
    if (hasUnsentWork) setShowCancelConfirmation(true);
    else discardResponseAndLeave();
  }

  async function handleDownloadDocument(documentId: string, fileName: string) {
    try {
      setDownloadError(undefined);
      const accessToken = await getAccessTokenSilently();
      await downloadOwnerEvidenceDocument(
        accessToken,
        request.evidenceRequestId,
        documentId,
        fileName,
      );
    } catch (error) {
      setDownloadError(getErrorMessage(error, "Unable to download the document."));
    }
  }

  async function handleRespond(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const result = await respondToEvidenceRequest.mutateAsync({
      evidenceRequestId: request.evidenceRequestId,
      request: {
        respondentName: respondentName.trim(),
        respondentTitle: respondentTitle.trim(),
        respondentEmail: respondentEmail.trim(),
        respondentMobileNumber: respondentMobileNumber.trim() || null,
        respondentTelephoneNumber: respondentTelephoneNumber.trim() || null,
        responseText: responseText.trim() || null,
        otherConcerns: otherConcerns.trim() || null,
        attachments,
      },
    });

    setSavedStatus(result.status);
    setSavedDocuments(result.documents ?? []);
    setSavedResponses(result.responses ?? []);
    setPendingFollowUpCount(result.pendingFollowUpCount ?? pendingFollowUpCount);
    setResponseText("");
    setOtherConcerns("");
    setAttachments([]);
  }

  async function handleReplacementUpload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const result = await uploadReplacementEvidenceDocuments.mutateAsync({
      evidenceRequestId: request.evidenceRequestId,
      attachments: replacementAttachments,
    });

    setSavedStatus(result.status);
    setSavedDocuments(result.documents ?? []);
    setReplacementAttachments([]);
  }

  return (
    <article className="rounded-lg border border-slate-800 bg-slate-900 p-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div>
          <h2 className="text-xl font-semibold text-white">{request.title}</h2>
          <p className="mt-1 text-sm text-slate-400">
            {request.companyName ?? "Company"} · {request.submissionReference ?? request.submissionId}
          </p>
        </div>
        <span className="w-fit rounded-md border border-amber-700 px-3 py-1 text-xs font-semibold text-amber-200">
          {request.status}
        </span>
        <span
          className={`w-fit rounded-md border px-3 py-1 text-xs font-semibold ${
            request.isOverdue
              ? "border-red-700 text-red-200"
              : "border-emerald-700 text-emerald-200"
          }`}
        >
          {formatEvidenceDueLabel(request)}
        </span>
      </div>

      <dl className="mt-4 grid gap-3 text-sm md:grid-cols-3">
        <div>
          <dt className="text-slate-400">Category</dt>
          <dd className="font-medium text-white">{request.category}</dd>
        </div>
        <div>
          <dt className="text-slate-400">Due</dt>
          <dd className="font-medium text-white">{formatDate(request.dueAtUtc)}</dd>
        </div>
        <div>
          <dt className="text-slate-400">Requested by</dt>
          <dd className="break-all font-medium text-white">{request.requestedByUserId}</dd>
        </div>
      </dl>

      <p className="mt-4 text-sm text-slate-300">{request.description}</p>
      {documentRequirement !== "Optional" && (
        <p className="mt-3 rounded-md border border-sky-800 bg-sky-950/30 p-3 text-sm text-sky-100">
          Document requirement: <strong>{documentRequirement === "NarrativeOnly" ? "Written response only" : documentRequirement}</strong>.
          {documentRequirement === "Required" && " The first response and any requested remediation must include at least one supporting file. You may add a later pre-review follow-up without repeating a file."}
          {documentRequirement === "NarrativeOnly" && " Underwriting will evaluate the named contact and written explanation; the response is still an assertion until a reviewer validates it."}
        </p>
      )}

      <section className="mt-4 rounded-md border border-slate-800 bg-slate-950 p-4 text-sm">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-slate-400">Review decision</span>
          <span className="rounded-md border border-cyan-800 px-2 py-1 text-xs font-semibold text-cyan-200">
            {request.reviewDecision}
          </span>
        </div>
        {request.reviewReason && (
          <p className="mt-3 text-slate-300">
            <span className="font-semibold text-white">Underwriter reason:</span>{" "}
            {request.reviewReason}
          </p>
        )}
        {request.remediationGuidance && (
          <p className="mt-2 text-amber-100">
            <span className="font-semibold text-white">What to send next:</span>{" "}
            {request.remediationGuidance}
          </p>
        )}
        {request.reviewedByUserId && request.reviewedAtUtc && (
          <p className="mt-2 text-xs text-slate-400">
            Reviewed by {request.reviewedByUserId} on{" "}
            {formatDate(request.reviewedAtUtc)}.
          </p>
        )}
      </section>

      {savedResponses.length > 0 && (
        <section className="mt-4 rounded-md border border-slate-800 bg-slate-950 p-4">
          <h3 className="text-sm font-semibold text-white">Response history</h3>
          <p className="mt-1 text-xs text-slate-400">
            Each submitted response is retained as audit history. A follow-up adds a new entry and never replaces an earlier answer.
          </p>
          <ol
            aria-label="Response history entries"
            tabIndex={0}
            className="mt-3 max-h-80 space-y-3 overflow-y-auto pr-2 focus:outline-none focus:ring-2 focus:ring-emerald-400"
          >
            {savedResponses.map((response) => (
              <li key={response.responseId} className="rounded-md border border-slate-800 p-3 text-sm">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="font-semibold text-white">{response.kind} response</span>
                  <time className="text-xs text-slate-400">{new Date(response.respondedAtUtc).toLocaleString()}</time>
                </div>
                <p className="mt-2 text-slate-300">
                  {response.respondentName} · {response.respondentTitle} · {response.respondentEmail}
                  {response.respondentMobileNumber ? ` · Mobile ${response.respondentMobileNumber}` : ""}
                  {response.respondentTelephoneNumber ? ` · Telephone ${response.respondentTelephoneNumber}` : ""}
                  {!response.respondentMobileNumber && !response.respondentTelephoneNumber && response.respondentPhone ? ` · Contact ${response.respondentPhone}` : ""}
                </p>
                {response.responseText && <p className="mt-2 whitespace-pre-wrap text-slate-200">{response.responseText}</p>}
                {response.otherConcerns && (
                  <p className="mt-2 whitespace-pre-wrap rounded-md border border-amber-900 bg-amber-950/20 p-2 text-amber-100">
                    <span className="font-semibold">Other concerns:</span> {response.otherConcerns}
                  </p>
                )}
              </li>
            ))}
          </ol>
        </section>
      )}

      {savedStatus && (
        <TransientStatusMessage
          className="mt-4 text-sm font-semibold"
          onDismiss={() => setSavedStatus(undefined)}
        >
          Evidence response saved: {savedStatus}
        </TransientStatusMessage>
      )}

      {respondToEvidenceRequest.error !== null &&
        respondToEvidenceRequest.error !== undefined && (
          <p className="mt-4 whitespace-pre-wrap rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">
            {getErrorMessage(
              respondToEvidenceRequest.error,
              "Unable to submit evidence response.",
            )}
          </p>
        )}

      {uploadReplacementEvidenceDocuments.error !== null &&
        uploadReplacementEvidenceDocuments.error !== undefined && (
          <p className="mt-4 whitespace-pre-wrap rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">
            {getErrorMessage(
              uploadReplacementEvidenceDocuments.error,
              "Unable to upload replacement evidence.",
            )}
          </p>
        )}

      {canSubmitResponse && (
        <form className="mt-5 space-y-4" onSubmit={handleRespond} noValidate>
          {isPreReviewFollowUp && (
            <div className="rounded-md border border-emerald-800 bg-emerald-950/30 p-3 text-sm text-emerald-100">
              <p>Underwriting has not reviewed this request yet. You may correct contact details or add a message, concern, or supporting file; the original response remains unchanged.</p>
              <p className="mt-2 font-semibold">Unread follow-ups: {pendingFollowUpCount} of {maxPendingFollowUps}</p>
              {!hasFollowUpCapacity && <p className="mt-2 text-amber-200">The current limit is reached. You can send another follow-up after an underwriter opens one of the pending entries.</p>}
            </div>
          )}
          <label className="block text-sm font-medium text-slate-200">
            Respondent name
            <input
              required
              maxLength={respondentNameMaxLength}
              value={respondentName}
              onChange={(event) => setRespondentName(event.target.value)}
              className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400"
            />
          </label>
          <label className="block text-sm font-medium text-slate-200">
            Respondent title
            <input
              required
              maxLength={respondentTitleMaxLength}
              value={respondentTitle}
              onChange={(event) => setRespondentTitle(event.target.value)}
              className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400"
            />
          </label>
          <label className="block text-sm font-medium text-slate-200">
            Respondent email
            <input
              required
              type="email"
              maxLength={respondentEmailMaxLength}
              value={respondentEmail}
              onChange={(event) => setRespondentEmail(event.target.value)}
              aria-invalid={respondentEmailError !== undefined}
              aria-describedby={`respondent-email-help-${request.evidenceRequestId}`}
              className={`mt-2 w-full rounded-lg border bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400 ${respondentEmailError ? "border-red-500" : "border-slate-700"}`}
            />
            <span id={`respondent-email-help-${request.evidenceRequestId}`} className={`mt-2 block text-xs ${respondentEmailError ? "text-red-300" : "text-slate-400"}`}>
              {respondentEmailError ?? "Underwriting may use this address to verify the response. It is not treated as proof by itself."}
            </span>
          </label>
          <label className="block text-sm font-medium text-slate-200">
            Respondent mobile number (optional)
            <input
              type="tel"
              inputMode="tel"
              maxLength={24}
              placeholder="0917 123 4567 or +63 917 123 4567"
              value={respondentMobileNumber}
              onChange={(event) => setRespondentMobileNumber(event.target.value)}
              aria-invalid={respondentMobileError !== undefined}
              aria-describedby={`respondent-mobile-help-${request.evidenceRequestId}`}
              className={`mt-2 w-full rounded-lg border bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400 ${respondentMobileError ? "border-red-500" : "border-slate-700"}`}
            />
            <span id={`respondent-mobile-help-${request.evidenceRequestId}`} className={`mt-2 block text-xs ${respondentMobileError ? "text-red-300" : "text-slate-400"}`}>
              {respondentMobileError ?? "Philippine mobile numbers use 11 domestic digits beginning with 09, or country code +63 followed by 10 digits beginning with 9."}
            </span>
          </label>
          <label className="block text-sm font-medium text-slate-200">
            Respondent telephone number (optional)
            <input
              type="tel"
              inputMode="tel"
              maxLength={24}
              placeholder="02 8123 4567 or +63 2 8123 4567"
              value={respondentTelephoneNumber}
              onChange={(event) => setRespondentTelephoneNumber(event.target.value)}
              aria-invalid={respondentTelephoneError !== undefined}
              aria-describedby={`respondent-telephone-help-${request.evidenceRequestId}`}
              className={`mt-2 w-full rounded-lg border bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400 ${respondentTelephoneError ? "border-red-500" : "border-slate-700"}`}
            />
            <span id={`respondent-telephone-help-${request.evidenceRequestId}`} className={`mt-2 block text-xs ${respondentTelephoneError ? "text-red-300" : "text-slate-400"}`}>
              {respondentTelephoneError ?? "Include the Philippine area code. Metro Manila commonly uses 02 domestically or +63 2 internationally."}
            </span>
          </label>
          <label className="block text-sm font-medium text-slate-200">
            Evidence response
            <textarea
              required={!isPreReviewFollowUp}
              maxLength={evidenceResponseMaxLength}
              value={responseText}
              onChange={(event) => setResponseText(event.target.value)}
              className="mt-2 min-h-24 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400"
            />
            {isPreReviewFollowUp && <span className="mt-2 block text-xs text-slate-400">Optional for a follow-up when you add a concern or file instead.</span>}
            <span className="mt-1 block text-right text-xs text-slate-500">{responseText.length}/{evidenceResponseMaxLength}</span>
          </label>
          <label className="block text-sm font-medium text-slate-200">
            Other concerns (optional)
            <textarea
              value={otherConcerns}
              maxLength={otherConcernsMaxLength}
              onChange={(event) => setOtherConcerns(event.target.value)}
              className="mt-2 min-h-20 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white outline-none focus:border-emerald-400"
            />
            <span className="mt-2 block text-xs text-slate-400">Use this for context, caveats, or questions that do not belong in the main evidence explanation.</span>
            <span className="mt-1 block text-right text-xs text-slate-500">{otherConcerns.length}/{otherConcernsMaxLength}</span>
          </label>
          {documentRequirement !== "NarrativeOnly" && (
            <FileDropzone
              label="Evidence files"
              description="Upload up to 5 files. Supported formats: PDF, PNG, JPEG, TXT, CSV, DOCX, and XLSX."
              accept=".pdf,.png,.jpg,.jpeg,.txt,.csv,.docx,.xlsx"
              files={attachments}
              onFilesChange={setAttachments}
              required={requiresDocumentForThisResponse}
              disabled={respondToEvidenceRequest.isPending}
            />
          )}
          {(respondentEmailError || respondentMobileError || respondentTelephoneError) && (
            <p role="alert" className="rounded-md border border-red-800 bg-red-950/50 p-3 text-sm text-red-200">
              Correct the highlighted contact details before submitting this evidence response.
            </p>
          )}
          <div className="flex flex-wrap gap-3">
            <button
              type="submit"
              disabled={
                respondToEvidenceRequest.isPending ||
                !hasRequiredRespondentDetails ||
                !hasRequiredNarrative ||
                (requiresDocumentForThisResponse && attachments.length === 0) ||
                (isPreReviewFollowUp && (!hasFollowUpContent || !hasFollowUpCapacity))
              }
              className="rounded-lg bg-emerald-400 px-5 py-3 text-sm font-semibold text-slate-950 hover:bg-emerald-300 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-400"
            >
              {isPreReviewFollowUp
                ? "Send follow-up"
                : needsSupplementalEvidence
                  ? "Submit remediation evidence"
                  : "Submit evidence response"}
            </button>
            <button type="button" onClick={cancelResponse} className="rounded-lg border border-slate-600 px-5 py-3 text-sm font-semibold text-slate-100 hover:border-slate-400">Cancel</button>
          </div>
        </form>
      )}

      {showCancelConfirmation && (
        <ConfirmationDialog
          title="Cancel this evidence response?"
          description="Your unsent text and selected files exist only on this page. Leaving now will discard them; the evidence request itself will remain open."
          confirmLabel="Discard response"
          tone="warning"
          onCancel={() => setShowCancelConfirmation(false)}
          onConfirm={discardResponseAndLeave}
        />
      )}

      {documents.length > 0 && (
        <section className="mt-5 rounded-md border border-slate-800 bg-slate-950 p-4">
          <h3 className="text-sm font-semibold text-white">Evidence documents</h3>
          {downloadError && (
            <p className="mt-2 rounded-md border border-red-900 bg-red-950 p-2 text-xs text-red-200">
              {downloadError}
            </p>
          )}
          <ul className="mt-3 space-y-2 text-sm">
            {documents.map((document) => (
              <li key={document.documentId} className="rounded-md border border-slate-800 p-3">
                {document.isDownloadAvailable ? (
                  <button
                    type="button"
                    onClick={() =>
                      void handleDownloadDocument(
                        document.documentId,
                        document.originalFileName,
                      )
                    }
                    className="cursor-pointer font-semibold text-emerald-300 hover:text-emerald-200"
                  >
                    Download {document.originalFileName}
                  </button>
                ) : (
                  <span className="font-semibold text-slate-200">
                    {document.originalFileName}
                  </span>
                )}
                <span className="ml-2 text-slate-400">
                  {document.contentType} | {document.sizeBytes.toLocaleString()} bytes
                </span>
                <div className="mt-2 flex flex-wrap items-center gap-2">
                  <span className="rounded-md border border-cyan-800 px-2 py-1 text-xs font-semibold text-cyan-200">
                    {document.scanStatus}
                  </span>
                  {document.scanResultReason && (
                    <span className="text-xs text-slate-400">
                      {document.scanResultReason}
                    </span>
                  )}
                </div>
                {!document.isDownloadAvailable && (
                  <p className="mt-2 text-xs text-amber-200">
                    Download unavailable until security screening is clean.
                  </p>
                )}
              </li>
            ))}
          </ul>
          {canUploadReplacement && (
            <form className="mt-4 space-y-3" onSubmit={handleReplacementUpload}>
              <FileDropzone
                label="Replacement evidence files"
                description="Upload up to 5 clean replacement files in PDF, PNG, JPEG, TXT, CSV, DOCX, or XLSX format."
                accept=".pdf,.png,.jpg,.jpeg,.txt,.csv,.docx,.xlsx"
                files={replacementAttachments}
                onFilesChange={setReplacementAttachments}
                disabled={uploadReplacementEvidenceDocuments.isPending}
                tone="amber"
              />
              <button
                type="submit"
                disabled={
                  uploadReplacementEvidenceDocuments.isPending ||
                  replacementAttachments.length === 0
                }
                className="rounded-lg bg-amber-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-amber-200 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-400"
              >
                Upload replacement evidence
              </button>
            </form>
          )}
        </section>
      )}
    </article>
  );
}

export function EvidenceRequestsPage() {
  const [status, setStatus] = useState("");
  const [search, setSearch] = useState("");
  const [appliedSearch, setAppliedSearch] = useState("");
  const [category, setCategory] = useState("");
  const [reviewDecision, setReviewDecision] = useState("");
  const [documentRequirement, setDocumentRequirement] = useState("");
  const [overdueOnly, setOverdueOnly] = useState(false);
  const [cursor, setCursor] = useState<string>();
  const [cursorHistory, setCursorHistory] = useState<Array<string | undefined>>([]);
  const evidenceRequestsQuery = useEvidenceRequests({
    status: status || undefined,
    search: appliedSearch || undefined,
    category: category || undefined,
    reviewDecision: reviewDecision || undefined,
    documentRequirement: documentRequirement || undefined,
    overdue: overdueOnly || undefined,
    cursor,
    pageSize: 12,
  });
  const evidenceRequests = evidenceRequestsQuery.data?.evidenceRequests ?? [];

  function resetPage() {
    setCursor(undefined);
    setCursorHistory([]);
  }

  function openNextPage() {
    const next = evidenceRequestsQuery.data?.nextCursor;
    if (!next) return;
    setCursorHistory((history) => [...history, cursor]);
    setCursor(next);
  }

  function openPreviousPage() {
    setCursorHistory((history) => {
      const previous = history.at(-1);
      setCursor(previous);
      return history.slice(0, -1);
    });
  }

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
      <section className="mx-auto max-w-5xl">
        <Breadcrumbs items={[{ label: "Dashboard", to: "/dashboard" }, { label: "Evidence requests" }]} />

        <p className="mt-8 text-sm font-semibold uppercase tracking-wide text-emerald-400">
          Evidence requests
        </p>
        <h1 className="mt-4 text-4xl font-bold tracking-tight">
          Underwriting evidence requests
        </h1>
        <p className="mt-4 max-w-3xl text-slate-300">
          Review concise request summaries here, then open one request to provide
          supporting cyber-control evidence and manage its private documents.
        </p>

        <form className="mt-6 grid gap-4 rounded-lg border border-slate-800 bg-slate-900 p-4 md:grid-cols-2 lg:grid-cols-3" onSubmit={(event) => { event.preventDefault(); setAppliedSearch(search.trim()); resetPage(); }}>
          <label className="text-sm font-semibold text-slate-200 lg:col-span-2">Search your evidence requests<input className="mt-2 block min-h-10 w-full rounded-md border border-slate-700 bg-slate-950 px-3 text-white" placeholder="Request, company, submission reference, or exact ID" value={search} onChange={(event) => setSearch(event.target.value)} /></label>
          <div className="flex items-end gap-3"><button className="min-h-10 rounded-md bg-emerald-400 px-4 font-semibold text-slate-950" type="submit">Search</button><button className="min-h-10 rounded-md border border-slate-600 px-4 font-semibold" type="button" onClick={() => { setSearch(""); setAppliedSearch(""); setStatus(""); setCategory(""); setReviewDecision(""); setDocumentRequirement(""); setOverdueOnly(false); resetPage(); }}>Clear</button></div>
          <label className="text-sm font-semibold text-slate-200">
            Status
            <select
              value={status}
              onChange={(event) => {
                setStatus(event.target.value);
                resetPage();
              }}
              className="mt-2 block min-h-10 rounded-md border border-slate-700 bg-slate-950 px-3 text-white"
            >
              <option value="">All statuses</option>
              <option value="Open">Open</option>
              <option value="Responded">Responded</option>
              <option value="Accepted">Accepted</option>
              <option value="Cancelled">Cancelled</option>
            </select>
          </label>
          <label className="text-sm font-semibold text-slate-200">Category<select value={category} onChange={(event) => { setCategory(event.target.value); resetPage(); }} className="mt-2 block min-h-10 w-full rounded-md border border-slate-700 bg-slate-950 px-3 text-white"><option value="">All categories</option><option value="MultiFactorAuthentication">Multi-factor authentication</option><option value="EndpointDetectionAndResponse">Endpoint detection and response</option><option value="BackupRecovery">Backup and recovery</option><option value="IncidentResponsePlan">Incident response plan</option><option value="SecurityQuestionnaireClarification">Questionnaire clarification</option></select></label>
          <label className="text-sm font-semibold text-slate-200">Review decision<select value={reviewDecision} onChange={(event) => { setReviewDecision(event.target.value); resetPage(); }} className="mt-2 block min-h-10 w-full rounded-md border border-slate-700 bg-slate-950 px-3 text-white"><option value="">All decisions</option><option value="NotReviewed">Not reviewed</option><option value="Satisfied">Satisfied</option><option value="Insufficient">Insufficient</option><option value="NeedsClarification">Needs clarification</option></select></label>
          <label className="text-sm font-semibold text-slate-200">Document requirement<select value={documentRequirement} onChange={(event) => { setDocumentRequirement(event.target.value); resetPage(); }} className="mt-2 block min-h-10 w-full rounded-md border border-slate-700 bg-slate-950 px-3 text-white"><option value="">All requirements</option><option value="Required">Required</option><option value="Optional">Optional</option><option value="NarrativeOnly">Written response only</option></select></label>
          <label className="flex min-h-10 items-center gap-2 text-sm font-semibold text-slate-200">
            <input
              type="checkbox"
              checked={overdueOnly}
              onChange={(event) => {
                setOverdueOnly(event.target.checked);
                resetPage();
              }}
            />
            Overdue only
          </label>
        </form>

        {evidenceRequestsQuery.isPending && (
          <p className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-300">
            Loading evidence requests...
          </p>
        )}

        {evidenceRequestsQuery.isError && (
          <p className="mt-8 whitespace-pre-wrap rounded-lg border border-red-900 bg-red-950 p-4 text-sm text-red-200">
            {getErrorMessage(
              evidenceRequestsQuery.error,
              "Unable to load evidence requests.",
            )}
          </p>
        )}

        {evidenceRequestsQuery.isSuccess && evidenceRequests.length === 0 && (
          <section className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-lg font-semibold text-white">
              No evidence requests are waiting for response.
            </h2>
            <p className="mt-2 text-sm text-slate-300">
              Requests appear here when underwriting needs more information for
              one of your referred quotes.
            </p>
          </section>
        )}

        {evidenceRequests.length > 0 && (
          <section className="mt-8 space-y-4">
            {evidenceRequests.map((request: EvidenceRequestSummary) => (
              <article
                key={request.evidenceRequestId}
                className="rounded-lg border border-slate-800 bg-slate-900 p-5"
              >
                <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                  <div>
                    <h2 className="text-lg font-semibold text-white">{request.title}</h2>
                    <p className="mt-2 text-sm text-slate-300">{request.description}</p>
                    <p className="mt-3 text-xs text-slate-400">
                      {request.companyName ?? "Company"} · {request.submissionReference ?? request.submissionId} · {request.category}
                    </p>
                    <p className="mt-2 text-xs font-semibold text-sky-200">Documents: {request.documentRequirement === "NarrativeOnly" ? "Written response only" : request.documentRequirement ?? "Required"}</p>
                  </div>
                  <div className="flex shrink-0 flex-col items-start gap-2 sm:items-end">
                    <span className="rounded-md border border-amber-700 px-3 py-1 text-xs font-semibold text-amber-200">
                      {request.status}
                    </span>
                    <span className={request.isOverdue ? "text-sm text-red-200" : "text-sm text-emerald-200"}>
                      {formatEvidenceDueLabel(request)}
                    </span>
                  </div>
                </div>
                {request.remediationGuidance && (
                  <p className="mt-4 rounded-md border border-amber-800 bg-amber-950/30 p-3 text-sm text-amber-100">
                    {request.remediationGuidance}
                  </p>
                )}
                <Link
                  to={`/evidence-requests/${request.evidenceRequestId}`}
                  className="mt-4 inline-flex rounded-md border border-emerald-400/60 px-4 py-2 text-sm font-semibold text-emerald-200 hover:bg-emerald-400 hover:text-slate-950"
                >
                  Open evidence request
                </Link>
              </article>
            ))}
            <nav aria-label="Evidence request pages" className="flex items-center justify-between pt-2">
              <button
                type="button"
                disabled={cursorHistory.length === 0}
                onClick={openPreviousPage}
                className="rounded-md border border-slate-700 px-4 py-2 text-sm font-semibold disabled:opacity-40"
              >
                Previous
              </button>
              <button
                type="button"
                disabled={!evidenceRequestsQuery.data?.nextCursor}
                onClick={openNextPage}
                className="rounded-md border border-slate-700 px-4 py-2 text-sm font-semibold disabled:opacity-40"
              >
                Next
              </button>
            </nav>
          </section>
        )}
      </section>
    </main>
  );
}
