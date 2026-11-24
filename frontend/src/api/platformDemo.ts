import type {
  AdminUserItem,
  EventCategory,
  ManagedEvent,
  ManagedTicketTier,
  OrganizerOrderItem,
  RefundRequestItem,
  UserProfile,
  UserRole,
} from "./types";

const PROFILE_KEY = "ticketing.platform.profile";
const MANAGED_EVENTS_KEY = "ticketing.platform.managedEvents";
const ORDERS_KEY = "ticketing.platform.orders";
const REFUNDS_KEY = "ticketing.platform.refunds";
const USERS_KEY = "ticketing.platform.users";

const DEFAULT_PROFILE: UserProfile = {
  fullName: "Nguyễn Minh Anh",
  email: "minhanh.nguyen@email.com",
  phone: "0908 234 567",
  dob: "1998-05-14",
  gender: "Nữ",
  idNumber: "079198001234",
  city: "TP. Hồ Chí Minh",
  district: "TP. Thủ Đức",
  address: "Tòa nhà The Global City, Đường Đỗ Xuân Hợp, Phường An Phú",
  taxCompany: "CÔNG TY TNHH SÁNG TẠO NIGHTS & NOTES",
  taxCode: "0316889900",
  taxAddress: "12 Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh",
  taxEmail: "accounting@nightsandnotes.vn",
  twoFactorEnabled: true,
};

const DEFAULT_MANAGED_EVENTS: ManagedEvent[] = [
  {
    id: "midnight-jazz",
    title: "MIDNIGHT JAZZ — Chạm vào đêm",
    category: "Âm nhạc",
    city: "TP. Hồ Chí Minh",
    venue: "The Global City, TP. Thủ Đức",
    date: "12/12/2026 · 19:30",
    organizer: "Nights & Notes",
    verified: true,
    status: "Published",
    description:
      "Một đêm nhạc jazz ngoài trời nơi những giai điệu ngẫu hứng gặp ánh đèn thành phố.",
    schedule: [
      { time: "19:00", title: "Đón khách & Check-in", desc: "Quét mã QR tại cổng chính" },
      { time: "19:30", title: "Mở màn Acoustic", desc: "Phần trình diễn khởi động cùng ban nhạc" },
      { time: "21:00", title: "Đêm nhạc chính Midnight Jazz", desc: "Những bản phối ngẫu hứng" },
    ],
    tiers: [
      { id: "tt-1", name: "Standard", description: "Khu vực đứng, tầm nhìn sân khấu", price: 590000, total: 250, sold: 124, reserved: 8, maxPerOrder: 6 },
      { id: "tt-2", name: "Premium", description: "Khu vực gần sân khấu, 1 đồ uống", price: 990000, total: 100, sold: 58, reserved: 4, maxPerOrder: 4 },
      { id: "tt-3", name: "Backstage pass", description: "Lối vào riêng, gặp gỡ nghệ sĩ", price: 1590000, total: 50, sold: 32, reserved: 2, maxPerOrder: 2 },
    ],
  },
  {
    id: "saigon-indie-live",
    title: "Saigon Indie Live — Những ngày xanh",
    category: "Âm nhạc",
    city: "TP. Hồ Chí Minh",
    venue: "Vườn Bia, Bình Thạnh",
    date: "19/12/2026 · 18:30",
    organizer: "Saigon Indie Club",
    verified: true,
    status: "Published",
    description: "Đêm nhạc indie gần gũi dưới ánh đèn vàng.",
    schedule: [{ time: "18:30", title: "Khai mạc", desc: "Mở cổng đón khách" }],
    tiers: [
      { id: "tt-4", name: "General Admission", description: "Vé vào cổng tiêu chuẩn", price: 290000, total: 300, sold: 204, reserved: 6, maxPerOrder: 6 },
      { id: "tt-5", name: "Front Stage", description: "Khu vực gần sân khấu, gồm đồ uống", price: 550000, total: 80, sold: 52, reserved: 2, maxPerOrder: 4 },
    ],
  },
  {
    id: "autumn-symphony",
    title: "Đêm Nhạc Giao Hưởng Mùa Thu — Autumn Symphony",
    category: "Âm nhạc",
    city: "Hà Nội",
    venue: "Nhà hát Hồ Gươm, 40 Hàng Bài, Hoàn Kiếm",
    date: "26/12/2026 · 20:00",
    organizer: "Nights & Notes",
    verified: true,
    status: "Pending",
    description: "Đêm nhạc thính phòng kết hợp ánh sáng nghệ thuật trình diễn các tác phẩm kinh điển.",
    schedule: [
      { time: "19:00", title: "Đón khách tại sảnh chính", desc: "Thưởng thức tiệc trà nhẹ" },
      { time: "20:00", title: "Khúc dạo đầu Mùa Thu", desc: "Dàn nhạc giao hưởng trình diễn Chương I" },
    ],
    tiers: [
      { id: "tt-6", name: "Standard", description: "Hạng ghế tiêu chuẩn tầng 1", price: 690000, total: 400, sold: 0, reserved: 0, maxPerOrder: 5 },
      { id: "tt-7", name: "VIP Box", description: "Hạng ghế VIP tầm nhìn trung tâm", price: 1290000, total: 100, sold: 0, reserved: 0, maxPerOrder: 4 },
    ],
  },
  {
    id: "street-food-fest",
    title: "Saigon Street Food & Craft Beer Fest",
    category: "Ẩm thực",
    city: "TP. Hồ Chí Minh",
    venue: "Công viên Bờ Sông Sài Gòn, Thủ Đức",
    date: "02/01/2027 · 16:00",
    organizer: "Taste Vietnam",
    verified: false,
    status: "Pending",
    description: "Lễ hội ẩm thực đường phố và bia thủ công với hơn 40 gian hàng địa phương.",
    schedule: [{ time: "16:00", title: "Mở cửa lễ hội", desc: "Check-in & nhận voucher thử món" }],
    tiers: [
      { id: "tt-8", name: "Entry Pass", description: "Vé vào cổng + 2 voucher đồ uống", price: 250000, total: 500, sold: 0, reserved: 0, maxPerOrder: 6 },
    ],
  },
];

