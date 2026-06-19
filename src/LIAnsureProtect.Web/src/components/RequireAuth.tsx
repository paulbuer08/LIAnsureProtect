import { useAuth0 } from "@auth0/auth0-react";
import type { ReactNode } from "react";

type RequireAuthProps = {
  children: ReactNode;
};

export function RequireAuth({ children }: RequireAuthProps) {
  const { isAuthenticated, isLoading, loginWithRedirect } = useAuth0();

  if (isLoading) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-slate-950 px-6 text-white">
        <p className="text-slate-300">Checking authentication...</p>
      </main>
    );
  }

  if (!isAuthenticated) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-slate-950 px-6 text-white">
        <section className="max-w-xl text-center">
          <p className="text-sm font-semibold uppercase tracking-wide text-emerald-400">
            Authentication required
          </p>

          <h1 className="mt-4 text-3xl font-bold tracking-tight">
            Please log in to continue
          </h1>

          <p className="mt-4 text-slate-300">
            This is a signed-in area. Log in with Auth0 to continue to your
            protected LIAnsureProtect workflow.
          </p>

          <button
            type="button"
            onClick={() => loginWithRedirect()}
            className="mt-8 rounded-lg bg-emerald-400 px-5 py-3 text-sm font-semibold text-slate-950 shadow-sm hover:bg-emerald-300"
          >
            Log in with Auth0
          </button>
        </section>
      </main>
    );
  }

  return children;
}
