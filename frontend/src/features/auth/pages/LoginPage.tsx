import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import axios from "axios";
import { ArrowRight, Briefcase, Check, QrCode, ShieldCheck, Sparkles, UserRound } from "lucide-react";
import { isDemoMode } from "../../../api/client";
import type { UserSession } from "../../../api/types";

interface LoginPageProps {
  login: (role: UserSession["role"], email: string, password: string) => Promise<void>;
  register: (name: string, email: string, password: string) => Promise<void>;
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

export function LoginPage({ login, register }: LoginPageProps) {
  const navigate = useNavigate();
  const [mode, setMode] = useState<"login" | "register">("login");
  const [role, setRole] = useState<UserSession["role"]>("customer");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

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
    } else if (!isDemoMode && (!email.trim() || !password)) {
      setError("Vui lòng nhập email và mật khẩu.");
      setLoading(false);
      return;
    }

    try {
      if (mode === "register") {
        await register(name.trim(), email.trim(), password);
        navigate("/my-tickets");
      } else {
        await login(role, email, password);
        const destinations: Record<UserSession["role"], string> = {
          customer: "/my-tickets",
          staff: "/staff/check-in",
          organizer: "/organizer/analytics",
          admin: "/admin/moderation",
        };
        navigate(destinations[role] ?? "/my-tickets");
      }
    } catch (exception) {
      setError(
        extractErrorMessage(
          exception,
          mode === "login" ? "Không thể đăng nhập." : "Không thể tạo tài khoản.",
        ),
      );
    } finally {
      setLoading(false);
    }
  };

  const changeMode = (nextMode: "login" | "register") => {
    setMode(nextMode);
    setError("");
    setPassword("");
    setConfirmPassword("");
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
        <div className="eyebrow muted-eyebrow">CHÀO MỪNG BẠN</div>
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

        <h2>
          {mode === "login" ? (
            <>
              Đăng nhập
              <br />
              vào Ticketing.
            </>
          ) : (
            <>
              Tạo tài khoản
              <br />
              Ticketing của bạn.
            </>
          )}
        </h2>

        <p>
          {mode === "login"
            ? "Chọn vai trò để tiếp tục tới không gian phù hợp."
            : "Đăng ký tài khoản khách hàng để lưu và quản lý vé."}
        </p>

        {mode === "login" && (
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

          <label className="form-field">
            <span>Mật khẩu</span>
            <input
              type="password"
              autoComplete={mode === "login" ? "current-password" : "new-password"}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              placeholder={mode === "register" ? "Ít nhất 8 ký tự (hoa, thường, số, ký tự đặc biệt)" : "Nhập mật khẩu"}
            />
          </label>

          {mode === "register" && (
            <label className="form-field">
              <span>Xác nhận mật khẩu</span>
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

        {error && <div className="alert-error">{error}</div>}

        <button type="submit" disabled={loading} className="btn-primary w-full justify-center">
          {loading ? "Đang xử lý..." : mode === "login" ? "Tiếp tục" : "Tạo tài khoản"}{" "}
          <ArrowRight size={17} />
        </button>

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

