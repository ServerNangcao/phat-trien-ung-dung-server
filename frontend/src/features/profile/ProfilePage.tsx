import { useState, type FormEvent } from "react";
import { Check, Lock, ReceiptText, ShieldCheck, User } from "lucide-react";
import { platformDemoApi } from "../../api/platformDemo";
import type { UserProfile } from "../../api/types";
import { StatusBadge } from "../../components/ui/StatusBadge";

type ProfileTab = "info" | "vat" | "security";

const inputClass =
  "w-full rounded-xl border border-[#E6E4DD] bg-white px-3.5 py-2.5 text-sm text-[#182226] outline-none transition-colors focus:border-[#F05A3E] focus-visible:ring-2 focus-visible:ring-[#F05A3E]/25";
const labelClass = "block text-xs font-semibold text-[#526066] mb-1.5";

export function ProfilePage() {
  const [tab, setTab] = useState<ProfileTab>("info");
  const [profile, setProfile] = useState<UserProfile>(() => platformDemoApi.getProfile());
  const [savedNotice, setSavedNotice] = useState("");
  const [passwordForm, setPasswordForm] = useState({ current: "", next: "", confirm: "" });
  const [passwordError, setPasswordError] = useState("");

  const notify = (msg: string) => {
    setSavedNotice(msg);
    window.setTimeout(() => setSavedNotice(""), 3000);
  };

  const handleSaveProfile = (event: FormEvent) => {
    event.preventDefault();
    platformDemoApi.saveProfile(profile);
    notify(tab === "vat" ? "Đã lưu thông tin xuất hóa đơn VAT." : "Đã cập nhật hồ sơ cá nhân.");
  };

  const handleToggle2FA = () => {
    const updated = { ...profile, twoFactorEnabled: !profile.twoFactorEnabled };
    setProfile(updated);
    platformDemoApi.saveProfile(updated);
    notify(updated.twoFactorEnabled ? "Đã bật xác thực 2 lớp (2FA)." : "Đã tắt xác thực 2 lớp (2FA).");
  };

  const handlePasswordSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (passwordForm.next.length < 8) {
      setPasswordError("Mật khẩu mới cần tối thiểu 8 ký tự.");
      return;
    }
    if (passwordForm.next !== passwordForm.confirm) {
      setPasswordError("Mật khẩu xác nhận không khớp.");
      return;
    }
    setPasswordError("");
    setPasswordForm({ current: "", next: "", confirm: "" });
    notify("Đã đổi mật khẩu thành công.");
  };

  return (
    <div className="mx-auto max-w-5xl px-5 py-8 lg:px-8">
      <div className="mb-8 flex flex-wrap items-end justify-between gap-4">
        <div>
          <div className="eyebrow muted-eyebrow">TÀI KHOẢN CÁ NHÂN</div>
          <h1 className="section-title">
            Hồ sơ &amp; <em>bảo mật.</em>
          </h1>
        </div>
        {savedNotice && (
          <div
            role="status"
            className="inline-flex items-center gap-2 rounded-xl bg-[#182226] px-4 py-2.5 text-xs font-semibold text-white shadow-sm"
          >
            <Check size={15} className="text-emerald-400" />
            {savedNotice}
          </div>
        )}
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
        <aside className="lg:col-span-4 space-y-4">
          <div className="rounded-2xl border border-[#E6E4DD] bg-white p-5">
            <div className="flex items-center gap-3.5">
              <div className="flex h-12 w-12 items-center justify-center rounded-full bg-[#F05A3E]/15 text-lg font-extrabold text-[#F05A3E]">
                {profile.fullName.charAt(0).toUpperCase()}
              </div>
              <div className="min-w-0">
                <p className="truncate font-bold text-[#182226]">{profile.fullName}</p>
                <p className="truncate text-xs text-[#647075]">{profile.email}</p>
              </div>
            </div>
            <div className="mt-4 flex items-center justify-between border-t border-[#EEECE6] pt-3.5 text-xs">
              <span className="text-[#647075]">Bảo mật 2FA</span>
              <StatusBadge tone={profile.twoFactorEnabled ? "success" : "warning"}>
                {profile.twoFactorEnabled ? "Đang bật" : "Chưa bật"}
              </StatusBadge>
            </div>
          </div>

          <nav aria-label="Danh mục hồ sơ" className="rounded-2xl border border-[#E6E4DD] bg-white p-2 space-y-1">
            {[
              { id: "info" as const, label: "Thông tin cá nhân", icon: User },
              { id: "vat" as const, label: "Xuất hóa đơn VAT", icon: ReceiptText },
              { id: "security" as const, label: "Bảo mật & 2FA", icon: ShieldCheck },
            ].map((item) => {
              const Icon = item.icon;
              const active = tab === item.id;
              return (
                <button
                  key={item.id}
                  type="button"
                  onClick={() => setTab(item.id)}
                  className={`flex w-full items-center gap-2.5 rounded-xl px-3.5 py-2.5 text-left text-sm font-semibold transition-colors cursor-pointer ${
                    active
                      ? "bg-[#F05A3E] text-white"
                      : "text-[#334146] hover:bg-[#F3ECE2]/60"
                  }`}
                >
                  <Icon size={16} />
                  <span>{item.label}</span>
                </button>
              );
            })}
          </nav>
        </aside>

        <section className="lg:col-span-8 rounded-2xl border border-[#E6E4DD] bg-white p-6 sm:p-8">
          {tab === "info" && (
            <form onSubmit={handleSaveProfile} className="space-y-5">
              <h2 className="text-lg font-extrabold text-[#182226]">Thông tin cá nhân</h2>
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div>
                  <label htmlFor="pf-name" className={labelClass}>Họ và tên</label>
                  <input
                    id="pf-name"
                    className={inputClass}
                    value={profile.fullName}
                    onChange={(e) => setProfile({ ...profile, fullName: e.target.value })}
                    required
                  />
                </div>
                <div>
                  <label htmlFor="pf-email" className={labelClass}>Email</label>
                  <input
                    id="pf-email"
                    type="email"
                    className={inputClass}
                    value={profile.email}
                    onChange={(e) => setProfile({ ...profile, email: e.target.value })}
                    required
                  />
                </div>
                <div>
                  <label htmlFor="pf-phone" className={labelClass}>Số điện thoại</label>
                  <input
                    id="pf-phone"
                    className={inputClass}
                    value={profile.phone}
                    onChange={(e) => setProfile({ ...profile, phone: e.target.value })}
                  />
                </div>
                <div>
                  <label htmlFor="pf-dob" className={labelClass}>Ngày sinh</label>
                  <input
                    id="pf-dob"
                    type="date"
                    className={inputClass}
                    value={profile.dob}
                    onChange={(e) => setProfile({ ...profile, dob: e.target.value })}
                  />
                </div>
                <div>
                  <label htmlFor="pf-gender" className={labelClass}>Giới tính</label>
                  <select
                    id="pf-gender"
                    className={inputClass}
                    value={profile.gender}
                    onChange={(e) => setProfile({ ...profile, gender: e.target.value as UserProfile["gender"] })}
                  >
                    <option value="Nữ">Nữ</option>
                    <option value="Nam">Nam</option>
                    <option value="Khác">Khác</option>
                  </select>
                </div>
                <div>
                  <label htmlFor="pf-cccd" className={labelClass}>CCCD / Hộ chiếu</label>
                  <input
                    id="pf-cccd"
                    className={inputClass}
                    value={profile.idNumber}
                    onChange={(e) => setProfile({ ...profile, idNumber: e.target.value })}
                  />
                </div>
                <div>
                  <label htmlFor="pf-city" className={labelClass}>Tỉnh / Thành phố</label>
                  <input
                    id="pf-city"
                    className={inputClass}
                    value={profile.city}
                    onChange={(e) => setProfile({ ...profile, city: e.target.value })}
                  />
                </div>
                <div>
                  <label htmlFor="pf-district" className={labelClass}>Quận / Huyện</label>
                  <input
                    id="pf-district"
                    className={inputClass}
                    value={profile.district}
                    onChange={(e) => setProfile({ ...profile, district: e.target.value })}
                  />
                </div>
              </div>
              <div>
                <label htmlFor="pf-address" className={labelClass}>Địa chỉ chi tiết</label>
                <input
                  id="pf-address"
                  className={inputClass}
                  value={profile.address}
                  onChange={(e) => setProfile({ ...profile, address: e.target.value })}
                />
              </div>
              <div className="pt-2">
                <button type="submit" className="btn-primary">Lưu thay đổi</button>
              </div>
            </form>
          )}

          {tab === "vat" && (
            <form onSubmit={handleSaveProfile} className="space-y-5">
              <div>
                <h2 className="text-lg font-extrabold text-[#182226]">Thông tin xuất hóa đơn VAT</h2>
                <p className="mt-1 text-xs text-[#647075]">
                  Dùng cho đơn hàng doanh nghiệp khi thanh toán vé sự kiện.
                </p>
              </div>
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div className="sm:col-span-2">
                  <label htmlFor="vat-company" className={labelClass}>Tên công ty / Tổ chức</label>
                  <input
                    id="vat-company"
                    className={inputClass}
                    value={profile.taxCompany}
                    onChange={(e) => setProfile({ ...profile, taxCompany: e.target.value })}
                  />
                </div>
                <div>
                  <label htmlFor="vat-code" className={labelClass}>Mã số thuế</label>
                  <input
                    id="vat-code"
                    className={inputClass}
                    value={profile.taxCode}
                    onChange={(e) => setProfile({ ...profile, taxCode: e.target.value })}
                  />
                </div>
                <div>
                  <label htmlFor="vat-email" className={labelClass}>Email nhận hóa đơn</label>
                  <input
                    id="vat-email"
                    type="email"
                    className={inputClass}
                    value={profile.taxEmail}
                    onChange={(e) => setProfile({ ...profile, taxEmail: e.target.value })}
                  />
                </div>
                <div className="sm:col-span-2">
                  <label htmlFor="vat-addr" className={labelClass}>Địa chỉ đăng ký kinh doanh</label>
                  <input
                    id="vat-addr"
                    className={inputClass}
                    value={profile.taxAddress}
                    onChange={(e) => setProfile({ ...profile, taxAddress: e.target.value })}
                  />
                </div>
              </div>
              <div className="pt-2">
                <button type="submit" className="btn-primary">Lưu thông tin VAT</button>
              </div>
            </form>
          )}

          {tab === "security" && (
            <div className="space-y-8">
              <div className="flex flex-wrap items-center justify-between gap-4 rounded-2xl border border-[#E6E4DD] bg-[#F9F8F4] p-5">
                <div className="space-y-1">
                  <div className="flex items-center gap-2 font-bold text-[#182226]">
                    <ShieldCheck size={18} className="text-[#F05A3E]" />
                    <span>Xác thực hai lớp (2FA)</span>
                  </div>
                  <p className="text-xs text-[#647075]">
                    Bảo vệ tài khoản bằng mã OTP khi đăng nhập từ thiết bị lạ.
                  </p>
                </div>
                <button
                  type="button"
                  onClick={handleToggle2FA}
                  className={profile.twoFactorEnabled ? "btn-outline" : "btn-primary"}
                >
                  {profile.twoFactorEnabled ? "Tắt 2FA" : "Bật 2FA"}
                </button>
              </div>

              <form onSubmit={handlePasswordSubmit} className="space-y-4">
                <div className="flex items-center gap-2 font-bold text-[#182226]">
                  <Lock size={17} className="text-[#647075]" />
                  <span>Đổi mật khẩu</span>
                </div>
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                  <div>
                    <label htmlFor="pw-cur" className={labelClass}>Mật khẩu hiện tại</label>
                    <input
                      id="pw-cur"
                      type="password"
                      className={inputClass}
                      value={passwordForm.current}
                      onChange={(e) => setPasswordForm({ ...passwordForm, current: e.target.value })}
                      required
                    />
                  </div>
                  <div>
                    <label htmlFor="pw-new" className={labelClass}>Mật khẩu mới</label>
                    <input
                      id="pw-new"
                      type="password"
                      className={inputClass}
                      value={passwordForm.next}
                      onChange={(e) => setPasswordForm({ ...passwordForm, next: e.target.value })}
                      required
                    />
                  </div>
                  <div>
                    <label htmlFor="pw-cfm" className={labelClass}>Xác nhận mật khẩu</label>
                    <input
                      id="pw-cfm"
                      type="password"
                      className={inputClass}
                      value={passwordForm.confirm}
                      onChange={(e) => setPasswordForm({ ...passwordForm, confirm: e.target.value })}
                      required
                    />
                  </div>
                </div>
                {passwordError && <p className="text-xs font-semibold text-rose-600">{passwordError}</p>}
                <button type="submit" className="btn-primary">Cập nhật mật khẩu</button>
              </form>
            </div>
          )}
        </section>
      </div>
    </div>
  );
}
