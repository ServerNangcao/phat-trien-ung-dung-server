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
  TicketTier,
  UserProfileResponse,
} from "./types";

const DEFAULT_IMAGES = [
  "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?auto=format&fit=crop&w=1400&q=85",
  "https://images.unsplash.com/photo-1459749411175-04bf5292ceea?auto=format&fit=crop&w=900&q=80",
  "https://images.unsplash.com/photo-1540575467063-178a50c2df87?auto=format&fit=crop&w=900&q=80",
  "https://images.unsplash.com/photo-1565193566173-7a0ee3dbe261?auto=format&fit=crop&w=900&q=80",
];

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function normalizeEvent(raw: any): EventItem {
  if (!raw) return raw;

  const startsAt = raw.startsAt ? new Date(raw.startsAt) : null;
  const hasValidDate = startsAt && !isNaN(startsAt.getTime());

  const date = hasValidDate
    ? startsAt.toISOString().slice(0, 10)
    : (raw.date || new Date().toISOString().slice(0, 10));

  const time = hasValidDate
    ? startsAt.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit", hour12: false })
    : (raw.time || "19:00");

  const minPrice = raw.minPrice ?? 0;
  const maxPrice = raw.maxPrice ?? minPrice;

  const existingTiers = Array.isArray(raw.tiers) ? raw.tiers : [];
  const tiers: TicketTier[] =
    existingTiers.length > 0
      ? existingTiers
      : [
          {
            id: "standard",
            name: "Vé tiêu chuẩn",
            price: minPrice || 250000,
            remaining: 100,
            description: "Khu vực tiêu chuẩn, tự do vị trí",
          },
          ...(maxPrice > minPrice
            ? [
                {
                  id: "vip",
                  name: "Vé VIP",
                  price: maxPrice,
                  remaining: 30,
                  description: "Khu vực ưu tiên đặc biệt, góc nhìn đẹp",
                },
              ]
            : []),
        ];

  const seed = (raw.id || raw.slug || "").toString();
  const hash = seed.split("").reduce((acc: number, char: string) => acc + char.charCodeAt(0), 0);
  const fallbackImage = DEFAULT_IMAGES[Math.abs(hash) % DEFAULT_IMAGES.length];

  return {
    id: String(raw.slug || raw.id || ""),
    title: raw.title || "Sự kiện",
    category: (raw.categoryName || raw.category?.name || raw.category || "Âm nhạc") as EventItem["category"],
    location: raw.venueCity || raw.venue?.city || raw.location || "TP. Hồ Chí Minh",
    venue: raw.venue?.name || raw.venueName || raw.venueCity || raw.venue || "Địa điểm cập nhật",
    date,
    time,
    image: raw.coverImageUrl || raw.coverImageKey || raw.image || fallbackImage,
    accent: raw.accent || "#173e42",
    description: raw.description || "Thông tin chi tiết về sự kiện sẽ được ban tổ chức cập nhật sớm nhất.",
    organizer: raw.organizer || "Ban Tổ Chức",
    featured: Boolean(raw.featured),
    tiers,
  };
}

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
    const params: Record<string, unknown> = {
      page: filters.page ?? 1,
      pageSize: filters.limit ?? 20,
    };
    if (filters.query) params.search = filters.query;
    if (filters.location && filters.location !== "Tất cả địa điểm") params.city = filters.location;
    if (filters.maxPrice) params.maxPrice = filters.maxPrice;

    try {
      const { data } = await apiClient.get<{ items?: unknown[]; totalCount?: number; total?: number }>("/api/events", { params });
      const rawItems = Array.isArray(data?.items) ? data.items : [];
      const items = rawItems.map(normalizeEvent);
      return {
        items,
        total: data?.totalCount ?? data?.total ?? items.length,
      };
    } catch (error) {
      console.warn("[TicketingApi] Kết nối Backend thất bại, chuyển tạm sang dữ liệu Demo:", error);
      return demoApi.events(filters);
    }
  },
  event: async (id: string) => {
    if (isDemoMode) return demoApi.event(id);
    const isGuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id);
    const url = isGuid ? `/api/events/${id}` : `/api/events/slug/${id}`;
    try {
      const { data } = await apiClient.get<unknown>(url);
      return normalizeEvent(data);
    } catch (error) {
      console.warn(`[TicketingApi] Không tìm thấy sự kiện "${id}" trên Backend, dùng dữ liệu Demo:`, error);
      return demoApi.event(id);
    }
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
