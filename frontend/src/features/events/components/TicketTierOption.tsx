import { Check } from "lucide-react";
import type { TicketTier } from "../../../api/types";
import { formatRemainingTickets } from "../utils/ticket-stock";

const currency = new Intl.NumberFormat("vi-VN");
const formatMoney = (price: number) => (price === 0 ? "Miễn phí" : `${currency.format(price)}đ`);

interface TicketTierOptionProps {
  tier: TicketTier;
  selected: boolean;
  onSelect: () => void;
}

export function TicketTierOption({ tier, selected, onSelect }: TicketTierOptionProps) {
  const isSoldOut = tier.remaining <= 0;
  const stock = formatRemainingTickets(tier.remaining);
  const stockClass =
    stock.tone === "sold-out" ? "sold-out-text" : stock.tone === "low" ? "low-stock" : "";

  return (
    <button
      type="button"
      disabled={isSoldOut}
      aria-disabled={isSoldOut}
      className={`tier-option ${selected && !isSoldOut ? "selected" : ""} ${isSoldOut ? "sold-out" : ""}`}
      onClick={() => {
        if (!isSoldOut) onSelect();
      }}
    >
      <span className={`radio-mark ${selected && !isSoldOut ? "checked" : ""}`}>
        {selected && !isSoldOut && <Check size={12} />}
      </span>
      <span className="tier-desc">
        <b>{tier.name}</b>
        <small>{tier.description}</small>
        <small className={stockClass}>{stock.label}</small>
      </span>
      <b className="tier-price">{formatMoney(tier.price)}</b>
    </button>
  );
}
