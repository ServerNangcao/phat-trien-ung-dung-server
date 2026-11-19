import { useEffect, useState } from "react";
import { apiClient, isDemoMode } from "../../../api/client";
import type { AuthResponse, UserProfileResponse, UserSession } from "../../../api/types";

function resolveRole(roles: string[]): UserSession["role"] {
  const rolesLower = roles.map((r) => r.toLowerCase());
  if (rolesLower.includes("admin")) return "admin";
  if (rolesLower.includes("organizer")) return "organizer";
  if (rolesLower.includes("staff")) return "staff";
  return "customer";
}

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

    // Xác thực lại quyền thật với backend khi tải trang (tránh giả mạo localStorage)
    if (!isDemoMode && localStorage.getItem("ticketing.accessToken")) {
      apiClient
        .get<UserProfileResponse>("/api/identity/me")
        .then(({ data }) => {
          const synced: UserSession = {
            name: data.fullName,
            role: resolveRole(data.roles),
            email: data.email,
            userId: data.id,
          };
          localStorage.setItem("ticketing.user", JSON.stringify(synced));
          setUser(synced);
        })
        .catch(() => {
          // Interceptor sẽ tự xử lý refresh token hoặc bắn event session-expired nếu 401
        });
    }

    return () => window.removeEventListener("ticketing:session-expired", clearSession);
  }, []);

  const login = async (
    role: UserSession["role"],
    email: string,
    password: string,
  ): Promise<UserSession> => {
    if (isDemoMode) {
      const demoNames: Record<UserSession["role"], string> = {
        customer: "Linh Nguyễn",
        staff: "Minh Anh · Soát vé",
        organizer: "Bảo Long · Ban tổ chức",
        admin: "Thu Hà · Quản trị viên",
      };
      const demoUser: UserSession = {
        name: demoNames[role] ?? "Người dùng",
        role,
      };
      localStorage.setItem("ticketing.user", JSON.stringify(demoUser));
      setUser(demoUser);
      return demoUser;
    }

    const { data } = await apiClient.post<AuthResponse>("/api/identity/login", { email, password });
    localStorage.setItem("ticketing.accessToken", data.accessToken);
    localStorage.setItem("ticketing.refreshToken", data.refreshToken);

    const session: UserSession = {
      name: data.fullName,
      role: resolveRole(data.roles),
      email: data.email,
      userId: data.userId,
    };
    localStorage.setItem("ticketing.user", JSON.stringify(session));
    setUser(session);
    return session;
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
