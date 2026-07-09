import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useParams } from "react-router";

import { useAcceptQuote } from "../hooks/useAcceptQuote";
import { useBindPolicy } from "../hooks/useBindPolicy";
import { useCreateQuote } from "../hooks/useCreateQuote";
import { useSubmissionDetail } from "../hooks/useSubmissionDetail";
import { useSubmitSubmission } from "../hooks/useSubmitSubmission";
import { useUpdateSubmission } from "../hooks/useUpdateSubmission";
import {
  submissionIntakeSchema,
  type SubmissionIntakeFormValues,
} from "../schemas/submissionIntakeSchema";
import type {
  AnnualRevenueBand,
  BackupMaturity,
  CyberIndustryClass,
  CyberSecurityControlStatus,
  SensitiveDataExposure,
} from "../types";

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Unable to load submission.";
}

const fieldClassName =
  "mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-4 py-3 text-sm text-white outline-none focus:border-emerald-400";

const selectClassName = `${fieldClassName} min-h-11`;

const moneyFormatter = new Intl.NumberFormat("en-US", {
  currency: "USD",
  maximumFractionDigits: 0,
  style: "currency",
});

const defaultEffectiveDate = new Date(Date.now() + 24 * 60 * 60 * 1000)
  .toISOString()
  .slice(0, 10);

