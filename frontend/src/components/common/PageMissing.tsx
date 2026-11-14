import { Link } from "react-router-dom";
import { ArrowRight, Compass } from "lucide-react";

export function PageMissing() {
  return (
    <div className="checkout-empty">
      <Compass size={32} />
      <h1>Không tìm thấy sự kiện</h1>
      <p>Sự kiện có thể đã kết thúc hoặc không còn mở bán.</p>
      <Link to="/" className="btn-primary">
        Về trang khám phá <ArrowRight size={17} />
      </Link>
    </div>
  );
}

