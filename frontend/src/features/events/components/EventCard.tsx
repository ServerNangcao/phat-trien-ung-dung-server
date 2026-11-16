import { useState } from "react";
import { Link } from "react-router-dom";
import { ArrowUpRight, CalendarDays, Heart, MapPin } from "lucide-react";
import type { EventItem } from "../../../api/types";

const currency = new Intl.NumberFormat("vi-VN");
const formatMoney = (price: number) => `${currency.format(price)}đ`;
const formatDate = (date?: string) => {
  if (!date) return "Sắp diễn ra";
  const d = date.includes("T") ? new Date(date) : new Date(`${date}T12:00:00`);
  if (isNaN(d.getTime())) return date;
  return new Intl.DateTimeFormat("vi-VN", {
    weekday: "short",
    day: "2-digit",
    month: "short",
  }).format(d);
};

const DEFAULT_COVER = "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?auto=format&fit=crop&w=1400&q=85";

interface EventCardProps {
  event: EventItem;
}

export function EventCard({ event }: EventCardProps) {
  const [isSaved, setIsSaved] = useState(false);
  const lowestPrice =
    event.tiers && event.tiers.length > 0
      ? Math.min(...event.tiers.map((tier) => tier.price))
      : ((event as { minPrice?: number }).minPrice ?? 0);

  return (
    <Link to={`/events/${event.id}`} className="event-card group">
      <div className="event-image-wrap">
        <img
          src={event.image || DEFAULT_COVER}
          alt={event.title}
          className="event-image"
          loading="lazy"
          onError={(e) => {
            (e.currentTarget as HTMLImageElement).src = DEFAULT_COVER;
          }}
        />
        <span className="event-type">{event.category || "Sự kiện"}</span>
        <button
          type="button"
          aria-label="Lưu sự kiện"
          className={`save-button ${isSaved ? "saved" : ""}`}
          onClick={(eventClick) => {
            eventClick.preventDefault();
            setIsSaved((prev) => !prev);
          }}
        >
          <Heart size={17} />
        </button>
      </div>
      <div className="event-meta">
        <span>
          <CalendarDays size={14} /> {formatDate(event.date)} · {event.time}
        </span>
        <span>
          <MapPin size={14} /> {event.location}
        </span>
      </div>
      <h3>{event.title}</h3>
      <p className="event-venue">{event.venue}</p>
      <div className="event-card-footer">
        <span>
          Từ <b>{formatMoney(lowestPrice)}</b>
        </span>
        <span className="event-card-arrow">
          <ArrowUpRight size={17} />
        </span>
      </div>
    </Link>
  );
}

