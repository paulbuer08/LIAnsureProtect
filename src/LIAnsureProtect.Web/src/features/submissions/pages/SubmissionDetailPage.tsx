import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useParams } from "react-router";

import { useSubmissionDetail } from "../hooks/useSubmissionDetail";
import { useSubmitSubmission } from "../hooks/useSubmitSubmission";
import { useUpdateSubmission } from "../hooks/useUpdateSubmission";
import {
  submissionIntakeSchema,
  type SubmissionIntakeFormValues,
} from "../schemas/submissionIntakeSchema";

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Unable to load submission.";
}

const fieldClassName =
  "mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-4 py-3 text-sm text-white outline-none focus:border-emerald-400";

export function SubmissionDetailPage() {
  const { submissionId } = useParams();
  const [isEditing, setIsEditing] = useState(false);
  const submissionQuery = useSubmissionDetail(submissionId);
  const updateSubmissionMutation = useUpdateSubmission();
  const submitSubmissionMutation = useSubmitSubmission();
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
          </section>
        )}
      </section>
    </main>
  );
}