const DEFAULT_ORDERS: OrganizerOrderItem[] = [
  {
    orderCode: "ORD-2612-8829",
    eventId: "midnight-jazz",
    eventTitle: "MIDNIGHT JAZZ — Chạm vào đêm",
    buyerName: "Nguyễn Minh Anh",
    buyerEmail: "minhanh.nguyen@email.com",
    tierName: "Standard",
    quantity: 2,
    totalAmount: 1180000,
    paymentStatus: "Paid",
    checkInStatus: "CheckedIn",
    createdAt: "12/12/2026 19:14",
  },
  {
    orderCode: "ORD-2612-8820",
    eventId: "midnight-jazz",
    eventTitle: "MIDNIGHT JAZZ — Chạm vào đêm",
    buyerName: "Trần Hoàng Nam",
    buyerEmail: "nam.tran@company.vn",
    tierName: "Backstage pass",
    quantity: 1,
    totalAmount: 1590000,
    paymentStatus: "Paid",
    checkInStatus: "CheckedIn",
    createdAt: "12/12/2026 18:50",
  },
  {
    orderCode: "ORD-2612-8815",
    eventId: "midnight-jazz",
    eventTitle: "MIDNIGHT JAZZ — Chạm vào đêm",
    buyerName: "Lê Hoàng Châu",
    buyerEmail: "chau.le@design.io",
    tierName: "Premium",
    quantity: 2,
    totalAmount: 1980000,
    paymentStatus: "Paid",
    checkInStatus: "NotCheckedIn",
    createdAt: "11/12/2026 21:05",
  },
  {
    orderCode: "ORD-2612-8801",
    eventId: "saigon-indie-live",
    eventTitle: "Saigon Indie Live — Những ngày xanh",
    buyerName: "Phạm Quốc Bảo",
    buyerEmail: "bao.pham@gmail.com",
    tierName: "Front Stage",
    quantity: 2,
    totalAmount: 1100000,
    paymentStatus: "Pending",
    checkInStatus: "NotCheckedIn",
    createdAt: "11/12/2026 15:40",
  },
  {
    orderCode: "ORD-2612-8790",
    eventId: "midnight-jazz",
    eventTitle: "MIDNIGHT JAZZ — Chạm vào đêm",
    buyerName: "Võ Thảo Vy",
    buyerEmail: "vy.vo@studio.vn",
    tierName: "Standard",
    quantity: 1,
    totalAmount: 590000,
    paymentStatus: "Refunded",
    checkInStatus: "NotCheckedIn",
    createdAt: "10/12/2026 09:22",
  },
];

