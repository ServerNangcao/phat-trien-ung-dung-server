import { useEffect, useMemo, useRef, useState } from "react";
import { Link, Navigate, Outlet, Route, Routes, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { useMutation, useQueries, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { QRCodeSVG } from "qrcode.react";
import {
  ArrowDown, ArrowLeft, ArrowRight, ArrowUpRight, BadgeCheck, CalendarDays, Check,
  ChevronDown, ChevronLeft, ChevronRight, Clock3, Compass, CreditCard, Headphones,
  Heart, LockKeyhole, MapPin, Menu, Minus, Music2, Plus, QrCode, Search, ShieldCheck,
  Sparkles, Ticket, TicketCheck, UserRound, X,
} from "lucide-react";
import type { Html5Qrcode } from "html5-qrcode";
import { apiClient, isDemoMode } from "../api/client";
import { ticketingApi } from "../api/service";
import { EVENT_PAGE_SIZE, type CheckInStatus, type EventItem, type PurchasedTicket, type Reservation, type TicketTier } from "../api/types";

const categories = ["Tất cả", "Âm nhạc", "Nghệ thuật", "Hội thảo", "Ẩm thực", "Thể thao"];
const locations = ["Tất cả địa điểm", "TP. Hồ Chí Minh", "Hà Nội", "Đà Nẵng"];
const currency = new Intl.NumberFormat("vi-VN");
const formatMoney = (price: number) => `${currency.format(price)}đ`;
const formatDate = (date: string) => new Intl.DateTimeFormat("vi-VN", { weekday: "short", day: "2-digit", month: "short" }).format(new Date(`${date}T12:00:00`));

type UserRole = "customer" | "staff";
interface UserSession { name: string; role: UserRole }

function useSession() {
  const [user, setUser] = useState<UserSession | null>(() => {
    const saved = localStorage.getItem("ticketing.user");
    return saved ? JSON.parse(saved) as UserSession : null;
  });
  useEffect(() => {
    const clearSession = () => {
      localStorage.removeItem("ticketing.user");
      setUser(null);
    };
    window.addEventListener("ticketing:session-expired", clearSession);
    return () => window.removeEventListener("ticketing:session-expired", clearSession);
  }, []);
  const login = async (role: UserRole, email: string, password: string) => {
    if (isDemoMode) {
      const demoUser = { name: role === "staff" ? "Minh Anh · Soát vé" : "Linh Nguyễn", role };
      localStorage.setItem("ticketing.user", JSON.stringify(demoUser));
      setUser(demoUser);
      return;
    }
    const { data } = await apiClient.post<{ accessToken: string; refreshToken: string; name: string; role: UserRole }>("/api/auth/login", { email, password });
    localStorage.setItem("ticketing.accessToken", data.accessToken);
    localStorage.setItem("ticketing.refreshToken", data.refreshToken);
    const session = { name: data.name, role: data.role };
    localStorage.setItem("ticketing.user", JSON.stringify(session));
    setUser(session);
  };
  const register = async (name: string, email: string, password: string) => {
    if (isDemoMode) {
      const accounts = JSON.parse(localStorage.getItem("ticketing.demo.accounts") ?? "[]") as Array<{ name: string; email: string }>;
      if (accounts.some((account) => account.email.toLocaleLowerCase() === email.toLocaleLowerCase())) {
        throw new Error("Email này đã được đăng ký.");
      }
      const demoUser = { name, role: "customer" as const };
      localStorage.setItem("ticketing.demo.accounts", JSON.stringify([...accounts, { name, email }]));
      localStorage.setItem("ticketing.user", JSON.stringify(demoUser));
      setUser(demoUser);
      return;
    }
    const { data } = await apiClient.post<{ accessToken: string; refreshToken: string; name: string; role: UserRole }>("/api/auth/register", { name, email, password });
    localStorage.setItem("ticketing.accessToken", data.accessToken);
    localStorage.setItem("ticketing.refreshToken", data.refreshToken);
    const session = { name: data.name, role: data.role };
    localStorage.setItem("ticketing.user", JSON.stringify(session));
    setUser(session);
  };
  const logout = () => {
    localStorage.removeItem("ticketing.user");
    localStorage.removeItem("ticketing.accessToken");
    localStorage.removeItem("ticketing.refreshToken");
    setUser(null);
  };
  return { user, login, register, logout };
}

function App() {
  const session = useSession();
  return (
    <div className="min-h-screen bg-paper text-ink">
      <Routes>
        <Route element={<SiteLayout user={session.user} logout={session.logout} />}>
          <Route path="/" element={<DiscoverPage />} />
          <Route path="/events/:id" element={<EventPage />} />
          <Route path="/checkout/:id" element={<CheckoutPage />} />
          <Route path="/payment/return" element={<PaymentReturn />} />
          <Route path="/my-tickets" element={<MyTicketsPage />} />
          <Route path="/login" element={<LoginPage login={session.login} register={session.register} />} />
        </Route>
        <Route path="/staff/check-in" element={
          <RequireRole role="staff" user={session.user}><StaffPage user={session.user!} logout={session.logout} /></RequireRole>
        } />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </div>
  );
}

function SiteLayout({ user, logout }: { user: UserSession | null; logout: () => void }) {
  const [mobileMenu, setMobileMenu] = useState(false);
  return (
    <>
      <header className="site-header">
        <div className="mx-auto flex h-[76px] max-w-[1240px] items-center justify-between px-5 lg:px-8">
          <Link to="/" className="brand" aria-label="Ticketing trang chủ"><span className="brand-mark"><Ticket size={21} strokeWidth={2.5} /></span><span>ticketing<span className="brand-dot">.</span></span></Link>
          <nav className="hidden items-center gap-8 text-[14px] font-semibold text-slate-600 md:flex">
            <a href="/#events" className="hover:text-coral">Khám phá</a><a href="/#categories" className="hover:text-coral">Danh mục</a><a href="/#about" className="hover:text-coral">Dành cho bạn</a>
          </nav>
          <div className="hidden items-center gap-3 md:flex">
            <Link to="/my-tickets" className="header-icon" aria-label="Vé của tôi"><TicketCheck size={19} /><span>Vé của tôi</span></Link>
            {user ? <div className="flex items-center gap-3"><span className="text-sm font-semibold">{user.name.split(" · ")[0]}</span><button className="btn-quiet" onClick={logout}>Đăng xuất</button></div>
              : <Link to="/login" className="btn-outline">Đăng nhập</Link>}
            <Link to="/login" className="btn-primary !px-5 !py-2.5">Tổ chức sự kiện</Link>
          </div>
          <button className="icon-button md:hidden" onClick={() => setMobileMenu(!mobileMenu)} aria-label="Mở menu">{mobileMenu ? <X /> : <Menu />}</button>
        </div>
        {mobileMenu && <div className="mobile-menu">
          <Link to="/" onClick={() => setMobileMenu(false)}>Khám phá</Link><Link to="/my-tickets" onClick={() => setMobileMenu(false)}>Vé của tôi</Link><Link to="/login" onClick={() => setMobileMenu(false)}>Đăng nhập</Link>
        </div>}
      </header>
      <main><OutletContextSpacer /></main>
      <Footer />
    </>
  );
}

function OutletContextSpacer() {
  return <Outlet />;
}

function Footer() {
  return <footer id="about" className="footer-wrap"><div className="mx-auto flex max-w-[1240px] flex-col gap-6 px-5 py-9 md:flex-row md:items-center md:justify-between lg:px-8"><Link to="/" className="brand"><span className="brand-mark"><Ticket size={19} /></span><span>ticketing<span className="brand-dot">.</span></span></Link><p className="text-sm text-slate-500">Tìm điều khiến bạn háo hức. ✦ © 2026 Ticketing</p><div className="flex gap-5 text-sm font-medium text-slate-500"><a href="#events">Khám phá</a><a href="mailto:hello@ticketing.vn">Trợ giúp</a></div></div></footer>;
}

function DiscoverPage() {
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
  const filters = useMemo(() => ({
    query: debouncedQuery,
    category: category === "Tất cả" ? undefined : category,
    location: location === "Tất cả địa điểm" ? undefined : location,
    maxPrice: price === "all" ? undefined : Number(price),
    page,
    limit: EVENT_PAGE_SIZE,
  }), [category, debouncedQuery, location, page, price]);
  const { data, isLoading, isError } = useQuery({ queryKey: ["events", filters], queryFn: () => ticketingApi.events(filters) });
  const hotEvents = useQueries({
    queries: ["midnight-jazz", "the-sound-of-spring", "saigon-indie-live", "street-food-night"].map((id) => ({
      queryKey: ["event", id],
      queryFn: () => ticketingApi.event(id),
    })),
  });
  const availableHotEvents = hotEvents
    .map((query, index) => ({ event: query.data, index }))
    .filter((entry): entry is { event: EventItem; index: number } => !!entry.event);
  const [hotIndex, setHotIndex] = useState(0);
  const featured = hotEvents[hotIndex]?.data ?? availableHotEvents[0]?.event;
  const totalPages = Math.max(1, Math.ceil((data?.total ?? 0) / EVENT_PAGE_SIZE));
  const handleFilter = (setter: (value: string) => void, value: string) => { setter(value); setPage(1); };
  return (
    <div className="page-enter">
      <section className="hero-wrap mx-auto max-w-[1240px] px-5 pt-7 lg:px-8">
        <div className="hero-panel">
          <div className="hero-copy">
            <div className="eyebrow"><span className="eyebrow-dot" /> ĐI ĐÂU, LÀM GÌ CUỐI TUẦN?</div>
            <h1>Những khoảnh khắc<br />đáng để <em>mong chờ.</em></h1>
            <p>Chọn một sự kiện. Gặp những người thú vị.<br className="hidden sm:block" /> Tạo ra câu chuyện của riêng bạn.</p>
            <a href="#events" className="btn-primary mt-7 inline-flex">Khám phá sự kiện <ArrowRight size={17} /></a>
            <div className="hero-social"><span className="avatar-stack"><i>H</i><i>M</i><i>T</i></span><span><b>2.4k+</b> người đang tìm kế hoạch mới</span></div>
          </div>
          <div className="hero-art" style={{ backgroundImage: `linear-gradient(90deg, rgba(22,57,58,.15), transparent 38%), url("${featured?.image ?? ""}")` }}>
            <span className="hero-tag"><Sparkles size={14} /> TUẦN NÀY ĐANG HOT</span>
            <div className="hero-art-bottom">
              <span className="hero-date"><CalendarDays size={15} /> {featured ? new Intl.DateTimeFormat("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" }).format(new Date(`${featured.date}T12:00:00`)) : "SẮP DIỄN RA"}</span>
              <h2>{featured?.title ?? "Đang tải sự kiện..."}</h2>
              {featured && <Link to={`/events/${featured.id}`} className="hero-link" aria-label={`Xem sự kiện ${featured.title}`}><ArrowUpRight size={19} /></Link>}
            </div>
            <div className="hero-art-note"><Music2 size={15} /> {featured?.organizer ?? "Ticketing tuyển chọn"}</div>
            <div className="hot-event-strip" aria-label="4 sự kiện nổi bật trong tuần">
              {availableHotEvents.map(({ event, index }) => <button
                key={event.id}
                className={`hot-event-chip ${hotIndex === index ? "active" : ""}`}
                onClick={() => setHotIndex(index)}
                aria-pressed={hotIndex === index}
                aria-label={`Xem sự kiện nổi bật: ${event.title}`}
              >
                <img src={event.image} alt="" />
                <span><b>{String(index + 1).padStart(2, "0")}</b><small>{event.title}</small></span>
              </button>)}
            </div>
          </div>
        </div>
        <div className="trust-row"><span><ShieldCheck size={16} /> Thanh toán an toàn</span><span><BadgeCheck size={16} /> Vé điện tử xác thực</span><span><Headphones size={16} /> Hỗ trợ tận tâm</span><span className="trust-note">Điều hay đang chờ bạn <ArrowDown size={14} /></span></div>
      </section>

      <section id="categories" className="mx-auto max-w-[1240px] scroll-mt-24 px-5 pt-12 lg:px-8">
        <div className="section-heading"><div><div className="eyebrow muted-eyebrow">CHỌN TÂM TRẠNG CỦA BẠN</div><h2 className="section-title">Hôm nay bạn muốn <em>đi đâu?</em></h2></div><Link to="#events" className="text-link">Tất cả danh mục <ArrowRight size={16} /></Link></div>
        <div className="category-scroller">
          {[
            { label: "Âm nhạc", mark: "♫", className: "category-music" },
            { label: "Nghệ thuật", mark: "✳", className: "category-art" },
            { label: "Hội thảo", mark: "◉", className: "category-talk" },
            { label: "Ẩm thực", mark: "⌁", className: "category-food" },
            { label: "Thể thao", mark: "↗", className: "category-sport" },
          ].map((item) => <button key={item.label} className={`category-tile ${item.className} ${category === item.label ? "category-selected" : ""}`} onClick={() => handleFilter(setCategory, category === item.label ? "Tất cả" : item.label)}><span className="category-symbol">{item.mark}</span><span>{item.label}</span><ArrowUpRight size={16} className="category-arrow" /></button>)}
        </div>
      </section>

      <section id="events" className="mx-auto max-w-[1240px] scroll-mt-24 px-5 pb-20 pt-16 lg:px-8">
        <div className="section-heading events-heading"><div><div className="eyebrow muted-eyebrow">LỊCH TRÌNH HAY HO</div><h2 className="section-title">Sự kiện dành cho <em>bạn.</em></h2></div><span className="event-count">{data?.total ?? "—"} SỰ KIỆN</span></div>
        <div className="filter-bar">
          <label className="search-field"><Search size={18} /><input value={query} onChange={(event) => handleFilter(setQuery, event.target.value)} placeholder="Bạn đang tìm điều gì?" aria-label="Tìm sự kiện" /><kbd>⌘ K</kbd></label>
          <label className="filter-select"><MapPin size={16} /><select value={location} onChange={(event) => handleFilter(setLocation, event.target.value)} aria-label="Địa điểm">{locations.map((item) => <option key={item}>{item}</option>)}</select><ChevronDown size={14} /></label>
          <label className="filter-select"><CreditCard size={16} /><select value={price} onChange={(event) => handleFilter(setPrice, event.target.value)} aria-label="Khoảng giá"><option value="all">Mọi mức giá</option><option value="300000">Dưới 300.000đ</option><option value="600000">Dưới 600.000đ</option><option value="1000000">Dưới 1.000.000đ</option></select><ChevronDown size={14} /></label>
        </div>
        <div className="category-pills">
          {categories.map((item) => <button key={item} className={`filter-pill ${category === item ? "active" : ""}`} onClick={() => handleFilter(setCategory, item)}>{item}</button>)}
        </div>
        {isError ? <Notice title="Chưa thể tải sự kiện" body="Kết nối API thất bại. Kiểm tra API backend và VITE_API_URL." /> : isLoading ? <div className="event-grid">{[1, 2, 3, 4].map((item) => <div key={item} className="skeleton-card" />)}</div> : data?.items.length ? <>
          <div className="event-grid">
            {data.items.map((event) => <EventCard key={event.id} event={event} />)}
          </div>
          {totalPages > 1 && <div className="pagination"><button disabled={page === 1} aria-label="Trang trước" onClick={() => setPage(page - 1)}><ChevronLeft size={17} /></button><span>Trang <b>{page}</b> / {totalPages}</span><button disabled={page === totalPages} aria-label="Trang sau" onClick={() => setPage(page + 1)}><ChevronRight size={17} /></button></div>}
        </> : <div className="empty-state"><Compass size={28} /><h3>Chưa có sự kiện phù hợp</h3><p>Thử đổi từ khóa hoặc bộ lọc để khám phá thêm nhé.</p><button className="btn-outline" onClick={() => {setQuery(""); setCategory("Tất cả"); setLocation("Tất cả địa điểm"); setPrice("all");}}>Xóa bộ lọc</button></div>}
      </section>
    </div>
  );
}

function EventCard({ event }: { event: EventItem }) {
  const lowestPrice = Math.min(...event.tiers.map((tier) => tier.price));
  return <Link to={`/events/${event.id}`} className="event-card group">
    <div className="event-image-wrap"><img src={event.image} alt="" className="event-image" loading="lazy" /><span className="event-type">{event.category}</span><button aria-label="Lưu sự kiện" className="save-button" onClick={(eventClick) => {eventClick.preventDefault(); eventClick.currentTarget.classList.toggle("saved");}}><Heart size={17} /></button></div>
    <div className="event-meta"><span><CalendarDays size={14} /> {formatDate(event.date)} · {event.time}</span><span><MapPin size={14} /> {event.location}</span></div>
    <h3>{event.title}</h3><p className="event-venue">{event.venue}</p>
    <div className="event-card-footer"><span>Từ <b>{formatMoney(lowestPrice)}</b></span><span className="event-card-arrow"><ArrowUpRight size={17} /></span></div>
  </Link>;
}

function EventPage() {
  const { id = "" } = useParams();
  const eventQuery = useQuery({ queryKey: ["event", id], queryFn: () => ticketingApi.event(id) });
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
  return <div className="mx-auto max-w-[1240px] page-enter px-5 pb-20 pt-7 lg:px-8">
    <Link to="/" className="back-link"><ArrowLeft size={16} /> Tất cả sự kiện</Link>
    <div className="detail-hero"><img src={event.image} alt="" /><div className="detail-hero-shade" /><div className="detail-hero-copy"><span className="event-type !static">{event.category}</span><h1>{event.title}</h1><p><CalendarDays size={17} /> {formatDate(event.date)} · {event.time}<span className="detail-divider">/</span><MapPin size={17} /> {event.venue}</p></div></div>
    <div className="detail-layout">
      <article className="detail-content">
        <div className="organizer-line"><div className="organizer-avatar">{event.organizer.slice(0, 1)}</div><div><span>ĐƯỢC TỔ CHỨC BỞI</span><b>{event.organizer}</b></div><span className="verified"><BadgeCheck size={16} /> Đã xác thực</span></div>
        <div className="prose-copy"><h2>Về sự kiện này</h2><p>{event.description}</p><p>Hẹn gặp bạn ở một buổi tối thật khác. Đừng quên đến sớm để tận hưởng trọn vẹn không khí và chuẩn bị sẵn vé điện tử trên điện thoại.</p><h2>Lịch trình</h2><div className="timeline"><span>18:30</span><p><b>Mở cửa đón khách</b><br />Check-in và làm quen với không gian</p></div><div className="timeline"><span>19:30</span><p><b>Chương trình bắt đầu</b><br />Trải nghiệm chính thức mở màn</p></div><h2>Địa điểm</h2><div className="map-card"><div className="map-grid"><MapPin size={24} /></div><div><b>{event.venue}</b><span>{event.location}</span></div><a href={`https://maps.google.com/?q=${encodeURIComponent(event.venue)}`} target="_blank" rel="noreferrer">Mở bản đồ <ArrowUpRight size={14} /></a></div></div>
      </article>
      <aside className="ticket-panel"><div className="ticket-panel-heading"><span>VÉ THAM DỰ</span><div className="stock-status"><i /> Đang mở bán</div></div><h2>Chọn trải nghiệm<br />của bạn.</h2>
        <div className="tier-list">{event.tiers.map((item) => <TicketTierOption key={item.id} tier={item} selected={tier.id === item.id} onSelect={() => setSelectedTier(item.id)} />)}</div>
        <div className="quantity-row"><div><b>Số lượng vé</b><small>Tối đa 6 vé / đơn</small></div><div className="quantity-picker"><button disabled={quantity <= 1} onClick={() => setQuantity(quantity - 1)} aria-label="Giảm số lượng"><Minus size={15} /></button><b>{quantity}</b><button disabled={quantity >= 6 || quantity >= tier.remaining} onClick={() => setQuantity(quantity + 1)} aria-label="Tăng số lượng"><Plus size={15} /></button></div></div>
        <div className="total-row"><span>Tổng cộng</span><b>{formatMoney(tier.price * quantity)}</b></div>
        <button className="btn-primary w-full justify-center" disabled={reserveMutation.isPending} onClick={() => reserveMutation.mutate()}>{reserveMutation.isPending ? "Đang giữ vé..." : "Giữ vé & tiếp tục"} <ArrowRight size={17} /></button>
        {reserveMutation.isError && <p className="form-error">{(reserveMutation.error as Error).message}</p>}
        <p className="secure-note"><LockKeyhole size={13} /> Giữ vé miễn phí trong 10 phút</p>
      </aside>
    </div>
  </div>;
}

function TicketTierOption({ tier, selected, onSelect }: { tier: TicketTier; selected: boolean; onSelect: () => void }) {
  return <button className={`tier-option ${selected ? "selected" : ""}`} onClick={onSelect}><span className={`radio-mark ${selected ? "checked" : ""}`}>{selected && <Check size={12} />}</span><span className="tier-desc"><b>{tier.name}</b><small>{tier.description}</small><small className={tier.remaining < 25 ? "low-stock" : ""}>{tier.remaining < 25 ? "Chỉ còn " : ""}{tier.remaining} vé</small></span><b className="tier-price">{formatMoney(tier.price)}</b></button>;
}

const checkoutSchema = z.object({
  name: z.string().trim().min(2, "Vui lòng nhập họ tên."),
  email: z.string().trim().email("Email chưa đúng định dạng."),
  phone: z.string().trim().regex(/^(0|\+84)[0-9]{9,10}$/, "Số điện thoại chưa đúng định dạng."),
  accepted: z.literal(true, { error: "Vui lòng đồng ý với điều khoản." }),
});
type CheckoutForm = z.infer<typeof checkoutSchema>;

function CheckoutPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [reservation] = useState<Reservation | null>(() => {
    const saved = sessionStorage.getItem("ticketing.reservation");
    return saved ? JSON.parse(saved) as Reservation : null;
  });
  const [seconds, setSeconds] = useState(() => reservation ? Math.max(0, Math.ceil((reservation.expiresAt - Date.now()) / 1000)) : 0);
  const eventQuery = useQuery({ queryKey: ["event", id], queryFn: () => ticketingApi.event(id), enabled: !!reservation && reservation.eventId === id });
  const { register, handleSubmit, formState: { errors } } = useForm<CheckoutForm>({ resolver: zodResolver(checkoutSchema) });
  useEffect(() => {
    if (!reservation) return;
    const interval = window.setInterval(() => setSeconds(Math.max(0, Math.ceil((reservation.expiresAt - Date.now()) / 1000))), 1000);
    return () => window.clearInterval(interval);
  }, [reservation]);
  const checkoutMutation = useMutation({
    mutationFn: async (form: CheckoutForm) => {
      if (!reservation) throw new Error("Không tìm thấy vé đang giữ.");
      return ticketingApi.checkout({ reservationId: reservation.id, name: form.name, email: form.email, phone: form.phone });
    },
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: ["tickets"] });
      if (isDemoMode) navigate(result.paymentUrl);
      else window.location.assign(result.paymentUrl);
    },
  });
  if (!reservation || reservation.eventId !== id) return <div className="checkout-empty"><Ticket size={32} /><h1>Chưa có vé đang giữ</h1><p>Chọn hạng vé để bắt đầu đặt chỗ nhé.</p><Link to={`/events/${id}`} className="btn-primary">Quay lại sự kiện <ArrowRight size={17} /></Link></div>;
  if (seconds <= 0) return <div className="checkout-empty"><Clock3 size={32} /><h1>Hết thời gian giữ vé</h1><p>Để đảm bảo số lượng vé chính xác, bạn cần chọn vé lại.</p><Link to={`/events/${id}`} className="btn-primary">Chọn vé lại <ArrowRight size={17} /></Link></div>;
  const event = eventQuery.data;
  const tier = event?.tiers.find((item) => item.id === reservation.tierId);
  const time = `${String(Math.floor(seconds / 60)).padStart(2, "0")}:${String(seconds % 60).padStart(2, "0")}`;
  return <div className="mx-auto max-w-[1040px] page-enter px-5 pb-20 pt-9 lg:px-8">
    <Link to={`/events/${id}`} className="back-link"><ArrowLeft size={16} /> Quay lại chọn vé</Link>
    <div className="checkout-heading"><div><div className="eyebrow muted-eyebrow">CHỈ CÒN MỘT BƯỚC</div><h1>Hoàn tất <em>đặt vé.</em></h1></div><div className="countdown-chip"><Clock3 size={17} /><span>Vé được giữ trong <b>{time}</b></span></div></div>
    <form className="checkout-layout" onSubmit={handleSubmit((data) => checkoutMutation.mutate(data))}>
      <section className="checkout-form-card"><div className="step-label"><i>01</i><div><b>Thông tin người tham dự</b><span>Vé điện tử sẽ được gửi đến email này</span></div></div>
        <div className="form-grid"><label className="form-field full"><span>Họ và tên</span><input placeholder="Ví dụ: Nguyễn Minh Anh" autoComplete="name" {...register("name")} />{errors.name && <small className="form-error">{errors.name.message}</small>}</label><label className="form-field"><span>Email nhận vé</span><input placeholder="ban@email.com" type="email" autoComplete="email" {...register("email")} />{errors.email && <small className="form-error">{errors.email.message}</small>}</label><label className="form-field"><span>Số điện thoại</span><input placeholder="09xx xxx xxx" type="tel" autoComplete="tel" {...register("phone")} />{errors.phone && <small className="form-error">{errors.phone.message}</small>}</label></div>
        <div className="step-label payment-label"><i>02</i><div><b>Phương thức thanh toán</b><span>Bảo mật theo tiêu chuẩn thanh toán</span></div></div>
        <div className="payment-choice"><span className="payment-radio" /><span className="vnpay-mark">VNPAY</span><span><b>Ví điện tử / Thẻ ngân hàng</b><small>ATM nội địa, Visa, Mastercard, QR Pay</small></span><ShieldCheck size={17} className="payment-shield" /></div>
        <label className="terms-field"><input type="checkbox" {...register("accepted")} /><span>Tôi đồng ý với <a href="#terms">Điều khoản & Chính sách</a> của Ticketing.</span></label>{errors.accepted && <small className="form-error">{errors.accepted.message}</small>}
        {checkoutMutation.isError && <div className="alert-error">{(checkoutMutation.error as Error).message}</div>}
      </section>
      <aside className="order-summary"><h2>Đơn hàng của bạn</h2>{event && <div className="summary-event"><img src={event.image} alt="" /><div><b>{event.title}</b><span>{formatDate(event.date)} · {event.time}</span><span>{event.venue}</span></div></div>}
        <div className="summary-line"><span>{tier?.name ?? "Vé"} × {reservation.quantity}</span><b>{formatMoney((tier?.price ?? 0) * reservation.quantity)}</b></div><div className="summary-line subtle"><span>Phí dịch vụ</span><b>Miễn phí</b></div><div className="summary-total"><span>Tổng thanh toán</span><b>{formatMoney((tier?.price ?? 0) * reservation.quantity)}</b></div>
        <button className="btn-primary w-full justify-center" disabled={checkoutMutation.isPending}>{checkoutMutation.isPending ? "Đang tạo đơn..." : "Tiếp tục thanh toán"} <ArrowRight size={17} /></button><p className="secure-note"><LockKeyhole size={13} /> Thông tin thanh toán được mã hóa</p>
      </aside>
    </form>
  </div>;
}

