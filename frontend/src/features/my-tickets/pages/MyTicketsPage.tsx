import { useMemo, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowRight, Check, RotateCcw, Ticket, TicketCheck, X } from "lucide-react";
import { ticketingApi } from "../../../api/service";
import type { MyRefundRequestItem, PurchasedTicket } from "../../../api/types";
import { Notice } from "../../../components/common/Notice";
import { PageLoading } from "../../../components/common/PageLoading";
import { DigitalTicketCard } from "../components/DigitalTicketCard";

export function MyTicketsPage() {
  const queryClient = useQueryClient();
  const hasToken = Boolean(
    localStorage.getItem("ticketing.accessToken") || localStorage.getItem("ticketing.user"),
  );

  const { data, isLoading, isError } = useQuery({
    queryKey: ["tickets"],
    queryFn: ticketingApi.tickets,
    enabled: hasToken,
  });

  const { data: myRefunds = [] } = useQuery({
    queryKey: ["refunds", "mine"],
    queryFn: ticketingApi.refunds.getMine,
    enabled: hasToken,
  });

  const [filter, setFilter] = useState<"all" | "valid" | "used" | "cancelled">("all");
  const [refundTarget, setRefundTarget] = useState<PurchasedTicket | null>(null);
  const [refundReason, setRefundReason] = useState("");
  const [refundError, setRefundError] = useState<string | null>(null);
  const [refundNotice, setRefundNotice] = useState<string | null>(null);

  const refundByOrderId = useMemo(() => {
    const map = new Map<string, MyRefundRequestItem>();
    for (const item of myRefunds) {
      const existing = map.get(item.orderId);
      if (!existing) {
        map.set(item.orderId, item);
        continue;
      }
      if (
        (item.status === "Pending" || item.status === "Approved") &&
        existing.status === "Rejected"
      ) {
        map.set(item.orderId, item);
      }
    }
    return map;
  }, [myRefunds]);

  const createRefundMutation = useMutation({
    mutationFn: ({ orderId, reason }: { orderId: string; reason: string }) =>
      ticketingApi.refunds.create(orderId, reason),
    onSuccess: async () => {
      setRefundError(null);
      setRefundTarget(null);
      setRefundReason("");
      setRefundNotice("Đã gửi yêu cầu hoàn tiền. Quản trị viên sẽ xem xét và phản hồi sớm nhất.");
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["refunds", "mine"] }),
        queryClient.invalidateQueries({ queryKey: ["admin", "refunds"] }),
      ]);
    },
    onError: (err: unknown) => {
      const axiosErr = err as {
        response?: { data?: { error?: string; detail?: string; message?: string } };
        message?: string;
      };
      setRefundError(
        axiosErr.response?.data?.error ??
          axiosErr.response?.data?.detail ??
          axiosErr.response?.data?.message ??
          "Không thể gửi yêu cầu hoàn tiền. Vui lòng thử lại.",
      );
    },
  });

  const handleOpenRefund = (ticket: PurchasedTicket) => {
    setRefundError(null);
    setRefundReason("");
    setRefundTarget(ticket);
  };

  const handleSubmitRefund = (event: FormEvent) => {
    event.preventDefault();
    if (!refundTarget?.orderId) return;
    const trimmed = refundReason.trim();
    if (trimmed.length < 5) {
      setRefundError("Vui lòng nhập lý do hoàn tiền tối thiểu 5 ký tự.");
      return;
    }
    setRefundError(null);
    createRefundMutation.mutate({ orderId: refundTarget.orderId, reason: trimmed });
  };

  const visible = (data ?? []).filter((ticket) => filter === "all" || ticket.status === filter);

  return (
    <div className="mx-auto max-w-[1040px] page-enter px-5 pb-20 pt-12 lg:px-8">
      <div className="tickets-head">
        <div>
          <div className="eyebrow muted-eyebrow">BỘ SƯU TẬP TRẢI NGHIỆM</div>
          <h1 className="section-title">
            Vé của <em>bạn.</em>
          </h1>
          <p>Những cuộc hẹn hay sắp tới, ở ngay đây.</p>
        </div>
        <div className="ticket-count-badge">
          <TicketCheck size={19} />
          <span>
            {data?.length ?? 0}
            <small> tấm vé</small>
          </span>
        </div>
      </div>

      {refundNotice && (
        <div
          role="status"
          className="mb-5 flex items-center justify-between gap-3 rounded-2xl border border-emerald-200 bg-emerald-50 px-5 py-3.5 text-sm text-emerald-800"
        >
          <span className="inline-flex items-center gap-2 font-semibold">
            <Check size={16} />
            {refundNotice}
          </span>
          <button
            type="button"
            aria-label="Đóng thông báo"
            onClick={() => setRefundNotice(null)}
            className="rounded-lg p-1 text-emerald-700 hover:bg-emerald-100 cursor-pointer"
          >
            <X size={15} />
          </button>
        </div>
      )}

      <div className="ticket-tabs">
        {(["all", "valid", "used", "cancelled"] as const).map((value) => (
          <button
            type="button"
            className={filter === value ? "active" : ""}
            key={value}
            onClick={() => setFilter(value)}
          >
            {value === "all"
              ? "Tất cả vé"
              : value === "valid"
                ? "Sắp diễn ra"
                : value === "used"
                  ? "Đã sử dụng"
                  : "Đã hủy"}
          </button>
        ))}
      </div>

      {isError ? (
        <Notice title="Không thể tải vé" body="API chưa phản hồi. Vui lòng thử lại sau." />
      ) : isLoading ? (
        <PageLoading />
      ) : visible.length === 0 ? (
        <div className="empty-tickets">
          <div>
            <Ticket size={29} />
          </div>
          <h2>{data?.length ? "Không có vé trong mục này" : "Hành trình bắt đầu từ đây"}</h2>
          <p>
            {data?.length
              ? "Các vé khác của bạn sẽ xuất hiện ở bộ lọc khác."
              : "Khám phá sự kiện hay và đặt tấm vé đầu tiên nhé."}
          </p>
          <Link to="/" className="btn-primary">
            Khám phá sự kiện <ArrowRight size={16} />
          </Link>
        </div>
      ) : (
        <div className="ticket-list">
          {visible.map((ticket) => (
            <DigitalTicketCard
              key={ticket.id}
              ticket={ticket}
              refundRequest={ticket.orderId ? refundByOrderId.get(ticket.orderId) : undefined}
              onRequestRefund={handleOpenRefund}
            />
          ))}
        </div>
      )}

      {refundTarget && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="refund-modal-title"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4 backdrop-blur-sm"
        >
          <form
            onSubmit={handleSubmitRefund}
            className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-6 text-[#182226] shadow-2xl space-y-4"
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <RotateCcw size={18} className="text-[#F05B45]" />
                <h2 id="refund-modal-title" className="text-lg font-extrabold text-[#182226]">
                  Yêu cầu hoàn tiền
                </h2>
              </div>
              <button
                type="button"
                aria-label="Đóng hộp thoại"
                onClick={() => setRefundTarget(null)}
                className="rounded-lg p-1.5 text-[#526066] hover:bg-black/5 cursor-pointer"
              >
                <X size={18} />
              </button>
            </div>

            <div className="rounded-xl border border-amber-200 bg-amber-50 px-3.5 py-2.5 text-xs text-amber-900">
              <p className="font-semibold">{refundTarget.eventTitle}</p>
              <p className="mt-1">
                Yêu cầu hoàn tiền áp dụng cho <b>toàn bộ đơn hàng</b> chứa vé này. Khi được Quản trị viên duyệt, tất cả vé thuộc đơn sẽ bị hủy và không thể check-in.
              </p>
            </div>

            {refundError && (
              <p className="rounded-xl border border-rose-200 bg-rose-50 px-3 py-2 text-xs font-medium text-rose-700">
                {refundError}
              </p>
            )}

            <div>
              <label htmlFor="refund-reason" className="mb-1.5 block text-xs font-semibold text-[#526066]">
                Lý do hoàn tiền
              </label>
              <textarea
                id="refund-reason"
                rows={4}
                required
                minLength={5}
                maxLength={500}
                placeholder="VD: Bận lịch đột xuất không thể tham dự sự kiện..."
                className="w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm text-[#182226] outline-none focus:border-[#F05B45] focus-visible:ring-2 focus-visible:ring-[#F05B45]/25"
                value={refundReason}
                onChange={(e) => setRefundReason(e.target.value)}
              />
            </div>

            <div className="flex justify-end gap-2 pt-1">
              <button
                type="button"
                onClick={() => setRefundTarget(null)}
                className="btn-outline"
              >
                Hủy
              </button>
              <button
                type="submit"
                disabled={createRefundMutation.isPending}
                className="btn-primary"
              >
                {createRefundMutation.isPending ? "Đang gửi..." : "Gửi yêu cầu"}
              </button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
}
