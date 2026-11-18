import { Navigate, Route, Routes } from "react-router-dom";
import { ErrorBoundary } from "../components/common/ErrorBoundary";
import { SiteLayout } from "../layouts/SiteLayout";
import { useSession } from "../features/auth/hooks/use-session";
import { RequireRole } from "../features/auth/components/RequireRole";
import { LoginPage } from "../features/auth/pages/LoginPage";
import { DiscoverPage } from "../features/events/pages/DiscoverPage";
import { EventDetailPage } from "../features/events/pages/EventDetailPage";
import { CheckoutPage } from "../features/checkout/pages/CheckoutPage";
import { PaymentReturnPage } from "../features/checkout/pages/PaymentReturnPage";
import { MyTicketsPage } from "../features/my-tickets/pages/MyTicketsPage";
import { StaffCheckInPage } from "../features/checkin/pages/StaffCheckInPage";

function App() {
  const session = useSession();

  return (
    <ErrorBoundary>
      <div className="min-h-screen bg-paper text-ink">
        <Routes>
          <Route element={<SiteLayout user={session.user} logout={session.logout} />}>
            <Route path="/" element={<DiscoverPage />} />
            <Route path="/events/:id" element={<EventDetailPage />} />
            <Route path="/checkout/:id" element={<CheckoutPage />} />
            <Route path="/payment/return" element={<PaymentReturnPage />} />
            <Route path="/my-tickets" element={<MyTicketsPage />} />
            <Route
              path="/login"
              element={<LoginPage login={session.login} register={session.register} />}
            />
          </Route>
          <Route
            path="/staff/check-in"
            element={
              <RequireRole role="staff" user={session.user}>
                <StaffCheckInPage user={session.user!} logout={session.logout} />
              </RequireRole>
            }
          />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </div>
    </ErrorBoundary>
  );
}

export default App;
