import { lazy, Suspense, type ReactNode } from "react";
import { Navigate, Route, Routes } from "react-router";

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

const QuoteDetailPage = lazy(() =>
  import("./features/submissions/pages/QuoteDetailPage").then((module) => ({
    default: module.QuoteDetailPage,
  })),
);

const QuoteHistoryPage = lazy(() =>
  import("./features/submissions/pages/QuoteHistoryPage").then((module) => ({
    default: module.QuoteHistoryPage,
  })),
);

const EvidenceRequestDetailPage = lazy(() =>
  import("./features/evidence/pages/EvidenceRequestDetailPage").then((module) => ({
    default: module.EvidenceRequestDetailPage,
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
const PoliciesPage = lazy(() => import("./features/policies/pages/PoliciesPage").then((module) => ({ default: module.PoliciesPage })));
const PolicyDetailPage = lazy(() => import("./features/policies/pages/PolicyDetailPage").then((module) => ({ default: module.PolicyDetailPage })));

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
          path="/submissions/:submissionId/quotes"
          element={
            <ProtectedRoute allowedRoles={roleGroups.customerWork}>
              <QuoteHistoryPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/submissions/:submissionId/quotes/:quoteId"
          element={
            <ProtectedRoute allowedRoles={roleGroups.customerWork}>
              <QuoteDetailPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/evidence-requests/:evidenceRequestId"
          element={
            <ProtectedRoute allowedRoles={roleGroups.customerWork}>
              <EvidenceRequestDetailPage />
            </ProtectedRoute>
          }
        />
        <Route path="/evidence" element={<Navigate to="/evidence-requests" replace />} />
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
          path="/policies"
          element={<ProtectedRoute allowedRoles={roleGroups.policyWork}><PoliciesPage /></ProtectedRoute>}
        />
        <Route
          path="/policies/:policyId"
          element={<ProtectedRoute allowedRoles={roleGroups.policyWork}><PolicyDetailPage /></ProtectedRoute>}
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
