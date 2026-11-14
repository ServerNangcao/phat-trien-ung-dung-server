import { useEffect, useState } from "react";
import { apiClient, isDemoMode } from "../../../api/client";
import type { AuthResponse, UserSession } from "../../../api/types";

export function useSession() {
  const [user, setUser] = useState<UserSession | null>(() => {
    const saved = localStorage.getItem("ticketing.user");
    return saved ? (JSON.parse(saved) as UserSession) : null;
  });

  useEffect(() => {
    const clearSession = () => {
      localStorage.removeItem("ticketing.user");
      setUser(null);
    };
    window.addEventListener("ticketing:session-expired", clearSession);
    return () => window.removeEventListener("ticketing:session-expired", clearSession);
  }, []);

  const login = async (role: "customer" | "staff", email: string, password: string) => {
    if (isDemoMode) {
      const demoUser: UserSession = {
        name: role === "staff" ? "Minh Anh · Soát vé" : "Linh Nguyễn",
        role,
      };
      localStorage.setItem("ticketing.user", JSON.stringify(demoUser));
      setUser(demoUser);
      return;
    }

    const { data } = await apiClient.post<AuthResponse>("/api/identity/login", { email, password });
    localStorage.setItem("ticketing.accessToken", data.accessToken);
    localStorage.setItem("ticketing.refreshToken", data.refreshToken);

    const isStaff = data.roles.some((r) => r.toLowerCase() === "staff" || r.toLowerCase() === "admin");
    const session: UserSession = {
      name: data.fullName,
      role: isStaff ? "staff" : "customer",
      email: data.email,
      userId: data.userId,
    };
    localStorage.setItem("ticketing.user", JSON.stringify(session));
    setUser(session);
  };

  const register = async (name: string, email: string, password: string) => {
    if (isDemoMode) {
      const accounts = JSON.parse(
        localStorage.getItem("ticketing.demo.accounts") ?? "[]",
      ) as Array<{ name: string; email: string }>;
      if (accounts.some((account) => account.email.toLocaleLowerCase() === email.toLocaleLowerCase())) {
        throw new Error("Email này đã được đăng ký.");
      }
      const demoUser: UserSession = { name, role: "customer" };
      localStorage.setItem("ticketing.demo.accounts", JSON.stringify([...accounts, { name, email }]));
      localStorage.setItem("ticketing.user", JSON.stringify(demoUser));
      setUser(demoUser);
      return;
    }

    const { data } = await apiClient.post<AuthResponse>("/api/identity/register", {
      fullName: name,
      email,
      password,
    });
    localStorage.setItem("ticketing.accessToken", data.accessToken);
    localStorage.setItem("ticketing.refreshToken", data.refreshToken);

    const session: UserSession = {
      name: data.fullName,
      role: "customer",
      email: data.email,
      userId: data.userId,
    };
    localStorage.setItem("ticketing.user", JSON.stringify(session));
    setUser(session);
  };

  const logout = () => {
    localStorage.removeItem("ticketing.user");
    localStorage.removeItem("ticketing.accessToken");
    localStorage.removeItem("ticketing.refreshToken");
    setUser(null);
  };

  return { user, login, register, logout };
}
