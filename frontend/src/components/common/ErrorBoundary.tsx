import { Component, type ErrorInfo, type ReactNode } from "react";
import { Link } from "react-router-dom";
import { Compass, RotateCw } from "lucide-react";

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

/**
 * ErrorBoundary bắt lỗi unhandled runtime JavaScript error trong cây component con,
 * ngăn không cho toàn bộ trang bị crash trắng (White Screen of Death).
 */
export class ErrorBoundary extends Component<Props, State> {
  public override state: State = {
    hasError: false,
    error: null,
  };

  public static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  public override componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    console.error("ErrorBoundary caught an error:", error, errorInfo);
  }

  private handleReset = () => {
    this.setState({ hasError: false, error: null });
    window.location.reload();
  };

  public override render(): ReactNode {
    if (this.state.hasError) {
      if (this.props.fallback) {
        return this.props.fallback;
      }

      return (
        <div className="checkout-empty page-enter min-h-[60vh] flex flex-col items-center justify-center text-center p-8">
          <Compass size={36} className="text-coral mb-4" />
          <h1 className="text-2xl font-bold mb-2">Đã xảy ra lỗi không mong muốn</h1>
          <p className="text-slate-500 max-w-md mb-6 text-sm">
            {this.state.error?.message || "Ứng dụng gặp sự cố trong quá trình xử lý giao diện. Vui lòng tải lại trang."}
          </p>
          <div className="flex gap-4">
            <button className="btn-primary inline-flex items-center gap-2" onClick={this.handleReset}>
              <RotateCw size={16} /> Tải lại trang
            </button>
            <Link to="/" className="btn-outline">
              Về trang chủ
            </Link>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
