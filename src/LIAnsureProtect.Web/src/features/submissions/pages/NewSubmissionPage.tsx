import { useAuth0 } from "@auth0/auth0-react";
import { Link } from "react-router";

import { SubmissionIntakeForm } from "../components/SubmissionIntakeForm";

export function NewSubmissionPage() {
  const { user } = useAuth0();

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-12 text-white">
      <section className="mx-auto max-w-3xl">
        <Link
          to="/dashboard"
          className="inline-flex text-sm font-semibold text-emerald-300 hover:text-emerald-200"
        >
          Back to dashboard
        </Link>

        <p className="mt-8 text-sm font-semibold uppercase tracking-wide text-emerald-400">
          Submission intake
        </p>

        <h1 className="mt-4 text-4xl font-bold tracking-tight">
          Create draft submission
        </h1>

        <p className="mt-4 text-slate-300">
          Enter the first applicant details for a cyber insurance submission.
          This creates an editable draft; it does not submit the application
          for underwriting yet.
        </p>

        {user?.email && (
          <p className="mt-4 text-sm text-slate-400">
            Signed in as <span className="text-slate-200">{user.email}</span>
          </p>
        )}

        <SubmissionIntakeForm />
      </section>
    </main>
  );
}
