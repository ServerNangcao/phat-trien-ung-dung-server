import type { ManagedEventStatus, TicketTier } from "../../../api/types.ts";

export interface RawApiTicketType {
  id: string;
  name: string;
  price?: number;
  capacity?: number;
  quantityAvailable?: number;
  maxPerOrder?: number;
}

export function formatRemainingTickets(remaining: number): {
  label: string;
  tone: "sold-out" | "low" | "normal";
} {
  if (remaining <= 0) {
    return { label: "Hết vé", tone: "sold-out" };
  }
  if (remaining < 25) {
    return {
      label: `Chỉ còn ${remaining.toLocaleString("vi-VN")} vé`,
      tone: "low",
    };
  }
  return {
    label: `Còn ${remaining.toLocaleString("vi-VN")} vé`,
    tone: "normal",
  };
}

export function resolveSelectableTier(
  tiers: TicketTier[],
  selectedTierId: string,
): TicketTier | undefined {
  if (tiers.length === 0) return undefined;
  const chosen = tiers.find((item) => item.id === selectedTierId);
  if (chosen && chosen.remaining > 0) return chosen;
  return tiers.find((item) => item.remaining > 0) ?? tiers[0];
}

export function computeMaxSelectableQuantity(tier: TicketTier | undefined): number {
  if (!tier || tier.remaining <= 0) return 0;
  return Math.min(tier.maxPerOrder ?? 6, tier.remaining);
}

export function getEventStockSummary(
  status: ManagedEventStatus | undefined,
  tiers: TicketTier[],
): {
  label: string;
  isErrorTone: boolean;
  allSoldOut: boolean;
  hasNoTiers: boolean;
} {
  if (status === "Cancelled") {
    return { label: "Đã hủy", isErrorTone: true, allSoldOut: false, hasNoTiers: false };
  }
  if (status === "Ended") {
    return { label: "Đã kết thúc", isErrorTone: true, allSoldOut: false, hasNoTiers: false };
  }
  if (status === "Rejected") {
    return { label: "Ngừng mở bán", isErrorTone: true, allSoldOut: false, hasNoTiers: false };
  }
  if (tiers.length === 0) {
    return { label: "Chưa mở bán", isErrorTone: true, allSoldOut: false, hasNoTiers: true };
  }
  if (tiers.every((tier) => tier.remaining <= 0)) {
    return { label: "Hết vé", isErrorTone: true, allSoldOut: true, hasNoTiers: false };
  }
  return { label: "Đang mở bán", isErrorTone: false, allSoldOut: false, hasNoTiers: false };
}

export function mapApiTicketTypesToTiers(ticketTypes: RawApiTicketType[]): TicketTier[] {
  return ticketTypes.map((t) => ({
    id: t.id,
    name: t.name,
    price: Number(t.price ?? 0),
    remaining: Math.max(0, Number(t.quantityAvailable ?? t.capacity ?? 0)),
    maxPerOrder: Number(t.maxPerOrder ?? 4),
    description: `${t.name} (Tối đa ${t.maxPerOrder ?? 4} vé/đơn)`,
  }));
}
