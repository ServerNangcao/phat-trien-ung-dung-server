import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  ArrowLeft,
  ArrowRight,
  Clock3,
  LockKeyhole,
  ShieldCheck,
  Ticket,
} from "lucide-react";
import { isDemoMode } from "../../../api/client";
import { ticketingApi } from "../../../api/service";
import type { Reservation } from "../../../api/types";
import { checkoutSchema, type CheckoutFormValues } from "../schemas/checkout-schema";

const currency = new Intl.NumberFormat("vi-VN");
const formatMoney = (price: number) => `${currency.format(price)}đ`;
const formatDate = (date: string) =>
  new Intl.DateTimeFormat("vi-VN", {
    weekday: "short",
    day: "2-digit",
    month: "short",
  }).format(new Date(`${date}T12:00:00`));

export function CheckoutPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [reservation] = useState<Reservation | null>(() => {
    const saved = sessionStorage.getItem("ticketing.reservation");
    return saved ? (JSON.parse(saved) as Reservation) : null;
  });

  const [seconds, setSeconds] = useState(() =>
    reservation ? Math.max(0, Math.ceil((reservation.expiresAt - Date.now()) / 1000)) : 0,
  );

  const eventQuery = useQuery({
    queryKey: ["event", id],
    queryFn: () => ticketingApi.event(id),
    enabled: !!reservation && reservation.eventId === id,
  });

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CheckoutFormValues>({
    resolver: zodResolver(checkoutSchema),
  });

  useEffect(() => {
    if (!reservation) return;
    const interval = window.setInterval(
      () => setSeconds(Math.max(0, Math.ceil((reservation.expiresAt - Date.now()) / 1000))),
      1000,
    );
    return () => window.clearInterval(interval);
  }, [reservation]);

  const checkoutMutation = useMutation({
    mutationFn: async (form: CheckoutFormValues) => {
      if (!reservation) throw new Error("Không tìm thấy vé đang giữ.");
      return ticketingApi.checkout({
        reservationId: reservation.id,
        name: form.name,
        email: form.email,
        phone: form.phone,
      });
    },
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: ["tickets"] });
      if (isDemoMode) navigate(result.paymentUrl);
      else window.location.assign(result.paymentUrl);
    },
  });

  if (!reservation || reservation.eventId !== id) {
    return (
      <div className="checkout-empty">
        <Ticket size={32} />
        <h1>Chưa có vé đang giữ</h1>
        <p>Chọn hạng vé để bắt đầu đặt chỗ nhé.</p>
        <Link to={`/events/${id}`} className="btn-primary">
          Quay lại sự kiện <ArrowRight size={17} />
        </Link>
      </div>
    );
  }

  if (seconds <= 0) {
    return (
      <div className="checkout-empty">
        <Clock3 size={32} />
        <h1>Hết thời gian giữ vé</h1>
        <p>Để đảm bảo số lượng vé chính xác, bạn cần chọn vé lại.</p>
        <Link to={`/events/${id}`} className="btn-primary">
          Chọn vé lại <ArrowRight size={17} />
        </Link>
      </div>
    );
  }

  const event = eventQuery.data;
  const tier = event?.tiers.find((item) => item.id === reservation.tierId);
  const time = `${String(Math.floor(seconds / 60)).padStart(2, "0")}:${String(seconds % 60).padStart(2, "0")}`;

  return (
    <div className="mx-auto max-w-[1040px] page-enter px-5 pb-20 pt-9 lg:px-8">
      <Link to={`/events/${id}`} className="back-link">
        <ArrowLeft size={16} /> Quay lại chọn vé
      </Link>

      <div className="checkout-heading">
        <div>
          <div className="eyebrow muted-eyebrow">CHỈ CÒN MỘT BƯỚC</div>
          <h1>
            Hoàn tất <em>đặt vé.</em>
          </h1>
        </div>
        <div className="countdown-chip">
          <Clock3 size={17} />
          <span>
            Vé được giữ trong <b>{time}</b>
          </span>
        </div>
      </div>

      <form
        className="checkout-layout"
        onSubmit={handleSubmit((data) => checkoutMutation.mutate(data))}
      >
        <section className="checkout-form-card">
          <div className="step-label">
            <i>01</i>
            <div>
              <b>Thông tin người tham dự</b>
              <span>Vé điện tử sẽ được gửi đến email này</span>
            </div>
          </div>

          <div className="form-grid">
            <label className="form-field full">
              <span>Họ và tên</span>
              <input
                placeholder="Ví dụ: Nguyễn Minh Anh"
                autoComplete="name"
                {...register("name")}
              />
              {errors.name && <small className="form-error">{errors.name.message}</small>}
            </label>

            <label className="form-field">
              <span>Email nhận vé</span>
              <input
                placeholder="ban@email.com"
                type="email"
                autoComplete="email"
                {...register("email")}
              />
              {errors.email && <small className="form-error">{errors.email.message}</small>}
            </label>

            <label className="form-field">
              <span>Số điện thoại</span>
              <input
                placeholder="09xx xxx xxx"
                type="tel"
                autoComplete="tel"
                {...register("phone")}
              />
              {errors.phone && <small className="form-error">{errors.phone.message}</small>}
            </label>
          </div>

          <div className="step-label payment-label">
            <i>02</i>
            <div>
              <b>Phương thức thanh toán</b>
              <span>Bảo mật theo tiêu chuẩn thanh toán</span>
            </div>
          </div>

          <div className="payment-choice">
            <span className="payment-radio" />
            <span className="vnpay-mark">VNPAY</span>
            <span>
              <b>Ví điện tử / Thẻ ngân hàng</b>
              <small>ATM nội địa, Visa, Mastercard, QR Pay</small>
            </span>
            <ShieldCheck size={17} className="payment-shield" />
          </div>

          <label className="terms-field">
            <input type="checkbox" {...register("accepted")} />
            <span>
              Tôi đồng ý với <a href="#terms">Điều khoản & Chính sách</a> của Ticketing.
            </span>
          </label>
          {errors.accepted && <small className="form-error">{errors.accepted.message}</small>}

          {checkoutMutation.isError && (
            <div className="alert-error">{(checkoutMutation.error as Error).message}</div>
          )}
        </section>

        <aside className="order-summary">
          <h2>Đơn hàng của bạn</h2>
          {event && (
            <div className="summary-event">
              <img src={event.image} alt="" />
              <div>
                <b>{event.title}</b>
                <span>
                  {formatDate(event.date)} · {event.time}
                </span>
                <span>{event.venue}</span>
              </div>
            </div>
          )}

          <div className="summary-line">
            <span>
              {tier?.name ?? "Vé"} × {reservation.quantity}
            </span>
            <b>{formatMoney((tier?.price ?? 0) * reservation.quantity)}</b>
          </div>
          <div className="summary-line subtle">
            <span>Phí dịch vụ</span>
            <b>Miễn phí</b>
          </div>
          <div className="summary-total">
            <span>Tổng thanh toán</span>
            <b>{formatMoney((tier?.price ?? 0) * reservation.quantity)}</b>
          </div>

          <button
            type="submit"
            className="btn-primary w-full justify-center"
            disabled={checkoutMutation.isPending}
          >
            {checkoutMutation.isPending ? "Đang tạo đơn..." : "Tiếp tục thanh toán"}{" "}
            <ArrowRight size={17} />
          </button>
          <p className="secure-note">
            <LockKeyhole size={13} /> Thông tin thanh toán được mã hóa
          </p>
        </aside>
      </form>
    </div>
  );
}

