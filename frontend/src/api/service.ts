import { apiClient, isDemoMode } from "./client";
import { demoApi } from "./demo";
import { platformDemoApi } from "./platformDemo";
import { mapApiTicketTypesToTiers, type RawApiTicketType } from "../features/events/utils/ticket-stock";
import type {
  AdminUserItem,
  AuthResponse,
  CategoryDto,
  CheckInStatus,
  CheckoutInput,
  CreateOrganizerEventInput,
  CreateTicketTypeInput,
  EventItem,
  LoginRequest,
  ManagedEvent,
  ManagedEventStatus,
  ManagedTicketTier,
  MyRefundRequestItem,
  OrganizerOrderItem,
  PurchasedTicket,
  RefundRequestItem,
  RegisterRequest,
  Reservation,
  TicketTier,
  UserProfileResponse,
  UserRole,
  VenueDto,
} from "./types";

const DEFAULT_IMAGES = [
  "https://cdn.sukienbanve.id.vn/midnight-jazz.jpg",
  "https://cdn.sukienbanve.id.vn/the-sound-of-spring.jpg",
  "https://cdn.sukienbanve.id.vn/creative-morning.jpg",
  "https://cdn.sukienbanve.id.vn/ceramic-sunday.jpg",
];

const EVENT_STATUS_MAP: Record<string, ManagedEventStatus> = {
  OnSale: "Published",
  PendingApproval: "Pending",
  Draft: "Draft",
  Rejected: "Rejected",
  Ended: "Ended",
  Cancelled: "Cancelled",
  Published: "Published",
  Pending: "Pending",
};

function normalizeEventStatus(status?: string | null): ManagedEventStatus | undefined {
  if (!status) return undefined;
  return EVENT_STATUS_MAP[status] ?? (status as ManagedEventStatus);
}

