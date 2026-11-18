import { useState } from "react";
import { Link, Outlet } from "react-router-dom";
import { Menu, Ticket, TicketCheck, X } from "lucide-react";
import type { UserSession } from "../api/types";
import { Footer } from "./Footer";

interface SiteLayoutProps {
  user: UserSession | null;
  logout: () => void;
}

export function SiteLayout({ user, logout }: SiteLayoutProps) {
  const [mobileMenu, setMobileMenu] = useState(false);

  return (
    <>
      <header className="site-header">
        <div className="mx-auto flex h-[76px] max-w-[1240px] items-center justify-between px-5 lg:px-8">
          <Link to="/" className="brand" aria-label="Ticketing trang chủ">
            <span className="brand-mark">
              <Ticket size={21} strokeWidth={2.5} />
            </span>
            <span>
              ticketing<span className="brand-dot">.</span>
            </span>
          </Link>

          <nav className="hidden items-center gap-8 text-[14px] font-semibold text-slate-600 md:flex">
            <a href="/#events" className="hover:text-coral">
              Khám phá
            </a>
            <a href="/#categories" className="hover:text-coral">
              Danh mục
            </a>
            <a href="/#about" className="hover:text-coral">
              Dành cho bạn
            </a>
          </nav>

          <div className="hidden items-center gap-3 md:flex">
            <Link to="/my-tickets" className="header-icon" aria-label="Vé của tôi">
              <TicketCheck size={19} />
              <span>Vé của tôi</span>
            </Link>
            {user ? (
              <div className="flex items-center gap-3">
                <span className="text-sm font-semibold">{user.name.split(" · ")[0]}</span>
                <button className="btn-quiet" onClick={logout}>
                  Đăng xuất
                </button>
              </div>
            ) : (
              <Link to="/login" className="btn-outline">
                Đăng nhập
              </Link>
            )}
            <Link to="/login" className="btn-primary !px-5 !py-2.5">
              Tổ chức sự kiện
            </Link>
          </div>

          <button
            className="icon-button md:hidden"
            onClick={() => setMobileMenu(!mobileMenu)}
            aria-label="Mở menu"
          >
            {mobileMenu ? <X /> : <Menu />}
          </button>
        </div>

        {mobileMenu && (
          <div className="mobile-menu">
            <Link to="/" onClick={() => setMobileMenu(false)}>
              Khám phá
            </Link>
            <Link to="/my-tickets" onClick={() => setMobileMenu(false)}>
              Vé của tôi
            </Link>
            <Link to="/login" onClick={() => setMobileMenu(false)}>
              Đăng nhập
            </Link>
          </div>
        )}
      </header>

      <main>
        <Outlet />
      </main>

      <Footer />
    </>
  );
}

