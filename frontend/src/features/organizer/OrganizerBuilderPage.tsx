import { useState, type ChangeEvent, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { Calendar, Check, CloudUpload, Plus, Trash2 } from "lucide-react";
import { platformDemoApi } from "../../api/platformDemo";
import type { EventCategory } from "../../api/types";
import { OrganizerLayout } from "./OrganizerLayout";

const CATEGORIES: EventCategory[] = ["Âm nhạc", "Nghệ thuật", "Hội thảo", "Ẩm thực", "Thể thao"];
const inputClass =
  "w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm text-[#182226] outline-none transition-colors focus:border-[#F05B45] focus-visible:ring-2 focus-visible:ring-[#F05B45]/25";
const labelClass = "block text-xs font-semibold text-[#526066] mb-1.5";

export function OrganizerBuilderPage() {
  const navigate = useNavigate();
  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [title, setTitle] = useState("Đêm Nhạc Giao Hưởng Mùa Thu — Autumn Symphony");
  const [category, setCategory] = useState<EventCategory>("Âm nhạc");
  const [city, setCity] = useState("Hà Nội");
  const [venue, setVenue] = useState("Nhà hát Hồ Gươm, 40 Hàng Bài, Hoàn Kiếm");
  const [date, setDate] = useState("26/12/2026 · 20:00");
  const [description, setDescription] = useState(
    "Đêm nhạc thính phòng kết hợp ánh sáng nghệ thuật trình diễn các tác phẩm kinh điển trong không gian kiến trúc châu Âu sang trọng.",
  );
  const [posterName, setPosterName] = useState("autumn-symphony-keyvisual-4k.webp");
  const [posterPreview, setPosterPreview] = useState<string>("");
  const [uploadProgress, setUploadProgress] = useState(100);
  const [schedule, setSchedule] = useState([
    { time: "19:00", title: "Đón khách tại sảnh chính", desc: "Thưởng thức tiệc trà nhẹ & check-in" },
    { time: "20:00", title: "Khúc dạo đầu Mùa Thu", desc: "Dàn nhạc giao hưởng trình diễn Chương I" },
  ]);

  const handleFileChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;
    setPosterName(file.name);
    setUploadProgress(40);
    const reader = new FileReader();
    reader.onload = () => {
      setPosterPreview(typeof reader.result === "string" ? reader.result : "");
      setUploadProgress(100);
    };
    reader.readAsDataURL(file);
  };

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    platformDemoApi.createManagedEvent({
      title,
      category,
      city,
      venue,
      date,
      description,
      schedule: schedule.filter((s) => s.title.trim()),
    });
    navigate("/organizer/inventory");
  };

  return (
    <OrganizerLayout
      title="Tạo sự kiện mới"
      subtitle="Hoàn thiện thông tin, tải poster và thiết lập lịch trình trước khi gửi Admin kiểm duyệt."
    >
      <div className="mb-6 flex flex-wrap gap-2">
        {[
          { num: 1 as const, label: "1. Thông tin & Địa điểm" },
          { num: 2 as const, label: "2. Poster & Key Visual" },
          { num: 3 as const, label: "3. Lịch trình & Gửi duyệt" },
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

      <form onSubmit={handleSubmit} className="rounded-2xl border border-slate-200 bg-white p-6 sm:p-8 text-[#182226] space-y-6">
        {step === 1 && (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="sm:col-span-2">
              <label htmlFor="ev-title" className={labelClass}>Tên sự kiện</label>
              <input
                id="ev-title"
                className={inputClass}
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                required
              />
            </div>
            <div>
              <label htmlFor="ev-cat" className={labelClass}>Danh mục</label>
              <select
                id="ev-cat"
                className={inputClass}
                value={category}
                onChange={(e) => setCategory(e.target.value as EventCategory)}
              >
                {CATEGORIES.map((c) => (
                  <option key={c} value={c}>{c}</option>
                ))}
              </select>
            </div>
            <div>
              <label htmlFor="ev-city" className={labelClass}>Thành phố</label>
              <select
                id="ev-city"
                className={inputClass}
                value={city}
                onChange={(e) => setCity(e.target.value)}
              >
                <option value="TP. Hồ Chí Minh">TP. Hồ Chí Minh</option>
                <option value="Hà Nội">Hà Nội</option>
                <option value="Đà Nẵng">Đà Nẵng</option>
              </select>
            </div>
            <div>
              <label htmlFor="ev-venue" className={labelClass}>Địa điểm tổ chức</label>
              <input
                id="ev-venue"
                className={inputClass}
                value={venue}
                onChange={(e) => setVenue(e.target.value)}
                required
              />
            </div>
            <div>
              <label htmlFor="ev-date" className={labelClass}>Thời gian diễn ra</label>
              <input
                id="ev-date"
                className={inputClass}
                value={date}
                onChange={(e) => setDate(e.target.value)}
                required
              />
            </div>
            <div className="sm:col-span-2">
              <label htmlFor="ev-desc" className={labelClass}>Mô tả chi tiết</label>
              <textarea
                id="ev-desc"
                rows={4}
                className={inputClass}
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
                <p className="mt-1 text-xs text-[#526066]">Hỗ trợ PNG, JPG, WebP tối đa 10MB</p>
              </div>
              <input
                id="ev-poster"
                type="file"
                accept="image/*"
                className="sr-only"
                onChange={handleFileChange}
              />
            </label>

            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
              <div className="flex items-center justify-between text-xs font-semibold">
                <span className="text-[#182226]">{posterName}</span>
                <span className="text-emerald-700">{uploadProgress}%</span>
              </div>
              <div className="mt-2 h-2 w-full overflow-hidden rounded-full bg-slate-200">
                <div className="h-full bg-emerald-500 transition-all" style={{ width: `${uploadProgress}%` }} />
              </div>
              {posterPreview && (
                <img
                  src={posterPreview}
                  alt="Xem trước poster"
                  className="mt-4 max-h-56 w-full rounded-xl object-cover"
                />
              )}
            </div>

            <div className="flex justify-between">
              <button type="button" onClick={() => setStep(1)} className="btn-outline">
                Quay lại
              </button>
              <button type="button" onClick={() => setStep(3)} className="btn-primary">
                Tiếp theo: Lịch trình
              </button>
            </div>
          </div>
        )}

        {step === 3 && (
          <div className="space-y-5">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2 text-sm font-bold text-[#182226]">
                <Calendar size={17} className="text-[#F05B45]" />
                <span>Lịch trình chương trình</span>
              </div>
              <button
                type="button"
                onClick={() => setSchedule([...schedule, { time: "21:30", title: "", desc: "" }])}
                className="btn-outline !py-1.5 !px-3 text-xs"
              >
                <Plus size={14} /> Thêm mốc
              </button>
            </div>

            <div className="space-y-3">
              {schedule.map((slot, index) => (
                <div key={index} className="grid grid-cols-1 gap-3 rounded-xl border border-slate-200 bg-slate-50 p-3 sm:grid-cols-12">
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
                    onClick={() => setSchedule(schedule.filter((_, idx) => idx !== index))}
                    className="flex items-center justify-center rounded-xl border border-slate-200 bg-white text-[#526066] hover:border-rose-400 hover:text-rose-600 sm:col-span-1 cursor-pointer"
                  >
                    <Trash2 size={15} />
                  </button>
                </div>
              ))}
            </div>

            <div className="flex justify-between pt-2">
              <button type="button" onClick={() => setStep(2)} className="btn-outline">
                Quay lại
              </button>
              <button type="submit" className="btn-primary">
                <Check size={16} /> Gửi Admin kiểm duyệt
              </button>
            </div>
          </div>
        )}
      </form>
    </OrganizerLayout>
  );
}
