import type { ReactNode } from "react";
import { NavLink } from "react-router-dom";
import { RefreshCw, ShieldCheck, Users } from "lucide-react";

const TABS = [
  { to: "/admin/moderation", label: "Kiểm duyệt sự kiện", icon: ShieldCheck },
  { to: "/admin/refunds", label: "Xử lý hoàn tiền", icon: RefreshCw },
  { to: "/admin/users", label: "Quản trị tài khoản", icon: Users },
];

export function AdminLayout({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle: string;
  children: ReactNode;
}) {
  return (
    <div className="mx-auto max-w-6xl px-5 py-8 lg:px-8">
      <div className="mb-6">
        <div className="eyebrow muted-eyebrow">QUẢN TRỊ HỆ THỐNG · ADMIN PORTAL</div>
        <h1 className="section-title">{title}</h1>
        <p className="mt-1 text-sm text-[#647075]">{subtitle}</p>
      </div>

      <nav
        aria-label="Điều hướng Quản trị viên"
        className="mb-8 flex gap-2 overflow-x-auto border-b border-[#E6E4DD] pb-3"
      >
        {TABS.map((tab) => {
          const Icon = tab.icon;
          return (
            <NavLink
              key={tab.to}
              to={tab.to}
              className={({ isActive }) =>
                `inline-flex shrink-0 items-center gap-2 rounded-xl px-4 py-2 text-sm font-semibold transition-colors ${
                  isActive
                    ? "bg-[#182226] text-white"
                    : "bg-white text-[#526066] border border-[#E6E4DD] hover:border-[#182226]/30"
                }`
              }
            >
              <Icon size={16} />
              <span>{tab.label}</span>
            </NavLink>
          );
        })}
      </nav>

      {children}
    </div>
  );
}