function PaymentReturn() {
  const [search] = useSearchParams();
  const success = search.get("status") !== "failed";
  return <div className="payment-return page-enter"><div className={`payment-result-icon ${success ? "success" : "failed"}`}>{success ? <Check size={30} /> : <X size={28} />}</div><div className="eyebrow muted-eyebrow">{success ? "THANH TOÁN THÀNH CÔNG" : "THANH TOÁN CHƯA HOÀN TẤT"}</div><h1>{success ? <>Một trải nghiệm hay<br /><em>đang chờ bạn.</em></> : <>Bạn có thể thử<br /><em>thanh toán lại.</em></>}</h1><p>{success ? "Vé điện tử đã sẵn sàng. Mở vé của bạn để xem mã QR check-in." : "Giao dịch chưa được xác nhận. Vé đang giữ có thể đã hết hạn."}</p><div className="payment-actions"><Link to="/my-tickets" className="btn-primary">Mở vé của tôi <TicketCheck size={17} /></Link><Link to="/" className="btn-outline">Khám phá thêm</Link></div><div className="demo-payment-note">{isDemoMode ? "Đang ở chế độ demo · không có giao dịch tiền thật" : "Kết quả giao dịch do cổng thanh toán trả về"}</div></div>;
}

function MyTicketsPage() {
  const { data, isLoading, isError } = useQuery({ queryKey: ["tickets"], queryFn: ticketingApi.tickets });
  const [filter, setFilter] = useState<"all" | "valid" | "used">("all");
  const visible = (data ?? []).filter((ticket) => filter === "all" || ticket.status === filter);
  return <div className="mx-auto max-w-[1040px] page-enter px-5 pb-20 pt-12 lg:px-8">
    <div className="tickets-head"><div><div className="eyebrow muted-eyebrow">BỘ SƯU TẬP TRẢI NGHIỆM</div><h1 className="section-title">Vé của <em>bạn.</em></h1><p>Những cuộc hẹn hay sắp tới, ở ngay đây.</p></div><div className="ticket-count-badge"><TicketCheck size={19} /><span>{data?.length ?? 0}<small> tấm vé</small></span></div></div>
    <div className="ticket-tabs">{(["all", "valid", "used"] as const).map((value) => <button className={filter === value ? "active" : ""} key={value} onClick={() => setFilter(value)}>{value === "all" ? "Tất cả vé" : value === "valid" ? "Sắp diễn ra" : "Đã sử dụng"}</button>)}</div>
    {isError ? <Notice title="Không thể tải vé" body="API chưa phản hồi. Vui lòng thử lại sau." /> : isLoading ? <PageLoading /> : visible.length === 0 ? <div className="empty-tickets"><div><Ticket size={29} /></div><h2>{data?.length ? "Không có vé trong mục này" : "Hành trình bắt đầu từ đây"}</h2><p>{data?.length ? "Các vé khác của bạn sẽ xuất hiện ở bộ lọc khác." : "Khám phá sự kiện hay và đặt tấm vé đầu tiên nhé."}</p><Link to="/" className="btn-primary">Khám phá sự kiện <ArrowRight size={16} /></Link></div> : <div className="ticket-list">{visible.map((ticket) => <DigitalTicket key={ticket.id} ticket={ticket} />)}</div>}
  </div>;
}

