import { lazy, Suspense, type ReactNode } from "react";
import { Route, Routes } from "react-router";

import { AppShell } from "./components/AppShell";
import { RequireAuth } from "./components/RequireAuth";
import { RequireRole } from "./components/RequireRole";
import { roleGroups } from "./lib/roleAccess";
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

function ProtectedRoute({
  allowedRoles,
  children,
}: {
  allowedRoles?: readonly string[];
  children: ReactNode;
}) {
  const page = <AppShell>{children}</AppShell>;

  return (
    <RequireAuth>
      {allowedRoles ? (
        <RequireRole allowedRoles={[...allowedRoles]}>{page}</RequireRole>
      ) : (
        page
      )}
    </RequireAuth>
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
            <ProtectedRoute>
              <DashboardPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/submissions/new"
          element={
            <ProtectedRoute allowedRoles={roleGroups.customerWork}>
              <NewSubmissionPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/submissions"
          element={
            <ProtectedRoute allowedRoles={roleGroups.customerWork}>
              <SubmissionsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/submissions/:submissionId"
          element={
            <ProtectedRoute allowedRoles={roleGroups.customerWork}>
              <SubmissionDetailPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/underwriting/quote-referrals"
          element={
            <ProtectedRoute allowedRoles={roleGroups.underwritingWork}>
              <UnderwritingQuoteReferralsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/evidence-requests"
          element={
            <ProtectedRoute allowedRoles={roleGroups.customerWork}>
              <EvidenceRequestsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/claims/adjudication"
          element={
            <ProtectedRoute allowedRoles={roleGroups.claimsAdjudication}>
              <ClaimsAdjudicationPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/claims/new"
          element={
            <ProtectedRoute allowedRoles={roleGroups.customerWork}>
              <NewClaimPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/claims"
          element={
            <ProtectedRoute allowedRoles={roleGroups.customerWork}>
              <ClaimsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/claims/:claimId"
          element={
            <ProtectedRoute allowedRoles={roleGroups.customerWork}>
              <ClaimDetailPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/notifications"
          element={
            <ProtectedRoute allowedRoles={roleGroups.notifications}>
              <NotificationsPage />
            </ProtectedRoute>
          }
        />
      </Routes>
    </Suspense>
  );
}

export default App;
