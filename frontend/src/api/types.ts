export type EventCategory = "Âm nhạc" | "Nghệ thuật" | "Hội thảo" | "Ẩm thực" | "Thể thao";
export const EVENT_PAGE_SIZE = 8;

export interface TicketTier {
  id: string;
  name: string;
  price: number;
  remaining: number;
  maxPerOrder?: number;
  description: string;
}

export interface EventItem {
  id: string;        // slug hoặc id dùng cho URL
  rawId?: string;    // GUID gốc từ backend dùng để gọi API reserve
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
  status?: ManagedEventStatus;
  rejectReason?: string;
  ticketTypesLoadError?: boolean;
  tiers: TicketTier[];
}

export interface Reservation {
  id: string;
  eventId: string;
  eventSlug?: string;
  tierId: string;
  quantity: number;
  expiresAt: number;
}

export interface PurchasedTicket {
  id: string;
  orderId?: string;
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

// ───────────────────────────────────────────────
// Identity & Authentication Types
// ───────────────────────────────────────────────
export type UserRole =
  | "Customer"
  | "Organizer"
  | "Staff"
  | "Admin"
  | "customer"
  | "staff"
  | "organizer"
  | "admin";

export interface UserSession {
  name: string;
  role: "customer" | "staff" | "organizer" | "admin";
  email?: string;
  userId?: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  userId: string;
  email: string;
  fullName: string;
  roles: string[];
}

export interface UserProfileResponse {
  id: string;
  email: string;
  fullName: string;
  phoneNumber?: string | null;
  isActive: boolean;
  createdAt: string;
  roles: string[];
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  fullName: string;
  password: string;
  phoneNumber?: string | null;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ForgotPasswordResponse {
  message: string;
  resetToken?: string;
  cooldownSeconds?: number;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
}

export interface ResetPasswordResponse {
  message: string;
}

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

export type ManagedEventStatus =
  | "Published"
  | "Pending"
  | "Draft"
  | "Rejected"
  | "OnSale"
  | "PendingApproval"
  | "Ended"
  | "Cancelled";

export interface CategoryDto {
  id: string;
  name: string;
  slug: string;
}

export interface VenueDto {
  id: string;
  name: string;
  address: string;
  city: string;
  latitude?: number | null;
  longitude?: number | null;
  capacity?: number | null;
}

export interface CreateOrganizerEventInput {
  categoryId: string;
  venueId: string;
  title: string;
  description?: string;
  startsAt: string;
  endsAt: string;
}

export interface CreateTicketTypeInput {
  name: string;
  price: number;
  totalQuantity: number;
  maxPerOrder: number;
}

export interface ManagedTicketTier {
  id: string;
  name: string;
  description: string;
  price: number;
  total: number;
  sold: number;
  reserved: number;
  available?: number;
  maxPerOrder: number;
}

export interface ManagedEvent {
  id: string;
  slug?: string;
  title: string;
  category: EventCategory | string;
  city: string;
  venue: string;
  venueCapacity?: number | null;
  date: string;
  startsAt?: string;
  endsAt?: string;
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
  orderId?: string;
  orderCode: string;
  eventTitle: string;
  buyerName: string;
  buyerEmail: string;
  amount: number;
  reason: string;
  requestedAt: string;
  status: "Pending" | "Approved" | "Rejected";
  rawStatus?: string;
}

export interface MyRefundRequestItem {
  id: string;
  orderId: string;
  orderCode: string;
  amount: number;
  reason: string;
  status: "Pending" | "Approved" | "Rejected";
  rawStatus?: string;
  requestedAt: string;
}

export interface AdminUserItem {
  id: string;
  name: string;
  email: string;
  role: UserRole;
  status: "Active" | "Locked";
  createdAt: string;
}
