import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useQueries, useQuery } from "@tanstack/react-query";
import {
  ArrowDown,
  ArrowRight,
  ArrowUpRight,
  BadgeCheck,
  CalendarDays,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Compass,
  CreditCard,
  Headphones,
  MapPin,
  Music2,
  Search,
  ShieldCheck,
  Sparkles,
} from "lucide-react";
import { ticketingApi } from "../../../api/service";
import { EVENT_PAGE_SIZE, type EventItem } from "../../../api/types";
import { Notice } from "../../../components/common/Notice";
import { EventCard } from "../components/EventCard";

const categories = ["Tất cả", "Âm nhạc", "Nghệ thuật", "Hội thảo", "Ẩm thực", "Thể thao"];
const locations = ["Tất cả địa điểm", "TP. Hồ Chí Minh", "Hà Nội", "Đà Nẵng"];

export function DiscoverPage() {
  const [query, setQuery] = useState("");
  const [debouncedQuery, setDebouncedQuery] = useState("");
  const [category, setCategory] = useState("Tất cả");
  const [location, setLocation] = useState("Tất cả địa điểm");
  const [price, setPrice] = useState("all");
  const [page, setPage] = useState(1);

  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedQuery(query), 250);
    return () => window.clearTimeout(timer);
  }, [query]);

  const filters = useMemo(
    () => ({
      query: debouncedQuery,
      category: category === "Tất cả" ? undefined : category,
      location: location === "Tất cả địa điểm" ? undefined : location,
      maxPrice: price === "all" ? undefined : Number(price),
      page,
      limit: EVENT_PAGE_SIZE,
    }),
    [category, debouncedQuery, location, page, price],
  );

  const { data, isLoading, isError } = useQuery({
    queryKey: ["events", filters],
    queryFn: () => ticketingApi.events(filters),
  });

  const hotEvents = useQueries({
    queries: ["midnight-jazz", "the-sound-of-spring", "saigon-indie-live", "street-food-night"].map(
      (id) => ({
        queryKey: ["event", id],
        queryFn: () => ticketingApi.event(id),
      }),
    ),
  });

  const availableHotEvents = hotEvents
    .map((q, index) => ({ event: q.data, index }))
    .filter((entry): entry is { event: EventItem; index: number } => !!entry.event);

  const [hotIndex, setHotIndex] = useState(0);
  const featured = hotEvents[hotIndex]?.data ?? availableHotEvents[0]?.event;
  const totalPages = Math.max(1, Math.ceil((data?.total ?? 0) / EVENT_PAGE_SIZE));

  const handleFilter = (setter: (value: string) => void, value: string) => {
    setter(value);
    setPage(1);
  };

  return (
    <div className="page-enter">
      <section className="hero-wrap mx-auto max-w-[1240px] px-5 pt-7 lg:px-8">
        <div className="hero-panel">
          <div className="hero-copy">
            <div className="eyebrow">
              <span className="eyebrow-dot" /> ĐI ĐÂU, LÀM GÌ CUỐI TUẦN?
            </div>
            <h1>
              Những khoảnh khắc
              <br />
              đáng để <em>mong chờ.</em>
            </h1>
            <p>
              Chọn một sự kiện. Gặp những người thú vị.
              <br className="hidden sm:block" /> Tạo ra câu chuyện của riêng bạn.
            </p>
            <a href="#events" className="btn-primary mt-7 inline-flex">
              Khám phá sự kiện <ArrowRight size={17} />
            </a>
            <div className="hero-social">
              <span className="avatar-stack">
                <i>H</i>
                <i>M</i>
                <i>T</i>
              </span>
              <span>
                <b>2.4k+</b> người đang tìm kế hoạch mới
              </span>
            </div>
          </div>

          <div
            className="hero-art"
            style={{
              backgroundImage: `linear-gradient(90deg, rgba(22,57,58,.15), transparent 38%), url("${featured?.image ?? ""}")`,
            }}
          >
            <span className="hero-tag">
              <Sparkles size={14} /> TUẦN NÀY ĐANG HOT
            </span>
            <div className="hero-art-bottom">
              <span className="hero-date">
                <CalendarDays size={15} />{" "}
                {featured
                  ? new Intl.DateTimeFormat("vi-VN", {
                      day: "2-digit",
                      month: "2-digit",
                      year: "numeric",
                    }).format(new Date(`${featured.date}T12:00:00`))
                  : "SẮP DIỄN RA"}
              </span>
              <h2>{featured?.title ?? "Đang tải sự kiện..."}</h2>
              {featured && (
                <Link
                  to={`/events/${featured.id}`}
                  className="hero-link"
                  aria-label={`Xem sự kiện ${featured.title}`}
                >
                  <ArrowUpRight size={19} />
                </Link>
              )}
            </div>
            <div className="hero-art-note">
              <Music2 size={15} /> {featured?.organizer ?? "Ticketing tuyển chọn"}
            </div>
            <div className="hot-event-strip" aria-label="4 sự kiện nổi bật trong tuần">
              {availableHotEvents.map(({ event, index }) => (
                <button
                  type="button"
                  key={event.id}
                  className={`hot-event-chip ${hotIndex === index ? "active" : ""}`}
                  onClick={() => setHotIndex(index)}
                  aria-pressed={hotIndex === index}
                  aria-label={`Xem sự kiện nổi bật: ${event.title}`}
                >
                  <img src={event.image} alt="" />
                  <span>
                    <b>{String(index + 1).padStart(2, "0")}</b>
                    <small>{event.title}</small>
                  </span>
                </button>
              ))}
            </div>
          </div>
        </div>

        <div className="trust-row">
          <span>
            <ShieldCheck size={16} /> Thanh toán an toàn
          </span>
          <span>
            <BadgeCheck size={16} /> Vé điện tử xác thực
          </span>
          <span>
            <Headphones size={16} /> Hỗ trợ tận tâm
          </span>
          <span className="trust-note">
            Điều hay đang chờ bạn <ArrowDown size={14} />
          </span>
        </div>
      </section>

      <section id="categories" className="mx-auto max-w-[1240px] scroll-mt-24 px-5 pt-12 lg:px-8">
        <div className="section-heading">
          <div>
            <div className="eyebrow muted-eyebrow">CHỌN TÂM TRẠNG CỦA BẠN</div>
            <h2 className="section-title">
              Hôm nay bạn muốn <em>đi đâu?</em>
            </h2>
          </div>
          <Link to="#events" className="text-link">
            Tất cả danh mục <ArrowRight size={16} />
          </Link>
        </div>
        <div className="category-scroller">
          {[
            { label: "Âm nhạc", mark: "♫", className: "category-music" },
            { label: "Nghệ thuật", mark: "✳", className: "category-art" },
            { label: "Hội thảo", mark: "◉", className: "category-talk" },
            { label: "Ẩm thực", mark: "⌁", className: "category-food" },
            { label: "Thể thao", mark: "↗", className: "category-sport" },
          ].map((item) => (
            <button
              type="button"
              key={item.label}
              className={`category-tile ${item.className} ${category === item.label ? "category-selected" : ""}`}
              onClick={() => handleFilter(setCategory, category === item.label ? "Tất cả" : item.label)}
            >
              <span className="category-symbol">{item.mark}</span>
              <span>{item.label}</span>
              <ArrowUpRight size={16} className="category-arrow" />
            </button>
          ))}
        </div>
      </section>

      <section id="events" className="mx-auto max-w-[1240px] scroll-mt-24 px-5 pb-20 pt-16 lg:px-8">
        <div className="section-heading events-heading">
          <div>
            <div className="eyebrow muted-eyebrow">LỊCH TRÌNH HAY HO</div>
            <h2 className="section-title">
              Sự kiện dành cho <em>bạn.</em>
            </h2>
          </div>
          <span className="event-count">{data?.total ?? "—"} SỰ KIỆN</span>
        </div>

        <div className="filter-bar">
          <label className="search-field">
            <Search size={18} />
            <input
              value={query}
              onChange={(event) => handleFilter(setQuery, event.target.value)}
              placeholder="Bạn đang tìm điều gì?"
              aria-label="Tìm sự kiện"
            />
            <kbd>⌘ K</kbd>
          </label>
          <label className="filter-select">
            <MapPin size={16} />
            <select
              value={location}
              onChange={(event) => handleFilter(setLocation, event.target.value)}
              aria-label="Địa điểm"
            >
              {locations.map((item) => (
                <option key={item}>{item}</option>
              ))}
            </select>
            <ChevronDown size={14} />
          </label>
          <label className="filter-select">
            <CreditCard size={16} />
            <select
              value={price}
              onChange={(event) => handleFilter(setPrice, event.target.value)}
              aria-label="Khoảng giá"
            >
              <option value="all">Mọi mức giá</option>
              <option value="300000">Dưới 300.000đ</option>
              <option value="600000">Dưới 600.000đ</option>
              <option value="1000000">Dưới 1.000.000đ</option>
            </select>
            <ChevronDown size={14} />
          </label>
        </div>

        <div className="category-pills">
          {categories.map((item) => (
            <button
              type="button"
              key={item}
              className={`filter-pill ${category === item ? "active" : ""}`}
              onClick={() => handleFilter(setCategory, item)}
            >
              {item}
            </button>
          ))}
        </div>

        {isError ? (
          <Notice
            title="Chưa thể tải sự kiện"
            body="Kết nối API thất bại. Kiểm tra API backend và VITE_API_URL."
          />
        ) : isLoading ? (
          <div className="event-grid">
            {[1, 2, 3, 4].map((item) => (
              <div key={item} className="skeleton-card" />
            ))}
          </div>
        ) : data?.items.length ? (
          <>
            <div className="event-grid">
              {data.items.map((event) => (
                <EventCard key={event.id} event={event} />
              ))}
            </div>
            {totalPages > 1 && (
              <div className="pagination">
                <button
                  type="button"
                  disabled={page === 1}
                  aria-label="Trang trước"
                  onClick={() => setPage(page - 1)}
                >
                  <ChevronLeft size={17} />
                </button>
                <span>
                  Trang <b>{page}</b> / {totalPages}
                </span>
                <button
                  type="button"
                  disabled={page === totalPages}
                  aria-label="Trang sau"
                  onClick={() => setPage(page + 1)}
                >
                  <ChevronRight size={17} />
                </button>
              </div>
            )}
          </>
        ) : (
          <div className="empty-state">
            <Compass size={28} />
            <h3>Chưa có sự kiện phù hợp</h3>
            <p>Thử đổi từ khóa hoặc bộ lọc để khám phá thêm nhé.</p>
            <button
              type="button"
              className="btn-outline"
              onClick={() => {
                setQuery("");
                setCategory("Tất cả");
                setLocation("Tất cả địa điểm");
                setPrice("all");
              }}
            >
              Xóa bộ lọc
            </button>
          </div>
        )}
      </section>
    </div>
  );
}

