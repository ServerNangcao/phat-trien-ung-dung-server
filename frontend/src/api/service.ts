import { apiClient, isDemoMode } from "./client";
import { demoApi } from "./demo";
import type {
  AuthResponse,
  CheckInStatus,
  CheckoutInput,
  EventItem,
  LoginRequest,
  PurchasedTicket,
  RegisterRequest,
  Reservation,
  UserProfileResponse,
} from "./types";

export const ticketingApi = {
  auth: {
    login: async (input: LoginRequest) => {
      const { data } = await apiClient.post<AuthResponse>("/api/identity/login", input);
      return data;
    },
    register: async (input: RegisterRequest) => {
      const { data } = await apiClient.post<AuthResponse>("/api/identity/register", input);
      return data;
    },
    profile: async () => {
      const { data } = await apiClient.get<UserProfileResponse>("/api/identity/me");
      return data;
    },
    changePassword: async (currentPassword: string, newPassword: string) => {
      await apiClient.post("/api/identity/change-password", { currentPassword, newPassword });
    },
  },
  events: async (filters: { query?: string; category?: string; location?: string; maxPrice?: number; page?: number; limit?: number }) => {
    if (isDemoMode) return demoApi.events(filters);
    const { data } = await apiClient.get<{ items: EventItem[]; total: number }>("/api/events", { params: filters });
    return data;
  },
  event: async (id: string) => {
    if (isDemoMode) return demoApi.event(id);
    const { data } = await apiClient.get<EventItem>(`/api/events/${id}`);
    return data;
  },
  reserve: async (eventId: string, tierId: string, quantity: number) => {
    if (isDemoMode) return demoApi.reserve(eventId, tierId, quantity);
    const { data } = await apiClient.post<Reservation>("/api/ticketing/reservations", { eventId, ticketTypeId: tierId, quantity });
    return data;
  },
  checkout: async (input: CheckoutInput) => {
    if (isDemoMode) return demoApi.checkout(input);
    const { data } = await apiClient.post<{ paymentUrl: string }>("/api/orders", input);
    return data;
  },
  tickets: async () => {
    if (isDemoMode) return demoApi.tickets();
    const { data } = await apiClient.get<PurchasedTicket[]>("/api/tickets/mine");
    return data;
  },
  checkIn: async (code: string) => {
    if (isDemoMode) return demoApi.checkIn(code);
    const { data } = await apiClient.post<{ status: CheckInStatus; count: number }>("/api/check-in/scan", { code });
    return data;
  },
  checkInCount: async () => {
    if (isDemoMode) return demoApi.checkInCount();
    const { data } = await apiClient.get<{ count: number }>("/api/check-in/count");
    return data.count;
  },
};
