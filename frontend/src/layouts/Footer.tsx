import { Link } from "react-router-dom";
import { Ticket } from "lucide-react";

export function Footer() {
  return (
    <footer id="about" className="footer-wrap">
      <div className="mx-auto flex max-w-[1240px] flex-col gap-6 px-5 py-9 md:flex-row md:items-center md:justify-between lg:px-8">
        <Link to="/" className="brand">
          <span className="brand-mark">
            <Ticket size={19} />
          </span>
          <span>
            ticketing<span className="brand-dot">.</span>
          </span>
        </Link>
        <p className="text-sm text-slate-500">
          Tìm điều khiến bạn háo hức. ✦ © 2026 Ticketing
        </p>
        <div className="flex gap-5 text-sm font-medium text-slate-500">
          <a href="#events">Khám phá</a>
          <a href="mailto:hello@ticketing.vn">Trợ giúp</a>
        </div>
      </div>
    </footer>
  );
}

