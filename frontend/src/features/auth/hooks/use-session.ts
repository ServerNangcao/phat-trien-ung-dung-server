import { useEffect, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { apiClient, isDemoMode } from "../../../api/client";
import type { AuthResponse, ForgotPasswordResponse, ResetPasswordResponse, UserProfileResponse, UserSession } from "../../../api/types";

function resolveRole(roles: string[]): UserSession["role"] {
  const rolesLower = roles.map((r) => r.toLowerCase());
  if (rolesLower.includes("admin")) return "admin";
  if (rolesLower.includes("organizer")) return "organizer";
  if (rolesLower.includes("staff")) return "staff";
  return "customer";
}

function readStoredUser(): UserSession | null {
  try {
    const saved = localStorage.getItem("ticketing.user");
    return saved ? (JSON.parse(saved) as UserSession) : null;
  } catch {
    localStorage.removeItem("ticketing.user");
    return null;
  }
}

export function useSession() {
  const queryClient = useQueryClient();
  const [user, setUser] = useState<UserSession | null>(readStoredUser);

  useEffect(() => {
    const clearSession = () => {
      localStorage.removeItem("ticketing.user");
      setUser(null);
      queryClient.clear();
    };
    window.addEventListener("ticketing:session-expired", clearSession);

    const syncSession = () => {
      setUser(readStoredUser());
    };
    window.addEventListener("ticketing:session-updated", syncSession);

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

    return () => {
      window.removeEventListener("ticketing:session-expired", clearSession);
      window.removeEventListener("ticketing:session-updated", syncSession);
    };
  }, [queryClient]);

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
        email: email.trim() || `${role}@ticketing.demo`,
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
      const demoUser: UserSession = { name, role: "customer", email: email.trim() };
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

  const forgotPassword = async (email: string): Promise<ForgotPasswordResponse> => {
    if (isDemoMode) {
      const mockOtp = Math.floor(100000 + Math.random() * 900000).toString();
      localStorage.setItem(`ticketing.demo.resetToken:${email.toLowerCase()}`, mockOtp);
      return {
        message: "Mã OTP đặt lại mật khẩu đã được tạo và gửi đến email của bạn.",
        resetToken: mockOtp,
        cooldownSeconds: 60,
      };
    }

    const { data } = await apiClient.post<ForgotPasswordResponse>("/api/identity/forgot-password", {
      email,
    });
    return data;
  };

  const resetPassword = async (
    email: string,
    token: string,
    newPassword: string,
  ): Promise<ResetPasswordResponse> => {
    if (isDemoMode) {
      const storedToken = localStorage.getItem(`ticketing.demo.resetToken:${email.toLowerCase()}`);
      if (storedToken && storedToken !== token.trim()) {
        throw new Error("Mã xác thực không chính xác hoặc đã hết hạn.");
      }
      localStorage.removeItem(`ticketing.demo.resetToken:${email.toLowerCase()}`);
      return {
        message: "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.",
      };
    }

    const { data } = await apiClient.post<ResetPasswordResponse>("/api/identity/reset-password", {
      email,
      token,
      newPassword,
    });
    return data;
  };

  const logout = () => {
    localStorage.removeItem("ticketing.user");
    localStorage.removeItem("ticketing.accessToken");
    localStorage.removeItem("ticketing.refreshToken");
    setUser(null);
    queryClient.clear();
  };

  return { user, login, register, logout, forgotPassword, resetPassword };
}
