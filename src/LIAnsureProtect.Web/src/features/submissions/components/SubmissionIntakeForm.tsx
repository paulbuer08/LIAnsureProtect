import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useNavigate } from "react-router";

import { getUserErrorMessage } from "../../../lib/apiClient";
import { useCreateSubmission } from "../hooks/useCreateSubmission";
import {
  submissionIntakeSchema,
  type SubmissionIntakeFormValues,
} from "../schemas/submissionIntakeSchema";

const fieldClassName =
  "mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-4 py-3 text-sm text-white outline-none focus:border-emerald-400";

export function SubmissionIntakeForm() {
  const navigate = useNavigate();
  const [matchingDraftId, setMatchingDraftId] = useState<string>();
  const [lastAttempt, setLastAttempt] = useState<
    { fingerprint: string; idempotencyKey: string } | undefined
  >(undefined);
  const {
    formState: { errors },
    getValues,
    handleSubmit,
    register,
  } = useForm<SubmissionIntakeFormValues>({
    resolver: zodResolver(submissionIntakeSchema),
    defaultValues: {
      applicantName: "",
      applicantEmail: "",
      companyName: "",
    },
  });

  const createSubmission = useCreateSubmission();

  function submitDraft(
    values: SubmissionIntakeFormValues,
    createAnotherDraft: boolean,
  ) {
    const request = { ...values, createAnotherDraft };
    const fingerprint = JSON.stringify(request);
    const previousAttempt = lastAttempt;
    const idempotencyKey =
      previousAttempt?.fingerprint === fingerprint
        ? previousAttempt.idempotencyKey
        : crypto.randomUUID();

    setLastAttempt({ fingerprint, idempotencyKey });
    createSubmission.mutate(
      { request, idempotencyKey },
      {
        onSuccess: (result) => {
          if (result.existingDraft) {
            setMatchingDraftId(result.submissionId);
            return;
          }

          void navigate(`/submissions/${result.submissionId}`, {
            replace: true,
            state: {
              draftCreated: true,
              possibleDuplicate: result.possibleDuplicate,
            },
          });
        },
      },
    );
  }

  function onSubmit(values: SubmissionIntakeFormValues) {
    submitDraft(values, false);
  }

  return (
    <>
      <form
        className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-6"
        onSubmit={handleSubmit(onSubmit)}
        noValidate
      >
        <div>
          <label
            className="text-sm font-semibold text-slate-100"
            htmlFor="applicantName"
          >
            Applicant name
          </label>
          <input
            aria-invalid={errors.applicantName ? "true" : "false"}
            className={fieldClassName}
            disabled={Boolean(matchingDraftId)}
            id="applicantName"
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
            htmlFor="applicantEmail"
          >
            Applicant email
          </label>
          <input
            aria-invalid={errors.applicantEmail ? "true" : "false"}
            className={fieldClassName}
            disabled={Boolean(matchingDraftId)}
            id="applicantEmail"
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
            htmlFor="companyName"
          >
            Company name
          </label>
          <input
            aria-invalid={errors.companyName ? "true" : "false"}
            className={fieldClassName}
            disabled={Boolean(matchingDraftId)}
            id="companyName"
            type="text"
            {...register("companyName")}
          />
          {errors.companyName && (
            <p className="mt-2 text-sm text-red-300">
              {errors.companyName.message}
            </p>
          )}
        </div>

        <button
          className="mt-6 rounded-lg bg-emerald-400 px-5 py-3 text-sm font-semibold text-slate-950 shadow-sm hover:bg-emerald-300 disabled:cursor-not-allowed disabled:bg-slate-600 disabled:text-slate-300"
          disabled={createSubmission.isPending || Boolean(matchingDraftId)}
          type="submit"
        >
          {createSubmission.isPending
            ? "Creating draft..."
            : "Create draft submission"}
        </button>
      </form>

      {matchingDraftId && (
        <section className="mt-6 rounded-lg border border-amber-500/50 bg-amber-950/30 p-5 text-sm text-amber-100">
          <h2 className="text-base font-semibold text-white">
            A matching draft already exists
          </h2>
          <p className="mt-3 leading-6">
            Continue the existing draft to avoid an accidental duplicate. If
            this is a genuinely separate application, you can create another
            draft explicitly.
          </p>
          <div className="mt-5 flex flex-wrap gap-3">
            <button
              type="button"
              className="rounded-md bg-emerald-300 px-4 py-2 font-semibold text-slate-950 hover:bg-emerald-200"
              onClick={() => void navigate(`/submissions/${matchingDraftId}`)}
            >
              Continue existing draft
            </button>
            <button
              type="button"
              className="rounded-md border border-amber-400/60 px-4 py-2 font-semibold text-amber-100 hover:bg-amber-950"
              disabled={createSubmission.isPending}
              onClick={() => submitDraft(getValues(), true)}
            >
              {createSubmission.isPending
                ? "Creating another draft..."
                : "Create another draft anyway"}
            </button>
          </div>
        </section>
      )}

      {createSubmission.isError && (
        <p className="mt-6 whitespace-pre-wrap rounded-lg border border-red-900 bg-red-950 p-4 text-sm text-red-200">
          {getUserErrorMessage(
            createSubmission.error,
            "Unable to create the draft submission.",
          )}
        </p>
      )}
    </>
  );
}
