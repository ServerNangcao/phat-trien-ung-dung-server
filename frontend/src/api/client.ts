import axios from "axios";

export const isDemoMode = import.meta.env.VITE_DATA_MODE === "demo";
export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? "http://localhost:8080",
  timeout: 12_000,
  headers: { "Content-Type": "application/json" },
});

apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem("ticketing.accessToken");
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

let refreshRequest: Promise<string | null> | null = null;
apiClient.interceptors.response.use(
  (response) => response,
  async (error: unknown) => {
    if (!axios.isAxiosError(error) || !error.config || error.response?.status !== 401) {
      return Promise.reject(error);
    }

    const request = error.config;
    if (request.url?.includes("/auth/refresh") || request.headers["x-refresh-retried"]) {
      localStorage.removeItem("ticketing.accessToken");
      localStorage.removeItem("ticketing.refreshToken");
      window.dispatchEvent(new Event("ticketing:session-expired"));
      return Promise.reject(error);
    }

    refreshRequest ??= apiClient
      .post<{ accessToken: string; refreshToken?: string }>("/api/auth/refresh", {
        refreshToken: localStorage.getItem("ticketing.refreshToken"),
      })
      .then(({ data }) => {
        localStorage.setItem("ticketing.accessToken", data.accessToken);
        if (data.refreshToken) localStorage.setItem("ticketing.refreshToken", data.refreshToken);
        return data.accessToken;
      })
      .catch(() => {
        localStorage.removeItem("ticketing.accessToken");
        localStorage.removeItem("ticketing.refreshToken");
        window.dispatchEvent(new Event("ticketing:session-expired"));
        return null;
      })
      .finally(() => {
        refreshRequest = null;
      });

    const accessToken = await refreshRequest;
    if (!accessToken) return Promise.reject(error);
    request.headers.Authorization = `Bearer ${accessToken}`;
    request.headers["x-refresh-retried"] = "true";
    return apiClient(request);
  },
);

export function isConflictError(error: unknown): boolean {
  return axios.isAxiosError(error) && error.response?.status === 409;
}

export function getApiErrorMessage(
  error: unknown,
  fallback: string,
  conflictFallback?: string,
): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as
      | { detail?: string; message?: string; error?: string; title?: string }
      | undefined;
    const serverMsg = data?.detail || data?.message || data?.error;
    if (error.response?.status === 409) {
      return serverMsg || conflictFallback || "Dữ liệu bị xung đột (409). Vui lòng tải lại và thử lại.";
    }
    if (serverMsg) return serverMsg;
  }
  if (error instanceof Error && !error.message.startsWith("Request failed with status code")) {
    return error.message;
  }
  return fallback;
}
