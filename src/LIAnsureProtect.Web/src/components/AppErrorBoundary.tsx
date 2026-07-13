import { Component, type ReactNode } from "react";

import { captureClientError } from "../lib/telemetry";

type Props = { children: ReactNode };
type State = { failed: boolean };

export class AppErrorBoundary extends Component<Props, State> {
  state: State = { failed: false };

  static getDerivedStateFromError(): State {
    return { failed: true };
  }

  componentDidCatch(error: Error) {
    captureClientError(error);
  }

  render() {
    if (this.state.failed) {
      return (
        <main className="flex min-h-screen items-center justify-center bg-slate-950 px-6 text-white">
          <section className="max-w-lg rounded-xl border border-slate-700 bg-slate-900 p-8 text-center">
            <h1 className="text-2xl font-semibold">This page could not be displayed.</h1>
            <p className="mt-3 text-slate-300">
              Refresh the page to try again. If the problem continues, contact support.
            </p>
            <button
              type="button"
              onClick={() => window.location.reload()}
              className="mt-6 rounded-md bg-emerald-400 px-4 py-2 font-semibold text-slate-950"
            >
              Refresh page
            </button>
          </section>
        </main>
      );
    }

    return this.props.children;
  }
}
