import { Link, useSearchParams } from "react-router-dom";
import { Check, TicketCheck, X } from "lucide-react";
import { isDemoMode } from "../../../api/client";

export function PaymentReturnPage() {
  const [search] = useSearchParams();
  const success = search.get("status") !== "failed";

  return (
    <div className="payment-return page-enter">
      <div className={`payment-result-icon ${success ? "success" : "failed"}`}>
        {success ? <Check size={30} /> : <X size={28} />}
      </div>
      <div className="eyebrow muted-eyebrow">
        {success ? "THANH TOÁN THÀNH CÔNG" : "THANH TOÁN CHƯA HOÀN TẤT"}
      </div>
      <h1>
        {success ? (
          <>
            Một trải nghiệm hay
            <br />
            <em>đang chờ bạn.</em>
          </>
        ) : (
          <>
            Bạn có thể thử
            <br />
            <em>thanh toán lại.</em>
          </>
        )}
      </h1>
      <p>
        {success
          ? "Vé điện tử đã sẵn sàng. Mở vé của bạn để xem mã QR check-in."
          : "Giao dịch chưa được xác nhận. Vé đang giữ có thể đã hết hạn."}
      </p>
      <div className="payment-actions">
        <Link to="/my-tickets" className="btn-primary">
          Mở vé của tôi <TicketCheck size={17} />
        </Link>
        <Link to="/" className="btn-outline">
          Khám phá thêm
        </Link>
      </div>
      <div className="demo-payment-note">
        {isDemoMode
          ? "Đang ở chế độ demo · không có giao dịch tiền thật"
          : "Kết quả giao dịch do cổng thanh toán trả về"}
      </div>
    </div>
  );
}

