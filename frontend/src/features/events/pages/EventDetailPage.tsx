import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useMutation, useQuery } from "@tanstack/react-query";
import {
  ArrowLeft,
  ArrowRight,
  ArrowUpRight,
  BadgeCheck,
  CalendarDays,
  LockKeyhole,
  MapPin,
  Minus,
  Plus,
} from "lucide-react";
import { ticketingApi } from "../../../api/service";
import { PageLoading } from "../../../components/common/PageLoading";
import { PageMissing } from "../../../components/common/PageMissing";
import { TicketTierOption } from "../components/TicketTierOption";

const currency = new Intl.NumberFormat("vi-VN");
const formatMoney = (price: number) => `${currency.format(price)}đ`;
const formatDate = (date: string) =>
  new Intl.DateTimeFormat("vi-VN", {
    weekday: "short",
    day: "2-digit",
    month: "short",
  }).format(new Date(`${date}T12:00:00`));

export function EventDetailPage() {
  const { id = "" } = useParams();
  const eventQuery = useQuery({
    queryKey: ["event", id],
    queryFn: () => ticketingApi.event(id),
  });

  const [selectedTier, setSelectedTier] = useState("");
  const [quantity, setQuantity] = useState(1);
  const navigate = useNavigate();

  const reserveMutation = useMutation({
    mutationFn: () => {
      const tierId = selectedTier || eventQuery.data?.tiers[0]?.id;
      if (!tierId) throw new Error("Sự kiện chưa có hạng vé để đặt.");
      return ticketingApi.reserve(id, tierId, quantity);
    },
    onSuccess: (reservation) => {
      sessionStorage.setItem("ticketing.reservation", JSON.stringify(reservation));
      navigate(`/checkout/${id}`);
    },
  });

  if (eventQuery.isLoading) return <PageLoading />;
  if (eventQuery.isError || !eventQuery.data) return <PageMissing />;

  const event = eventQuery.data;
  const tier = event.tiers.find((item) => item.id === selectedTier) ?? event.tiers[0];

  return (
    <div className="mx-auto max-w-[1240px] page-enter px-5 pb-20 pt-7 lg:px-8">
      <Link to="/" className="back-link">
        <ArrowLeft size={16} /> Tất cả sự kiện
      </Link>

      <div className="detail-hero">
        <img src={event.image} alt="" />
        <div className="detail-hero-shade" />
        <div className="detail-hero-copy">
          <span className="event-type !static">{event.category}</span>
          <h1>{event.title}</h1>
          <p>
            <CalendarDays size={17} /> {formatDate(event.date)} · {event.time}
            <span className="detail-divider">/</span>
            <MapPin size={17} /> {event.venue}
          </p>
        </div>
      </div>

      <div className="detail-layout">
        <article className="detail-content">
          <div className="organizer-line">
            <div className="organizer-avatar">{event.organizer.slice(0, 1)}</div>
            <div>
              <span>ĐƯỢC TỔ CHỨC BỞI</span>
              <b>{event.organizer}</b>
            </div>
            <span className="verified">
              <BadgeCheck size={16} /> Đã xác thực
            </span>
          </div>

          <div className="prose-copy">
            <h2>Về sự kiện này</h2>
            <p>{event.description}</p>
            <p>
              Hẹn gặp bạn ở một buổi tối thật khác. Đừng quên đến sớm để tận hưởng trọn vẹn không khí
              và chuẩn bị sẵn vé điện tử trên điện thoại.
            </p>

            <h2>Lịch trình</h2>
            <div className="timeline">
              <span>18:30</span>
              <p>
                <b>Mở cửa đón khách</b>
                <br />
                Check-in và làm quen với không gian
              </p>
            </div>
            <div className="timeline">
              <span>19:30</span>
              <p>
                <b>Chương trình bắt đầu</b>
                <br />
                Trải nghiệm chính thức mở màn
              </p>
            </div>

            <h2>Địa điểm</h2>
            <div className="map-card">
              <div className="map-grid">
                <MapPin size={24} />
              </div>
              <div>
                <b>{event.venue}</b>
                <span>{event.location}</span>
              </div>
              <a
                href={`https://maps.google.com/?q=${encodeURIComponent(event.venue)}`}
                target="_blank"
                rel="noreferrer"
              >
                Mở bản đồ <ArrowUpRight size={14} />
              </a>
            </div>
          </div>
        </article>

        <aside className="ticket-panel">
          <div className="ticket-panel-heading">
            <span>VÉ THAM DỰ</span>
            <div className="stock-status">
              <i /> Đang mở bán
            </div>
          </div>
          <h2>
            Chọn trải nghiệm
            <br />
            của bạn.
          </h2>

          <div className="tier-list">
            {event.tiers.map((item) => (
              <TicketTierOption
                key={item.id}
                tier={item}
                selected={tier.id === item.id}
                onSelect={() => setSelectedTier(item.id)}
              />
            ))}
          </div>

          <div className="quantity-row">
            <div>
              <b>Số lượng vé</b>
              <small>Tối đa 6 vé / đơn</small>
            </div>
            <div className="quantity-picker">
              <button
                type="button"
                disabled={quantity <= 1}
                onClick={() => setQuantity(quantity - 1)}
                aria-label="Giảm số lượng"
              >
                <Minus size={15} />
              </button>
              <b>{quantity}</b>
              <button
                type="button"
                disabled={quantity >= 6 || quantity >= tier.remaining}
                onClick={() => setQuantity(quantity + 1)}
                aria-label="Tăng số lượng"
              >
                <Plus size={15} />
              </button>
            </div>
          </div>

          <div className="total-row">
            <span>Tổng cộng</span>
            <b>{formatMoney(tier.price * quantity)}</b>
          </div>

          <button
            type="button"
            className="btn-primary w-full justify-center"
            disabled={reserveMutation.isPending}
            onClick={() => reserveMutation.mutate()}
          >
            {reserveMutation.isPending ? "Đang giữ vé..." : "Giữ vé & tiếp tục"}{" "}
            <ArrowRight size={17} />
          </button>

          {reserveMutation.isError && (
            <p className="form-error">{(reserveMutation.error as Error).message}</p>
          )}

          <p className="secure-note">
            <LockKeyhole size={13} /> Giữ vé miễn phí trong 10 phút
          </p>
        </aside>
      </div>
    </div>
  );
}

