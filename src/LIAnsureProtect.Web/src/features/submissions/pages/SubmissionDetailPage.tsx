import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect, useId, useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useLocation, useNavigate, useParams } from "react-router";

import { Breadcrumbs } from "../../../components/Breadcrumbs";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { TransientStatusMessage } from "../../../components/TransientStatusMessage";
import { getUserErrorMessage } from "../../../lib/apiClient";
import { formatCurrency } from "../../../lib/currency";
import { formatLocalDateTime } from "../../../lib/dateTime";
import { useAcceptQuote } from "../hooks/useAcceptQuote";
import { useBindPolicy } from "../hooks/useBindPolicy";
import { useCreateQuote } from "../hooks/useCreateQuote";
import { useSubmissionDetail } from "../hooks/useSubmissionDetail";
import { useSubmitSubmission } from "../hooks/useSubmitSubmission";
import { useUpdateSubmission } from "../hooks/useUpdateSubmission";
import { useDeleteDraftSubmission, useWithdrawSubmission } from "../hooks/useSubmissionLifecycle";
import {
  submissionIntakeSchema,
  type SubmissionIntakeFormValues,
} from "../schemas/submissionIntakeSchema";
import type {
  AnnualRevenueBand,
  BackupMaturity,
  CyberIndustryClass,
  CyberControlDetails,
  CyberSecurityControlStatus,
  SensitiveDataExposure,
} from "../types";
import { isCreatedQuote } from "../types";

type BooleanControlDetailKey = {
  [K in keyof CyberControlDetails]: CyberControlDetails[K] extends boolean
    ? K
    : never;
}[keyof CyberControlDetails];

function getErrorMessage(error: unknown) {
  return getUserErrorMessage(error, "Unable to load submission.");
}

const fieldClassName =
  "mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-4 py-3 text-sm text-white outline-none focus:border-emerald-400";

const selectClassName = `${fieldClassName} min-h-11`;

const defaultEffectiveDate = new Date(Date.now() + 24 * 60 * 60 * 1000)
  .toISOString()
  .slice(0, 10);

const incidentTypeOptions = [
  "Ransomware",
  "Data breach",
  "Business email compromise",
  "Funds transfer fraud",
  "DDoS / availability outage",
  "Malware or endpoint compromise",
  "Vendor / supply-chain incident",
  "Other",
];

const defaultControlDetails: CyberControlDetails = {
  mfaCoversPrivilegedAccess: true,
  mfaCoversEmail: true,
  mfaCoversRemoteAccess: true,
  mfaCoversWorkforce: true,
  mfaPhishingResistant: false,
  edrCoveragePercent: 98,
  edrCoversServers: true,
  edrActivelyMonitored: true,
  edrTamperProtection: true,
  backupsImmutableOrOffline: true,
  backupCredentialsSeparated: true,
  restoreTestedLast12Months: true,
  recoveryPointObjectiveHours: 4,
  recoveryTimeObjectiveHours: 8,
  incidentPlanApproved: true,
  incidentPlanUpdatedLast12Months: true,
  incidentPlanTestedLast12Months: true,
  incidentRolesNamed: true,
  sensitiveDataInventoryMaintained: true,
  sensitiveDataEncrypted: true,
  sensitiveDataTypes: ["Personal data", "Credentials"],
  sensitiveDataVolume: "Moderate",
};

type QuoteControlSource = {
  requestedLimit: number;
  retention: number;
  controlAssertions?: Array<{
    controlType: string;
    claimedState: string;
    detailsJson: string;
  }>;
};

type QuoteControlState = {
  requestedLimit: string;
  retention: string;
  mfaStatus: CyberSecurityControlStatus;
  edrStatus: CyberSecurityControlStatus;
  backupMaturity: BackupMaturity;
  hasIncidentResponsePlan: boolean;
  sensitiveDataExposure: SensitiveDataExposure;
  controlDetails: CyberControlDetails;
};

function readDetailsJson(value: string) {
  try {
    const parsed: unknown = JSON.parse(value);
    return parsed && typeof parsed === "object"
      ? (parsed as Record<string, unknown>)
      : {};
  } catch {
    return {};
  }
}

function quoteControlState(source: QuoteControlSource): QuoteControlState {
  const assertions = source.controlAssertions ?? [];
  const byType = new Map(
    assertions.map((assertion) => [assertion.controlType, assertion]),
  );
  const details = { ...defaultControlDetails };

  for (const assertion of assertions) {
    for (const [rawKey, value] of Object.entries(
      readDetailsJson(assertion.detailsJson),
    )) {
      const key = `${rawKey.charAt(0).toLowerCase()}${rawKey.slice(1)}` as keyof CyberControlDetails;
      if (key in details && value !== null && value !== undefined) {
        (details as Record<string, unknown>)[key] = value;
      }
    }
  }

  return {
    requestedLimit: String(source.requestedLimit),
    retention: String(source.retention),
    mfaStatus: (byType.get("MultiFactorAuthentication")?.claimedState ??
      "Implemented") as CyberSecurityControlStatus,
    edrStatus: (byType.get("EndpointDetectionAndResponse")?.claimedState ??
      "Implemented") as CyberSecurityControlStatus,
    backupMaturity: (byType.get("BackupRecovery")?.claimedState ??
      "Mature") as BackupMaturity,
    hasIncidentResponsePlan:
      byType.get("IncidentResponsePlan")?.claimedState === "InPlace",
    sensitiveDataExposure: (byType.get("SensitiveData")?.claimedState ??
      "Moderate") as SensitiveDataExposure,
    controlDetails: details,
  };
}

function controlFingerprint(
  state: Omit<QuoteControlState, "requestedLimit" | "retention">,
) {
  return JSON.stringify(state);
}

function getControlValidationErrors(
  state: Omit<QuoteControlState, "requestedLimit" | "retention">,
) {
  const errors: string[] = [];
  const { controlDetails } = state;

  if (
    state.mfaStatus === "Implemented" &&
    (!controlDetails.mfaCoversPrivilegedAccess ||
      !controlDetails.mfaCoversEmail ||
      !controlDetails.mfaCoversRemoteAccess)
  ) {
    errors.push(
      "Implemented MFA must cover privileged access, email, and remote access. Choose a lower MFA status if that is not yet true.",
    );
  }

  if (
    state.edrStatus === "Implemented" &&
    (controlDetails.edrCoveragePercent < 90 ||
      !controlDetails.edrCoversServers ||
      !controlDetails.edrActivelyMonitored ||
      !controlDetails.edrTamperProtection)
  ) {
    errors.push(
      "Implemented EDR requires at least 90% coverage, server coverage, active monitoring, and tamper protection. Choose a lower EDR status if that is not yet true.",
    );
  }

  if (
    state.backupMaturity === "Mature" &&
    (!controlDetails.backupsImmutableOrOffline ||
      !controlDetails.backupCredentialsSeparated ||
      !controlDetails.restoreTestedLast12Months)
  ) {
    errors.push(
      "Mature backups require immutable or offline copies, separate credentials, and a restore test in the last 12 months. Choose a lower backup maturity if that is not yet true.",
    );
  }

  if (
    state.hasIncidentResponsePlan &&
    (!controlDetails.incidentPlanApproved ||
      !controlDetails.incidentPlanUpdatedLast12Months ||
      !controlDetails.incidentPlanTestedLast12Months ||
      !controlDetails.incidentRolesNamed)
  ) {
    errors.push(
      "An incident response plan marked as in place must be approved, current, tested, and assign named roles. Mark the plan as not in place if any of those statements is not yet true.",
    );
  }

  if (
    state.sensitiveDataExposure === "Low" &&
    (!controlDetails.sensitiveDataInventoryMaintained ||
      !controlDetails.sensitiveDataEncrypted)
  ) {
    errors.push(
      "Low sensitive-data exposure requires a maintained data inventory and encryption. Choose a higher exposure level if either control is not yet true.",
    );
  }

  return errors;
}

