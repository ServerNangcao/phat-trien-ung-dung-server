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
import { ProfilePage } from "../features/profile/ProfilePage";
import { OrganizerAnalyticsPage } from "../features/organizer/OrganizerAnalyticsPage";
import { OrganizerBuilderPage } from "../features/organizer/OrganizerBuilderPage";
import { OrganizerInventoryPage } from "../features/organizer/OrganizerInventoryPage";
import { OrganizerOrdersPage } from "../features/organizer/OrganizerOrdersPage";
import { AdminModerationPage } from "../features/admin/AdminModerationPage";
import { AdminRefundsPage } from "../features/admin/AdminRefundsPage";
import { AdminUsersPage } from "../features/admin/AdminUsersPage";

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
            <Route path="/profile" element={<ProfilePage />} />
            <Route path="/organizer/analytics" element={<OrganizerAnalyticsPage />} />
            <Route path="/organizer/builder" element={<OrganizerBuilderPage />} />
            <Route path="/organizer/inventory" element={<OrganizerInventoryPage />} />
            <Route path="/organizer/orders" element={<OrganizerOrdersPage />} />
            <Route path="/admin/moderation" element={<AdminModerationPage />} />
            <Route path="/admin/refunds" element={<AdminRefundsPage />} />
            <Route path="/admin/users" element={<AdminUsersPage />} />
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