const DEFAULT_REFUNDS: RefundRequestItem[] = [
  {
    id: "rf-01",
    orderCode: "ORD-2612-8810",
    eventTitle: "MIDNIGHT JAZZ — Chạm vào đêm",
    buyerName: "Đặng Hữu Phước",
    buyerEmail: "phuoc.dang@email.com",
    amount: 990000,
    reason: "Thay đổi lịch công tác đột xuất, không thể tham dự đúng ngày.",
    requestedAt: "12/12/2026 10:15",
    status: "Pending",
  },
  {
    id: "rf-02",
    orderCode: "ORD-2612-8804",
    eventTitle: "Saigon Indie Live — Những ngày xanh",
    buyerName: "Huỳnh Ngọc Mai",
    buyerEmail: "mai.huynh@email.com",
    amount: 580000,
    reason: "Đặt nhầm số lượng vé (mua dư 2 vé Standard).",
    requestedAt: "11/12/2026 18:30",
    status: "Pending",
  },
  {
    id: "rf-03",
    orderCode: "ORD-2612-8790",
    eventTitle: "MIDNIGHT JAZZ — Chạm vào đêm",
    buyerName: "Võ Thảo Vy",
    buyerEmail: "vy.vo@studio.vn",
    amount: 590000,
    reason: "Thanh toán trùng 2 lần qua cổng VNPay.",
    requestedAt: "10/12/2026 09:25",
    status: "Approved",
  },
];

const DEFAULT_USERS: AdminUserItem[] = [
  { id: "usr-01", name: "Nguyễn Minh Anh", email: "minhanh.nguyen@email.com", role: "customer", status: "Active", createdAt: "02/10/2026" },
  { id: "usr-02", name: "Trần Bảo Long", email: "long.tran@nightsandnotes.vn", role: "organizer", status: "Active", createdAt: "15/09/2026" },
  { id: "usr-03", name: "Lê Minh Quân", email: "quan.le@ticketing.vn", role: "staff", status: "Active", createdAt: "01/11/2026" },
  { id: "usr-04", name: "Phạm Thu Hà", email: "ha.pham@ticketing.vn", role: "admin", status: "Active", createdAt: "01/08/2026" },
  { id: "usr-05", name: "Ngô Tấn Tài", email: "tai.ngo@spammail.test", role: "customer", status: "Locked", createdAt: "20/11/2026" },
];

function readStore<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : fallback;
  } catch {
    return fallback;
  }
}

function writeStore<T>(key: string, value: T): T {
  localStorage.setItem(key, JSON.stringify(value));
  return value;
}

export const formatVnd = (amount: number) => `${new Intl.NumberFormat("vi-VN").format(amount)}đ`;

