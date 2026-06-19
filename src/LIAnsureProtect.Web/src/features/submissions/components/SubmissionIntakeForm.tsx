import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";

import { useCreateSubmission } from "../hooks/useCreateSubmission";
import {
  submissionIntakeSchema,
  type SubmissionIntakeFormValues,
} from "../schemas/submissionIntakeSchema";

const fieldClassName =
  "mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-4 py-3 text-sm text-white outline-none focus:border-emerald-400";

export function SubmissionIntakeForm() {
  const {
    formState: { errors },
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

  function onSubmit(values: SubmissionIntakeFormValues) {
    createSubmission.mutate(values);
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
          disabled={createSubmission.isPending}
          type="submit"
        >
          {createSubmission.isPending
            ? "Creating submission..."
            : "Create draft submission"}
        </button>
      </form>

      {createSubmission.isSuccess && (
        <section className="mt-6 rounded-lg border border-emerald-900 bg-emerald-950 p-5 text-sm text-emerald-100">
          <h2 className="text-base font-semibold text-white">
            Draft submission created
          </h2>
          <p className="mt-3">
            <span className="font-semibold">Submission ID:</span>{" "}
            {createSubmission.data.submissionId}
          </p>
          <p className="mt-2">
            <span className="font-semibold">Status:</span>{" "}
            {createSubmission.data.status}
          </p>
        </section>
      )}

      {createSubmission.isError && (
        <p className="mt-6 whitespace-pre-wrap rounded-lg border border-red-900 bg-red-950 p-4 text-sm text-red-200">
          {createSubmission.error instanceof Error
            ? createSubmission.error.message
            : "Unable to create submission."}
        </p>
      )}
    </>
  );
}