const controlDetailCheckboxes: ReadonlyArray<{
  key: BooleanControlDetailKey;
  label: string;
}> = [
  { key: "mfaCoversPrivilegedAccess", label: "MFA covers privileged access" },
  { key: "mfaCoversEmail", label: "MFA covers email" },
  { key: "mfaCoversRemoteAccess", label: "MFA covers remote access" },
  { key: "mfaCoversWorkforce", label: "MFA covers the workforce" },
  { key: "mfaPhishingResistant", label: "Phishing-resistant MFA is used" },
  { key: "edrCoversServers", label: "EDR covers servers" },
  { key: "edrActivelyMonitored", label: "EDR alerts are actively monitored" },
  { key: "edrTamperProtection", label: "EDR tamper protection is enabled" },
  { key: "backupsImmutableOrOffline", label: "Backups include immutable or offline copies" },
  { key: "backupCredentialsSeparated", label: "Backup credentials are separated" },
  { key: "restoreTestedLast12Months", label: "A restore was tested in the last 12 months" },
  { key: "incidentPlanApproved", label: "Incident plan is formally approved" },
  { key: "incidentPlanUpdatedLast12Months", label: "Incident plan was updated in the last 12 months" },
  { key: "incidentPlanTestedLast12Months", label: "Incident plan was exercised in the last 12 months" },
  { key: "incidentRolesNamed", label: "Incident roles and contacts are named" },
  { key: "sensitiveDataInventoryMaintained", label: "A sensitive-data inventory is maintained" },
  { key: "sensitiveDataEncrypted", label: "Sensitive data is encrypted" },
];

const helpText: Record<string, string> = {
  annualRevenue:
    "Revenue is a size proxy. Larger revenue usually means larger digital operations, more third-party dependency, and larger potential loss exposure.",
  backupMaturity:
    "Mature means backups are tested, isolated, encrypted, and recoverable within defined recovery targets. Partial means backups exist but testing, isolation, or coverage is incomplete. Weak means backups are missing, untested, or likely exposed to the same attack path as production systems.",
  edrStatus:
    "EDR means endpoint detection and response. Implemented means covered endpoints are monitored and alerts are triaged. Partial means only some devices or alert workflows are covered. Not implemented means there is no reliable endpoint detection and response program.",
  incidentResponsePlan:
    "An incident response plan is a written and rehearsed playbook for detecting, containing, communicating, and recovering from cyber incidents. It should name roles, escalation paths, external contacts, legal/comms steps, backup recovery steps, and evidence preservation rules.",
  industryClass:
    "Industry affects baseline exposure. Healthcare and financial services carry heavier regulated-data exposure, retail often has payment and consumer data exposure, technology may carry platform or client-data risk, and professional services is the default lower-volatility class. Use Other when none fit.",
  mfaStatus:
    "MFA means multi-factor authentication. Implemented means privileged and remote access require MFA. Partial means only some users/apps are covered. Not implemented materially increases account-takeover risk.",
  priorCyberIncidents:
    "Prior incidents affect rating and underwriting because they show loss history and control maturity. Two or more prior incidents, or severe incident types like ransomware, data breach, business email compromise, or funds transfer fraud, trigger underwriter review.",
  requestedLimit:
    "Requested limit is the maximum policy coverage requested. Higher limits increase insurer exposure and usually increase premium or underwriting scrutiny.",
  retention:
    "Retention is the amount the insured keeps before insurance responds. Lower retention shifts more loss to the insurer and usually increases premium.",
  sensitiveDataExposure:
    "Low means little or no personal, financial, health, credential, or regulated data. Moderate means routine customer/employee data. High means large volumes of regulated, payment, health, financial, credential, or sensitive client data.",
};

function HelpButton({ id, label }: { id: keyof typeof helpText; label: string }) {
  const tooltipId = useId();

  return (
    <span className="group relative inline-flex align-middle">
      <button
        type="button"
        aria-describedby={tooltipId}
        aria-label={`More details about ${label}`}
        className="ml-2 inline-flex h-5 w-5 items-center justify-center rounded-full border border-sky-300 text-xs font-bold text-sky-200 hover:bg-sky-400 hover:text-slate-950 focus:bg-sky-400 focus:text-slate-950 focus:outline-none focus:ring-2 focus:ring-sky-300"
      >
        ?
      </button>
      <span
        id={tooltipId}
        role="tooltip"
        className="absolute left-7 top-0 z-10 hidden w-72 rounded-md border border-slate-700 bg-slate-950 p-3 text-xs font-normal leading-5 text-slate-200 shadow-xl group-focus-within:block group-hover:block"
      >
        {helpText[id]}
      </span>
    </span>
  );
}

