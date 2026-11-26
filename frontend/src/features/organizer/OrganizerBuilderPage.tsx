import { useEffect, useRef, useState, type ChangeEvent, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, Calendar, Check, CloudUpload, Plus, Ticket, Trash2 } from "lucide-react";
import { formatVnd } from "../../api/platformDemo";
import { ticketingApi } from "../../api/service";
import { OrganizerLayout } from "./OrganizerLayout";

interface DraftTicketTier {
  id: string;
  name: string;
  price: number;
  totalQuantity: number;
  maxPerOrder: number;
}

const inputClass =
  "w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm text-[#182226] outline-none transition-colors focus:border-[#F05B45] focus-visible:ring-2 focus-visible:ring-[#F05B45]/25";
const labelClass = "block text-xs font-semibold text-[#526066] mb-1.5";

function toLocalDateTimeValue(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function getDefaultDateRange() {
  const start = new Date();
  start.setDate(start.getDate() + 7);
  start.setHours(19, 0, 0, 0);
  const end = new Date(start.getTime() + 3 * 60 * 60 * 1000);
  return {
    startsAt: toLocalDateTimeValue(start),
    endsAt: toLocalDateTimeValue(end),
  };
}

export function OrganizerBuilderPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const defaults = getDefaultDateRange();

  const { data: categories = [], isLoading: isLoadingCategories } = useQuery({
    queryKey: ["events", "categories"],
    queryFn: ticketingApi.organizer.getCategories,
  });

  const { data: venues = [], isLoading: isLoadingVenues } = useQuery({
    queryKey: ["events", "venues"],
    queryFn: () => ticketingApi.organizer.getVenues(),
  });

  const [step, setStep] = useState<1 | 2 | 3 | 4>(1);
  const [title, setTitle] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [venueMode, setVenueMode] = useState<"existing" | "new">("existing");
  const [venueId, setVenueId] = useState("");
  const [newVenueName, setNewVenueName] = useState("");
  const [newVenueAddress, setNewVenueAddress] = useState("");
  const [newVenueCity, setNewVenueCity] = useState("TP. Hồ Chí Minh");
  const [newVenueCapacity, setNewVenueCapacity] = useState(500);
  const [startsAt, setStartsAt] = useState(defaults.startsAt);
  const [endsAt, setEndsAt] = useState(defaults.endsAt);
  const [description, setDescription] = useState("");
  const [posterFile, setPosterFile] = useState<File | null>(null);
  const [posterName, setPosterName] = useState("");
  const [posterPreview, setPosterPreview] = useState("");
  const [ticketTiers, setTicketTiers] = useState<DraftTicketTier[]>([
    { id: "tier-1", name: "Standard", price: 250000, totalQuantity: 100, maxPerOrder: 4 },
  ]);
  const [schedule, setSchedule] = useState<Array<{ id: string; time: string; title: string; desc: string }>>([
    { id: "slot-1", time: "18:30", title: "Đón khách & Check-in", desc: "Quét mã QR tại cổng" },
  ]);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const createdDraftEventId = useRef<string | null>(null);
  const createdTierIds = useRef<Set<string>>(new Set());

  useEffect(() => {
    if (!categoryId && categories.length > 0) {
      setCategoryId(categories[0].id);
    }
  }, [categories, categoryId]);

  useEffect(() => {
    if (venues.length > 0 && !venueId) {
      setVenueId(venues[0].id);
    } else if (!isLoadingVenues && venues.length === 0) {
      setVenueMode("new");
    }
  }, [venues, venueId, isLoadingVenues]);

  const selectedVenue = venues.find((v) => v.id === venueId);
  const effectiveCapacity =
    venueMode === "existing"
      ? (selectedVenue?.capacity ?? null)
      : newVenueCapacity > 0
        ? newVenueCapacity
        : null;
  const hasEffectiveCapacity = effectiveCapacity != null && effectiveCapacity > 0;
  const totalAllocatedTickets = ticketTiers.reduce(
    (sum, t) => sum + (Number.isFinite(t.totalQuantity) && t.totalQuantity > 0 ? t.totalQuantity : 0),
    0,
  );
  const remainingCapacity = hasEffectiveCapacity ? effectiveCapacity - totalAllocatedTickets : null;
  const isOverCapacity = hasEffectiveCapacity && totalAllocatedTickets > effectiveCapacity;

  const validateTicketTiers = (): string | null => {
    if (ticketTiers.length === 0) {
      return "Vui lòng thêm ít nhất 1 hạng vé và giá vé trước khi gửi duyệt.";
    }

    for (let i = 0; i < ticketTiers.length; i += 1) {
      const tier = ticketTiers[i];
      const tierLabel = tier.name.trim() || `#${i + 1}`;
      if (!tier.name.trim()) {
        return `Vui lòng nhập tên cho hạng vé #${i + 1}.`;
      }
      if (!Number.isFinite(tier.price) || tier.price < 0) {
        return `Giá vé của hạng "${tierLabel}" phải lớn hơn hoặc bằng 0.`;
      }
      if (!Number.isInteger(tier.totalQuantity) || tier.totalQuantity <= 0) {
        return `Số lượng phát hành của hạng "${tierLabel}" phải là số nguyên dương.`;
      }
      if (!Number.isInteger(tier.maxPerOrder) || tier.maxPerOrder <= 0 || tier.maxPerOrder > tier.totalQuantity) {
        return `Số vé tối đa/đơn của hạng "${tierLabel}" phải từ 1 đến ${tier.totalQuantity}.`;
      }
    }

    if (hasEffectiveCapacity && totalAllocatedTickets > effectiveCapacity) {
      return `Tổng số lượng vé phát hành (${totalAllocatedTickets.toLocaleString("vi-VN")}) vượt quá sức chứa địa điểm (${effectiveCapacity.toLocaleString("vi-VN")}).`;
    }

    return null;
  };

  const handleFileChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;
    setPosterFile(file);
    setPosterName(file.name);
    const reader = new FileReader();
    reader.onload = () => {
      setPosterPreview(typeof reader.result === "string" ? reader.result : "");
    };
    reader.readAsDataURL(file);
  };

  const createMutation = useMutation({
    mutationFn: async () => {
      if (!title.trim()) throw new Error("Vui lòng nhập tên sự kiện.");
      if (!categoryId) throw new Error("Vui lòng chọn danh mục sự kiện.");

      const startDate = new Date(startsAt);
      const endDate = new Date(endsAt);
      if (isNaN(startDate.getTime()) || isNaN(endDate.getTime()) || endDate <= startDate) {
        throw new Error("Thời gian kết thúc phải sau thời gian bắt đầu.");
      }

      const tierError = validateTicketTiers();
      if (tierError) {
        throw new Error(tierError);
      }

      if (!createdDraftEventId.current) {
        let resolvedVenueId = venueId;
        if (venueMode === "new" || !resolvedVenueId) {
          if (!newVenueName.trim() || !newVenueAddress.trim()) {
            throw new Error("Vui lòng nhập đầy đủ tên và địa chỉ địa điểm tổ chức.");
          }
          resolvedVenueId = await ticketingApi.organizer.createVenue({
            name: newVenueName.trim(),
            address: newVenueAddress.trim(),
            city: newVenueCity,
            capacity: newVenueCapacity > 0 ? newVenueCapacity : undefined,
          });
        }

        const scheduleText = schedule
          .filter((s) => s.title.trim())
          .map((s) => `- ${s.time}: ${s.title}${s.desc.trim() ? ` (${s.desc.trim()})` : ""}`)
          .join("\n");

        const fullDescription = scheduleText
          ? `${description.trim()}\n\nLịch trình:\n${scheduleText}`
          : description.trim();

        const newEventId = await ticketingApi.organizer.createEvent({
          categoryId,
          venueId: resolvedVenueId,
          title: title.trim(),
          description: fullDescription,
          startsAt: startDate.toISOString(),
          endsAt: endDate.toISOString(),
        });

        createdDraftEventId.current = newEventId;

        if (posterFile && ["image/jpeg", "image/png", "image/webp"].includes(posterFile.type)) {
          try {
            await ticketingApi.organizer.uploadCoverImage(newEventId, posterFile);
          } catch (uploadErr) {
            console.warn("[OrganizerBuilder] Bỏ qua lỗi tải ảnh bìa (R2 chưa cấu hình hoặc CORS):", uploadErr);
          }
        }
      }

      const eventId = createdDraftEventId.current;

      for (const tier of ticketTiers) {
        if (createdTierIds.current.has(tier.id)) continue;
        await ticketingApi.organizer.createTicketType(eventId, {
          name: tier.name.trim(),
          price: tier.price,
          totalQuantity: tier.totalQuantity,
          maxPerOrder: tier.maxPerOrder,
        });
        createdTierIds.current.add(tier.id);
      }

      await ticketingApi.organizer.submitEvent(eventId);
      return eventId;
    },
    onSuccess: async (eventId) => {
      createdDraftEventId.current = null;
      createdTierIds.current.clear();
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["organizer", "events"] }),
        queryClient.invalidateQueries({ queryKey: ["events", "venues"] }),
        queryClient.invalidateQueries({ queryKey: ["admin", "events"] }),
      ]);
      navigate(`/organizer/inventory?eventId=${eventId}`);
    },
    onError: (err: unknown) => {
      const msg =
        (err as { response?: { data?: { detail?: string; error?: string; title?: string } }; message?: string })
          ?.response?.data?.detail ??
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        (err as { message?: string })?.message ??
        "Không thể tạo sự kiện. Vui lòng kiểm tra lại thông tin.";
      setErrorMessage(msg);
    },
  });

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    setErrorMessage(null);
    createMutation.mutate();
  };

  return (
    <OrganizerLayout
      title="Tạo sự kiện mới"
      subtitle="Hoàn thiện thông tin, poster, hạng vé & giá vé trước khi gửi Admin kiểm duyệt."
    >
      <div className="mb-6 flex flex-wrap gap-2">
        {[
          { num: 1 as const, label: "1. Thông tin & Địa điểm" },
          { num: 2 as const, label: "2. Poster & Key Visual" },
          { num: 3 as const, label: "3. Hạng vé & Giá vé" },
          { num: 4 as const, label: "4. Lịch trình & Gửi duyệt" },
        ].map((item) => (
          <button
            key={item.num}
            type="button"
            onClick={() => setStep(item.num)}
            className={`rounded-xl px-4 py-2 text-xs font-bold transition-colors cursor-pointer ${
              step === item.num
                ? "bg-[#F05B45] text-white"
                : "border border-slate-200 bg-white text-[#475458] hover:text-[#182226]"
            }`}
          >
            {item.label}
          </button>
        ))}
      </div>

      {errorMessage && (
        <div className="mb-5 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
          {errorMessage}
        </div>
      )}

      <form
        onSubmit={handleSubmit}
        className="rounded-2xl border border-slate-200 bg-white p-6 sm:p-8 text-[#182226] space-y-6"
      >
        {step === 1 && (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="sm:col-span-2">
              <label htmlFor="ev-title" className={labelClass}>
                Tên sự kiện
              </label>
              <input
                id="ev-title"
                className={inputClass}
                placeholder="VD: Đêm Nhạc Acoustic Cuối Tuần"
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                required
              />
            </div>

            <div>
              <label htmlFor="ev-cat" className={labelClass}>
                Danh mục
              </label>
              <select
                id="ev-cat"
                className={inputClass}
                value={categoryId}
                onChange={(e) => setCategoryId(e.target.value)}
                disabled={isLoadingCategories}
                required
              >
                {categories.length === 0 ? (
                  <option value="">Đang tải danh mục...</option>
                ) : (
                  categories.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))
                )}
              </select>
            </div>

            <div>
              <label htmlFor="ev-venue-mode" className={labelClass}>
                Địa điểm tổ chức
              </label>
              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={() => setVenueMode("existing")}
                  disabled={venues.length === 0}
                  className={`flex-1 rounded-xl px-3 py-2.5 text-xs font-bold cursor-pointer border ${
                    venueMode === "existing"
                      ? "border-[#F05B45] bg-[#F05B45]/10 text-[#D94735]"
                      : "border-slate-200 bg-white text-[#526066]"
                  }`}
                >
                  Chọn địa điểm có sẵn
                </button>
                <button
                  type="button"
                  onClick={() => setVenueMode("new")}
                  className={`flex-1 rounded-xl px-3 py-2.5 text-xs font-bold cursor-pointer border ${
                    venueMode === "new"
                      ? "border-[#F05B45] bg-[#F05B45]/10 text-[#D94735]"
                      : "border-slate-200 bg-white text-[#526066]"
                  }`}
                >
                  + Địa điểm mới
                </button>
              </div>
            </div>

            {venueMode === "existing" && venues.length > 0 ? (
              <div className="sm:col-span-2">
                <label htmlFor="ev-venue-select" className={labelClass}>
                  Chọn địa điểm
                </label>
                <select
                  id="ev-venue-select"
                  className={inputClass}
                  value={venueId}
                  onChange={(e) => setVenueId(e.target.value)}
                  required
                >
                  {venues.map((v) => (
                    <option key={v.id} value={v.id}>
                      {v.name} — {v.address}, {v.city}
                      {v.capacity != null && v.capacity > 0
                        ? ` (Sức chứa: ${new Intl.NumberFormat("vi-VN").format(v.capacity)})`
                        : ""}
                    </option>
                  ))}
                </select>
                {selectedVenue?.capacity != null && selectedVenue.capacity > 0 && (
                  <p className="mt-1.5 text-xs text-[#526066]">
                    Sức chứa tối đa của địa điểm:{" "}
                    <span className="font-semibold text-[#182226]">
                      {new Intl.NumberFormat("vi-VN").format(selectedVenue.capacity)} khách
                    </span>{" "}
                    (tổng số vé các hạng không nên vượt quá giới hạn này).
                  </p>
                )}
              </div>
            ) : (
              <>
                <div>
                  <label htmlFor="ev-new-venue-name" className={labelClass}>
                    Tên địa điểm mới
                  </label>
                  <input
                    id="ev-new-venue-name"
                    className={inputClass}
                    placeholder="VD: Nhà hát Hồ Gươm"
                    value={newVenueName}
                    onChange={(e) => setNewVenueName(e.target.value)}
                    required
                  />
                </div>
                <div>
                  <label htmlFor="ev-new-venue-city" className={labelClass}>
                    Thành phố
                  </label>
                  <select
                    id="ev-new-venue-city"
                    className={inputClass}
                    value={newVenueCity}
                    onChange={(e) => setNewVenueCity(e.target.value)}
                  >
                    <option value="TP. Hồ Chí Minh">TP. Hồ Chí Minh</option>
                    <option value="Hà Nội">Hà Nội</option>
                    <option value="Đà Nẵng">Đà Nẵng</option>
                  </select>
                </div>
                <div>
                  <label htmlFor="ev-new-venue-address" className={labelClass}>
                    Địa chỉ cụ thể
                  </label>
                  <input
                    id="ev-new-venue-address"
                    className={inputClass}
                    placeholder="Số nhà, đường, quận/huyện..."
                    value={newVenueAddress}
                    onChange={(e) => setNewVenueAddress(e.target.value)}
                    required
                  />
                </div>
                <div>
                  <label htmlFor="ev-new-venue-cap" className={labelClass}>
                    Sức chứa dự kiến
                  </label>
                  <input
                    id="ev-new-venue-cap"
                    type="number"
                    min={10}
                    className={inputClass}
                    value={newVenueCapacity}
                    onChange={(e) => setNewVenueCapacity(Number(e.target.value))}
                  />
                </div>
              </>
            )}

            <div>
              <label htmlFor="ev-start" className={labelClass}>
                Thời gian bắt đầu
              </label>
              <input
                id="ev-start"
                type="datetime-local"
                className={inputClass}
                value={startsAt}
                onChange={(e) => setStartsAt(e.target.value)}
                required
              />
            </div>

            <div>
              <label htmlFor="ev-end" className={labelClass}>
                Thời gian kết thúc
              </label>
              <input
                id="ev-end"
                type="datetime-local"
                className={inputClass}
                value={endsAt}
                onChange={(e) => setEndsAt(e.target.value)}
                required
              />
            </div>

            <div className="sm:col-span-2">
              <label htmlFor="ev-desc" className={labelClass}>
                Mô tả chi tiết
              </label>
              <textarea
                id="ev-desc"
                rows={4}
                className={inputClass}
                placeholder="Giới thiệu nội dung chương trình, nghệ sĩ, lưu ý khi tham gia..."
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                required
              />
            </div>

            <div className="sm:col-span-2 flex justify-end">
              <button type="button" onClick={() => setStep(2)} className="btn-primary">
                Tiếp theo: Upload Poster
              </button>
            </div>
          </div>
        )}

        {step === 2 && (
          <div className="space-y-5">
            <label
              htmlFor="ev-poster"
              className="flex flex-col items-center justify-center gap-3 rounded-2xl border-2 border-dashed border-slate-300 bg-slate-50 p-8 text-center cursor-pointer hover:border-[#F05B45] transition-colors"
            >
              <span className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[#F05B45]/10 text-[#F05B45]">
                <CloudUpload size={24} />
              </span>
              <div>
                <p className="text-sm font-bold text-[#182226]">Chọn ảnh Poster / Key Visual (16:9 hoặc 4:5)</p>
                <p className="mt-1 text-xs text-[#526066]">Hỗ trợ PNG, JPG, WebP tối đa 10MB (không bắt buộc)</p>
              </div>
              <input
                id="ev-poster"
                type="file"
                accept="image/jpeg,image/png,image/webp"
                className="sr-only"
                onChange={handleFileChange}
              />
            </label>

            {posterName && (
              <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
                <div className="flex items-center justify-between text-xs font-semibold">
                  <span className="text-[#182226]">{posterName}</span>
                  <span className="text-emerald-700">Sẵn sàng tải lên</span>
                </div>
                {posterPreview && (
                  <img
                    src={posterPreview}
                    alt="Xem trước poster"
                    className="mt-4 max-h-56 w-full rounded-xl object-cover"
                  />
                )}
              </div>
            )}

            <div className="flex justify-between">
              <button type="button" onClick={() => setStep(1)} className="btn-outline">
                Quay lại
              </button>
              <button type="button" onClick={() => setStep(3)} className="btn-primary">
                Tiếp theo: Hạng vé &amp; Giá vé
              </button>
            </div>
          </div>
        )}

        {step === 3 && (
          <div className="space-y-5">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div className="flex items-center gap-2 text-sm font-bold text-[#182226]">
                <Ticket size={17} className="text-[#F05B45]" />
                <span>Thiết lập hạng vé &amp; giá vé</span>
              </div>
              <button
                type="button"
                onClick={() => {
                  setErrorMessage(null);
                  setTicketTiers([
                    ...ticketTiers,
                    { id: `tier-${Date.now()}`, name: "", price: 500000, totalQuantity: 50, maxPerOrder: 4 },
                  ]);
                }}
                className="btn-outline !py-1.5 !px-3 text-xs"
              >
                <Plus size={14} /> Thêm hạng vé
              </button>
            </div>

            <div
              className={`flex flex-wrap items-center justify-between gap-2 rounded-xl border px-4 py-3 text-xs font-semibold ${
                isOverCapacity
                  ? "border-rose-300 bg-rose-50 text-rose-800"
                  : "border-slate-200 bg-slate-50 text-[#526066]"
              }`}
            >
              <span>
                Đã phân bổ:{" "}
                <span className="font-bold text-[#182226]">
                  {hasEffectiveCapacity
                    ? `${totalAllocatedTickets.toLocaleString("vi-VN")} / ${effectiveCapacity.toLocaleString("vi-VN")} sức chứa`
                    : `${totalAllocatedTickets.toLocaleString("vi-VN")} vé`}
                </span>
                {hasEffectiveCapacity && !isOverCapacity && remainingCapacity !== null && (
                  <span> (còn {remainingCapacity.toLocaleString("vi-VN")} vé)</span>
                )}
              </span>
              <span>Bắt buộc có ít nhất 1 hạng vé trước khi gửi Admin duyệt.</span>
            </div>

            {isOverCapacity && (
              <div className="flex items-start gap-2 rounded-xl border border-rose-300 bg-rose-50 px-4 py-3 text-xs font-semibold text-rose-700">
                <AlertTriangle size={15} className="mt-0.5 shrink-0" />
                <span>
                  Tổng số lượng vé phát hành ({totalAllocatedTickets.toLocaleString("vi-VN")}) vượt quá sức chứa địa điểm (
                  {effectiveCapacity?.toLocaleString("vi-VN")}). Vui lòng điều chỉnh lại số lượng.
                </span>
              </div>
            )}

            <div className="space-y-3">
              {ticketTiers.map((tier, index) => (
                <div
                  key={tier.id}
                  className="grid grid-cols-1 gap-3 rounded-xl border border-slate-200 bg-slate-50 p-4 sm:grid-cols-12 sm:items-end"
                >
                  <div className="sm:col-span-4">
                    <label htmlFor={`tier-name-${tier.id}`} className={labelClass}>
                      Tên hạng vé #{index + 1}
                    </label>
                    <input
                      id={`tier-name-${tier.id}`}
                      className={inputClass}
                      placeholder="VD: Standard, VIP, Early Bird..."
                      value={tier.name}
                      onChange={(e) => {
                        const next = [...ticketTiers];
                        next[index] = { ...tier, name: e.target.value };
                        setTicketTiers(next);
                      }}
                      required
                    />
                  </div>
                  <div className="sm:col-span-3">
                    <label htmlFor={`tier-price-${tier.id}`} className={labelClass}>
                      Giá vé (VNĐ)
                    </label>
                    <input
                      id={`tier-price-${tier.id}`}
                      type="number"
                      min={0}
                      step={10000}
                      className={inputClass}
                      value={tier.price}
                      onChange={(e) => {
                        const next = [...ticketTiers];
                        next[index] = { ...tier, price: Number(e.target.value) };
                        setTicketTiers(next);
                      }}
                      required
                    />
                    <p className="mt-1 text-[11px] font-semibold text-[#526066]">
                      {tier.price === 0 ? "Miễn phí" : Number.isFinite(tier.price) && tier.price > 0 ? formatVnd(tier.price) : "—"}
                    </p>
                  </div>
                  <div className="sm:col-span-2">
                    <label htmlFor={`tier-qty-${tier.id}`} className={labelClass}>
                      Số lượng phát hành
                    </label>
                    <input
                      id={`tier-qty-${tier.id}`}
                      type="number"
                      min={1}
                      className={inputClass}
                      value={tier.totalQuantity}
                      onChange={(e) => {
                        const next = [...ticketTiers];
                        next[index] = { ...tier, totalQuantity: Number(e.target.value) };
                        setTicketTiers(next);
                      }}
                      required
                    />
                  </div>
                  <div className="sm:col-span-2">
                    <label htmlFor={`tier-max-${tier.id}`} className={labelClass}>
                      Tối đa/đơn
                    </label>
                    <input
                      id={`tier-max-${tier.id}`}
                      type="number"
                      min={1}
                      className={inputClass}
                      value={tier.maxPerOrder}
                      onChange={(e) => {
                        const next = [...ticketTiers];
                        next[index] = { ...tier, maxPerOrder: Number(e.target.value) };
                        setTicketTiers(next);
                      }}
                      required
                    />
                  </div>
                  <div className="sm:col-span-1 flex sm:justify-end">
                    <button
                      type="button"
                      aria-label={`Xóa hạng vé ${tier.name || index + 1}`}
                      disabled={ticketTiers.length <= 1}
                      onClick={() => setTicketTiers(ticketTiers.filter((t) => t.id !== tier.id))}
                      className="flex h-10 w-full sm:w-10 items-center justify-center rounded-xl border border-slate-200 bg-white text-[#526066] hover:border-rose-400 hover:text-rose-600 disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer"
                    >
                      <Trash2 size={15} />
                    </button>
                  </div>
                </div>
              ))}
            </div>

            <div className="flex justify-between pt-2">
              <button type="button" onClick={() => setStep(2)} className="btn-outline">
                Quay lại
              </button>
              <button
                type="button"
                onClick={() => {
                  const err = validateTicketTiers();
                  if (err) {
                    setErrorMessage(err);
                    return;
                  }
                  setErrorMessage(null);
                  setStep(4);
                }}
                className="btn-primary"
              >
                Tiếp theo: Lịch trình
              </button>
            </div>
          </div>
        )}

        {step === 4 && (
          <div className="space-y-5">
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
              <div className="flex flex-wrap items-center justify-between gap-2 mb-3">
                <div className="flex items-center gap-2 text-sm font-bold text-[#182226]">
                  <Ticket size={16} className="text-[#F05B45]" />
                  <span>
                    Tóm tắt hạng vé &amp; giá vé gửi Admin duyệt ({ticketTiers.length} hạng ·{" "}
                    {totalAllocatedTickets.toLocaleString("vi-VN")} vé)
                  </span>
                </div>
                <button
                  type="button"
                  onClick={() => setStep(3)}
                  className="text-xs font-bold text-[#D94735] hover:underline cursor-pointer"
                >
                  Chỉnh sửa hạng vé
                </button>
              </div>
              <div className="flex flex-wrap gap-2">
                {ticketTiers.map((tier) => (
                  <span
                    key={tier.id}
                    className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-[#182226]"
                  >
                    <span>{tier.name.trim() || "Chưa đặt tên"}</span>
                    <span className="text-[#526066]">·</span>
                    <span className="text-[#D94735]">
                      {tier.price === 0 ? "Miễn phí" : formatVnd(tier.price)}
                    </span>
                    <span className="text-[#526066]">·</span>
                    <span>{tier.totalQuantity.toLocaleString("vi-VN")} vé (tối đa {tier.maxPerOrder}/đơn)</span>
                  </span>
                ))}
              </div>
            </div>

            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2 text-sm font-bold text-[#182226]">
                <Calendar size={17} className="text-[#F05B45]" />
                <span>Lịch trình chương trình</span>
              </div>
              <button
                type="button"
                onClick={() =>
                  setSchedule([
                    ...schedule,
                    { id: `slot-${Date.now()}`, time: "20:00", title: "", desc: "" },
                  ])
                }
                className="btn-outline !py-1.5 !px-3 text-xs"
              >
                <Plus size={14} /> Thêm mốc
              </button>
            </div>

            <div className="space-y-3">
              {schedule.map((slot, index) => (
                <div
                  key={slot.id}
                  className="grid grid-cols-1 gap-3 rounded-xl border border-slate-200 bg-slate-50 p-3 sm:grid-cols-12"
                >
                  <input
                    aria-label="Giờ"
                    className={`${inputClass} sm:col-span-2`}
                    value={slot.time}
                    onChange={(e) => {
                      const next = [...schedule];
                      next[index] = { ...slot, time: e.target.value };
                      setSchedule(next);
                    }}
                  />
                  <input
                    aria-label="Tiêu đề mốc"
                    placeholder="Tiêu đề hoạt động"
                    className={`${inputClass} sm:col-span-4`}
                    value={slot.title}
                    onChange={(e) => {
                      const next = [...schedule];
                      next[index] = { ...slot, title: e.target.value };
                      setSchedule(next);
                    }}
                  />
                  <input
                    aria-label="Mô tả mốc"
                    placeholder="Mô tả ngắn"
                    className={`${inputClass} sm:col-span-5`}
                    value={slot.desc}
                    onChange={(e) => {
                      const next = [...schedule];
                      next[index] = { ...slot, desc: e.target.value };
                      setSchedule(next);
                    }}
                  />
                  <button
                    type="button"
                    aria-label="Xóa mốc lịch trình"
                    onClick={() => setSchedule(schedule.filter((s) => s.id !== slot.id))}
                    className="flex items-center justify-center rounded-xl border border-slate-200 bg-white text-[#526066] hover:border-rose-400 hover:text-rose-600 sm:col-span-1 cursor-pointer"
                  >
                    <Trash2 size={15} />
                  </button>
                </div>
              ))}
            </div>

            <div className="flex justify-between pt-2">
              <button type="button" onClick={() => setStep(3)} className="btn-outline">
                Quay lại
              </button>
              <button type="submit" disabled={createMutation.isPending} className="btn-primary">
                <Check size={16} />
                {createMutation.isPending ? "Đang gửi dữ liệu..." : "Gửi Admin kiểm duyệt"}
              </button>
            </div>
          </div>
        )}
      </form>
    </OrganizerLayout>
  );
}
