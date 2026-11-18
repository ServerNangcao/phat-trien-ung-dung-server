import type { ReactNode } from "react";
import { NavLink } from "react-router-dom";
import { BarChart3, Layers, PlusCircle, Users } from "lucide-react";

const TABS = [
  { to: "/organizer/analytics", label: "Doanh thu & Phân tích", icon: BarChart3 },
  { to: "/organizer/builder", label: "Tạo sự kiện", icon: PlusCircle },
  { to: "/organizer/inventory", label: "Hạng vé & Giữ chỗ 10p", icon: Layers },
  { to: "/organizer/orders", label: "Đơn hàng & Tham dự", icon: Users },
];

export function OrganizerLayout({
  title,
  subtitle,
  action,
  children,
}: {
  title: string;
  subtitle: string;
  action?: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="mx-auto max-w-6xl px-5 py-8 lg:px-8">
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <div className="eyebrow muted-eyebrow">BAN TỔ CHỨC · ORGANIZER STUDIO</div>
          <h1 className="section-title">{title}</h1>
          <p className="mt-1 text-sm text-[#647075]">{subtitle}</p>
        </div>
        {action}
      </div>

      <nav
        aria-label="Điều hướng Ban tổ chức"
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
