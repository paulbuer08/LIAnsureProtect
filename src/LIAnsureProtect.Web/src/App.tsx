import { lazy, Suspense } from "react";
import { Route, Routes } from "react-router";

import { RequireAuth } from "./components/RequireAuth";
import { RequireRole } from "./components/RequireRole";
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

const UnderwritingQuoteReferralsPage = lazy(() =>
  import("./features/underwriting/pages/UnderwritingQuoteReferralsPage").then(
    (module) => ({
      default: module.UnderwritingQuoteReferralsPage,
    }),
  ),
);

const EvidenceRequestsPage = lazy(() =>
  import("./features/evidence/pages/EvidenceRequestsPage").then((module) => ({
    default: module.EvidenceRequestsPage,
  })),
);

const ClaimsPage = lazy(() =>
  import("./features/claims/pages/ClaimsPage").then((module) => ({
    default: module.ClaimsPage,
  })),
);

const NewClaimPage = lazy(() =>
  import("./features/claims/pages/NewClaimPage").then((module) => ({
    default: module.NewClaimPage,
  })),
);

const ClaimDetailPage = lazy(() =>
  import("./features/claims/pages/ClaimDetailPage").then((module) => ({
    default: module.ClaimDetailPage,
  })),
);

const ClaimsAdjudicationPage = lazy(() =>
  import("./features/claims/pages/ClaimsAdjudicationPage").then((module) => ({
    default: module.ClaimsAdjudicationPage,
  })),
);

const NotificationsPage = lazy(() =>
  import("./features/notifications/pages/NotificationsPage").then((module) => ({
    default: module.NotificationsPage,
  })),
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
        <Route
          path="/underwriting/quote-referrals"
          element={
            <RequireAuth>
              <UnderwritingQuoteReferralsPage />
            </RequireAuth>
          }
        />
        <Route
          path="/evidence-requests"
          element={
            <RequireAuth>
              <EvidenceRequestsPage />
            </RequireAuth>
          }
        />
        <Route
          path="/claims/adjudication"
          element={
            <RequireAuth>
              <RequireRole allowedRoles={["ClaimsAdjuster", "Admin"]}>
                <ClaimsAdjudicationPage />
              </RequireRole>
            </RequireAuth>
          }
        />
        <Route
          path="/claims/new"
          element={
            <RequireAuth>
              <RequireRole allowedRoles={["Customer", "Broker", "Admin"]}>
                <NewClaimPage />
              </RequireRole>
            </RequireAuth>
          }
        />
        <Route
          path="/claims"
          element={
            <RequireAuth>
              <RequireRole allowedRoles={["Customer", "Broker", "Admin"]}>
                <ClaimsPage />
              </RequireRole>
            </RequireAuth>
          }
        />
        <Route
          path="/claims/:claimId"
          element={
            <RequireAuth>
              <RequireRole allowedRoles={["Customer", "Broker", "Admin"]}>
                <ClaimDetailPage />
              </RequireRole>
            </RequireAuth>
          }
        />
        <Route
          path="/notifications"
          element={
            <RequireAuth>
              <NotificationsPage />
            </RequireAuth>
          }
        />
      </Routes>
    </Suspense>
  );
}

export default App;
