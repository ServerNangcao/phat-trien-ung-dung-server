import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, X } from "lucide-react";
import { formatVnd } from "../../api/platformDemo";
import { ticketingApi } from "../../api/service";
import { StatusBadge } from "../../components/ui/StatusBadge";
import { AdminLayout } from "./AdminLayout";

function formatRequestedAt(raw: string): string {
  const parsed = new Date(raw);
  if (isNaN(parsed.getTime())) return raw;
  return parsed.toLocaleString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  });
}

const STATUS_LABEL: Record<string, string> = {
  Pending: "Chờ duyệt",
  Approved: "Đã hoàn tiền",
  Rejected: "Từ chối",
};

export function AdminRefundsPage() {
  const queryClient = useQueryClient();
  const [statusFilter, setStatusFilter] = useState<"ALL" | "Pending" | "Approved" | "Rejected">("ALL");
  const [actionError, setActionError] = useState<string | null>(null);

  const { data: refunds = [], isLoading, isError } = useQuery({
    queryKey: ["admin", "refunds"],
    queryFn: () => ticketingApi.refunds.getAdminList(),
  });

  const filtered = useMemo(() => {
    if (statusFilter === "ALL") return refunds;
    return refunds.filter((item) => item.status === statusFilter);
  }, [refunds, statusFilter]);

  const approveMutation = useMutation({
    mutationFn: (id: string) => ticketingApi.refunds.approve(id),
    onSuccess: async () => {
      setActionError(null);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["admin", "refunds"] }),
        queryClient.invalidateQueries({ queryKey: ["organizer", "orders"] }),
        queryClient.invalidateQueries({ queryKey: ["tickets"] }),
        queryClient.invalidateQueries({ queryKey: ["refunds", "mine"] }),
      ]);
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { error?: string; detail?: string } } };
      setActionError(
        axiosErr.response?.data?.error ??
          axiosErr.response?.data?.detail ??
          "Không thể phê duyệt yêu cầu hoàn tiền này.",
      );
    },
  });

  const rejectMutation = useMutation({
    mutationFn: (id: string) => ticketingApi.refunds.reject(id),
    onSuccess: async () => {
      setActionError(null);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["admin", "refunds"] }),
        queryClient.invalidateQueries({ queryKey: ["refunds", "mine"] }),
      ]);
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { error?: string; detail?: string } } };
      setActionError(
        axiosErr.response?.data?.error ??
          axiosErr.response?.data?.detail ??
          "Không thể từ chối yêu cầu hoàn tiền này.",
      );
    },
  });

  const isBusy = approveMutation.isPending || rejectMutation.isPending;

  return (
    <AdminLayout
      title="Xử lý hoàn tiền"
      subtitle="Phê duyệt hoặc từ chối yêu cầu hoàn tiền của khách hàng trước khi chuyển về cổng thanh toán."
    >
      <div className="mb-5 flex flex-wrap gap-2">
        {[
          { id: "ALL" as const, label: "Tất cả" },
          { id: "Pending" as const, label: "Chờ duyệt" },
          { id: "Approved" as const, label: "Đã hoàn tiền" },
          { id: "Rejected" as const, label: "Từ chối" },
        ].map((tab) => (
          <button
            key={tab.id}
            type="button"
            onClick={() => setStatusFilter(tab.id)}
            className={`rounded-xl px-3.5 py-2 text-xs font-bold cursor-pointer transition-colors ${
              statusFilter === tab.id
                ? "bg-[#F05B45] text-white"
                : "border border-slate-200 bg-white text-[#475458] hover:text-[#182226]"
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {(isError || actionError) && (
        <div className="mb-5 rounded-2xl border border-rose-200 bg-rose-50 px-5 py-4 text-sm text-rose-700">
          {actionError ?? "Không thể tải danh sách yêu cầu hoàn tiền từ máy chủ."}
        </div>
      )}

      <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white text-[#182226]">
        {isLoading ? (
          <div className="p-8 text-center text-sm text-[#526066]">
            Đang tải danh sách yêu cầu hoàn tiền...
          </div>
        ) : filtered.length === 0 ? (
          <div className="p-10 text-center text-sm text-[#526066]">
            {refunds.length === 0
              ? "Chưa có yêu cầu hoàn tiền nào."
              : "Không có yêu cầu hoàn tiền phù hợp với bộ lọc."}
          </div>
        ) : (
          <table className="w-full min-w-[780px] text-left text-sm">
            <thead className="border-b border-slate-200 bg-slate-50 text-xs font-bold uppercase tracking-wider text-[#526066]">
              <tr>
                <th className="px-5 py-3.5">Mã đơn</th>
                <th className="px-5 py-3.5">Khách hàng</th>
                <th className="px-5 py-3.5">Sự kiện &amp; Lý do</th>
                <th className="px-5 py-3.5">Số tiền</th>
                <th className="px-5 py-3.5">Trạng thái</th>
                <th className="px-5 py-3.5 text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200 bg-white">
              {filtered.map((item) => (
                <tr key={item.id} className="hover:bg-slate-50">
                  <td className="px-5 py-4">
                    <p className="font-mono text-xs font-bold text-[#182226]">{item.orderCode}</p>
                    <p className="text-[11px] text-[#526066]">{formatRequestedAt(item.requestedAt)}</p>
                  </td>
                  <td className="px-5 py-4">
                    <p className="font-bold text-[#182226]">{item.buyerName}</p>
                    <p className="text-xs text-[#526066]">{item.buyerEmail}</p>
                  </td>
                  <td className="px-5 py-4 max-w-xs">
                    <p className="font-semibold text-[#182226]">{item.eventTitle}</p>
                    <p className="text-xs text-[#526066]">{item.reason}</p>
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
                      {STATUS_LABEL[item.status] ?? item.status}
                    </StatusBadge>
                  </td>
                  <td className="px-5 py-4 text-right">
                    {item.status === "Pending" ? (
                      <div className="inline-flex gap-2">
                        <button
                          type="button"
                          disabled={isBusy}
                          onClick={() => approveMutation.mutate(item.id)}
                          className="inline-flex items-center gap-1 rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-emerald-700 disabled:opacity-50 cursor-pointer"
                        >
                          <Check size={14} /> Duyệt
                        </button>
                        <button
                          type="button"
                          disabled={isBusy}
                          onClick={() => rejectMutation.mutate(item.id)}
                          className="inline-flex items-center gap-1 rounded-lg border border-rose-300 bg-white px-3 py-1.5 text-xs font-semibold text-rose-700 hover:bg-rose-50 disabled:opacity-50 cursor-pointer"
                        >
                          <X size={14} /> Từ chối
                        </button>
                      </div>
                    ) : (
                      <span className="text-xs text-[#526066]">Đã xử lý</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </AdminLayout>
  );
}
