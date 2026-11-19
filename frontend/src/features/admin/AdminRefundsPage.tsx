import { useState } from "react";
import { Check, X } from "lucide-react";
import { formatVnd, platformDemoApi } from "../../api/platformDemo";
import type { RefundRequestItem } from "../../api/types";
import { StatusBadge } from "../../components/ui/StatusBadge";
import { AdminLayout } from "./AdminLayout";

export function AdminRefundsPage() {
  const [refunds, setRefunds] = useState<RefundRequestItem[]>(() => platformDemoApi.getRefunds());

  const handleDecision = (id: string, status: "Approved" | "Rejected") => {
    setRefunds(platformDemoApi.decideRefund(id, status));
  };

  return (
    <AdminLayout
      title="Xử lý hoàn tiền"
      subtitle="Phê duyệt hoặc từ chối yêu cầu hoàn tiền của khách hàng trước khi chuyển về cổng thanh toán."
    >
      <div className="overflow-x-auto rounded-2xl border border-[#E6E4DD] bg-white">
        <table className="w-full min-w-[780px] text-left text-sm">
          <thead className="border-b border-[#E6E4DD] bg-[#F9F8F4] text-xs font-bold uppercase tracking-wider text-[#647075]">
            <tr>
              <th className="px-5 py-3.5">Mã đơn</th>
              <th className="px-5 py-3.5">Khách hàng</th>
              <th className="px-5 py-3.5">Sự kiện &amp; Lý do</th>
              <th className="px-5 py-3.5">Số tiền</th>
              <th className="px-5 py-3.5">Trạng thái</th>
              <th className="px-5 py-3.5 text-right">Thao tác</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[#EEECE6]">
            {refunds.map((item) => (
              <tr key={item.id} className="hover:bg-[#F9F8F4]/60">
                <td className="px-5 py-4">
                  <p className="font-mono text-xs font-bold text-[#182226]">{item.orderCode}</p>
                  <p className="text-[11px] text-[#647075]">{item.requestedAt}</p>
                </td>
                <td className="px-5 py-4">
                  <p className="font-bold text-[#182226]">{item.buyerName}</p>
                  <p className="text-xs text-[#647075]">{item.buyerEmail}</p>
                </td>
                <td className="px-5 py-4 max-w-xs">
                  <p className="font-semibold text-[#182226]">{item.eventTitle}</p>
                  <p className="text-xs text-[#647075]">{item.reason}</p>
                </td>
                <td className="px-5 py-4 font-bold text-[#182226]">{formatVnd(item.amount)}</td>
                <td className="px-5 py-4">
                  <StatusBadge
                    tone={
                      item.status === "Approved"
                        ? "success"
                        : item.status === "Pending"
                          ? "warning"
                          : "danger"
                    }
                  >
                    {item.status}
                  </StatusBadge>
                </td>
                <td className="px-5 py-4 text-right">
                  {item.status === "Pending" ? (
                    <div className="inline-flex gap-2">
                      <button
                        type="button"
                        onClick={() => handleDecision(item.id, "Approved")}
                        className="inline-flex items-center gap-1 rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-emerald-700 cursor-pointer"
                      >
                        <Check size={14} /> Duyệt
                      </button>
                      <button
                        type="button"
                        onClick={() => handleDecision(item.id, "Rejected")}
                        className="inline-flex items-center gap-1 rounded-lg border border-rose-300 bg-white px-3 py-1.5 text-xs font-semibold text-rose-700 hover:bg-rose-50 cursor-pointer"
                      >
                        <X size={14} /> Từ chối
                      </button>
                    </div>
                  ) : (
                    <span className="text-xs text-[#647075]">Đã xử lý</span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </AdminLayout>
  );
}
