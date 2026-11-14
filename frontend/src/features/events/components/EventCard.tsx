import { useState } from "react";
import { Link } from "react-router-dom";
import { ArrowUpRight, CalendarDays, Heart, MapPin } from "lucide-react";
import type { EventItem } from "../../../api/types";

const currency = new Intl.NumberFormat("vi-VN");
const formatMoney = (price: number) => `${currency.format(price)}đ`;
const formatDate = (date: string) =>
  new Intl.DateTimeFormat("vi-VN", {
    weekday: "short",
    day: "2-digit",
    month: "short",
  }).format(new Date(`${date}T12:00:00`));

interface EventCardProps {
  event: EventItem;
}

export function EventCard({ event }: EventCardProps) {
  const [isSaved, setIsSaved] = useState(false);
  const lowestPrice = Math.min(...event.tiers.map((tier) => tier.price));

  return (
    <Link to={`/events/${event.id}`} className="event-card group">
      <div className="event-image-wrap">
        <img src={event.image} alt="" className="event-image" loading="lazy" />
        <span className="event-type">{event.category}</span>
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