export function SubmissionDetailPage() {
  const { submissionId } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const [isEditing, setIsEditing] = useState(false);
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
  const [isWithdrawDialogOpen, setIsWithdrawDialogOpen] = useState(false);
  const [isCancelReassessmentDialogOpen, setIsCancelReassessmentDialogOpen] =
    useState(false);
  const [isSubmittedNoticeVisible, setIsSubmittedNoticeVisible] = useState(false);
  const [isQuoteAcceptedNoticeVisible, setIsQuoteAcceptedNoticeVisible] =
    useState(false);
  const [isWithdrawnNoticeVisible, setIsWithdrawnNoticeVisible] = useState(false);
  const [draftCreatedNotice, setDraftCreatedNotice] = useState(() => {
    const routeState = location.state as {
      draftCreated?: boolean;
      possibleDuplicate?: boolean;
    } | null;

    return routeState?.draftCreated
      ? { possibleDuplicate: Boolean(routeState.possibleDuplicate) }
      : null;
  });
  const [industryClass, setIndustryClass] =
    useState<CyberIndustryClass>("ProfessionalServices");
  const [usesOtherIndustry, setUsesOtherIndustry] = useState(false);
  const [otherIndustryDescription, setOtherIndustryDescription] = useState("");
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
  const [priorCyberIncidentTypes, setPriorCyberIncidentTypes] = useState<
    string[]
  >([]);
  const [priorCyberIncidentDetails, setPriorCyberIncidentDetails] = useState("");
  const [sensitiveDataExposure, setSensitiveDataExposure] =
    useState<SensitiveDataExposure>("Moderate");
  const [attestationAccepted, setAttestationAccepted] = useState(false);
  const [attestedByName, setAttestedByName] = useState("");
  const [attestedByTitle, setAttestedByTitle] = useState("");
  const [isReassessing, setIsReassessing] = useState(false);
  const [reassessmentBaseline, setReassessmentBaseline] = useState<string>();
  const [controlDetails, setControlDetails] = useState<CyberControlDetails>(
    defaultControlDetails,
  );
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
  const deleteDraftMutation = useDeleteDraftSubmission();
  const withdrawMutation = useWithdrawSubmission();

  useEffect(() => {
    if ((location.state as { draftCreated?: boolean } | null)?.draftCreated) {
      void navigate(location.pathname, { replace: true, state: null });
    }
  }, [location.pathname, location.state, navigate]);

  function dismissDraftCreatedNotice() {
    setDraftCreatedNotice(null);
  }

  function dismissDraftUpdatedNotice() {
    updateSubmissionMutation.reset();
  }
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
  const quoteOutcome = createQuoteMutation.data;
  const createdQuote = quoteOutcome && isCreatedQuote(quoteOutcome) ? quoteOutcome : undefined;
  const queuedReassessment = quoteOutcome && !isCreatedQuote(quoteOutcome) ? quoteOutcome : undefined;
  const latestQuote = displayedSubmission?.latestQuote;
  const relatedPolicy = displayedSubmission?.relatedPolicy;
  const acceptedQuote = acceptQuoteMutation.data;
  const boundPolicy = bindPolicyMutation.data;
  const activeQuoteId =
    boundPolicy?.quoteId ??
    acceptedQuote?.quoteId ??
    createdQuote?.quoteId ??
    latestQuote?.quoteId;
  const activeQuoteStatus =
    boundPolicy?.status ??
    acceptedQuote?.status ??
    createdQuote?.status ??
    latestQuote?.status;
  const activePremium =
    boundPolicy?.premium ??
    acceptedQuote?.premium ??
    createdQuote?.premium ??
    latestQuote?.premium;
  const activeRequestedLimit =
    boundPolicy?.requestedLimit ??
    acceptedQuote?.requestedLimit ??
    createdQuote?.requestedLimit ??
    latestQuote?.requestedLimit;
  const activeRetention =
    boundPolicy?.retention ??
    acceptedQuote?.retention ??
    createdQuote?.retention ??
    latestQuote?.retention;
  const activeRiskTier = createdQuote?.riskTier ?? latestQuote?.riskTier;
  const activeExpiresAtUtc =
    boundPolicy?.expirationDateUtc ??
    acceptedQuote?.expiresAtUtc ??
    createdQuote?.expiresAtUtc ??
    latestQuote?.expiresAtUtc;
  const activeSubjectivities =
    createdQuote?.subjectivities ??
    acceptedQuote?.subjectivities
      .split("\n")
      .filter((subjectivity) => subjectivity.trim().length > 0) ??
    latestQuote?.subjectivities ??
    [];
  const activeReferralReasons =
    createdQuote?.referralReasons ?? latestQuote?.referralReasons ?? [];
  const activeAssuranceStatus =
    createdQuote?.assuranceStatus ?? latestQuote?.assuranceStatus;
  const activeEvidenceRequiredCount =
    createdQuote?.evidenceRequiredCount ??
    latestQuote?.evidenceRequiredCount ??
    0;
  const activeEvidenceSatisfiedCount =
    createdQuote?.evidenceSatisfiedCount ??
    latestQuote?.evidenceSatisfiedCount ??
    0;
  const activeQuoteVersion = createdQuote?.version ?? latestQuote?.version ?? 1;
  const canGenerateQuote =
    displayedSubmission?.status === "Submitted" && activeQuoteStatus === undefined;
  const canAcceptQuote =
    (activeQuoteStatus === "Quoted" || activeQuoteStatus === "Approved") &&
    activeAssuranceStatus !== "EvidenceRequired" &&
    activeAssuranceStatus !== "Rejected";
  const canBindPolicy = activeQuoteStatus === "Accepted";
  const canStartReassessment =
    activeQuoteId !== undefined &&
    !relatedPolicy &&
    !boundPolicy &&
    activeQuoteStatus !== "Accepted" &&
    activeQuoteStatus !== "Bound" &&
    activeQuoteStatus !== "Superseded";
  const canWithdraw = displayedSubmission?.status === "Submitted"
    && activeQuoteStatus !== "Accepted"
    && activeQuoteStatus !== "Bound";
  const isQuoteReferred = activeQuoteStatus === "Referred";
  const journeyStage = relatedPolicy || boundPolicy
    ? `Policy ${relatedPolicy?.coverageState ?? "Bound"}`
    : activeQuoteStatus === "Accepted"
      ? "Quote accepted"
      : activeQuoteStatus === "Referred"
        ? "Under review"
        : activeQuoteStatus
          ? "Quote ready"
          : displayedSubmission
            ? `${displayedSubmission.status} intake`
            : "Loading";
  const priorIncidentCount = Number(priorCyberIncidents);
  const needsPriorIncidentDetails = priorIncidentCount > 0;
  const canGenerateQuoteRequest =
    attestationAccepted &&
    attestedByName.trim().length > 0 &&
    attestedByTitle.trim().length > 0 &&
    (!needsPriorIncidentDetails ||
      (priorCyberIncidentTypes.length > 0 &&
        priorCyberIncidentDetails.trim().length > 0));
  const currentControlFingerprint = controlFingerprint({
    mfaStatus,
    edrStatus,
    backupMaturity,
    hasIncidentResponsePlan,
    sensitiveDataExposure,
    controlDetails,
  });
  const controlValidationErrors = getControlValidationErrors({
    mfaStatus,
    edrStatus,
    backupMaturity,
    hasIncidentResponsePlan,
    sensitiveDataExposure,
    controlDetails,
  });
  const hasValidControlDetails = controlValidationErrors.length === 0;
  const hasReassessmentChanges =
    !isReassessing ||
    (reassessmentBaseline !== undefined &&
      reassessmentBaseline !== currentControlFingerprint);
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

    submitSubmissionMutation.mutate(displayedSubmission.submissionId, {
      onSuccess: () => setIsSubmittedNoticeVisible(true),
    });
  }

  function handleGenerateQuote() {
    if (
      !displayedSubmission ||
      !canGenerateQuoteRequest ||
      !hasValidControlDetails ||
      !hasReassessmentChanges
    ) {
      return;
    }

    createQuoteMutation.mutate(
      {
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
        priorCyberIncidents: priorIncidentCount,
        sensitiveDataExposure,
        otherIndustryDescription: usesOtherIndustry
          ? otherIndustryDescription.trim()
          : null,
        priorCyberIncidentTypes: needsPriorIncidentDetails
          ? priorCyberIncidentTypes
          : null,
        priorCyberIncidentDetails: needsPriorIncidentDetails
          ? priorCyberIncidentDetails.trim()
          : null,
        attestationAccepted,
        attestedByName: attestedByName.trim(),
          attestedByTitle: attestedByTitle.trim(),
          isReassessment: isReassessing,
          ...(isReassessing ? { baseQuoteVersion: activeQuoteVersion } : {}),
          controlDetails,
        },
      },
      {
        onSuccess: () => {
          setIsReassessing(false);
          setReassessmentBaseline(undefined);
          setAttestationAccepted(false);
        },
      },
    );
  }

  function applyQuoteControls(source: QuoteControlSource) {
    const state = quoteControlState(source);
    setRequestedLimit(state.requestedLimit);
    setRetention(state.retention);
    setMfaStatus(state.mfaStatus);
    setEdrStatus(state.edrStatus);
    setBackupMaturity(state.backupMaturity);
    setHasIncidentResponsePlan(state.hasIncidentResponsePlan);
    setSensitiveDataExposure(state.sensitiveDataExposure);
    setControlDetails(state.controlDetails);
    return controlFingerprint({
      mfaStatus: state.mfaStatus,
      edrStatus: state.edrStatus,
      backupMaturity: state.backupMaturity,
      hasIncidentResponsePlan: state.hasIncidentResponsePlan,
      sensitiveDataExposure: state.sensitiveDataExposure,
      controlDetails: state.controlDetails,
    });
  }

  function finishCancellingReassessment() {
    const source = createdQuote ?? latestQuote;
    if (source) applyQuoteControls(source);
    setIsReassessing(false);
    setReassessmentBaseline(undefined);
    setIsCancelReassessmentDialogOpen(false);
    setAttestationAccepted(false);
    setAttestedByName("");
    setAttestedByTitle("");
    createQuoteMutation.reset();
  }

  function handleCancelReassessment() {
    if (hasReassessmentChanges) {
      setIsCancelReassessmentDialogOpen(true);
      return;
    }

    finishCancellingReassessment();
  }

  function handleIncidentTypeChange(incidentType: string, checked: boolean) {
    setPriorCyberIncidentTypes((current) =>
      checked
        ? [...new Set([...current, incidentType])]
        : current.filter((value) => value !== incidentType),
    );
  }

  function handleAcceptQuote() {
    if (!activeQuoteId) {
      return;
    }

    acceptQuoteMutation.mutate(
      {
        quoteId: activeQuoteId,
        request: {
          acceptedByName:
            acceptedByName.trim() || displayedSubmission?.applicantName || "",
          acceptedByTitle,
          subjectivitiesAcknowledged,
        },
      },
      { onSuccess: () => setIsQuoteAcceptedNoticeVisible(true) },
    );
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

  function handleDeleteDraft() {
    setIsDeleteDialogOpen(true);
  }

  async function confirmDeleteDraft() {
    if (!displayedSubmission) return;
    try {
      await deleteDraftMutation.mutateAsync(displayedSubmission.submissionId);
      setIsDeleteDialogOpen(false);
      await navigate("/submissions");
    } catch {
      setIsDeleteDialogOpen(false);
    }
  }

  function handleWithdrawSubmission() {
    setIsWithdrawDialogOpen(true);
  }

  async function confirmWithdrawSubmission() {
    if (!displayedSubmission) return;
    try {
      await withdrawMutation.mutateAsync(displayedSubmission.submissionId);
      setIsWithdrawDialogOpen(false);
      setIsWithdrawnNoticeVisible(true);
    } catch {
      setIsWithdrawDialogOpen(false);
    }
  }

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
      <section className="mx-auto max-w-4xl">
        <Breadcrumbs items={[{ label: "Dashboard", to: "/dashboard" }, { label: "Submissions", to: "/submissions" }, { label: displayedSubmission?.submissionReference ?? "Submission detail" }]} />

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
            <div className="mb-6 flex flex-col gap-3 border-b border-slate-800 pb-5 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <p className="text-xs font-semibold uppercase text-emerald-400">Journey stage</p>
                <p className="mt-1 text-lg font-semibold text-white">{journeyStage}</p>
              </div>
              {relatedPolicy && (
                <Link to={`/policies/${relatedPolicy.policyId}`} className="inline-flex rounded-md bg-emerald-300 px-4 py-2 font-semibold text-slate-950">View policy</Link>
              )}
            </div>
            {draftCreatedNotice && (
              <TransientStatusMessage
                className="mb-6"
                onDismiss={dismissDraftCreatedNotice}
              >
                <p className="font-semibold text-white">Draft submission created.</p>
                {draftCreatedNotice.possibleDuplicate && (
                  <p className="mt-2 leading-6">
                    Another open application exists for this company. This draft
                    was created because its details were not an exact draft match.
                  </p>
                )}
              </TransientStatusMessage>
            )}

            <form onSubmit={handleSubmit(handleUpdateSubmission)} noValidate>
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <h2 className="text-xl font-semibold text-white">Submission record</h2>
                {canSubmit && !isEditing && (
                  <button
                    type="button"
                    onClick={handleStartEditing}
                    className="inline-flex min-h-10 items-center justify-center rounded-md border border-slate-700 px-4 py-2 text-sm font-semibold text-slate-100 hover:border-slate-500"
                  >
                    Edit draft details
                  </button>
                )}
              </div>

              <div className="mt-5 grid gap-5 sm:grid-cols-2">
                <div>
                  <p className="font-semibold text-slate-400">Submission reference</p>
                  <p className="mt-1 font-mono text-emerald-300">{displayedSubmission.submissionReference ?? displayedSubmission.submissionId}</p>
                </div>
                <div>
                  <p className="font-semibold text-slate-400">Status</p>
                  <p className="mt-1 text-white">{displayedSubmission.status}</p>
                </div>
                <div>
                  {isEditing ? (
                    <>
                      <label className="font-semibold text-slate-300" htmlFor="editApplicantName">Applicant name</label>
                      <input autoFocus aria-invalid={errors.applicantName ? "true" : "false"} className={fieldClassName} id="editApplicantName" type="text" {...register("applicantName")} />
                      {errors.applicantName && <p className="mt-2 text-sm text-red-300">{errors.applicantName.message}</p>}
                    </>
                  ) : (
                    <>
                      <p className="font-semibold text-slate-400">Applicant</p>
                      <p className="mt-1 text-white">{displayedSubmission.applicantName}</p>
                    </>
                  )}
                </div>
                <div>
                  {isEditing ? (
                    <>
                      <label className="font-semibold text-slate-300" htmlFor="editApplicantEmail">Applicant email</label>
                      <input aria-invalid={errors.applicantEmail ? "true" : "false"} className={fieldClassName} id="editApplicantEmail" type="email" {...register("applicantEmail")} />
                      {errors.applicantEmail && <p className="mt-2 text-sm text-red-300">{errors.applicantEmail.message}</p>}
                    </>
                  ) : (
                    <>
                      <p className="font-semibold text-slate-400">Applicant email</p>
                      <p className="mt-1 text-white">{displayedSubmission.applicantEmail}</p>
                    </>
                  )}
                </div>
                <div>
                  {isEditing ? (
                    <>
                      <label className="font-semibold text-slate-300" htmlFor="editCompanyName">Company name</label>
                      <input aria-invalid={errors.companyName ? "true" : "false"} className={fieldClassName} id="editCompanyName" type="text" {...register("companyName")} />
                      {errors.companyName && <p className="mt-2 text-sm text-red-300">{errors.companyName.message}</p>}
                    </>
                  ) : (
                    <>
                      <p className="font-semibold text-slate-400">Company</p>
                      <p className="mt-1 text-white">{displayedSubmission.companyName}</p>
                    </>
                  )}
                </div>
                <div>
                  <p className="font-semibold text-slate-400">Created</p>
                  <p className="mt-1 text-white"><time dateTime={displayedSubmission.createdAtUtc} title={displayedSubmission.createdAtUtc}>{formatLocalDateTime(displayedSubmission.createdAtUtc)}</time></p>
                </div>
                <details className="sm:col-span-2 text-slate-400"><summary className="cursor-pointer font-semibold">Technical information</summary><p className="mt-2 break-all font-mono text-xs">Submission ID: {displayedSubmission.submissionId}</p></details>
              </div>

              {canSubmit && (
                <div className="mt-6 border-t border-slate-800 pt-5">
                  {isEditing ? (
                    <div className="flex flex-wrap gap-3">
                      <button type="submit" disabled={updateSubmissionMutation.isPending} className="inline-flex min-h-10 items-center rounded-md bg-emerald-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-emerald-200 disabled:cursor-not-allowed disabled:bg-slate-600 disabled:text-slate-300">
                        {updateSubmissionMutation.isPending ? "Saving..." : "Save changes"}
                      </button>
                      <button type="button" onClick={handleCancelEditing} disabled={updateSubmissionMutation.isPending} className="inline-flex min-h-10 items-center rounded-md border border-slate-700 px-4 py-2 text-sm font-semibold text-slate-100 hover:border-slate-500 disabled:cursor-not-allowed disabled:text-slate-500">Cancel</button>
                    </div>
                  ) : (
                    <p className="max-w-2xl leading-6 text-slate-300">
                      Review these details before submitting. Draft fields remain editable until the application is submitted.
                    </p>
                  )}
                </div>
              )}
            </form>

            {updateSubmissionMutation.isSuccess && !isEditing && (
              <TransientStatusMessage
                className="mt-5 text-sm"
                onDismiss={dismissDraftUpdatedNotice}
              >
                Draft details updated.
              </TransientStatusMessage>
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

            {isSubmittedNoticeVisible && (
              <TransientStatusMessage
                className="mt-5 text-sm"
                onDismiss={() => setIsSubmittedNoticeVisible(false)}
              >
                Submission submitted successfully.
              </TransientStatusMessage>
            )}

            {submitSubmissionMutation.isError && (
              <p className="mt-5 whitespace-pre-wrap rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">
                {getErrorMessage(submitSubmissionMutation.error)}
              </p>
            )}

            {(canGenerateQuote || isReassessing) && (
              <div className="mt-6 border-t border-slate-800 pt-5">
                <h2 className="text-base font-semibold text-white">
                  {isReassessing ? "Reassess quote" : "Generate quote"}
                </h2>
                <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-300">
                  {isReassessing
                    ? "Change at least one control answer. Your first valid reassessment is created immediately. After a successful reassessment, wait 30 minutes before requesting another; only requests beyond the self-service allowance go to underwriting review. Claimed improvements require supporting evidence, and a new quote version will preserve the prior result. A better outcome is not guaranteed."
                    : "Provide your organization's current security posture as accurately as possible. These answers affect the risk assessment, premium, and whether underwriting evidence is required. Any quote may remain subject to verification of selected controls."}
                </p>

                <div className="mt-5 grid gap-4 sm:grid-cols-2">
                  <div className="text-sm font-semibold text-slate-100">
                    <div>
                      <label htmlFor="quote-industry-class">Industry class</label>
                      <HelpButton id="industryClass" label="Industry class" />
                    </div>
                    <select
                      id="quote-industry-class"
                      className={selectClassName}
                      value={usesOtherIndustry ? "Other" : industryClass}
                      onChange={(event) => {
                        if (event.target.value === "Other") {
                          setUsesOtherIndustry(true);
                          setIndustryClass("ProfessionalServices");
                          return;
                        }

                        setUsesOtherIndustry(false);
                        setIndustryClass(event.target.value as CyberIndustryClass);
                      }}
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
                      <option value="Other">Other / not listed</option>
                    </select>
                  </div>

                  {usesOtherIndustry && (
                    <label className="text-sm font-semibold text-slate-100">
                      Describe industry
                      <input
                        className={fieldClassName}
                        type="text"
                        value={otherIndustryDescription}
                        onChange={(event) =>
                          setOtherIndustryDescription(event.target.value)
                        }
                        placeholder="Example: logistics marketplace"
                      />
                    </label>
                  )}

                  <label className="text-sm font-semibold text-slate-100">
                    Annual revenue
                    <HelpButton id="annualRevenue" label="Annual revenue" />
                    <select
                      className={selectClassName}
                      value={annualRevenueBand}
                      onChange={(event) =>
                        setAnnualRevenueBand(
                          event.target.value as AnnualRevenueBand,
                        )
                      }
                    >
                      <option value="Under1M">Under ₱1M</option>
                      <option value="From1MTo10M">₱1M to ₱10M</option>
                      <option value="From10MTo50M">₱10M to ₱50M</option>
                      <option value="From50MTo250M">₱50M to ₱250M</option>
                    </select>
                  </label>

                  <label className="text-sm font-semibold text-slate-100">
                    Requested limit
                    <HelpButton id="requestedLimit" label="Requested limit" />
                    <select
                      className={selectClassName}
                      value={requestedLimit}
                      onChange={(event) => setRequestedLimit(event.target.value)}
                    >
                      <option value="250000">₱250,000</option>
                      <option value="500000">₱500,000</option>
                      <option value="1000000">₱1,000,000</option>
                      <option value="2000000">₱2,000,000</option>
                      <option value="5000000">₱5,000,000</option>
                    </select>
                  </label>

                  <label className="text-sm font-semibold text-slate-100">
                    Retention
                    <HelpButton id="retention" label="Retention" />
                    <select
                      className={selectClassName}
                      value={retention}
                      onChange={(event) => setRetention(event.target.value)}
                    >
                      <option value="2500">₱2,500</option>
                      <option value="5000">₱5,000</option>
                      <option value="10000">₱10,000</option>
                      <option value="25000">₱25,000</option>
                    </select>
                  </label>

                  <div className="text-sm font-semibold text-slate-100">
                    <div>
                      <label htmlFor="quote-mfa-status">MFA status</label>
                      <HelpButton id="mfaStatus" label="MFA status" />
                    </div>
                    <select
                      id="quote-mfa-status"
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
                  </div>

                  <label className="text-sm font-semibold text-slate-100">
                    EDR status
                    <HelpButton id="edrStatus" label="EDR status" />
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
                    <HelpButton id="backupMaturity" label="Backup maturity" />
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
                    <HelpButton
                      id="sensitiveDataExposure"
                      label="Sensitive data exposure"
                    />
                    <select
                      className={selectClassName}
                      value={sensitiveDataExposure}
                      onChange={(event) =>
                        setSensitiveDataExposure(
                          event.target.value as SensitiveDataExposure,
                        )
                      }
                    >
                      <option value="Unknown">Unknown / not assessed</option>
                      <option value="Low">Low</option>
                      <option value="Moderate">Moderate</option>
                      <option value="High">High</option>
                    </select>
                  </label>

                  <label className="text-sm font-semibold text-slate-100">
                    Prior cyber incidents
                    <HelpButton
                      id="priorCyberIncidents"
                      label="Prior cyber incidents"
                    />
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
                    <span>
                      Incident response plan in place
                      <HelpButton
                        id="incidentResponsePlan"
                        label="Incident response plan"
                      />
                    </span>
                  </label>
                </div>

                <fieldset className="mt-5 rounded-md border border-slate-700 bg-slate-950/50 p-4">
                  <legend className="px-2 text-sm font-semibold text-white">
                    How the controls are implemented
                  </legend>
                  <p className="mt-2 text-sm leading-6 text-slate-300">
                    These details make broad answers measurable. They are still
                    customer assertions and may require documents or read-only
                    system evidence before underwriting treats them as verified.
                  </p>
                  <div className="mt-4 grid gap-3 sm:grid-cols-2">
                    {controlDetailCheckboxes.map((detail) => (
                      <label
                        key={detail.key}
                        className="flex items-start gap-3 text-sm text-slate-100"
                      >
                        <input
                          checked={controlDetails[detail.key]}
                          className="mt-1 h-4 w-4 rounded border-slate-700 bg-slate-950"
                          type="checkbox"
                          onChange={(event) =>
                            setControlDetails((current) => ({
                              ...current,
                              [detail.key]: event.target.checked,
                            }))
                          }
                        />
                        <span>{detail.label}</span>
                      </label>
                    ))}
                  </div>
                  <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                    <label className="text-sm font-semibold text-slate-100">
                      EDR coverage %
                      <input
                        className={fieldClassName}
                        min="0"
                        max="100"
                        type="number"
                        value={controlDetails.edrCoveragePercent}
                        onChange={(event) =>
                          setControlDetails((current) => ({
                            ...current,
                            edrCoveragePercent: Number(event.target.value),
                          }))
                        }
                      />
                    </label>
                    <label className="text-sm font-semibold text-slate-100">
                      Recovery point hours
                      <input
                        className={fieldClassName}
                        min="0"
                        max="720"
                        type="number"
                        value={controlDetails.recoveryPointObjectiveHours}
                        onChange={(event) =>
                          setControlDetails((current) => ({
                            ...current,
                            recoveryPointObjectiveHours: Number(event.target.value),
                          }))
                        }
                      />
                    </label>
                    <label className="text-sm font-semibold text-slate-100">
                      Recovery time hours
                      <input
                        className={fieldClassName}
                        min="0"
                        max="720"
                        type="number"
                        value={controlDetails.recoveryTimeObjectiveHours}
                        onChange={(event) =>
                          setControlDetails((current) => ({
                            ...current,
                            recoveryTimeObjectiveHours: Number(event.target.value),
                          }))
                        }
                      />
                    </label>
                    <label className="text-sm font-semibold text-slate-100">
                      Sensitive-data volume
                      <input
                        className={fieldClassName}
                        type="text"
                        value={controlDetails.sensitiveDataVolume}
                        onChange={(event) =>
                          setControlDetails((current) => ({
                            ...current,
                            sensitiveDataVolume: event.target.value,
                          }))
                        }
                      />
                    </label>
                  </div>
                  {controlValidationErrors.length > 0 && (
                    <div role="alert" className="mt-4 rounded-md border border-red-800 bg-red-950/50 p-3 text-sm text-red-200">
                      <p className="font-semibold">Resolve these control inconsistencies:</p>
                      <ul className="mt-2 list-disc space-y-1 pl-5">
                        {controlValidationErrors.map((error) => (
                          <li key={error}>{error}</li>
                        ))}
                      </ul>
                    </div>
                  )}
                </fieldset>

                {needsPriorIncidentDetails && (
                  <div className="mt-5 rounded-md border border-slate-700 bg-slate-950/60 p-4">
                    <h3 className="text-sm font-semibold text-white">
                      Prior incident history
                    </h3>
                    <p className="mt-2 text-sm leading-6 text-slate-300">
                      Select the incident types and summarize what happened.
                      Severe prior incidents can affect referral and
                      underwriting decisioning.
                    </p>
                    <div className="mt-4 grid gap-3 sm:grid-cols-2">
                      {incidentTypeOptions.map((incidentType) => (
                        <label
                          key={incidentType}
                          className="flex items-center gap-3 text-sm text-slate-100"
                        >
                          <input
                            checked={priorCyberIncidentTypes.includes(
                              incidentType,
                            )}
                            className="h-4 w-4 rounded border-slate-700 bg-slate-950"
                            type="checkbox"
                            onChange={(event) =>
                              handleIncidentTypeChange(
                                incidentType,
                                event.target.checked,
                              )
                            }
                          />
                          {incidentType}
                        </label>
                      ))}
                    </div>
                    <label className="mt-4 block text-sm font-semibold text-slate-100">
                      Incident details
                      <textarea
                        className={`${fieldClassName} min-h-28`}
                        value={priorCyberIncidentDetails}
                        onChange={(event) =>
                          setPriorCyberIncidentDetails(event.target.value)
                        }
                        placeholder="Summarize timing, affected systems/data, business impact, recovery status, root cause, and controls added after the incident."
                      />
                    </label>
                    {!canGenerateQuoteRequest && (
                      <p className="mt-3 text-sm text-amber-200">
                        Select at least one incident type and provide details
                        before generating a quote.
                      </p>
                    )}
                  </div>
                )}

                <div className="mt-5 rounded-md border border-sky-800 bg-sky-950/40 p-4">
                  <h3 className="text-sm font-semibold text-sky-100">
                    Control attestation
                  </h3>
                  <p className="mt-2 text-sm leading-6 text-slate-200">
                    I confirm that these answers are accurate to the best of my
                    knowledge and understand that supporting evidence may be
                    requested. Verified differences may change the risk
                    assessment, premium, quote terms, or underwriting decision.
                  </p>
                  <div className="mt-4 grid gap-4 sm:grid-cols-2">
                    <label className="text-sm font-semibold text-slate-100">
                      Attesting person
                      <input
                        className={fieldClassName}
                        type="text"
                        value={attestedByName}
                        onChange={(event) => setAttestedByName(event.target.value)}
                        placeholder={displayedSubmission?.applicantName ?? "Full name"}
                      />
                    </label>
                    <label className="text-sm font-semibold text-slate-100">
                      Title or role
                      <input
                        className={fieldClassName}
                        type="text"
                        value={attestedByTitle}
                        onChange={(event) => setAttestedByTitle(event.target.value)}
                        placeholder="Example: Chief Information Security Officer"
                      />
                    </label>
                  </div>
                  <label className="mt-4 flex items-start gap-3 text-sm text-slate-100">
                    <input
                      checked={attestationAccepted}
                      className="mt-1 h-4 w-4 rounded border-slate-700 bg-slate-950"
                      type="checkbox"
                      onChange={(event) => setAttestationAccepted(event.target.checked)}
                    />
                    <span>I confirm this attestation.</span>
                  </label>
                  <p className="mt-3 text-xs leading-5 text-slate-400">
                    This wording is a product-hardening draft and must receive
                    legal/compliance approval before production use.
                  </p>
                </div>

                <button
                  type="button"
                  onClick={handleGenerateQuote}
                  disabled={
                    createQuoteMutation.isPending ||
                    !canGenerateQuoteRequest ||
                    !hasValidControlDetails ||
                    !hasReassessmentChanges
                  }
                  className="mt-5 inline-flex min-h-10 items-center rounded-md bg-emerald-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-emerald-200 disabled:cursor-not-allowed disabled:bg-slate-600 disabled:text-slate-300"
                >
                  {createQuoteMutation.isPending
                    ? "Generating..."
                    : isReassessing
                      ? "Create reassessment"
                      : "Generate quote"}
                </button>
                {isReassessing && (
                  <button
                    type="button"
                    onClick={handleCancelReassessment}
                    className="ml-3 mt-5 inline-flex min-h-10 items-center rounded-md border border-slate-600 px-4 py-2 text-sm font-semibold text-white hover:border-slate-400"
                  >
                    Cancel reassessment
                  </button>
                )}
                {isReassessing && !hasReassessmentChanges && (
                  <p className="mt-3 text-sm text-amber-200">
                    The control answers still match the latest saved quote. A
                    reassessment needs at least one different high-level status or
                    detailed implementation answer; changing only the attesting
                    person does not create a new risk assessment.
                  </p>
                )}
              </div>
            )}

            {createQuoteMutation.isError && (
              <p className="mt-5 whitespace-pre-wrap rounded-md border border-red-900 bg-red-950 p-3 text-sm text-red-200">
                {getErrorMessage(createQuoteMutation.error)}
              </p>
            )}

            {queuedReassessment && (
              <p className="mt-5 rounded-md border border-sky-700 bg-sky-950/40 p-4 text-sm leading-6 text-sky-100">
                <span className="font-semibold">Reassessment awaiting underwriting review.</span>{" "}
                Your current quote remains active. You will receive a notification after underwriting approves or declines the request.
              </p>
            )}

            {activeQuoteId && (
              <div className="mt-6 border-t border-slate-800 pt-5">
                <h2 className="text-base font-semibold text-white">
                  Latest quote · version {activeQuoteVersion}
                </h2>
                <Link
                  className="mt-2 inline-flex min-h-10 items-center font-semibold text-emerald-300 underline hover:text-emerald-200"
                  to={`/submissions/${submissionId}/quotes/${activeQuoteId}`}
                >
                  View quote version {activeQuoteVersion}
                </Link>
                <Link
                  className="ml-5 mt-2 inline-flex min-h-10 items-center font-semibold text-sky-300 underline hover:text-sky-200"
                  to={`/submissions/${submissionId}/quotes`}
                >
                  View all quote versions
                </Link>
                {activeAssuranceStatus === "EvidenceRequired" && (
                  <div className="mt-4 rounded-md border border-amber-700 bg-amber-950/40 p-4 text-sm text-amber-100">
                    <p className="font-semibold">Provisional — evidence required</p>
                    <p className="mt-2 leading-6">
                      Underwriting must satisfy {activeEvidenceRequiredCount} control
                      verification{activeEvidenceRequiredCount === 1 ? "" : "s"} before
                      this quote can be accepted. {activeEvidenceSatisfiedCount} currently
                      satisfied. Automated checks assist review but do not make the final
                      insurance decision.
                    </p>
                    <Link className="mt-3 inline-block font-semibold text-amber-200 underline" to="/evidence-requests">
                      Open evidence requests
                    </Link>
                  </div>
                )}
                <dl className="mt-4 grid gap-4 sm:grid-cols-2">
                  <div>
                    <dt className="font-semibold text-slate-400">Quote ID</dt>
                    <dd className="mt-1 break-all text-white">
                      {activeQuoteId}
                    </dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-400">Status</dt>
                    <dd className="mt-1 text-white">{activeQuoteStatus}</dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-400">Premium</dt>
                    <dd className="mt-1 text-white">
                      {formatCurrency(activePremium ?? 0)}
                    </dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-400">Risk tier</dt>
                    <dd className="mt-1 text-white">{activeRiskTier}</dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-400">Limit</dt>
                    <dd className="mt-1 text-white">
                      {formatCurrency(activeRequestedLimit ?? 0)}
                    </dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-400">Retention</dt>
                    <dd className="mt-1 text-white">
                      {formatCurrency(activeRetention ?? 0)}
                    </dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-400">
                      {activeQuoteStatus === "Bound"
                        ? "Policy expires UTC"
                        : "Expires UTC"}
                    </dt>
                    <dd className="mt-1 text-white">
                      <time dateTime={activeExpiresAtUtc}>
                        {activeExpiresAtUtc}
                      </time>
                    </dd>
                  </div>
                  {createdQuote?.providerIndication && (
                    <div>
                      <dt className="font-semibold text-slate-400">Provider</dt>
                      <dd className="mt-1 text-white">
                        {createdQuote.providerIndication.providerName} -{" "}
                        {createdQuote.providerIndication.marketDisposition}
                      </dd>
                    </div>
                  )}
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

                {activeReferralReasons.length > 0 && (
                  <div className="mt-5 rounded-md border border-amber-500/50 bg-amber-950/30 p-4 text-sm text-amber-100">
                    <h3 className="font-semibold">Underwriting referral</h3>
                    <ul className="mt-2 list-disc space-y-1 pl-5">
                      {activeReferralReasons.map((reason) => (
                        <li key={reason}>{reason}</li>
                      ))}
                    </ul>
                  </div>
                )}

                {canStartReassessment && !isReassessing && (
                  <div className="mt-5 rounded-md border border-slate-700 bg-slate-950/50 p-4 text-sm text-slate-200">
                    <p className="font-semibold text-white">Controls changed?</p>
                    <p className="mt-2 leading-6">
                      Request a reassessment before acceptance or binding. The
                      current quote remains in audit history as superseded, and
                      claimed improvements require evidence. Reassessment does
                      not guarantee a lower premium or approval.
                    </p>
                    <button
                      className="mt-3 inline-flex min-h-10 items-center rounded-md border border-slate-600 px-4 py-2 font-semibold text-white hover:border-emerald-300 hover:text-emerald-200"
                      type="button"
                      onClick={() => {
                        const source = createdQuote ?? latestQuote;
                        if (source) {
                          setReassessmentBaseline(applyQuoteControls(source));
                        }
                        setIsReassessing(true);
                        setAttestationAccepted(false);
                        setAttestedByName("");
                        setAttestedByTitle("");
                      }}
                    >
                      Reassess controls
                    </button>
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

            {isQuoteAcceptedNoticeVisible && (
              <TransientStatusMessage
                className="mt-5 text-sm"
                onDismiss={() => setIsQuoteAcceptedNoticeVisible(false)}
              >
                Quote accepted successfully.
              </TransientStatusMessage>
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

            {canSubmit && !isEditing && (
              <div className="mt-6 border-t border-slate-800 pt-5">
                <h2 className="text-base font-semibold text-white">Delete this draft</h2>
                <p className="mt-2 text-slate-300">Only drafts can be deleted. Once submitted, the application becomes retained audit history.</p>
                <button type="button" onClick={handleDeleteDraft} disabled={deleteDraftMutation.isPending} className="mt-4 rounded-md border border-red-500/60 px-4 py-2 font-semibold text-red-200">Delete draft</button>
              </div>
            )}

            {canWithdraw && (
              <div className="mt-6 border-t border-slate-800 pt-5">
                <h2 className="text-base font-semibold text-white">Withdraw application</h2>
                <p className="mt-2 text-slate-300">Withdrawal retains this submitted record and does not rewrite or delete separate quote history.</p>
                <button type="button" onClick={handleWithdrawSubmission} disabled={withdrawMutation.isPending} className="mt-4 rounded-md border border-amber-500/60 px-4 py-2 font-semibold text-amber-200">Withdraw submission</button>
              </div>
            )}

            {isWithdrawnNoticeVisible && (
              <TransientStatusMessage
                className="mt-5"
                onDismiss={() => setIsWithdrawnNoticeVisible(false)}
                tone="warning"
              >
                Submission withdrawn. The record remains available as audit history.
              </TransientStatusMessage>
            )}
            {(withdrawMutation.isError || deleteDraftMutation.isError) && <p className="mt-5 rounded-md border border-red-900 bg-red-950 p-3 text-red-200">{getErrorMessage(withdrawMutation.error ?? deleteDraftMutation.error)}</p>}

            {relatedPolicy && (
              <section className="mt-6 rounded-md border border-emerald-500/40 bg-emerald-950/20 p-5">
                <h2 className="text-base font-semibold text-white">Related policy</h2>
                <p className="mt-2 text-slate-300">
                  The submission remains {displayedSubmission.status}; the separate contract is {relatedPolicy.contractualStatus} with coverage {relatedPolicy.coverageState}.
                </p>
                <div className="mt-4 flex flex-wrap items-center gap-4">
                  <span className="font-semibold text-white">{relatedPolicy.policyNumber}</span>
                  <span>{new Date(relatedPolicy.effectiveDateUtc).toLocaleDateString()} – {new Date(relatedPolicy.expirationDateUtc).toLocaleDateString()}</span>
                  <Link to={`/policies/${relatedPolicy.policyId}`} className="font-semibold text-emerald-300">View policy</Link>
                </div>
              </section>
            )}
          </section>
        )}
      </section>
      {isDeleteDialogOpen && displayedSubmission && (
        <ConfirmationDialog
          title="Delete this draft?"
          description={`This permanently deletes the draft for ${displayedSubmission.companyName}. This action cannot be undone.`}
          confirmLabel="Delete draft"
          information={{
            title: "Why can this draft be deleted?",
            description:
              "This application has not been submitted yet, so it is still an editable draft and may be permanently removed. After you select Submit submission and the system accepts it, the application becomes retained business and audit history. It can no longer be deleted; when eligible, it may only be withdrawn so the record remains traceable.",
          }}
          isPending={deleteDraftMutation.isPending}
          onCancel={() => setIsDeleteDialogOpen(false)}
          onConfirm={() => void confirmDeleteDraft()}
          pendingLabel="Deleting draft..."
        />
      )}
      {isWithdrawDialogOpen && displayedSubmission && (
        <ConfirmationDialog
          title="Withdraw this application?"
          description={`This stops the submitted application for ${displayedSubmission.companyName} from continuing through the eligible pre-contract journey.`}
          confirmLabel="Withdraw application"
          information={{
            title: "Why is withdrawal different from deletion?",
            description:
              "A submitted application is retained business and audit history, so withdrawal changes its status without erasing the record. Separate quote history is also preserved. Withdrawal is unavailable after a quote is accepted or a policy is bound.",
          }}
          isPending={withdrawMutation.isPending}
          onCancel={() => setIsWithdrawDialogOpen(false)}
          onConfirm={() => void confirmWithdrawSubmission()}
          pendingLabel="Withdrawing application..."
          tone="warning"
        />
      )}
      {isCancelReassessmentDialogOpen && (
        <ConfirmationDialog
          title="Discard reassessment changes?"
          description="This removes the control edits in this reassessment form. It does not change or delete the current quote."
          confirmLabel="Discard changes"
          tone="warning"
          information={{
            title: "Your current quote stays unchanged",
            description:
              "A reassessment is not saved until creation succeeds. Cancelling restores the answers from the current quote and keeps its audit history intact.",
          }}
          onCancel={() => setIsCancelReassessmentDialogOpen(false)}
          onConfirm={finishCancellingReassessment}
        />
      )}
    </main>
  );
}