function DigitalTicket({ ticket }: { ticket: PurchasedTicket }) {
  const statusLabel = ticket.status === "used" ? "Đã sử dụng" : ticket.status === "cancelled" ? "Đã hủy" : "Sẵn sàng check-in";
  const download = () => {
    const svg = document.getElementById(`qr-${ticket.id}`);
    if (!svg) return;
    const qr = svg.outerHTML.replace(/<svg\b[^>]*>/, (openingTag) =>
      openingTag.replace(/\s(?:width|height)="[^"]*"/g, "").replace("<svg", '<svg x="575" y="46" width="154" height="154"'));
    const escape = (value: string) => value.replace(/[&<>"']/g, (character) => ({
      "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&apos;",
    })[character]!);
    const ticketImage = `<svg xmlns="http://www.w3.org/2000/svg" width="780" height="250" viewBox="0 0 780 250"><rect width="780" height="250" rx="18" fill="#fff"/><path d="M0 18Q0 0 18 0h744q18 0 18 18v40H0z" fill="#f05b45"/><text x="28" y="37" fill="#fff" font-family="Arial,sans-serif" font-size="18" font-weight="700">ticketing.</text><text x="28" y="91" fill="#828a8d" font-family="Arial,sans-serif" font-size="11" font-weight="700">VÉ ĐIỆN TỬ · ${escape(ticket.tierName.toUpperCase())}</text><text x="28" y="124" fill="#1d2735" font-family="Arial,sans-serif" font-size="21" font-weight="700">${escape(ticket.eventTitle)}</text><text x="28" y="156" fill="#5c6770" font-family="Arial,sans-serif" font-size="13">${escape(formatDate(ticket.date))} · ${escape(ticket.venue)}</text><text x="28" y="185" fill="#5c6770" font-family="Arial,sans-serif" font-size="13">Vé dành cho: ${escape(ticket.holderName)}</text><text x="28" y="220" fill="#92999b" font-family="Arial,sans-serif" font-size="10">MÃ VÉ  ${escape(ticket.code)}</text>${qr}<path d="M556 58v130" stroke="#e5e3de" stroke-dasharray="4 5"/></svg>`;
    const blob = new Blob([ticketImage], { type: "image/svg+xml;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url; anchor.download = `ticket-${ticket.id}.svg`; anchor.click();
    URL.revokeObjectURL(url);
  };
  return <article className="digital-ticket">
    <div className="ticket-poster"><img src={ticket.image} alt="" /><div className="ticket-poster-overlay" /><span>ticketing<span className="brand-dot">.</span></span></div>
    <div className="digital-ticket-info"><span className={`ticket-status ${ticket.status}`}><i /> {statusLabel}</span><h2>{ticket.eventTitle}</h2><div className="digital-ticket-meta"><span><CalendarDays size={15} /> {formatDate(ticket.date)}</span><span><MapPin size={15} /> {ticket.venue}</span><span><Ticket size={15} /> {ticket.tierName}</span></div><div className="ticket-owner">Vé dành cho <b>{ticket.holderName}</b></div>    <button className="download-button" onClick={download}><ArrowDown size={15} /> Tải vé điện tử</button></div>
    <div className="digital-ticket-qr"><QRCodeSVG id={`qr-${ticket.id}`} value={ticket.code} size={126} level="H" includeMargin /><span>{ticket.code}</span></div>
  </article>;
}

function LoginPage({ login, register }: {
  login: (role: UserRole, email: string, password: string) => Promise<void>;
  register: (name: string, email: string, password: string) => Promise<void>;
}) {
  const navigate = useNavigate();
  const [mode, setMode] = useState<"login" | "register">("login");
  const [role, setRole] = useState<UserRole>("customer");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setLoading(true); setError("");
    if (mode === "register") {
      if (name.trim().length < 2) { setError("Vui lòng nhập họ tên (ít nhất 2 ký tự)."); setLoading(false); return; }
      if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) { setError("Email chưa đúng định dạng."); setLoading(false); return; }
      if (password.length < 8) { setError("Mật khẩu cần có ít nhất 8 ký tự."); setLoading(false); return; }
      if (password !== confirmPassword) { setError("Mật khẩu xác nhận chưa khớp."); setLoading(false); return; }
    } else if (!isDemoMode && (!email.trim() || !password)) {
      setError("Vui lòng nhập email và mật khẩu."); setLoading(false); return;
    }
    try {
      if (mode === "register") {
        await register(name.trim(), email.trim(), password);
        navigate("/my-tickets");
      } else {
        await login(role, email, password);
        navigate(role === "staff" ? "/staff/check-in" : "/my-tickets");
      }
    }
    catch (exception) { setError((exception as Error).message || (mode === "login" ? "Không thể đăng nhập." : "Không thể tạo tài khoản.")); }
    finally { setLoading(false); }
  };
  const changeMode = (nextMode: "login" | "register") => {
    setMode(nextMode); setError(""); setPassword(""); setConfirmPassword(""); setRole("customer");
  };
  return <div className="login-page page-enter"><div className="login-art"><span>GẶP NHAU Ở MỘT NƠI THẬT HAY.</span><h1>Điều đáng nhớ<br />bắt đầu từ<br /><em>một tấm vé.</em></h1><div className="login-sticker"><Sparkles size={20} /><span>See you<br />out there!</span></div></div><form className="login-form" onSubmit={(event) => void submit(event)}>
    <div className="eyebrow muted-eyebrow">CHÀO MỪNG BẠN</div>
    <div className="auth-tabs" role="tablist" aria-label="Tài khoản"><button type="button" role="tab" aria-selected={mode === "login"} className={mode === "login" ? "active" : ""} onClick={() => changeMode("login")}>Đăng nhập</button><button type="button" role="tab" aria-selected={mode === "register"} className={mode === "register" ? "active" : ""} onClick={() => changeMode("register")}>Đăng ký</button></div>
    <h2>{mode === "login" ? <>Đăng nhập<br />vào Ticketing.</> : <>Tạo tài khoản<br />Ticketing của bạn.</>}</h2>
    <p>{mode === "login" ? "Chọn vai trò để tiếp tục tới không gian phù hợp." : "Đăng ký tài khoản khách hàng để lưu và quản lý vé."}</p>
    {mode === "login" && <div className="role-selector"><button type="button" onClick={() => setRole("customer")} className={role === "customer" ? "selected" : ""}><UserRound size={18} /><span><b>Khách hàng</b><small>Quản lý vé đã mua</small></span><span className="radio-mark">{role === "customer" && <Check size={12} />}</span></button><button type="button" onClick={() => setRole("staff")} className={role === "staff" ? "selected" : ""}><QrCode size={18} /><span><b>Nhân viên check-in</b><small>Quét vé tại sự kiện</small></span><span className="radio-mark">{role === "staff" && <Check size={12} />}</span></button></div>}
    <div className="login-credentials">
      {mode === "register" && <label className="form-field"><span>Họ và tên</span><input autoComplete="name" value={name} onChange={(event) => setName(event.target.value)} placeholder="Ví dụ: Nguyễn Minh Anh" /></label>}
      <label className="form-field"><span>Email</span><input type="email" autoComplete="email" value={email} onChange={(event) => setEmail(event.target.value)} placeholder="ban@email.com" /></label>
      <label className="form-field"><span>Mật khẩu</span><input type="password" autoComplete={mode === "login" ? "current-password" : "new-password"} value={password} onChange={(event) => setPassword(event.target.value)} placeholder={mode === "register" ? "Ít nhất 8 ký tự" : "Nhập mật khẩu"} /></label>
      {mode === "register" && <label className="form-field"><span>Xác nhận mật khẩu</span><input type="password" autoComplete="new-password" value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} placeholder="Nhập lại mật khẩu" /></label>}
    </div>
    {error && <div className="alert-error">{error}</div>}
    <button type="submit" disabled={loading} className="btn-primary w-full justify-center">{loading ? "Đang xử lý..." : mode === "login" ? "Tiếp tục" : "Tạo tài khoản"} <ArrowRight size={17} /></button>
    {isDemoMode && mode === "login" && <p className="login-demo-note">Chế độ demo · đăng nhập thử không cần mật khẩu</p>}
    <Link className="login-back" to="/">← Quay lại khám phá</Link>
  </form></div>;
}

