import { useEffect } from "react";
import { Link, useNavigate } from "react-router";
import { useAuth0 } from "@auth0/auth0-react";

import { captureSafeEvent } from "../lib/telemetry";

export function LoginCallbackPage() {
  const { error, isAuthenticated, isLoading, user } = useAuth0();
  const navigate = useNavigate();

  useEffect(() => {
    if (!isLoading && isAuthenticated && !error) {
      navigate("/dashboard", { replace: true });
    }
  }, [error, isAuthenticated, isLoading, navigate]);

  useEffect(() => {
    if (error) {
      captureSafeEvent("authentication_callback_failed", {
        errorType: error.name,
      });
    }
  }, [error]);

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-950 px-6 text-white">
      <section className="max-w-xl text-center">
        <p className="text-sm font-semibold uppercase tracking-wide text-emerald-400">
          Authentication
        </p>

        <h1 className="mt-4 text-3xl font-bold tracking-tight">
          Login callback
        </h1>

        <p className="mt-4 text-slate-300">
          Auth0 redirected the browser back to the React app. The app will
          continue to the dashboard automatically after the Auth0 React SDK
          finishes loading the signed-in session.
        </p>

        <div className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-4 text-left text-sm text-slate-200">
          <p>
            <span className="font-semibold text-white">Loading:</span>{" "}
            {isLoading ? "yes" : "no"}
          </p>

          <p className="mt-2">
            <span className="font-semibold text-white">Authenticated:</span>{" "}
            {isAuthenticated ? "yes" : "no"}
          </p>

          <p className="mt-2">
            <span className="font-semibold text-white">User email:</span>{" "}
            {user?.email ?? "not available"}
          </p>
        </div>

        {error ? (
          <div className="mt-6 rounded-lg border border-red-500/40 bg-red-950/40 p-4 text-left text-sm text-red-100">
            <p className="font-semibold">Sign-in could not be completed</p>
            <p className="mt-2">
              Please try signing in again. If the problem continues, contact
              support and tell them when the error occurred.
            </p>
          </div>
        ) : null}

        <Link
          to="/dashboard"
          className="mt-8 inline-flex rounded-lg bg-emerald-400 px-5 py-3 text-sm font-semibold text-slate-950 shadow-sm hover:bg-emerald-300"
        >
          Continue to dashboard
        </Link>
      </section>
    </main>
  );
}