async function fetchEventTicketTiers(eventId: string): Promise<ManagedTicketTier[]> {
  try {
    const { data: rawTiers } = await apiClient.get<
      Array<{
        id: string;
        name: string;
        price: number;
        capacity: number;
        quantityAvailable: number;
        maxPerOrder: number;
        soldQuantity?: number;
        reservedQuantity?: number;
      }>
    >(`/api/events/${eventId}/ticket-types`);

    if (!Array.isArray(rawTiers)) return [];

    return rawTiers.map((t) => ({
      id: t.id,
      name: t.name,
      description: `Tối đa ${t.maxPerOrder} vé/đơn`,
      price: Number(t.price ?? 0),
      total: Number(t.capacity ?? 0),
      sold: Number(t.soldQuantity ?? 0),
      reserved: Number(t.reservedQuantity ?? 0),
      available: Number(t.quantityAvailable ?? 0),
      maxPerOrder: Number(t.maxPerOrder ?? 4),
    }));
  } catch {
    return [];
  }
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function normalizeEvent(raw: any, options?: { useFallbackTiers?: boolean }): EventItem {
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
  const useFallbackTiers = options?.useFallbackTiers ?? true;

  const existingTiers = Array.isArray(raw.tiers) ? raw.tiers : [];
  const tiers: TicketTier[] =
    existingTiers.length > 0 || !useFallbackTiers
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
    rawId: raw.id ? String(raw.id) : undefined,
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
    status: normalizeEventStatus(raw.status),
    rejectReason: raw.rejectReason ?? undefined,
    ticketTypesLoadError: Boolean(raw.ticketTypesLoadError),
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
    updateProfile: async (input: { fullName: string; phoneNumber?: string }) => {
      const { data } = await apiClient.put<UserProfileResponse>("/api/identity/me", input);
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
    if (filters.category && filters.category !== "Tất cả") params.category = filters.category;
    if (filters.location && filters.location !== "Tất cả địa điểm") params.city = filters.location;
    if (filters.maxPrice) params.maxPrice = filters.maxPrice;

    try {
      const { data } = await apiClient.get<{ items?: unknown[]; totalCount?: number; total?: number }>("/api/events", { params });
      const rawItems = Array.isArray(data?.items) ? data.items : [];
      const items = rawItems.map((item) => normalizeEvent(item));
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
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const { data } = await apiClient.get<any>(url);
      if (data?.id) {
        try {
          const { data: ticketTypes } = await apiClient.get<RawApiTicketType[]>(
            `/api/events/${data.id}/ticket-types`,
          );
          data.tiers = Array.isArray(ticketTypes) ? mapApiTicketTypesToTiers(ticketTypes) : [];
        } catch (ticketErr) {
          console.warn(`[TicketingApi] Không thể tải hạng vé cho sự kiện "${data.id}":`, ticketErr);
          data.tiers = [];
          data.ticketTypesLoadError = true;
        }
      }
      return normalizeEvent(data, { useFallbackTiers: false });
    } catch (error) {
      console.warn(`[TicketingApi] Không tìm thấy sự kiện "${id}" trên Backend, dùng dữ liệu Demo:`, error);
      return demoApi.event(id);
    }
  },
  reserve: async (eventId: string, tierId: string, quantity: number): Promise<Reservation> => {
    if (isDemoMode) return demoApi.reserve(eventId, tierId, quantity);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const { data } = await apiClient.post<any>("/api/ticketing/reservations", { eventId, ticketTypeId: tierId, quantity });
    const expiresAtMs = typeof data.expiresAt === "number"
      ? data.expiresAt
      : new Date(data.expiresAt).getTime();
    return {
      id: data.id,
      eventId: data.eventId,
      tierId: data.tierId || data.ticketTypeId,
      quantity: data.quantity,
      expiresAt: expiresAtMs,
    };
  },
  checkout: async (input: CheckoutInput) => {
    if (isDemoMode) return demoApi.checkout(input);
    const { data } = await apiClient.post<{ paymentUrl: string }>("/api/orders", input);
    return data;
  },
  tickets: async (): Promise<PurchasedTicket[]> => {
    if (isDemoMode) return demoApi.tickets();
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const { data } = await apiClient.get<any[]>("/api/tickets/mine");
    const rawList = Array.isArray(data) ? data : [];
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return rawList.map((t: any) => ({
      id: t.id,
      orderId: t.orderId,
      eventId: t.eventId,
      eventTitle: t.eventTitle || "Vé tham gia sự kiện",
      image: t.image || "https://cdn.sukienbanve.id.vn/creative-morning.jpg",
      venue: t.venue || "TP. Hồ Chí Minh",
      date: t.date || new Date(t.createdAt || Date.now()).toISOString().slice(0, 10),
      tierName: t.ticketTypeName || t.tierName || "Vé tiêu chuẩn",
      holderName: t.holderName || t.customerEmail || "Khách Hàng",
      code: t.code || t.qrSignature || t.qrPayload || t.ticketCode || t.id,
      status: ((t.status || "valid").toLowerCase() === "used" ? "used" : (t.status || "valid").toLowerCase() === "cancelled" ? "cancelled" : "valid") as "valid" | "used" | "cancelled",
    }));
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
  getAdminUsers: async (): Promise<AdminUserItem[]> => {
    if (isDemoMode) return platformDemoApi.getUsers();
    try {
      const { data } = await apiClient.get<
        Array<{
          id: string;
          email: string;
          fullName: string;
          phoneNumber?: string | null;
          isActive: boolean;
          createdAt: string;
          roles: string[];
        }>
      >("/api/identity/users");

      return data.map((u) => {
        const rolesLower = (u.roles || []).map((r) => r.toLowerCase());
        let role: UserRole = "customer";
        if (rolesLower.includes("admin")) role = "admin";
        else if (rolesLower.includes("organizer")) role = "organizer";
        else if (rolesLower.includes("staff")) role = "staff";

        const d = new Date(u.createdAt);
        const formattedDate = !isNaN(d.getTime())
          ? d.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" })
          : u.createdAt;

        return {
          id: u.id,
          name: u.fullName || u.email,
          email: u.email,
          role,
          status: u.isActive ? "Active" : "Locked",
          createdAt: formattedDate,
        };
      });
    } catch (error) {
      console.warn("[TicketingApi] Không thể lấy danh sách người dùng từ Backend, dùng fallback Demo:", error);
      return platformDemoApi.getUsers();
    }
  },
  updateUserRole: async (userId: string, role: UserRole): Promise<void> => {
    if (isDemoMode) {
      platformDemoApi.updateUserRole(userId, role);
      return;
    }
    const capitalizedRole = role.charAt(0).toUpperCase() + role.slice(1);
    await apiClient.put(`/api/identity/users/${userId}/role`, { role: capitalizedRole });
  },
  updateUserStatus: async (userId: string, isActive: boolean): Promise<void> => {
    if (isDemoMode) {
      platformDemoApi.toggleUserStatus(userId);
      return;
    }
    await apiClient.put(`/api/identity/users/${userId}/status`, { isActive });
  },
  organizer: {
    getCategories: async (): Promise<CategoryDto[]> => {
      if (isDemoMode) {
        return [
          { id: "cat-music", name: "Âm nhạc", slug: "am-nhac" },
          { id: "cat-art", name: "Nghệ thuật", slug: "nghe-thuat" },
          { id: "cat-workshop", name: "Hội thảo", slug: "hoi-thao" },
          { id: "cat-food", name: "Ẩm thực", slug: "am-thuc" },
          { id: "cat-sport", name: "Thể thao", slug: "the-thao" },
        ];
      }
      const { data } = await apiClient.get<CategoryDto[]>("/api/categories");
      return Array.isArray(data) ? data : [];
    },
    getVenues: async (city?: string): Promise<VenueDto[]> => {
      if (isDemoMode) return [];
      const { data } = await apiClient.get<VenueDto[]>("/api/venues", {
        params: city ? { city } : undefined,
      });
      return Array.isArray(data) ? data : [];
    },
    createVenue: async (input: { name: string; address: string; city: string; capacity?: number }): Promise<string> => {
      const { data } = await apiClient.post<string>("/api/venues", input);
      return data;
    },
    getMyEvents: async (): Promise<ManagedEvent[]> => {
      if (isDemoMode) return platformDemoApi.getManagedEvents();
      const { data } = await apiClient.get<{
        items?: Array<{
          id: string;
          title: string;
          slug: string;
          startsAt: string;
          endsAt: string;
          status: string;
          rejectReason?: string | null;
          categoryName: string;
          venueName: string;
          venueCity: string;
          venueCapacity?: number | null;
          description?: string | null;
        }>;
      }>("/api/events/mine", { params: { page: 1, pageSize: 100 } });

      const rawItems = Array.isArray(data?.items) ? data.items : [];
      const enriched = await Promise.all(
        rawItems.map(async (ev): Promise<ManagedEvent> => {
          const tiers = await fetchEventTicketTiers(ev.id);

          const startsAtDate = new Date(ev.startsAt);
          const formattedDate = !isNaN(startsAtDate.getTime())
            ? `${startsAtDate.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" })} · ${startsAtDate.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit", hour12: false })}`
            : ev.startsAt;

          return {
            id: ev.id,
            slug: ev.slug,
            title: ev.title,
            category: ev.categoryName || "Âm nhạc",
            city: ev.venueCity || "",
            venue: ev.venueName || "",
            venueCapacity: ev.venueCapacity ?? null,
            date: formattedDate,
            startsAt: ev.startsAt,
            endsAt: ev.endsAt,
            organizer: "Ban Tổ Chức",
            verified: true,
            status: normalizeEventStatus(ev.status) ?? "Draft",
            rejectReason: ev.rejectReason ?? undefined,
            description: ev.description || "",
            schedule: [],
            tiers,
          };
        }),
      );

      return enriched;
    },
    createEvent: async (input: CreateOrganizerEventInput): Promise<string> => {
      const { data } = await apiClient.post<string>("/api/events", input);
      return data;
    },
    uploadCoverImage: async (eventId: string, file: File): Promise<void> => {
      const { data: presign } = await apiClient.post<{
        uploadUrl: string;
        objectKey: string;
      }>(`/api/events/${eventId}/cover-image/presign`, {
        contentType: file.type,
      });

      const uploadResponse = await fetch(presign.uploadUrl, {
        method: "PUT",
        headers: { "Content-Type": file.type },
        body: file,
      });

      if (!uploadResponse.ok) {
        throw new Error("Không thể tải ảnh lên máy chủ lưu trữ.");
      }

      await apiClient.post(`/api/events/${eventId}/cover-image/confirm`, {
        objectKey: presign.objectKey,
      });
    },
    submitEvent: async (eventId: string): Promise<void> => {
      await apiClient.post(`/api/events/${eventId}/submit`);
    },
    createTicketType: async (eventId: string, input: CreateTicketTypeInput): Promise<void> => {
      await apiClient.post(`/api/events/${eventId}/ticket-types`, input);
    },
    getOrders: async (params?: { eventId?: string; status?: string; search?: string }): Promise<OrganizerOrderItem[]> => {
      if (isDemoMode) return platformDemoApi.getOrders();
      const { data } = await apiClient.get<OrganizerOrderItem[]>("/api/orders/organizer", { params });
      return Array.isArray(data) ? data : [];
    },
  },
  adminEvents: {
    list: async (status?: string): Promise<ManagedEvent[]> => {
      if (isDemoMode) return platformDemoApi.getManagedEvents();
      const { data } = await apiClient.get<{

        items?: Array<{
          id: string;
          title: string;
          slug: string;
          startsAt: string;
          endsAt: string;
          status: string;
          rejectReason?: string | null;
          categoryName: string;
          venueName: string;
          venueCity: string;
          venueCapacity?: number | null;
          description?: string | null;
        }>;
      }>("/api/events/admin", {
        params: { page: 1, pageSize: 100, ...(status ? { status } : {}) },
      });

      const rawItems = Array.isArray(data?.items) ? data.items : [];

      return Promise.all(
        rawItems.map(async (ev): Promise<ManagedEvent> => {
          const tiers = await fetchEventTicketTiers(ev.id);
          const startsAtDate = new Date(ev.startsAt);
          const formattedDate = !isNaN(startsAtDate.getTime())
            ? `${startsAtDate.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" })} · ${startsAtDate.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit", hour12: false })}`
            : ev.startsAt;

          return {
            id: ev.id,
            slug: ev.slug,
            title: ev.title,
            category: ev.categoryName || "Âm nhạc",
            city: ev.venueCity || "",
            venue: ev.venueName || "",
            venueCapacity: ev.venueCapacity ?? null,
            date: formattedDate,
            startsAt: ev.startsAt,
            endsAt: ev.endsAt,
            organizer: "Ban Tổ Chức",
            verified: true,
            status: normalizeEventStatus(ev.status) ?? "Draft",
            rejectReason: ev.rejectReason ?? undefined,
            description: ev.description || "",
            schedule: [],
            tiers,
          };
        }),
      );
    },
    approve: async (eventId: string): Promise<void> => {
      if (isDemoMode) {
        platformDemoApi.moderateEvent(eventId, "Published");
        return;
      }
      await apiClient.post(`/api/events/${eventId}/approve`);
    },
    reject: async (eventId: string, reason: string): Promise<void> => {
      if (isDemoMode) {
        platformDemoApi.moderateEvent(eventId, "Rejected", reason);
        return;
      }
      await apiClient.post(`/api/events/${eventId}/reject`, { reason });
    },
    cancel: async (eventId: string, reason?: string): Promise<void> => {
      if (isDemoMode) {
        platformDemoApi.moderateEvent(eventId, "Cancelled", reason);
        return;
      }
      await apiClient.post(`/api/events/${eventId}/cancel`, reason ? { reason } : {});
    },
  },
  refunds: {
    create: async (orderId: string, reason: string): Promise<MyRefundRequestItem> => {
      if (isDemoMode) {
        const created = platformDemoApi.createRefund(orderId, reason);
        return {
          id: created.id,
          orderId,
          orderCode: created.orderCode,
          amount: created.amount,
          reason: created.reason,
          status: created.status,
          requestedAt: created.requestedAt,
        };
      }
      const { data } = await apiClient.post<MyRefundRequestItem>("/api/refunds", {
        orderId,
        reason,
      });
      return data;
    },
    getMine: async (): Promise<MyRefundRequestItem[]> => {
      if (isDemoMode) {
        return platformDemoApi
          .getRefunds()
          .filter((r) => Boolean(r.orderId))
          .map((r) => ({
            id: r.id,
            orderId: r.orderId!,
            orderCode: r.orderCode,
            amount: r.amount,
            reason: r.reason,
            status: r.status,
            requestedAt: r.requestedAt,
          }));
      }
      const { data } = await apiClient.get<MyRefundRequestItem[]>("/api/refunds/mine");
      return Array.isArray(data) ? data : [];
    },
    getAdminList: async (status?: string): Promise<RefundRequestItem[]> => {
      if (isDemoMode) {
        const list = platformDemoApi.getRefunds();
        if (!status || status === "ALL") return list;
        return list.filter((item) => item.status === status);
      }
      const { data } = await apiClient.get<RefundRequestItem[]>("/api/refunds/admin", {
        params: status && status !== "ALL" ? { status } : undefined,
      });
      return Array.isArray(data) ? data : [];
    },
    approve: async (id: string): Promise<void> => {
      if (isDemoMode) {
        platformDemoApi.decideRefund(id, "Approved");
        return;
      }
      await apiClient.post(`/api/refunds/${id}/approve`);
    },
    reject: async (id: string): Promise<void> => {
      if (isDemoMode) {
        platformDemoApi.decideRefund(id, "Rejected");
        return;
      }
      await apiClient.post(`/api/refunds/${id}/reject`);
    },
  },
};
