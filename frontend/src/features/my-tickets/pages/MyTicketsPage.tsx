import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { ArrowRight, Ticket, TicketCheck } from "lucide-react";
import { ticketingApi } from "../../../api/service";
import { Notice } from "../../../components/common/Notice";
import { PageLoading } from "../../../components/common/PageLoading";
import { DigitalTicketCard } from "../components/DigitalTicketCard";

export function MyTicketsPage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["tickets"],
    queryFn: ticketingApi.tickets,
  });

  const [filter, setFilter] = useState<"all" | "valid" | "used">("all");
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

      <div className="ticket-tabs">
        {(["all", "valid", "used"] as const).map((value) => (
          <button
            type="button"
            className={filter === value ? "active" : ""}
            key={value}
            onClick={() => setFilter(value)}
          >
            {value === "all" ? "Tất cả vé" : value === "valid" ? "Sắp diễn ra" : "Đã sử dụng"}
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
            <DigitalTicketCard key={ticket.id} ticket={ticket} />
          ))}
        </div>
      )}
    </div>
  );
}

