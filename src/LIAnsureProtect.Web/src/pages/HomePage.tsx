import { useAuth0 } from "@auth0/auth0-react";
import { Link } from "react-router";

export function HomePage() {
  const { isAuthenticated, isLoading, loginWithRedirect, logout, user } =
    useAuth0();

  return (
    <main className="min-h-screen bg-slate-950 text-white">
      <section className="mx-auto flex min-h-screen max-w-5xl flex-col justify-center px-6 py-16">
        <p className="text-sm font-semibold uppercase tracking-wide text-emerald-400">
          LIAnsureProtect
        </p>

        <h1 className="mt-4 max-w-3xl text-4xl font-bold tracking-tight sm:text-5xl">
          Frontend login and session foundation
        </h1>

        <p className="mt-6 max-w-2xl text-lg leading-8 text-slate-300">
          This React app will become the browser-facing entry point for signing
          in, reading the current user session, and calling protected
          LIAnsureProtect API endpoints.
        </p>

        {isAuthenticated && (
          <p className="mt-6 text-sm text-slate-400">
            Signed in as{" "}
            <span className="font-medium text-slate-200">
              {user?.email ?? "authenticated user"}
            </span>
          </p>
        )}

        <div className="mt-10 flex flex-wrap gap-4">
          {isAuthenticated ? (
            <>
              <Link
                to="/dashboard"
                className="rounded-lg bg-emerald-400 px-5 py-3 text-sm font-semibold text-slate-950 shadow-sm hover:bg-emerald-300"
              >
                Go to dashboard
              </Link>

              <button
                type="button"
                onClick={() =>
                  logout({
                    logoutParams: {
                      returnTo: window.location.origin,
                    },
                  })
                }
                className="rounded-lg border border-slate-700 px-5 py-3 text-sm font-semibold text-slate-200 hover:border-slate-500"
              >
                Log out
              </button>
            </>
          ) : (
            <button
              type="button"
              onClick={() => loginWithRedirect()}
              disabled={isLoading}
              className="rounded-lg bg-emerald-400 px-5 py-3 text-sm font-semibold text-slate-950 shadow-sm hover:bg-emerald-300 disabled:cursor-not-allowed disabled:bg-slate-600 disabled:text-slate-300"
            >
              {isLoading ? "Checking session..." : "Log in with Auth0"}
            </button>
          )}
        </div>
      </section>
    </main>
  );
}
