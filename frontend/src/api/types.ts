export type EventCategory = "Âm nhạc" | "Nghệ thuật" | "Hội thảo" | "Ẩm thực" | "Thể thao";
export const EVENT_PAGE_SIZE = 8;

export interface TicketTier {
  id: string;
  name: string;
  price: number;
  remaining: number;
  description: string;
}

export interface EventItem {
  id: string;
  title: string;
  category: EventCategory;
  location: string;
  venue: string;
  date: string;
  time: string;
  image: string;
  accent: string;
  description: string;
  organizer: string;
  featured?: boolean;
  tiers: TicketTier[];
}

export interface Reservation {
  id: string;
  eventId: string;
  tierId: string;
  quantity: number;
  expiresAt: number;
}

export interface PurchasedTicket {
  id: string;
  eventId: string;
  eventTitle: string;
  image: string;
  venue: string;
  date: string;
  tierName: string;
  holderName: string;
  code: string;
  status: "valid" | "used" | "cancelled";
}

export interface CheckoutInput {
  reservationId: string;
  name: string;
  email: string;
  phone: string;
}

export type CheckInStatus = "valid" | "used" | "cancelled" | "invalid";

export type UserRole = "customer" | "staff" | "organizer" | "admin";

export interface UserProfile {
  fullName: string;
  email: string;
  phone: string;
  dob: string;
  gender: "Nam" | "Nữ" | "Khác";
  idNumber: string;
  city: string;
  district: string;
  address: string;
  taxCompany: string;
  taxCode: string;
  taxAddress: string;
  taxEmail: string;
  twoFactorEnabled: boolean;
}

export type ManagedEventStatus = "Published" | "Pending" | "Draft" | "Rejected";

export interface ManagedTicketTier {
  id: string;
  name: string;
  description: string;
  price: number;
  total: number;
  sold: number;
  reserved: number;
  maxPerOrder: number;
}

export interface ManagedEvent {
  id: string;
  title: string;
  category: EventCategory;
  city: string;
  venue: string;
  date: string;
  organizer: string;
  verified: boolean;
  status: ManagedEventStatus;
  rejectReason?: string;
  description: string;
  schedule: Array<{ time: string; title: string; desc: string }>;
  tiers: ManagedTicketTier[];
}

export interface OrganizerOrderItem {
  orderCode: string;
  eventId: string;
  eventTitle: string;
  buyerName: string;
  buyerEmail: string;
  tierName: string;
  quantity: number;
  totalAmount: number;
  paymentStatus: "Paid" | "Pending" | "Refunded";
  checkInStatus: "CheckedIn" | "NotCheckedIn";
  createdAt: string;
}

export interface RefundRequestItem {
  id: string;
  orderCode: string;
  eventTitle: string;
  buyerName: string;
  buyerEmail: string;
  amount: number;
  reason: string;
  requestedAt: string;
  status: "Pending" | "Approved" | "Rejected";
}

export interface AdminUserItem {
  id: string;
  name: string;
  email: string;
  role: UserRole;
  status: "Active" | "Locked";
  createdAt: string;
}
