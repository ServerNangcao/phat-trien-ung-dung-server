import assert from "node:assert/strict";
import { describe, it } from "node:test";
import type { TicketTier } from "../../../api/types.ts";
import {
  computeMaxSelectableQuantity,
  formatRemainingTickets,
  getEventStockSummary,
  mapApiTicketTypesToTiers,
  resolveSelectableTier,
} from "./ticket-stock.ts";

const soldOutTier: TicketTier = {
  id: "tier-sold-out",
  name: "Early Bird",
  price: 200000,
  remaining: 0,
  maxPerOrder: 4,
  description: "Early Bird (Tối đa 4 vé/đơn)",
};

const lowStockTier: TicketTier = {
  id: "tier-low",
  name: "VIP",
  price: 500000,
  remaining: 3,
  maxPerOrder: 6,
  description: "VIP (Tối đa 6 vé/đơn)",
};

const normalTier: TicketTier = {
  id: "tier-normal",
  name: "Standard",
  price: 300000,
  remaining: 100,
  maxPerOrder: 4,
  description: "Standard (Tối đa 4 vé/đơn)",
};

describe("ticket-stock regression tests", () => {
  it("formats remaining tickets accurately for sold-out, low-stock, and normal stock", () => {
    assert.deepEqual(formatRemainingTickets(0), { label: "Hết vé", tone: "sold-out" });
    assert.deepEqual(formatRemainingTickets(12), { label: "Chỉ còn 12 vé", tone: "low" });
    assert.deepEqual(formatRemainingTickets(100), { label: "Còn 100 vé", tone: "normal" });
  });

  it("computes max selectable quantity as 0 when tier is sold out", () => {
    assert.equal(computeMaxSelectableQuantity(soldOutTier), 0);
    assert.equal(computeMaxSelectableQuantity(lowStockTier), 3);
    assert.equal(computeMaxSelectableQuantity(normalTier), 4);
    assert.equal(computeMaxSelectableQuantity(undefined), 0);
  });

  it("skips sold-out tiers when resolving selectable tier", () => {
    assert.equal(resolveSelectableTier([soldOutTier, lowStockTier], "")?.id, lowStockTier.id);
    assert.equal(resolveSelectableTier([soldOutTier, lowStockTier], soldOutTier.id)?.id, lowStockTier.id);
    assert.equal(resolveSelectableTier([soldOutTier], "")?.id, soldOutTier.id);
    assert.equal(resolveSelectableTier([], ""), undefined);
  });

  it("computes event stock summary for sold-out, empty-tiers, and active events", () => {
    assert.deepEqual(getEventStockSummary("OnSale", [soldOutTier]), {
      label: "Hết vé",
      isErrorTone: true,
      allSoldOut: true,
      hasNoTiers: false,
    });

    assert.deepEqual(getEventStockSummary("OnSale", []), {
      label: "Chưa mở bán",
      isErrorTone: true,
      allSoldOut: false,
      hasNoTiers: true,
    });

    assert.deepEqual(getEventStockSummary("OnSale", [normalTier]), {
      label: "Đang mở bán",
      isErrorTone: false,
      allSoldOut: false,
      hasNoTiers: false,
    });
  });

  it("maps API ticket types preserving quantityAvailable === 0 and avoiding fake fallback tiers", () => {
    const mapped = mapApiTicketTypesToTiers([
      { id: "1", name: "VIP", price: 500000, quantityAvailable: 0, capacity: 50, maxPerOrder: 4 },
    ]);
    assert.equal(mapped.length, 1);
    assert.equal(mapped[0].remaining, 0);

    assert.deepEqual(mapApiTicketTypesToTiers([]), []);
  });
});
