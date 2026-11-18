import type { ReactNode } from "react";
import { Navigate } from "react-router-dom";
import type { UserSession } from "../../../api/types";

interface RequireRoleProps {
  role: "customer" | "staff" | "organizer" | "admin";
  user: UserSession | null;
  children: ReactNode;
}

export function RequireRole({ role, user, children }: RequireRoleProps) {
  if (!user || user.role !== role) {
    return <Navigate to="/login" replace />;
  }
  return <>{children}</>;
}
