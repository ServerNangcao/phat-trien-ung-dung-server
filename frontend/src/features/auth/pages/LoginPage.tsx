import { useEffect, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import axios from "axios";
import { ArrowLeft, ArrowRight, Briefcase, Check, KeyRound, Mail, QrCode, ShieldCheck, Sparkles, UserRound } from "lucide-react";
import { isDemoMode } from "../../../api/client";
import type { ForgotPasswordResponse, ResetPasswordResponse, UserSession } from "../../../api/types";

interface LoginPageProps {
  login: (role: UserSession["role"], email: string, password: string) => Promise<UserSession>;
  register: (name: string, email: string, password: string) => Promise<void>;
  forgotPassword?: (email: string) => Promise<ForgotPasswordResponse>;
  resetPassword?: (email: string, token: string, newPassword: string) => Promise<ResetPasswordResponse>;
}

function extractErrorMessage(exception: unknown, fallback: string): string {
  if (axios.isAxiosError(exception) && exception.response?.data) {
    const data = exception.response.data as {
      detail?: string;
      title?: string;
      errors?: Record<string, string[]>;
      message?: string;
    };

    let msg =
      data.detail ||
      (data.errors && typeof data.errors === "object" ? Object.values(data.errors).flat().join("; ") : undefined) ||
      data.message ||
      data.title;

    if (msg) {
      return msg
        .replace(/Passwords must have at least one non alphanumeric character\.?/gi, "Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt (!, @, #, $...).")
        .replace(/Passwords must have at least one uppercase \('A'-'Z'\)\.?/gi, "Mật khẩu phải chứa ít nhất 1 chữ cái in hoa (A-Z).")
        .replace(/Passwords must have at least one lowercase \('a'-'z'\)\.?/gi, "Mật khẩu phải chứa ít nhất 1 chữ cái thường (a-z).")
        .replace(/Passwords must have at least one digit \('0'-'9'\)\.?/gi, "Mật khẩu phải chứa ít nhất 1 chữ số (0-9).")
        .replace(/Passwords must be at least (\d+) characters\.?/gi, "Mật khẩu phải có ít nhất $1 ký tự.");
    }
  }

  return (exception as Error)?.message || fallback;
}

export function LoginPage({ login, register, forgotPassword, resetPassword }: LoginPageProps) {
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: { pathname?: string; search?: string } } | null)?.from;
  const fromUrl = from?.pathname ? `${from.pathname}${from.search ?? ""}` : null;
  const [mode, setMode] = useState<"login" | "register" | "forgot" | "reset">("login");
  const [role, setRole] = useState<UserSession["role"]>("customer");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [infoMessage, setInfoMessage] = useState("");
  const [demoOtp, setDemoOtp] = useState<string | null>(null);
  const [resendCooldown, setResendCooldown] = useState(0);

  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [token, setToken] = useState("");

  useEffect(() => {
    if (resendCooldown <= 0) {
      return;
    }
    const timer = window.setInterval(() => {
      setResendCooldown((prev) => (prev > 1 ? prev - 1 : 0));
    }, 1000);
    return () => window.clearInterval(timer);
  }, [resendCooldown]);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setLoading(true);
    setError("");

    if (mode === "register") {
      if (name.trim().length < 2) {
        setError("Vui lòng nhập họ tên (ít nhất 2 ký tự).");
        setLoading(false);
        return;
      }
      if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
        setError("Email chưa đúng định dạng.");
        setLoading(false);
        return;
      }
      if (password.length < 8) {
        setError("Mật khẩu cần có ít nhất 8 ký tự.");
        setLoading(false);
        return;
      }
      if (password !== confirmPassword) {
        setError("Mật khẩu xác nhận chưa khớp.");
        setLoading(false);
        return;
      }
    } else if (mode === "forgot") {
      if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
        setError("Vui lòng nhập địa chỉ email hợp lệ.");
        setLoading(false);
        return;
      }
    } else if (mode === "reset") {
      if (!email.trim() || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
        setError("Vui lòng nhập địa chỉ email hợp lệ.");
        setLoading(false);
        return;
      }
      if (!/^\d{6}$/.test(token.trim())) {
        setError("Vui lòng nhập đủ mã xác nhận gồm 6 chữ số.");
        setLoading(false);
        return;
      }
      if (password.length < 8) {
        setError("Mật khẩu mới cần có ít nhất 8 ký tự.");
        setLoading(false);
        return;
      }
      if (password !== confirmPassword) {
        setError("Mật khẩu xác nhận chưa khớp.");
        setLoading(false);
        return;
      }
    } else if (!isDemoMode && (!email.trim() || !password)) {
      setError("Vui lòng nhập email và mật khẩu.");
      setLoading(false);
      return;
    }

    try {
      if (mode === "register") {
        await register(name.trim(), email.trim(), password);
        navigate(fromUrl ?? "/my-tickets", { replace: true });
      } else if (mode === "forgot") {
        if (!forgotPassword) {
          throw new Error("Tính năng quên mật khẩu chưa được cấu hình.");
        }
        const res = await forgotPassword(email.trim());
        setInfoMessage(res.message);
        setResendCooldown(res.cooldownSeconds ?? 60);
        if (res.resetToken) {
          setDemoOtp(res.resetToken);
          setToken(res.resetToken);
        } else {
          setDemoOtp(null);
          setToken("");
        }
        // Chuyển sang màn hình nhập OTP / mật khẩu mới
        setMode("reset");
        setPassword("");
        setConfirmPassword("");
      } else if (mode === "reset") {
        if (!resetPassword) {
          throw new Error("Tính năng đặt lại mật khẩu chưa được cấu hình.");
        }
        const res = await resetPassword(email.trim(), token.trim(), password);
        setInfoMessage(res.message || "Đặt lại mật khẩu thành công. Vui lòng đăng nhập với mật khẩu mới.");
        setMode("login");
        setPassword("");
        setConfirmPassword("");
        setToken("");
        setDemoOtp(null);
      } else {
        const session = await login(role, email, password);
        const destinations: Record<UserSession["role"], string> = {
          customer: "/my-tickets",
          staff: "/staff/check-in",
          organizer: "/organizer/analytics",
          admin: "/admin/moderation",
        };
        navigate(fromUrl ?? destinations[session.role] ?? "/my-tickets", { replace: true });
      }
    } catch (exception) {
      const fallbackMsg =
        mode === "login"
          ? "Không thể đăng nhập."
          : mode === "register"
            ? "Không thể tạo tài khoản."
            : mode === "forgot"
              ? "Không thể gửi yêu cầu đặt lại mật khẩu."
              : "Không thể đặt lại mật khẩu.";
      setError(extractErrorMessage(exception, fallbackMsg));
    } finally {
      setLoading(false);
    }
  };

  const handleResendOtp = async () => {
    if (loading || resendCooldown > 0) {
      return;
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      setError("Vui lòng nhập địa chỉ email hợp lệ.");
      return;
    }
    if (!forgotPassword) {
      setError("Tính năng quên mật khẩu chưa được cấu hình.");
      return;
    }

    setLoading(true);
    setError("");
    try {
      const res = await forgotPassword(email.trim());
      setInfoMessage(res.message);
      setResendCooldown(res.cooldownSeconds ?? 60);
      if (res.resetToken) {
        setDemoOtp(res.resetToken);
        setToken(res.resetToken);
      } else {
        setDemoOtp(null);
      }
    } catch (exception) {
      setError(extractErrorMessage(exception, "Không thể gửi lại mã xác nhận."));
    } finally {
      setLoading(false);
    }
  };

  const changeMode = (nextMode: "login" | "register" | "forgot" | "reset") => {
    setMode(nextMode);
    setError("");
    if (nextMode === "login" || nextMode === "register") {
      setInfoMessage("");
      setDemoOtp(null);
      setResendCooldown(0);
    }
    setPassword("");
    setConfirmPassword("");
    if (nextMode !== "reset") {
      setToken("");
    }
    setRole("customer");
  };

  return (
    <div className="login-page page-enter">
      <div className="login-art">
        <span>GẶP NHAU Ở MỘT NƠI THẬT HAY.</span>
        <h1>
          Điều đáng nhớ
          <br />
          bắt đầu từ
          <br />
          <em>một tấm vé.</em>
        </h1>
        <div className="login-sticker">
          <Sparkles size={20} />
          <span>
            See you
            <br />
            out there!
          </span>
        </div>
      </div>

      <form className="login-form" onSubmit={(event) => void handleSubmit(event)}>
        <div className="eyebrow muted-eyebrow">
          {mode === "forgot" || mode === "reset" ? "BẢO MẬT TÀI KHOẢN" : "CHÀO MỪNG BẠN"}
        </div>

        {(mode === "login" || mode === "register") && (
          <div className="auth-tabs" role="tablist" aria-label="Tài khoản">
            <button
              type="button"
              role="tab"
              aria-selected={mode === "login"}
              className={mode === "login" ? "active" : ""}
              onClick={() => changeMode("login")}
            >
              Đăng nhập
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={mode === "register"}
              className={mode === "register" ? "active" : ""}
              onClick={() => changeMode("register")}
            >
              Đăng ký
            </button>
          </div>
        )}

        {(mode === "forgot" || mode === "reset") && (
          <div style={{ marginBottom: 12 }}>
            <button
              type="button"
              onClick={() => changeMode("login")}
              className="login-back"
              style={{ display: "inline-flex", alignItems: "center", gap: 6, margin: 0, padding: 0 }}
            >
              <ArrowLeft size={14} /> Quay lại đăng nhập
            </button>
          </div>
        )}

        <h2>
          {mode === "login" ? (
            <>
              Đăng nhập
              <br />
              vào Ticketing.
            </>
          ) : mode === "register" ? (
            <>
              Tạo tài khoản
              <br />
              Ticketing của bạn.
            </>
          ) : mode === "forgot" ? (
            <>
              Quên mật khẩu?
            </>
          ) : (
            <>
              Đặt lại mật khẩu mới
            </>
          )}
        </h2>

        <p>
          {mode === "login"
            ? isDemoMode
              ? "Chọn vai trò để tiếp tục tới không gian phù hợp."
              : "Nhập email và mật khẩu để tiếp tục."
            : mode === "register"
              ? "Đăng ký tài khoản khách hàng để lưu và quản lý vé."
              : mode === "forgot"
                ? "Nhập email đã đăng ký để nhận mã xác nhận gồm 6 chữ số qua hộp thư của bạn."
                : "Nhập mã xác nhận 6 chữ số đã gửi về email cùng mật khẩu mới để hoàn tất khôi phục tài khoản."}
        </p>

        {mode === "login" && isDemoMode && (
          <div className="role-selector sm:grid-cols-2">
            <button
              type="button"
              onClick={() => setRole("customer")}
              className={role === "customer" ? "selected" : ""}
            >
              <UserRound size={18} />
              <span>
                <b>Khách hàng</b>
                <small>Quản lý vé đã mua</small>
              </span>
              <span className="radio-mark">{role === "customer" && <Check size={12} />}</span>
            </button>
            <button
              type="button"
              onClick={() => setRole("staff")}
              className={role === "staff" ? "selected" : ""}
            >
              <QrCode size={18} />
              <span>
                <b>Nhân viên check-in</b>
                <small>Quét vé tại sự kiện</small>
              </span>
              <span className="radio-mark">{role === "staff" && <Check size={12} />}</span>
            </button>
            <button
              type="button"
              onClick={() => setRole("organizer")}
              className={role === "organizer" ? "selected" : ""}
            >
              <Briefcase size={18} />
              <span>
                <b>Ban tổ chức</b>
                <small>Tạo &amp; quản lý sự kiện</small>
              </span>
              <span className="radio-mark">{role === "organizer" && <Check size={12} />}</span>
            </button>
            <button
              type="button"
              onClick={() => setRole("admin")}
              className={role === "admin" ? "selected" : ""}
            >
              <ShieldCheck size={18} />
              <span>
                <b>Quản trị viên</b>
                <small>Kiểm duyệt &amp; hoàn tiền</small>
              </span>
              <span className="radio-mark">{role === "admin" && <Check size={12} />}</span>
            </button>
          </div>
        )}

        <div className="login-credentials">
          {mode === "register" && (
            <label className="form-field">
              <span>Họ và tên</span>
              <input
                autoComplete="name"
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder="Ví dụ: Nguyễn Minh Anh"
              />
            </label>
          )}

          <label className="form-field">
            <span>Email</span>
            <input
              type="email"
              autoComplete="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              placeholder="ban@email.com"
            />
          </label>

          {mode === "reset" && (
            <label className="form-field">
              <span>Mã xác nhận (6 chữ số)</span>
              <div style={{ position: "relative" }}>
                <input
                  type="text"
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  maxLength={6}
                  value={token}
                  onChange={(event) => setToken(event.target.value.replace(/\D/g, "").slice(0, 6))}
                  placeholder="Nhập mã 6 số từ email"
                  style={{ letterSpacing: token ? 4 : undefined, fontWeight: token ? 600 : undefined }}
                />
              </div>
            </label>
          )}

          {(mode === "login" || mode === "register" || mode === "reset") && (
            <label className="form-field">
              <span>{mode === "reset" ? "Mật khẩu mới" : "Mật khẩu"}</span>
              <input
                type="password"
                autoComplete={mode === "login" ? "current-password" : "new-password"}
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                placeholder={
                  mode === "register" || mode === "reset"
                    ? "Ít nhất 8 ký tự (hoa, thường, số, ký tự đặc biệt)"
                    : "Nhập mật khẩu"
                }
              />
            </label>
          )}

          {mode === "login" && (
            <div style={{ display: "flex", justifyContent: "flex-end", marginTop: -2 }}>
              <button
                type="button"
                onClick={() => changeMode("forgot")}
                style={{
                  background: "none",
                  border: "none",
                  padding: 0,
                  color: "var(--accent, #c84b36)",
                  fontSize: "11px",
                  fontWeight: 600,
                  cursor: "pointer",
                  textDecoration: "underline",
                }}
              >
                Quên mật khẩu?
              </button>
            </div>
          )}

          {(mode === "register" || mode === "reset") && (
            <label className="form-field">
              <span>{mode === "reset" ? "Xác nhận mật khẩu mới" : "Xác nhận mật khẩu"}</span>
              <input
                type="password"
                autoComplete="new-password"
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
                placeholder="Nhập lại mật khẩu"
              />
            </label>
          )}
        </div>

        {infoMessage && (
          <div className="alert-info" style={{ display: "flex", flexDirection: "column", gap: 4 }}>
            <div>{infoMessage}</div>
            {demoOtp && (
              <div style={{ marginTop: 4 }}>
                Mã OTP thử nghiệm của bạn:{" "}
                <strong
                  style={{
                    letterSpacing: 2,
                    fontSize: "13px",
                    background: "#ffffff",
                    padding: "2px 6px",
                    borderRadius: 4,
                    border: "1px solid #b9ddf5",
                  }}
                >
                  {demoOtp}
                </strong>
              </div>
            )}
          </div>
        )}

        {error && (
          <div className="alert-error" style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            <div>{error}</div>
            {mode === "register" && error.includes("Email này đã được sử dụng") && (
              <div style={{ display: "flex", gap: 8, marginTop: 4 }}>
                <button
                  type="button"
                  onClick={() => changeMode("login")}
                  style={{
                    background: "transparent",
                    border: "1px solid currentColor",
                    borderRadius: 4,
                    padding: "3px 10px",
                    fontSize: "11px",
                    fontWeight: 600,
                    cursor: "pointer",
                    color: "inherit",
                  }}
                >
                  Đăng nhập ngay
                </button>
                <button
                  type="button"
                  onClick={() => changeMode("forgot")}
                  style={{
                    background: "transparent",
                    border: "1px solid currentColor",
                    borderRadius: 4,
                    padding: "3px 10px",
                    fontSize: "11px",
                    fontWeight: 600,
                    cursor: "pointer",
                    color: "inherit",
                  }}
                >
                  Quên mật khẩu?
                </button>
              </div>
            )}
          </div>
        )}

        <button type="submit" disabled={loading} className="btn-primary w-full justify-center">
          {loading
            ? "Đang xử lý..."
            : mode === "login"
              ? "Tiếp tục"
              : mode === "register"
                ? "Tạo tài khoản"
                : mode === "forgot"
                  ? "Gửi mã xác nhận"
                  : "Xác nhận đặt lại mật khẩu"}{" "}
          {mode === "forgot" ? <Mail size={16} /> : mode === "reset" ? <KeyRound size={16} /> : <ArrowRight size={17} />}
        </button>

        {mode === "forgot" && (
          <div style={{ textAlign: "center", marginTop: 8 }}>
            <button
              type="button"
              onClick={() => changeMode("reset")}
              style={{
                background: "none",
                border: "none",
                color: "var(--surface-muted, #7c7774)",
                fontSize: "11px",
                cursor: "pointer",
                textDecoration: "underline",
              }}
            >
              Đã có mã xác nhận? Đặt lại mật khẩu ngay
            </button>
          </div>
        )}

        {mode === "reset" && (
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 8, marginTop: 8 }}>
            <button
              type="button"
              disabled={loading || resendCooldown > 0}
              onClick={() => void handleResendOtp()}
              style={{
                background: "none",
                border: "none",
                color: resendCooldown > 0 ? "var(--surface-muted, #9a9490)" : "var(--accent, #c84b36)",
                fontSize: "11px",
                fontWeight: 600,
                cursor: resendCooldown > 0 || loading ? "not-allowed" : "pointer",
                textDecoration: resendCooldown > 0 ? "none" : "underline",
                padding: 0,
              }}
            >
              {resendCooldown > 0
                ? `Gửi lại mã sau ${resendCooldown}s`
                : "Chưa nhận được mã? Gửi lại mã xác nhận"}
            </button>
            <button
              type="button"
              onClick={() => changeMode("forgot")}
              style={{
                background: "none",
                border: "none",
                color: "var(--surface-muted, #7c7774)",
                fontSize: "11px",
                cursor: "pointer",
                textDecoration: "underline",
                padding: 0,
              }}
            >
              Đổi email khác
            </button>
          </div>
        )}

        {isDemoMode && mode === "login" && (
          <p className="login-demo-note">Chế độ demo · đăng nhập thử không cần mật khẩu</p>
        )}

        <Link className="login-back" to="/">
          ← Quay lại khám phá
        </Link>
      </form>
    </div>
  );
}
