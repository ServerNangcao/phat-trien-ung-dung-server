import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import type { UserSession } from "../../../api/types";

interface RequireRoleProps {
  role: "customer" | "staff" | "organizer" | "admin";
  user: UserSession | null;
  children: ReactNode;
}

export function RequireRole({ role, user, children }: RequireRoleProps) {
  const location = useLocation();
  if (!user || user.role !== role) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }
  return <>{children}</>;
}

export function RequireAuth({ user, children }: { user: UserSession | null; children: ReactNode }) {
  const location = useLocation();
  if (!user) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }
  return <>{children}</>;
}