function RequireRole({ role, user, children }: { role: UserRole; user: UserSession | null; children: React.ReactNode }) {
  if (!user || user.role !== role) return <Navigate to="/login" replace />;
  return children;
}

function StaffPage({ user, logout }: { user: UserSession; logout: () => void }) {
  const [result, setResult] = useState<{ status: CheckInStatus; count: number; code: string } | null>(null);
  const [manualCode, setManualCode] = useState("");
  const [cameraError, setCameraError] = useState("");
  const [scannerActive, setScannerActive] = useState(false);
  const [audioEnabled, setAudioEnabled] = useState(false);
  const scanner = useRef<Html5Qrcode | null>(null);
  const sound = useRef<AudioContext | null>(null);
  const processCode = async (code: string) => {
    if (!code.trim()) return;
    try {
      const response = await ticketingApi.checkIn(code.trim());
      setCameraError("");
      setResult({ ...response, code: code.trim() });
      if (audioEnabled) playTone(response.status, sound.current);
    } catch (error) {
      setCameraError(error instanceof Error ? error.message : "Không thể xác thực mã vé.");
    }
  };
  const countQuery = useQuery({ queryKey: ["checkin-count"], queryFn: ticketingApi.checkInCount, refetchInterval: 5_000 });
  useEffect(() => () => {
    if (scanner.current?.isScanning) void scanner.current.stop();
    if (sound.current && sound.current.state !== "closed") void sound.current.close();
  }, []);
  const startScanner = async () => {
    setCameraError("");
    setAudioEnabled(true);
    try {
      const { Html5Qrcode } = await import("html5-qrcode");
      sound.current = new AudioContext();
      await sound.current.resume();
      const nextScanner = new Html5Qrcode("qr-reader");
      scanner.current = nextScanner;
      await nextScanner.start({ facingMode: "environment" }, { fps: 12, qrbox: { width: 250, height: 250 }, aspectRatio: 1 }, (decoded) => {
        void nextScanner.pause(true);
        void processCode(decoded).finally(() => window.setTimeout(() => { if (nextScanner.isScanning) nextScanner.resume(); }, 1800));
      }, () => undefined);
      setScannerActive(true);
    } catch (error) {
      setCameraError(error instanceof Error ? error.message : "Không thể mở camera. Hãy kiểm tra quyền truy cập.");
    }
  };
  const stopScanner = async () => {
    if (scanner.current?.isScanning) await scanner.current.stop();
    setScannerActive(false);
  };
  const outcome = result?.status;
  return <div className="staff-shell page-enter">
    <header className="staff-header"><Link to="/" className="brand"><span className="brand-mark"><Ticket size={20} /></span><span>ticketing<span className="brand-dot">.</span></span><span className="staff-label">GATE</span></Link><div><span className="staff-user"><span className="staff-live" />{user.name}</span><button className="btn-quiet" onClick={logout}>Đăng xuất</button></div></header>
    <div className="staff-content"><div className="staff-title-row"><div><div className="eyebrow muted-eyebrow">MIDNIGHT JAZZ · CỔNG A</div><h1>Soát vé <em>nhanh gọn.</em></h1><p>Đưa mã QR vào khung hình để xác thực vé.</p></div><div className="live-count"><span className="live-indicator" /><span><b>{countQuery.data ?? "—"}</b><small>đã vào cổng</small></span><div className="count-chart">▂▄▃▆▄▇▅█</div></div></div>
      <div className={`scanner-frame ${outcome ? `scanner-${outcome}` : ""}`}><div id="qr-reader" className="qr-reader" />{!scannerActive && !result && <div className="scanner-placeholder"><div className="scanner-crosshair"><QrCode size={42} /></div><span>Khung quét QR</span><small>Giữ mã QR ngay giữa khung hình</small></div>}{result && <div className={`scan-result ${outcome}`}><span className="scan-result-icon">{outcome === "valid" ? <Check size={34} /> : outcome === "used" || outcome === "cancelled" ? <X size={34} /> : <Search size={32} />}</span><b>{outcome === "valid" ? "VÉ HỢP LỆ" : outcome === "used" ? "VÉ ĐÃ ĐƯỢC SỬ DỤNG" : outcome === "cancelled" ? "VÉ ĐÃ BỊ HỦY" : "KHÔNG TÌM THẤY VÉ"}</b><span>{outcome === "valid" ? "Chúc bạn tham dự vui vẻ!" : outcome === "used" ? "Mã vé này đã được check-in trước đó." : outcome === "cancelled" ? "Vé đã bị hủy và không thể check-in." : "Kiểm tra lại mã hoặc liên hệ quản lý."}</span><small>{result.code}</small></div>}<div className="scan-corner c1" /><div className="scan-corner c2" /><div className="scan-corner c3" /><div className="scan-corner c4" /></div>
      {cameraError && <div className="alert-error camera-error">{cameraError}</div>}
      <div className="scanner-actions">{scannerActive ? <button className="btn-outline" onClick={() => void stopScanner()}>Tắt camera</button> : <button className="btn-primary" onClick={() => void startScanner()}><QrCode size={17} /> Bật camera quét vé</button>}<span>hoặc</span><form onSubmit={(event) => {event.preventDefault(); void processCode(manualCode);}} className="manual-code"><input value={manualCode} onChange={(event) => setManualCode(event.target.value)} placeholder="Nhập mã vé thủ công" aria-label="Mã vé" /><button aria-label="Xác thực mã"><ArrowRight size={17} /></button></form></div>
      <div className="staff-tip"><ShieldCheck size={17} /><span><b>Chỉ check-in một lần.</b> Vé sau khi xác thực sẽ không thể sử dụng lại.</span><a href="mailto:hello@ticketing.vn">Cần hỗ trợ?</a></div>
    </div>
  </div>;
}

