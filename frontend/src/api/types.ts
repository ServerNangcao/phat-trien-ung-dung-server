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
