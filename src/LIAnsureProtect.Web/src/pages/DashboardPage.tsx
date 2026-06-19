import { useState } from "react";
import { useAuth0 } from "@auth0/auth0-react";
import { Link } from "react-router";

export function DashboardPage() {
  const { getAccessTokenSilently, isAuthenticated, isLoading, logout, user } =
    useAuth0();
  const [accessTokenPreview, setAccessTokenPreview] = useState<string>();
  const [accessTokenError, setAccessTokenError] = useState<string>();
  const [isRequestingToken, setIsRequestingToken] = useState(false);

  async function handleGetAccessToken() {
    setIsRequestingToken(true);
    setAccessTokenError(undefined);

    try {
      const accessToken = await getAccessTokenSilently();
      const preview = `${accessToken.slice(0, 16)}...${accessToken.slice(-16)}`;

      setAccessTokenPreview(`${preview} (${accessToken.length} characters)`);
    } catch (error) {
      const message =
        error instanceof Error ? error.message : "Unable to get access token.";

      setAccessTokenError(message);
      setAccessTokenPreview(undefined);
    } finally {
      setIsRequestingToken(false);
    }
  }

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-16 text-white">
      <section className="mx-auto max-w-5xl">
        <p className="text-sm font-semibold uppercase tracking-wide text-emerald-400">
          Dashboard
        </p>

        <h1 className="mt-4 text-4xl font-bold tracking-tight">
          Signed-in dashboard placeholder
        </h1>

        <p className="mt-4 max-w-2xl text-slate-300">
          This page shows the current Auth0 browser session and links to the
          protected frontend workflows that call the LIAnsureProtect API.
        </p>

        <div className="mt-8 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-200">
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

        <div className="mt-6 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-200">
          <h2 className="text-base font-semibold text-white">
            API access token
          </h2>

          <p className="mt-2 text-slate-300">
            Request an Auth0 access token for the LIAnsureProtect API. Only a
            short preview is displayed so the full token is not exposed on
            screen.
          </p>

          <button
            type="button"
            onClick={handleGetAccessToken}
            disabled={isRequestingToken}
            className="mt-4 rounded-lg bg-emerald-400 px-5 py-3 text-sm font-semibold text-slate-950 shadow-sm hover:bg-emerald-300 disabled:cursor-not-allowed disabled:bg-slate-600 disabled:text-slate-300"
          >
            {isRequestingToken ? "Requesting token..." : "Get API access token"}
          </button>

          {accessTokenPreview && (
            <p className="mt-4 break-all rounded-md bg-slate-950 p-3 font-mono text-xs text-emerald-300">
              {accessTokenPreview}
            </p>
          )}

          {accessTokenError && (
            <p className="mt-4 rounded-md border border-red-900 bg-red-950 p-3 text-red-200">
              {accessTokenError}
            </p>
          )}
        </div>

        <div className="mt-6 rounded-lg border border-slate-800 bg-slate-900 p-5 text-sm text-slate-200">
          <h2 className="text-base font-semibold text-white">Submissions</h2>

          <p className="mt-2 text-slate-300">
            View existing draft submissions or create a new one. These
            workflows use the current Auth0 session to call the protected API.
          </p>

          <div className="mt-4 flex flex-wrap gap-3">
            <Link
              to="/submissions"
              className="inline-flex rounded-lg bg-emerald-400 px-5 py-3 text-sm font-semibold text-slate-950 shadow-sm hover:bg-emerald-300"
            >
              View submissions
            </Link>

            <Link
              to="/submissions/new"
              className="inline-flex rounded-lg border border-slate-700 px-5 py-3 text-sm font-semibold text-slate-200 hover:border-slate-500"
            >
              Create submission
            </Link>
          </div>
        </div>

        <div className="mt-8 flex flex-wrap gap-4">
          <Link
            to="/"
            className="inline-flex rounded-lg border border-slate-700 px-5 py-3 text-sm font-semibold text-slate-200 hover:border-slate-500"
          >
            Back to home
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
            className="inline-flex rounded-lg bg-slate-100 px-5 py-3 text-sm font-semibold text-slate-950 hover:bg-white"
          >
            Log out
          </button>
        </div>
      </section>
    </main>
  );
}
