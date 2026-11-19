import { useState, type FormEvent } from "react";
import { Clock3, Plus, X } from "lucide-react";
import { formatVnd, platformDemoApi } from "../../api/platformDemo";
import type { ManagedEvent } from "../../api/types";
import { StatusBadge } from "../../components/ui/StatusBadge";
import { OrganizerLayout } from "./OrganizerLayout";

const inputClass =
  "w-full rounded-xl border border-[#E6E4DD] bg-white px-3.5 py-2.5 text-sm text-[#182226] outline-none focus:border-[#F05A3E] focus-visible:ring-2 focus-visible:ring-[#F05A3E]/25";

export function OrganizerInventoryPage() {
  const [events, setEvents] = useState<ManagedEvent[]>(() => platformDemoApi.getManagedEvents());
  const [selectedEventId, setSelectedEventId] = useState(() => events[0]?.id ?? "");
  const [showModal, setShowModal] = useState(false);
  const [tierForm, setTierForm] = useState({
    name: "Early Bird Special",
    description: "Ưu đãi mở bán sớm giới hạn số lượng",
    price: 490000,
    total: 100,
    maxPerOrder: 4,
  });

  const currentEvent = events.find((e) => e.id === selectedEventId) ?? events[0];

  const handleAddTier = (event: FormEvent) => {
    event.preventDefault();
    if (!currentEvent) return;
    const updated = platformDemoApi.addTicketTier(currentEvent.id, tierForm);
    setEvents(updated);
    setShowModal(false);
  };

  return (
    <OrganizerLayout
      title="Hạng vé & giữ chỗ 10p"
      subtitle="Quản lý số lượng phát hành, vé đã bán và lượng vé đang tạm giữ trong phiên checkout 10 phút."
      action={
        <button type="button" onClick={() => setShowModal(true)} className="btn-primary">
          <Plus size={16} /> Thêm hạng vé
        </button>
      }
    >
      <div className="mb-6 flex flex-wrap items-center justify-between gap-4 rounded-2xl border border-[#E6E4DD] bg-white p-4">
        <div className="flex items-center gap-3">
          <label htmlFor="inv-event" className="text-xs font-bold uppercase tracking-wider text-[#647075]">
            Sự kiện:
          </label>
          <select
            id="inv-event"
            value={currentEvent?.id ?? ""}
            onChange={(e) => setSelectedEventId(e.target.value)}
            className="rounded-xl border border-[#E6E4DD] bg-[#F9F8F4] px-3 py-2 text-sm font-semibold text-[#182226]"
          >
            {events.map((ev) => (
              <option key={ev.id} value={ev.id}>{ev.title}</option>
            ))}
          </select>
        </div>
        <div className="inline-flex items-center gap-2 rounded-xl bg-amber-50 px-3 py-1.5 text-xs font-semibold text-amber-800 border border-amber-200">
          <Clock3 size={14} />
          <span>Cơ chế giữ chỗ tự động nhả kho sau 10:00 nếu chưa thanh toán</span>
        </div>
      </div>

      {currentEvent && (
        <div className="overflow-x-auto rounded-2xl border border-[#E6E4DD] bg-white">
          <table className="w-full min-w-[680px] text-left text-sm">
            <thead className="border-b border-[#E6E4DD] bg-[#F9F8F4] text-xs font-bold uppercase tracking-wider text-[#647075]">
              <tr>
                <th className="px-5 py-3.5">Hạng vé</th>
                <th className="px-5 py-3.5">Giá vé</th>
                <th className="px-5 py-3.5">Tổng phát hành</th>
                <th className="px-5 py-3.5">Đã bán</th>
                <th className="px-5 py-3.5">Đang giữ (10p)</th>
                <th className="px-5 py-3.5">Còn lại</th>
                <th className="px-5 py-3.5">Tối đa/đơn</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#EEECE6]">
              {currentEvent.tiers.map((tier) => {
                const available = Math.max(0, tier.total - tier.sold - tier.reserved);
                return (
                  <tr key={tier.id} className="hover:bg-[#F9F8F4]/60">
                    <td className="px-5 py-4">
                      <p className="font-bold text-[#182226]">{tier.name}</p>
                      <p className="text-xs text-[#647075]">{tier.description}</p>
                    </td>
                    <td className="px-5 py-4 font-bold text-[#182226]">{formatVnd(tier.price)}</td>
                    <td className="px-5 py-4">{tier.total}</td>
                    <td className="px-5 py-4 font-semibold text-emerald-700">{tier.sold}</td>
                    <td className="px-5 py-4">
                      <StatusBadge tone={tier.reserved > 0 ? "warning" : "neutral"}>
                        {tier.reserved} vé
                      </StatusBadge>
                    </td>
                    <td className="px-5 py-4 font-bold text-[#182226]">{available}</td>
                    <td className="px-5 py-4 text-[#647075]">{tier.maxPerOrder}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {showModal && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="add-tier-title"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/45 p-4 backdrop-blur-xs"
        >
          <form
            onSubmit={handleAddTier}
            className="w-full max-w-md rounded-2xl border border-[#E6E4DD] bg-white p-6 shadow-xl space-y-4"
          >
            <div className="flex items-center justify-between">
              <h2 id="add-tier-title" className="text-lg font-extrabold text-[#182226]">
                Thêm hạng vé mới
              </h2>
              <button
                type="button"
                aria-label="Đóng hộp thoại"
                onClick={() => setShowModal(false)}
                className="rounded-lg p-1.5 text-[#647075] hover:bg-[#F3ECE2] cursor-pointer"
              >
                <X size={18} />
              </button>
            </div>

            <div>
              <label htmlFor="tier-name" className="block text-xs font-semibold text-[#526066] mb-1">Tên hạng vé</label>
              <input
                id="tier-name"
                className={inputClass}
                value={tierForm.name}
                onChange={(e) => setTierForm({ ...tierForm, name: e.target.value })}
                required
              />
            </div>
            <div>
              <label htmlFor="tier-desc" className="block text-xs font-semibold text-[#526066] mb-1">Mô tả quyền lợi</label>
              <input
                id="tier-desc"
                className={inputClass}
                value={tierForm.description}
                onChange={(e) => setTierForm({ ...tierForm, description: e.target.value })}
                required
              />
            </div>
            <div className="grid grid-cols-3 gap-3">
              <div>
                <label htmlFor="tier-price" className="block text-xs font-semibold text-[#526066] mb-1">Giá (VNĐ)</label>
                <input
                  id="tier-price"
                  type="number"
                  min={0}
                  step={10000}
                  className={inputClass}
                  value={tierForm.price}
                  onChange={(e) => setTierForm({ ...tierForm, price: Number(e.target.value) })}
                  required
                />
              </div>
              <div>
                <label htmlFor="tier-total" className="block text-xs font-semibold text-[#526066] mb-1">Số lượng</label>
                <input
                  id="tier-total"
                  type="number"
                  min={1}
                  className={inputClass}
                  value={tierForm.total}
                  onChange={(e) => setTierForm({ ...tierForm, total: Number(e.target.value) })}
                  required
                />
              </div>
              <div>
                <label htmlFor="tier-max" className="block text-xs font-semibold text-[#526066] mb-1">Tối đa/đơn</label>
                <input
                  id="tier-max"
                  type="number"
                  min={1}
                  max={10}
                  className={inputClass}
                  value={tierForm.maxPerOrder}
                  onChange={(e) => setTierForm({ ...tierForm, maxPerOrder: Number(e.target.value) })}
                  required
                />
              </div>
            </div>
            <div className="flex justify-end gap-2 pt-2">
              <button type="button" onClick={() => setShowModal(false)} className="btn-outline">
                Hủy
              </button>
              <button type="submit" className="btn-primary">
                Lưu hạng vé
              </button>
            </div>
          </form>
        </div>
      )}
    </OrganizerLayout>
  );
}
