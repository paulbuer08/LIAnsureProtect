import { lazy, Suspense } from "react";
import { Route, Routes } from "react-router";

import { RequireAuth } from "./components/RequireAuth";
import "./App.css";

const HomePage = lazy(() =>
  import("./pages/HomePage").then((module) => ({ default: module.HomePage })),
);

const LoginCallbackPage = lazy(() =>
  import("./pages/LoginCallbackPage").then((module) => ({
    default: module.LoginCallbackPage,
  })),
);

const DashboardPage = lazy(() =>
  import("./pages/DashboardPage").then((module) => ({
    default: module.DashboardPage,
  })),
);

const NewSubmissionPage = lazy(() =>
  import("./features/submissions/pages/NewSubmissionPage").then((module) => ({
    default: module.NewSubmissionPage,
  })),
);

const SubmissionsPage = lazy(() =>
  import("./features/submissions/pages/SubmissionsPage").then((module) => ({
    default: module.SubmissionsPage,
  })),
);

const SubmissionDetailPage = lazy(() =>
  import("./features/submissions/pages/SubmissionDetailPage").then(
    (module) => ({
      default: module.SubmissionDetailPage,
    }),
  ),
);

function RouteLoadingFallback() {
  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-950 px-6 text-sm font-medium text-slate-300">
      Loading...
    </main>
  );
}

function App() {
  return (
    <Suspense fallback={<RouteLoadingFallback />}>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/callback" element={<LoginCallbackPage />} />
        <Route
          path="/dashboard"
          element={
            <RequireAuth>
              <DashboardPage />
            </RequireAuth>
          }
        />
        <Route
          path="/submissions/new"
          element={
            <RequireAuth>
              <NewSubmissionPage />
            </RequireAuth>
          }
        />
        <Route
          path="/submissions"
          element={
            <RequireAuth>
              <SubmissionsPage />
            </RequireAuth>
          }
        />
        <Route
          path="/submissions/:submissionId"
          element={
            <RequireAuth>
              <SubmissionDetailPage />
            </RequireAuth>
          }
        />
      </Routes>
    </Suspense>
  );
}

export default App;
