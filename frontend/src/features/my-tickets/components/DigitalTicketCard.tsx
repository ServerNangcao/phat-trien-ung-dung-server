import { QRCodeSVG } from "qrcode.react";
import { ArrowDown, CalendarDays, MapPin, RotateCcw, Ticket } from "lucide-react";
import type { MyRefundRequestItem, PurchasedTicket } from "../../../api/types";

const formatDate = (date: string) =>
  new Intl.DateTimeFormat("vi-VN", {
    weekday: "short",
    day: "2-digit",
    month: "short",
  }).format(new Date(`${date}T12:00:00`));

interface DigitalTicketCardProps {
  ticket: PurchasedTicket;
  refundRequest?: MyRefundRequestItem;
  onRequestRefund?: (ticket: PurchasedTicket) => void;
}

export function DigitalTicketCard({ ticket, refundRequest, onRequestRefund }: DigitalTicketCardProps) {
  const statusLabel =
    ticket.status === "used"
      ? "Đã sử dụng"
      : ticket.status === "cancelled"
        ? "Đã hủy"
        : "Sẵn sàng check-in";

  const download = () => {
    const svg = document.getElementById(`qr-${ticket.id}`);
    if (!svg) return;

    const qr = svg.outerHTML.replace(/<svg\b[^>]*>/, (openingTag) =>
      openingTag
        .replace(/\s(?:width|height)="[^"]*"/g, "")
        .replace("<svg", '<svg x="575" y="46" width="154" height="154"'),
    );

    const escape = (value: string) =>
      value.replace(/[&<>"']/g, (character) =>
        ({
          "&": "&amp;",
          "<": "&lt;",
          ">": "&gt;",
          '"': "&quot;",
          "'": "&apos;",
        })[character]!,
      );

    const ticketImage = `<svg xmlns="http://www.w3.org/2000/svg" width="780" height="250" viewBox="0 0 780 250"><rect width="780" height="250" rx="18" fill="#fff"/><path d="M0 18Q0 0 18 0h744q18 0 18 18v40H0z" fill="#f05b45"/><text x="28" y="37" fill="#fff" font-family="Arial,sans-serif" font-size="18" font-weight="700">ticketing.</text><text x="28" y="91" fill="#828a8d" font-family="Arial,sans-serif" font-size="11" font-weight="700">VÉ ĐIỆN TỬ · ${escape(ticket.tierName.toUpperCase())}</text><text x="28" y="124" fill="#1d2735" font-family="Arial,sans-serif" font-size="21" font-weight="700">${escape(ticket.eventTitle)}</text><text x="28" y="156" fill="#5c6770" font-family="Arial,sans-serif" font-size="13">${escape(formatDate(ticket.date))} · ${escape(ticket.venue)}</text><text x="28" y="185" fill="#5c6770" font-family="Arial,sans-serif" font-size="13">Vé dành cho: ${escape(ticket.holderName)}</text><text x="28" y="220" fill="#92999b" font-family="Arial,sans-serif" font-size="10">MÃ VÉ  ${escape(ticket.code)}</text>${qr}<path d="M556 58v130" stroke="#e5e3de" stroke-dasharray="4 5"/></svg>`;

    const blob = new Blob([ticketImage], { type: "image/svg+xml;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `ticket-${ticket.id}.svg`;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  return (
    <article className="digital-ticket">
      <div className="ticket-poster">
        <img src={ticket.image} alt="" />
        <div className="ticket-poster-overlay" />
        <span>
          ticketing<span className="brand-dot">.</span>
        </span>
      </div>
      <div className="digital-ticket-info">
        <span className={`ticket-status ${ticket.status}`}>
          <i /> {statusLabel}
        </span>
        <h2>{ticket.eventTitle}</h2>
        <div className="digital-ticket-meta">
          <span>
            <CalendarDays size={15} /> {formatDate(ticket.date)}
          </span>
          <span>
            <MapPin size={15} /> {ticket.venue}
          </span>
          <span>
            <Ticket size={15} /> {ticket.tierName}
          </span>
        </div>
        <div className="ticket-owner">
          Vé dành cho <b>{ticket.holderName}</b>
        </div>
        <div className="mt-3 flex flex-wrap items-center gap-2.5">
          {ticket.status !== "cancelled" && (
            <button type="button" className="download-button !mt-0" onClick={download}>
              <ArrowDown size={15} /> Tải vé điện tử
            </button>
          )}
          {ticket.status === "valid" && ticket.orderId && onRequestRefund && (
            refundRequest?.status === "Pending" ? (
              <span className="inline-flex items-center gap-1.5 rounded-full border border-amber-300 bg-amber-50 px-3 py-1.5 text-xs font-semibold text-amber-800">
                <RotateCcw size={13} /> Đang chờ duyệt hoàn tiền
              </span>
            ) : refundRequest?.status === "Approved" ? (
              <span className="inline-flex items-center gap-1.5 rounded-full border border-emerald-300 bg-emerald-50 px-3 py-1.5 text-xs font-semibold text-emerald-800">
                Đã duyệt hoàn tiền
              </span>
            ) : (
              <button
                type="button"
                onClick={() => onRequestRefund(ticket)}
                className="inline-flex items-center gap-1.5 rounded-full border border-rose-200 bg-white px-3.5 py-2 text-xs font-semibold text-rose-700 transition-colors hover:border-rose-300 hover:bg-rose-50 cursor-pointer"
              >
                <RotateCcw size={14} /> Yêu cầu hoàn tiền
              </button>
            )
          )}
        </div>
        {refundRequest?.status === "Rejected" && ticket.status === "valid" && (
          <p className="mt-2 text-xs text-rose-700">
            Yêu cầu hoàn tiền trước đó đã bị từ chối. Bạn có thể gửi lại yêu cầu mới nếu cần.
          </p>
        )}
      </div>
      <div className="digital-ticket-qr">
        <QRCodeSVG id={`qr-${ticket.id}`} value={ticket.code} size={126} level="H" includeMargin />
        <span>{ticket.code}</span>
      </div>
    </article>
  );
}

