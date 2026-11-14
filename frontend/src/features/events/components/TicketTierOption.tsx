import { Check } from "lucide-react";
import type { TicketTier } from "../../../api/types";

const currency = new Intl.NumberFormat("vi-VN");
const formatMoney = (price: number) => `${currency.format(price)}đ`;

interface TicketTierOptionProps {
  tier: TicketTier;
  selected: boolean;
  onSelect: () => void;
}

export function TicketTierOption({ tier, selected, onSelect }: TicketTierOptionProps) {
  return (
    <button
      type="button"
      className={`tier-option ${selected ? "selected" : ""}`}
      onClick={onSelect}
    >
      <span className={`radio-mark ${selected ? "checked" : ""}`}>
        {selected && <Check size={12} />}
      </span>
      <span className="tier-desc">
        <b>{tier.name}</b>
        <small>{tier.description}</small>
        <small className={tier.remaining < 25 ? "low-stock" : ""}>
          {tier.remaining < 25 ? "Chỉ còn " : ""}
          {tier.remaining} vé
        </small>
      </span>
      <b className="tier-price">{formatMoney(tier.price)}</b>
    </button>
  );
}

