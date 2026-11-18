import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { ArrowRight, Check, QrCode, Search, ShieldCheck, Ticket, X } from "lucide-react";
import type { Html5Qrcode } from "html5-qrcode";
import { ticketingApi } from "../../../api/service";
import type { CheckInStatus, UserSession } from "../../../api/types";
import { useSoundFeedback } from "../hooks/use-sound-feedback";

interface StaffCheckInPageProps {
  user: UserSession;
  logout: () => void;
}

export function StaffCheckInPage({ user, logout }: StaffCheckInPageProps) {
  const [result, setResult] = useState<{ status: CheckInStatus; count: number; code: string } | null>(
    null,
  );
  const [manualCode, setManualCode] = useState("");
  const [cameraError, setCameraError] = useState("");
  const [scannerActive, setScannerActive] = useState(false);
  const [audioEnabled, setAudioEnabled] = useState(false);

  const scanner = useRef<Html5Qrcode | null>(null);
  const { initAudio, closeAudio, playTone } = useSoundFeedback();

  const countQuery = useQuery({
    queryKey: ["checkin-count"],
    queryFn: ticketingApi.checkInCount,
    refetchInterval: 5_000,
  });

  const processCode = async (code: string) => {
    if (!code.trim()) return;
    try {
      const response = await ticketingApi.checkIn(code.trim());
      setCameraError("");
      setResult({ ...response, code: code.trim() });
      if (audioEnabled) {
        playTone(response.status);
      }
    } catch (error) {
      setCameraError(error instanceof Error ? error.message : "Không thể xác thực mã vé.");
    }
  };

  useEffect(() => {
    return () => {
      if (scanner.current?.isScanning) void scanner.current.stop();
      closeAudio();
    };
  }, []);

  const startScanner = async () => {
    setCameraError("");
    setAudioEnabled(true);
    try {
      const { Html5Qrcode } = await import("html5-qrcode");
      await initAudio();

      const nextScanner = new Html5Qrcode("qr-reader");
      scanner.current = nextScanner;

      await nextScanner.start(
        { facingMode: "environment" },
        { fps: 12, qrbox: { width: 250, height: 250 }, aspectRatio: 1 },
        (decoded) => {
          void nextScanner.pause(true);
          void processCode(decoded).finally(() =>
            window.setTimeout(() => {
              if (nextScanner.isScanning) nextScanner.resume();
            }, 1800),
          );
        },
        () => undefined,
      );
      setScannerActive(true);
    } catch (error) {
      setCameraError(
        error instanceof Error ? error.message : "Không thể mở camera. Hãy kiểm tra quyền truy cập.",
      );
    }
  };

  const stopScanner = async () => {
    if (scanner.current?.isScanning) await scanner.current.stop();
    setScannerActive(false);
  };

  const outcome = result?.status;

  return (
    <div className="staff-shell page-enter">
      <header className="staff-header">
        <Link to="/" className="brand">
          <span className="brand-mark">
            <Ticket size={20} />
          </span>
          <span>
            ticketing<span className="brand-dot">.</span>
          </span>
          <span className="staff-label">GATE</span>
        </Link>
        <div>
          <span className="staff-user">
            <span className="staff-live" />
            {user.name}
          </span>
          <button className="btn-quiet" onClick={logout}>
            Đăng xuất
          </button>
        </div>
      </header>

      <div className="staff-content">
        <div className="staff-title-row">
          <div>
            <div className="eyebrow muted-eyebrow">MIDNIGHT JAZZ · CỔNG A</div>
            <h1>
              Soát vé <em>nhanh gọn.</em>
            </h1>
            <p>Đưa mã QR vào khung hình để xác thực vé.</p>
          </div>
          <div className="live-count">
            <span className="live-indicator" />
            <span>
              <b>{countQuery.data ?? "—"}</b>
              <small>đã vào cổng</small>
            </span>
            <div className="count-chart">▂▄▃▆▄▇▅█</div>
          </div>
        </div>

        <div className={`scanner-frame ${outcome ? `scanner-${outcome}` : ""}`}>
          <div id="qr-reader" className="qr-reader" />
          {!scannerActive && !result && (
            <div className="scanner-placeholder">
              <div className="scanner-crosshair">
                <QrCode size={42} />
              </div>
              <span>Khung quét QR</span>
              <small>Giữ mã QR ngay giữa khung hình</small>
            </div>
          )}
          {result && (
            <div className={`scan-result ${outcome}`}>
              <span className="scan-result-icon">
                {outcome === "valid" ? (
                  <Check size={34} />
                ) : outcome === "used" || outcome === "cancelled" ? (
                  <X size={34} />
                ) : (
                  <Search size={32} />
                )}
              </span>
              <b>
                {outcome === "valid"
                  ? "VÉ HỢP LỆ"
                  : outcome === "used"
                    ? "VÉ ĐÃ ĐƯỢC SỬ DỤNG"
                    : outcome === "cancelled"
                      ? "VÉ ĐÃ BỊ HỦY"
                      : "KHÔNG TÌM THẤY VÉ"}
              </b>
              <span>
                {outcome === "valid"
                  ? "Chúc bạn tham dự vui vẻ!"
                  : outcome === "used"
                    ? "Mã vé này đã được check-in trước đó."
                    : outcome === "cancelled"
                      ? "Vé đã bị hủy và không thể check-in."
                      : "Kiểm tra lại mã hoặc liên hệ quản lý."}
              </span>
              <small>{result.code}</small>
            </div>
          )}
          <div className="scan-corner c1" />
          <div className="scan-corner c2" />
          <div className="scan-corner c3" />
          <div className="scan-corner c4" />
        </div>

        {cameraError && <div className="alert-error camera-error">{cameraError}</div>}

        <div className="scanner-actions">
          {scannerActive ? (
            <button className="btn-outline" onClick={() => void stopScanner()}>
              Tắt camera
            </button>
          ) : (
            <button className="btn-primary" onClick={() => void startScanner()}>
              <QrCode size={17} /> Bật camera quét vé
            </button>
          )}
          <span>hoặc</span>
          <form
            onSubmit={(event) => {
              event.preventDefault();
              void processCode(manualCode);
            }}
            className="manual-code"
          >
            <input
              value={manualCode}
              onChange={(event) => setManualCode(event.target.value)}
              placeholder="Nhập mã vé thủ công"
              aria-label="Mã vé"
            />
            <button aria-label="Xác thực mã">
              <ArrowRight size={17} />
            </button>
          </form>
        </div>

        <div className="staff-tip">
          <ShieldCheck size={17} />
          <span>
            <b>Chỉ check-in một lần.</b> Vé sau khi xác thực sẽ không thể sử dụng lại.
          </span>
          <a href="mailto:hello@ticketing.vn">Cần hỗ trợ?</a>
        </div>
      </div>
    </div>
  );
}