function playTone(status: CheckInStatus, context: AudioContext | null) {
  try {
    const activeContext = context ?? new AudioContext();
    const oscillator = activeContext.createOscillator();
    const gain = activeContext.createGain();
    oscillator.connect(gain); gain.connect(activeContext.destination);
    oscillator.frequency.value = status === "valid" ? 880 : status === "used" || status === "cancelled" ? 220 : 440;
    gain.gain.value = 0.08;
    oscillator.start(); oscillator.stop(activeContext.currentTime + 0.13);
    if (!context) oscillator.onended = () => void activeContext.close();
  } catch {
    // Audio is a progressive enhancement; visual scan feedback remains available.
  }
}

function Notice({ title, body }: { title: string; body: string }) {
  return <div className="notice-box"><X size={20} /><div><b>{title}</b><p>{body}</p></div></div>;
}

function PageLoading() { return <div className="page-loading"><span className="spinner" /> Đang tải thông tin...</div>; }
function PageMissing() { return <div className="checkout-empty"><Compass size={32} /><h1>Không tìm thấy sự kiện</h1><p>Sự kiện có thể đã kết thúc hoặc không còn mở bán.</p><Link to="/" className="btn-primary">Về trang khám phá <ArrowRight size={17} /></Link></div>; }

export default App;