export function SubmissionDetailPage() {
  const { submissionId } = useParams();
  const [isEditing, setIsEditing] = useState(false);
  const [industryClass, setIndustryClass] =
    useState<CyberIndustryClass>("ProfessionalServices");
  const [annualRevenueBand, setAnnualRevenueBand] =
    useState<AnnualRevenueBand>("From10MTo50M");
  const [requestedLimit, setRequestedLimit] = useState("1000000");
  const [retention, setRetention] = useState("10000");
  const [mfaStatus, setMfaStatus] =
    useState<CyberSecurityControlStatus>("Implemented");
  const [edrStatus, setEdrStatus] =
    useState<CyberSecurityControlStatus>("Implemented");
  const [backupMaturity, setBackupMaturity] =
    useState<BackupMaturity>("Mature");
  const [hasIncidentResponsePlan, setHasIncidentResponsePlan] = useState(true);
  const [priorCyberIncidents, setPriorCyberIncidents] = useState("0");
  const [sensitiveDataExposure, setSensitiveDataExposure] =
    useState<SensitiveDataExposure>("Moderate");
  const [acceptedByName, setAcceptedByName] = useState("");
  const [acceptedByTitle, setAcceptedByTitle] = useState("CFO");
  const [subjectivitiesAcknowledged, setSubjectivitiesAcknowledged] =
    useState(false);
  const [effectiveDate, setEffectiveDate] = useState(defaultEffectiveDate);
  const submissionQuery = useSubmissionDetail(submissionId);
  const updateSubmissionMutation = useUpdateSubmission();
  const submitSubmissionMutation = useSubmitSubmission();
  const createQuoteMutation = useCreateQuote();
  const acceptQuoteMutation = useAcceptQuote();
  const bindPolicyMutation = useBindPolicy();
  const submission = submissionQuery.data;
  const updatedSubmission =
    submission &&
    updateSubmissionMutation.data?.submissionId === submission.submissionId
      ? updateSubmissionMutation.data
      : submission;
  const displayedSubmission =
    updatedSubmission &&
    submitSubmissionMutation.data?.submissionId === updatedSubmission.submissionId
      ? {
          ...updatedSubmission,
          status: submitSubmissionMutation.data.status,
        }
      : updatedSubmission;
  const canSubmit = displayedSubmission?.status === "Draft";
  const createdQuote = createQuoteMutation.data;
  const acceptedQuote = acceptQuoteMutation.data;
  const boundPolicy = bindPolicyMutation.data;
  const activeQuoteId =
    boundPolicy?.quoteId ?? acceptedQuote?.quoteId ?? createdQuote?.quoteId;
  const activeQuoteStatus =
    boundPolicy?.status ?? acceptedQuote?.status ?? createdQuote?.status;
  const activePremium =
    boundPolicy?.premium ?? acceptedQuote?.premium ?? createdQuote?.premium;
  const activeRequestedLimit =
    boundPolicy?.requestedLimit ??
    acceptedQuote?.requestedLimit ??
    createdQuote?.requestedLimit;
  const activeRetention =
    boundPolicy?.retention ?? acceptedQuote?.retention ?? createdQuote?.retention;
  const activeSubjectivities =
    createdQuote?.subjectivities ??
    acceptedQuote?.subjectivities
      .split("\n")
      .filter((subjectivity) => subjectivity.trim().length > 0) ??
    [];
  const canGenerateQuote =
    displayedSubmission?.status === "Submitted" && activeQuoteStatus === undefined;
  const canAcceptQuote =
    activeQuoteStatus === "Quoted" || activeQuoteStatus === "Approved";
  const canBindPolicy = activeQuoteStatus === "Accepted";
  const isQuoteReferred = activeQuoteStatus === "Referred";
  const {
    formState: { errors },
    handleSubmit,
    register,
    reset,
  } = useForm<SubmissionIntakeFormValues>({
    resolver: zodResolver(submissionIntakeSchema),
    defaultValues: {
      applicantName: "",
      applicantEmail: "",
      companyName: "",
    },
  });

  function handleStartEditing() {
    if (!displayedSubmission) {
      return;
    }

    reset({
      applicantName: displayedSubmission.applicantName,
      applicantEmail: displayedSubmission.applicantEmail,
      companyName: displayedSubmission.companyName,
    });
    setIsEditing(true);
  }

  function handleCancelEditing() {
    setIsEditing(false);
    updateSubmissionMutation.reset();
  }

  function handleUpdateSubmission(values: SubmissionIntakeFormValues) {
    if (!displayedSubmission) {
      return;
    }

    updateSubmissionMutation.mutate(
      {
        submissionId: displayedSubmission.submissionId,
        request: values,
      },
      {
        onSuccess: () => {
          setIsEditing(false);
        },
      },
    );
  }

  function handleSubmitSubmission() {
    if (!displayedSubmission) {
      return;
    }

    submitSubmissionMutation.mutate(displayedSubmission.submissionId);
  }

  function handleGenerateQuote() {
    if (!displayedSubmission) {
      return;
    }

    createQuoteMutation.mutate({
      submissionId: displayedSubmission.submissionId,
      request: {
        industryClass,
        annualRevenueBand,
        requestedLimit: Number(requestedLimit),
        retention: Number(retention),
        mfaStatus,
        edrStatus,
        backupMaturity,
        hasIncidentResponsePlan,
        priorCyberIncidents: Number(priorCyberIncidents),
        sensitiveDataExposure,
      },
    });
  }

  function handleAcceptQuote() {
    if (!activeQuoteId) {
      return;
    }

    acceptQuoteMutation.mutate({
      quoteId: activeQuoteId,
      request: {
        acceptedByName:
          acceptedByName.trim() || displayedSubmission?.applicantName || "",
        acceptedByTitle,
        subjectivitiesAcknowledged,
      },
    });
  }

  function handleBindPolicy() {
    if (!activeQuoteId) {
      return;
    }

    bindPolicyMutation.mutate({
      quoteId: activeQuoteId,
      request: {
        effectiveDateUtc: new Date(`${effectiveDate}T00:00:00.000Z`).toISOString(),
      },
    });
  }

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
      <section className="mx-auto max-w-4xl">
        <Link
          to="/submissions"
          className="inline-flex text-sm font-semibold text-emerald-300 hover:text-emerald-200"
        >
          Back to submissions
        </Link>

        <p className="mt-8 text-sm font-semibold uppercase tracking-wide text-emerald-400">
          Submission
        </p>
        <h1 className="mt-4 text-4xl font-bold tracking-tight">
          Submission detail
        </h1>

        {submissionQuery.isPending && (
          <p className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-300">
            Loading submission...
          </p>
        )}

        {submissionQuery.isError && (
          <p className="mt-8 whitespace-pre-wrap rounded-lg border border-red-900 bg-red-950 p-4 text-sm text-red-200">
            {getErrorMessage(submissionQuery.error)}
          </p>
        )}

        {displayedSubmission && (
          <section className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-6 text-sm text-slate-200">
            <dl className="grid gap-5 sm:grid-cols-2">
              <div>
                <dt className="font-semibold text-slate-400">Submission ID</dt>
                <dd className="mt-1 break-all text-white">
                  {displayedSubmission.submissionId}
                </dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-400">Status</dt>
                <dd className="mt-1 text-white">{displayedSubmission.status}</dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-400">Applicant</dt>
                <dd className="mt-1 text-white">
                  {displayedSubmission.applicantName}
                </dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-400">
                  Applicant email
                </dt>
                <dd className="mt-1 text-white">
                  {displayedSubmission.applicantEmail}
                </dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-400">Company</dt>
                <dd className="mt-1 text-white">
                  {displayedSubmission.companyName}
                </dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-400">Created UTC</dt>
                <dd className="mt-1 text-white">
                  <time dateTime={displayedSubmission.createdAtUtc}>
                    {displayedSubmission.createdAtUtc}
                  </time>
                </dd>
              </div>
            </dl>

            {canSubmit && !isEditing && (
              <div className="mt-6 border-t border-slate-800 pt-5">
                <h2 className="text-base font-semibold text-white">
                  Review before submission
                </h2>
                <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-300">
                  This submission is still a draft. Update the intake details
                  before submitting if anything is incorrect.
                </p>
                <button
                  type="button"
                  onClick={handleStartEditing}
                  className="mt-4 inline-flex min-h-10 items-center rounded-md border border-slate-700 px-4 py-2 text-sm font-semibold text-slate-100 hover:border-slate-500"
                >
                  Edit draft details
                </button>
              </div>
            )}

            {canSubmit && isEditing && (
              <form
                className="mt-6 border-t border-slate-800 pt-5"
                onSubmit={handleSubmit(handleUpdateSubmission)}
                noValidate
              >
                <h2 className="text-base font-semibold text-white">
                  Edit draft details
                </h2>
                <div className="mt-5">
                  <label
                    className="text-sm font-semibold text-slate-100"
                    htmlFor="editApplicantName"
                  >
                    Applicant name
                  </label>
                  <input
                    aria-invalid={errors.applicantName ? "true" : "false"}
                    className={fieldClassName}
                    id="editApplicantName"
                    type="text"
                    {...register("applicantName")}
                  />
                  {errors.applicantName && (
                    <p className="mt-2 text-sm text-red-300">
                      {errors.applicantName.message}
                    </p>
                  )}
                </div>

                <div className="mt-5">
                  <label
                    className="text-sm font-semibold text-slate-100"
                    htmlFor="editApplicantEmail"
                  >
                    Applicant email
                  </label>
                  <input
                    aria-invalid={errors.applicantEmail ? "true" : "false"}
                    className={fieldClassName}
                    id="editApplicantEmail"
                    type="email"
                    {...register("applicantEmail")}
                  />
                  {errors.applicantEmail && (
                    <p className="mt-2 text-sm text-red-300">
                      {errors.applicantEmail.message}
                    </p>
                  )}
                </div>

                <div className="mt-5">
                  <label
                    className="text-sm font-semibold text-slate-100"
                    htmlFor="editCompanyName"
                  >
                    Company name
                  </label>
                  <input
                    aria-invalid={errors.companyName ? "true" : "false"}
                    className={fieldClassName}
                    id="editCompanyName"
                    type="text"
                    {...register("companyName")}
                  />
                  {errors.companyName && (
                    <p className="mt-2 text-sm text-red-300">
                      {errors.companyName.message}
                    </p>
                  )}
                </div>

                <div className="mt-6 flex flex-wrap gap-3">
                  <button
                    type="submit"
                    disabled={updateSubmissionMutation.isPending}
                    className="inline-flex min-h-10 items-center rounded-md bg-emerald-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-emerald-200 disabled:cursor-not-allowed disabled:bg-slate-600 disabled:text-slate-300"
                  >
                    {updateSubmissionMutation.isPending
                      ? "Saving..."
                      : "Save changes"}
                  </button>
                  <button
                    type="button"
                    onClick={handleCancelEditing}
                    disabled={updateSubmissionMutation.isPending}
                    className="inline-flex min-h-10 items-center rounded-md border border-slate-700 px-4 py-2 text-sm font-semibold text-slate-100 hover:border-slate-500 disabled:cursor-not-allowed disabled:text-slate-500"
                  >
                    Cancel
                  </button>
                </div>
              </form>
            )}

            {updateSubmissionMutation.isSuccess && !isEditing && (
              <p className="mt-5 rounded-md border border-emerald-500/40 bg-emerald-950/30 p-3 text-sm text-emerald-100">
                Draft details updated.
              </p>
            )}

            {updateSubmissionMutation.isError && (
              <p className="mt-5 whitespace-pre-wrap rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">
                {getErrorMessage(updateSubmissionMutation.error)}
              </p>
            )}

            {canSubmit && !isEditing && (
              <div className="mt-6 border-t border-slate-800 pt-5">
                <h2 className="text-base font-semibold text-white">
                  Submit this draft
                </h2>
                <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-300">
                  Submit the completed intake so quote generation and downstream
                  underwriting steps can start.
                </p>
                <button
                  type="button"
                  onClick={handleSubmitSubmission}
                  disabled={submitSubmissionMutation.isPending}
                  className="mt-4 inline-flex min-h-10 items-center rounded-md bg-emerald-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-emerald-200 disabled:cursor-not-allowed disabled:bg-slate-600 disabled:text-slate-300"
                >
                  {submitSubmissionMutation.isPending
                    ? "Submitting..."
                    : "Submit submission"}
                </button>
              </div>
            )}

            {submitSubmissionMutation.isSuccess && (
              <p className="mt-5 rounded-md border border-emerald-500/40 bg-emerald-950/30 p-3 text-sm text-emerald-100">
                Submission submitted successfully.
              </p>
            )}

            {submitSubmissionMutation.isError && (
              <p className="mt-5 whitespace-pre-wrap rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">
                {getErrorMessage(submitSubmissionMutation.error)}
              </p>
            )}

            {canGenerateQuote && (
              <div className="mt-6 border-t border-slate-800 pt-5">
                <h2 className="text-base font-semibold text-white">
                  Generate quote
                </h2>
                <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-300">
                  Complete the rating inputs for this submitted risk. The
                  defaults represent a clean cyber profile for the happy-path
                  walkthrough.
                </p>

                <div className="mt-5 grid gap-4 sm:grid-cols-2">
                  <label className="text-sm font-semibold text-slate-100">
                    Industry class
                    <select
                      className={selectClassName}
                      value={industryClass}
                      onChange={(event) =>
                        setIndustryClass(
                          event.target.value as CyberIndustryClass,
                        )
                      }
                    >
                      <option value="ProfessionalServices">
                        Professional services
                      </option>
                      <option value="Technology">Technology</option>
                      <option value="Retail">Retail</option>
                      <option value="Healthcare">Healthcare</option>
                      <option value="FinancialServices">
                        Financial services
                      </option>
                    </select>
                  </label>

                  <label className="text-sm font-semibold text-slate-100">
                    Annual revenue
                    <select
                      className={selectClassName}
                      value={annualRevenueBand}
                      onChange={(event) =>
                        setAnnualRevenueBand(
                          event.target.value as AnnualRevenueBand,
                        )
                      }
                    >
                      <option value="Under1M">Under $1M</option>
                      <option value="From1MTo10M">$1M to $10M</option>
                      <option value="From10MTo50M">$10M to $50M</option>
                      <option value="From50MTo250M">$50M to $250M</option>
                    </select>
                  </label>

                  <label className="text-sm font-semibold text-slate-100">
                    Requested limit
                    <select
                      className={selectClassName}
                      value={requestedLimit}
                      onChange={(event) => setRequestedLimit(event.target.value)}
                    >
                      <option value="250000">$250,000</option>
                      <option value="500000">$500,000</option>
                      <option value="1000000">$1,000,000</option>
                      <option value="2000000">$2,000,000</option>
                      <option value="5000000">$5,000,000</option>
                    </select>
                  </label>

                  <label className="text-sm font-semibold text-slate-100">
                    Retention
                    <select
                      className={selectClassName}
                      value={retention}
                      onChange={(event) => setRetention(event.target.value)}
                    >
                      <option value="2500">$2,500</option>
                      <option value="5000">$5,000</option>
                      <option value="10000">$10,000</option>
                      <option value="25000">$25,000</option>
                    </select>
                  </label>

                  <label className="text-sm font-semibold text-slate-100">
                    MFA status
                    <select
                      className={selectClassName}
                      value={mfaStatus}
                      onChange={(event) =>
                        setMfaStatus(
                          event.target.value as CyberSecurityControlStatus,
                        )
                      }
                    >
                      <option value="Implemented">Implemented</option>
                      <option value="Partial">Partial</option>
                      <option value="NotImplemented">Not implemented</option>
                    </select>
                  </label>

                  <label className="text-sm font-semibold text-slate-100">
                    EDR status
                    <select
                      className={selectClassName}
                      value={edrStatus}
                      onChange={(event) =>
                        setEdrStatus(
                          event.target.value as CyberSecurityControlStatus,
                        )
                      }
                    >
                      <option value="Implemented">Implemented</option>
                      <option value="Partial">Partial</option>
                      <option value="NotImplemented">Not implemented</option>
                    </select>
                  </label>

                  <label className="text-sm font-semibold text-slate-100">
                    Backup maturity
                    <select
                      className={selectClassName}
                      value={backupMaturity}
                      onChange={(event) =>
                        setBackupMaturity(event.target.value as BackupMaturity)
                      }
                    >
                      <option value="Mature">Mature</option>
                      <option value="Partial">Partial</option>
                      <option value="Weak">Weak</option>
                    </select>
                  </label>

                  <label className="text-sm font-semibold text-slate-100">
                    Sensitive data exposure
                    <select
                      className={selectClassName}
                      value={sensitiveDataExposure}
                      onChange={(event) =>
                        setSensitiveDataExposure(
                          event.target.value as SensitiveDataExposure,
                        )
                      }
                    >
                      <option value="Low">Low</option>
                      <option value="Moderate">Moderate</option>
                      <option value="High">High</option>
                    </select>
                  </label>

                  <label className="text-sm font-semibold text-slate-100">
                    Prior cyber incidents
                    <input
                      className={fieldClassName}
                      min="0"
                      max="5"
                      type="number"
                      value={priorCyberIncidents}
                      onChange={(event) =>
                        setPriorCyberIncidents(event.target.value)
                      }
                    />
                  </label>

                  <label className="mt-8 flex items-center gap-3 text-sm font-semibold text-slate-100">
                    <input
                      checked={hasIncidentResponsePlan}
                      className="h-4 w-4 rounded border-slate-700 bg-slate-950"
                      type="checkbox"
                      onChange={(event) =>
                        setHasIncidentResponsePlan(event.target.checked)
                      }
                    />
                    Incident response plan in place
                  </label>
                </div>

                <button
                  type="button"
                  onClick={handleGenerateQuote}
                  disabled={createQuoteMutation.isPending}
                  className="mt-5 inline-flex min-h-10 items-center rounded-md bg-emerald-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-emerald-200 disabled:cursor-not-allowed disabled:bg-slate-600 disabled:text-slate-300"
                >
                  {createQuoteMutation.isPending
                    ? "Generating..."
                    : "Generate quote"}
                </button>
              </div>
            )}

            {createQuoteMutation.isError && (
              <p className="mt-5 whitespace-pre-wrap rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">
                {getErrorMessage(createQuoteMutation.error)}
              </p>
            )}

            {createdQuote && (
              <div className="mt-6 border-t border-slate-800 pt-5">
                <h2 className="text-base font-semibold text-white">
                  Quote result
                </h2>
                <dl className="mt-4 grid gap-4 sm:grid-cols-2">
                  <div>
                    <dt className="font-semibold text-slate-400">Quote ID</dt>
                    <dd className="mt-1 break-all text-white">
                      {createdQuote.quoteId}
                    </dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-400">Status</dt>
                    <dd className="mt-1 text-white">{activeQuoteStatus}</dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-400">Premium</dt>
                    <dd className="mt-1 text-white">
                      {moneyFormatter.format(activePremium ?? 0)}
                    </dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-400">Risk tier</dt>
                    <dd className="mt-1 text-white">{createdQuote.riskTier}</dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-400">Limit</dt>
                    <dd className="mt-1 text-white">
                      {moneyFormatter.format(activeRequestedLimit ?? 0)}
                    </dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-400">Retention</dt>
                    <dd className="mt-1 text-white">
                      {moneyFormatter.format(activeRetention ?? 0)}
                    </dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-400">
                      Expires UTC
                    </dt>
                    <dd className="mt-1 text-white">
                      <time dateTime={createdQuote.expiresAtUtc}>
                        {createdQuote.expiresAtUtc}
                      </time>
                    </dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-400">Provider</dt>
                    <dd className="mt-1 text-white">
                      {createdQuote.providerIndication.providerName} -{" "}
                      {createdQuote.providerIndication.marketDisposition}
                    </dd>
                  </div>
                </dl>

                {activeSubjectivities.length > 0 && (
                  <div className="mt-5">
                    <h3 className="text-sm font-semibold text-slate-100">
                      Subjectivities
                    </h3>
                    <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-300">
                      {activeSubjectivities.map((subjectivity) => (
                        <li key={subjectivity}>{subjectivity}</li>
                      ))}
                    </ul>
                  </div>
                )}

                {createdQuote.referralReasons.length > 0 && (
                  <div className="mt-5 rounded-md border border-amber-500/50 bg-amber-950/30 p-4 text-sm text-amber-100">
                    <h3 className="font-semibold">Underwriting referral</h3>
                    <ul className="mt-2 list-disc space-y-1 pl-5">
                      {createdQuote.referralReasons.map((reason) => (
                        <li key={reason}>{reason}</li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>
            )}

            {isQuoteReferred && (
              <p className="mt-5 rounded-md border border-amber-500/50 bg-amber-950/30 p-3 text-sm text-amber-100">
                This quote needs underwriting review before the customer can
                accept it. An underwriter or admin can continue in the
                underwriting workbench.
              </p>
            )}

            {canAcceptQuote && (
              <div className="mt-6 border-t border-slate-800 pt-5">
                <h2 className="text-base font-semibold text-white">
                  Accept quote
                </h2>
                <div className="mt-5 grid gap-4 sm:grid-cols-2">
                  <label className="text-sm font-semibold text-slate-100">
                    Acceptor name
                    <input
                      className={fieldClassName}
                      type="text"
                      value={
                        acceptedByName || displayedSubmission?.applicantName || ""
                      }
                      onChange={(event) => setAcceptedByName(event.target.value)}
                    />
                  </label>
                  <label className="text-sm font-semibold text-slate-100">
                    Acceptor title
                    <input
                      className={fieldClassName}
                      type="text"
                      value={acceptedByTitle}
                      onChange={(event) =>
                        setAcceptedByTitle(event.target.value)
                      }
                    />
                  </label>
                </div>
                <label className="mt-5 flex items-start gap-3 text-sm text-slate-100">
                  <input
                    checked={subjectivitiesAcknowledged}
                    className="mt-1 h-4 w-4 rounded border-slate-700 bg-slate-950"
                    type="checkbox"
                    onChange={(event) =>
                      setSubjectivitiesAcknowledged(event.target.checked)
                    }
                  />
                  <span>
                    I acknowledge the quote subjectivities and understand they
                    must be satisfied before binding.
                  </span>
                </label>
                <button
                  type="button"
                  onClick={handleAcceptQuote}
                  disabled={acceptQuoteMutation.isPending}
                  className="mt-5 inline-flex min-h-10 items-center rounded-md bg-emerald-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-emerald-200 disabled:cursor-not-allowed disabled:bg-slate-600 disabled:text-slate-300"
                >
                  {acceptQuoteMutation.isPending ? "Accepting..." : "Accept quote"}
                </button>
              </div>
            )}

            {acceptQuoteMutation.isError && (
              <p className="mt-5 whitespace-pre-wrap rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">
                {getErrorMessage(acceptQuoteMutation.error)}
              </p>
            )}

            {acceptQuoteMutation.isSuccess && (
              <p className="mt-5 rounded-md border border-emerald-500/40 bg-emerald-950/30 p-3 text-sm text-emerald-100">
                Quote accepted successfully.
              </p>
            )}

            {canBindPolicy && (
              <div className="mt-6 border-t border-slate-800 pt-5">
                <h2 className="text-base font-semibold text-white">
                  Bind policy
                </h2>
                <label className="mt-4 block max-w-xs text-sm font-semibold text-slate-100">
                  Effective date
                  <input
                    className={fieldClassName}
                    type="date"
                    value={effectiveDate}
                    onChange={(event) => setEffectiveDate(event.target.value)}
                  />
                </label>
                <button
                  type="button"
                  onClick={handleBindPolicy}
                  disabled={bindPolicyMutation.isPending}
                  className="mt-5 inline-flex min-h-10 items-center rounded-md bg-emerald-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-emerald-200 disabled:cursor-not-allowed disabled:bg-slate-600 disabled:text-slate-300"
                >
                  {bindPolicyMutation.isPending ? "Binding..." : "Bind policy"}
                </button>
              </div>
            )}

            {bindPolicyMutation.isError && (
              <p className="mt-5 whitespace-pre-wrap rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">
                {getErrorMessage(bindPolicyMutation.error)}
              </p>
            )}

            {boundPolicy && (
              <div className="mt-6 rounded-md border border-emerald-500/40 bg-emerald-950/30 p-4 text-sm text-emerald-100">
                <h2 className="text-base font-semibold text-white">
                  Policy bound
                </h2>
                <dl className="mt-3 grid gap-3 sm:grid-cols-2">
                  <div>
                    <dt className="font-semibold text-emerald-200">
                      Policy number
                    </dt>
                    <dd className="mt-1 text-white">
                      {boundPolicy.policyNumber}
                    </dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-emerald-200">
                      Binding reference
                    </dt>
                    <dd className="mt-1 text-white">
                      {boundPolicy.bindingReference}
                    </dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-emerald-200">
                      Effective UTC
                    </dt>
                    <dd className="mt-1 text-white">
                      {boundPolicy.effectiveDateUtc}
                    </dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-emerald-200">
                      Expiration UTC
                    </dt>
                    <dd className="mt-1 text-white">
                      {boundPolicy.expirationDateUtc}
                    </dd>
                  </div>
                </dl>
              </div>
            )}
          </section>
        )}
      </section>
    </main>
  );
}