export const platformDemoApi = {
  getProfile: (): UserProfile => readStore(PROFILE_KEY, DEFAULT_PROFILE),
  saveProfile: (next: UserProfile): UserProfile => writeStore(PROFILE_KEY, next),

  getManagedEvents: (): ManagedEvent[] => readStore(MANAGED_EVENTS_KEY, DEFAULT_MANAGED_EVENTS),
  createManagedEvent: (input: {
    title: string;
    category: EventCategory;
    city: string;
    venue: string;
    date: string;
    description: string;
    schedule: Array<{ time: string; title: string; desc: string }>;
  }): ManagedEvent => {
    const list = platformDemoApi.getManagedEvents();
    const created: ManagedEvent = {
      id: `evt-${Date.now().toString(36)}`,
      title: input.title,
      category: input.category,
      city: input.city,
      venue: input.venue,
      date: input.date,
      organizer: "Nights & Notes",
      verified: true,
      status: "Pending",
      description: input.description,
      schedule: input.schedule,
      tiers: [
        {
          id: `tt-${Date.now().toString(36)}`,
          name: "Standard",
          description: "Hạng vé tiêu chuẩn",
          price: 490000,
          total: 200,
          sold: 0,
          reserved: 0,
          maxPerOrder: 4,
        },
      ],
    };
    writeStore(MANAGED_EVENTS_KEY, [created, ...list]);
    return created;
  },
  addTicketTier: (
    eventId: string,
    tier: Omit<ManagedTicketTier, "id" | "sold" | "reserved">,
  ): ManagedEvent[] => {
    const list = platformDemoApi.getManagedEvents();
    const next = list.map((ev) =>
      ev.id === eventId
        ? {
            ...ev,
            tiers: [
              ...ev.tiers,
              { ...tier, id: `tt-${Date.now().toString(36)}`, sold: 0, reserved: 0 },
            ],
          }
        : ev,
    );
    return writeStore(MANAGED_EVENTS_KEY, next);
  },
  moderateEvent: (eventId: string, status: "Published" | "Rejected", rejectReason?: string): ManagedEvent[] => {
    const list = platformDemoApi.getManagedEvents();
    const next = list.map((ev) =>
      ev.id === eventId ? { ...ev, status, rejectReason } : ev,
    );
    return writeStore(MANAGED_EVENTS_KEY, next);
  },

  getOrders: (): OrganizerOrderItem[] => readStore(ORDERS_KEY, DEFAULT_ORDERS),

  getRefunds: (): RefundRequestItem[] => readStore(REFUNDS_KEY, DEFAULT_REFUNDS),
  createRefund: (orderId: string, reason: string): RefundRequestItem => {
    const list = platformDemoApi.getRefunds();
    const created: RefundRequestItem = {
      id: `rf-${Date.now().toString(36)}`,
      orderId,
      orderCode: `ORD-${orderId.replace(/-/g, "").slice(0, 8).toUpperCase()}`,
      eventTitle: "Sự kiện đã đặt",
      buyerName: "Khách hàng",
      buyerEmail: "customer@ticketing.vn",
      amount: 0,
      reason,
      requestedAt: new Date().toISOString(),
      status: "Pending",
    };
    writeStore(REFUNDS_KEY, [created, ...list]);
    return created;
  },
  decideRefund: (id: string, status: "Approved" | "Rejected"): RefundRequestItem[] => {
    const list = platformDemoApi.getRefunds();
    const next = list.map((item) => (item.id === id ? { ...item, status } : item));
    return writeStore(REFUNDS_KEY, next);
  },

  getUsers: (): AdminUserItem[] => readStore(USERS_KEY, DEFAULT_USERS),
  updateUserRole: (id: string, role: UserRole): AdminUserItem[] => {
    const list = platformDemoApi.getUsers();
    const next = list.map((u) => (u.id === id ? { ...u, role } : u));
    return writeStore(USERS_KEY, next);
  },
  toggleUserStatus: (id: string): AdminUserItem[] => {
    const list = platformDemoApi.getUsers();
    const next = list.map((u) =>
      u.id === id ? { ...u, status: u.status === "Active" ? ("Locked" as const) : ("Active" as const) } : u,
    );
    return writeStore(USERS_KEY, next);
  },
};
