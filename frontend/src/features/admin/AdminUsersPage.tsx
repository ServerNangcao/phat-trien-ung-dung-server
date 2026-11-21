import { useEffect, useMemo, useState } from "react";
import { Loader2, Lock, Search, Unlock } from "lucide-react";
import { ticketingApi } from "../../api/service";
import type { AdminUserItem, UserRole } from "../../api/types";
import { StatusBadge } from "../../components/ui/StatusBadge";
import { AdminLayout } from "./AdminLayout";

const ROLE_LABELS: Record<string, string> = {
  customer: "Khách hàng",
  staff: "Soát vé (Staff)",
  organizer: "Ban tổ chức",
  admin: "Quản trị viên",
  Customer: "Khách hàng",
  Staff: "Soát vé (Staff)",
  Organizer: "Ban tổ chức",
  Admin: "Quản trị viên",
};

const ASSIGNABLE_ROLES: UserRole[] = ["customer", "staff", "organizer", "admin"];

export function AdminUsersPage() {
  const [users, setUsers] = useState<AdminUserItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoadingId, setActionLoadingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [query, setQuery] = useState("");

  useEffect(() => {
    let mounted = true;
    setLoading(true);
    ticketingApi
      .getAdminUsers()
      .then((data) => {
        if (mounted) {
          setUsers(data);
          setError(null);
        }
      })
      .catch((err) => {
        if (mounted) {
          setError("Không thể tải danh sách tài khoản từ máy chủ.");
          console.error(err);
        }
      })
      .finally(() => {
        if (mounted) setLoading(false);
      });

    return () => {
      mounted = false;
    };
  }, []);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return users;
    return users.filter(
      (u) => u.name.toLowerCase().includes(q) || u.email.toLowerCase().includes(q),
    );
  }, [users, query]);

  const handleRoleChange = async (id: string, role: UserRole) => {
    setActionLoadingId(id);
    try {
      await ticketingApi.updateUserRole(id, role);
      setUsers((prev) => prev.map((u) => (u.id === id ? { ...u, role } : u)));
    } catch (err) {
      console.error("Lỗi khi cập nhật vai trò:", err);
      alert("Không thể cập nhật vai trò người dùng.");
    } finally {
      setActionLoadingId(null);
    }
  };

  const handleToggleLock = async (id: string) => {
    const user = users.find((u) => u.id === id);
    if (!user) return;
    const nextIsActive = user.status !== "Active";
    setActionLoadingId(id);
    try {
      await ticketingApi.updateUserStatus(id, nextIsActive);
      setUsers((prev) =>
        prev.map((u) => (u.id === id ? { ...u, status: nextIsActive ? "Active" : "Locked" } : u)),
      );
    } catch (err) {
      console.error("Lỗi khi thay đổi trạng thái tài khoản:", err);
      alert("Không thể thay đổi trạng thái tài khoản.");
    } finally {
      setActionLoadingId(null);
    }
  };

  return (
    <AdminLayout
      title="Quản trị tài khoản"
      subtitle="Phân quyền vai trò (Customer, Staff, Organizer, Admin) và khóa/mở khóa tài khoản vi phạm."
    >
      <div className="mb-5 flex items-center gap-2.5 rounded-2xl border border-slate-200 bg-white p-4">
        <Search size={16} className="text-[#526066]" />
        <input
          aria-label="Tìm tài khoản theo tên hoặc email"
          placeholder="Tìm theo tên hoặc email người dùng..."
          className="w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2 text-sm text-[#182226] placeholder:text-[#7A8494] outline-none"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
      </div>

      {error && (
        <div className="mb-5 rounded-2xl border border-rose-200 bg-rose-50 p-4 text-xs font-semibold text-rose-700">
          {error}
        </div>
      )}

      <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white text-[#182226]">
        {loading ? (
          <div className="flex flex-col items-center justify-center p-12 text-[#526066]">
            <Loader2 size={32} className="animate-spin text-[#F05B45] mb-3" />
            <p className="text-sm font-semibold">Đang tải danh sách tài khoản thật...</p>
          </div>
        ) : filtered.length === 0 ? (
          <div className="p-12 text-center text-sm font-medium text-[#526066]">
            Không tìm thấy tài khoản phù hợp.
          </div>
        ) : (
          <table className="w-full min-w-[720px] text-left text-sm">
            <thead className="border-b border-slate-200 bg-slate-50 text-xs font-bold uppercase tracking-wider text-[#526066]">
              <tr>
                <th className="px-5 py-3.5">Người dùng</th>
                <th className="px-5 py-3.5">Vai trò</th>
                <th className="px-5 py-3.5">Ngày tạo</th>
                <th className="px-5 py-3.5">Trạng thái</th>
                <th className="px-5 py-3.5 text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200 bg-white">
              {filtered.map((user) => {
                const isProcessing = actionLoadingId === user.id;
                return (
                  <tr key={user.id} className="hover:bg-slate-50">
                    <td className="px-5 py-4">
                      <p className="font-bold text-[#182226]">{user.name}</p>
                      <p className="text-xs text-[#526066]">{user.email}</p>
                    </td>
                    <td className="px-5 py-4">
                      <select
                        aria-label={`Vai trò của ${user.name}`}
                        disabled={isProcessing}
                        value={user.role}
                        onChange={(e) => void handleRoleChange(user.id, e.target.value as UserRole)}
                        className="rounded-xl border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-[#182226] disabled:opacity-50"
                      >
                        {ASSIGNABLE_ROLES.map((role) => (
                          <option key={role} value={role}>
                            {ROLE_LABELS[role] ?? role}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td className="px-5 py-4 text-xs text-[#526066]">{user.createdAt}</td>
                    <td className="px-5 py-4">
                      <StatusBadge tone={user.status === "Active" ? "success" : "danger"}>
                        {user.status === "Active" ? "Hoạt động" : "Đã khóa"}
                      </StatusBadge>
                    </td>
                    <td className="px-5 py-4 text-right">
                      <button
                        type="button"
                        disabled={isProcessing}
                        onClick={() => void handleToggleLock(user.id)}
                        className="btn-outline !py-1.5 !px-3 text-xs disabled:opacity-50 inline-flex items-center gap-1.5"
                      >
                        {isProcessing ? (
                          <Loader2 size={13} className="animate-spin" />
                        ) : user.status === "Active" ? (
                          <>
                            <Lock size={13} /> Khóa
                          </>
                        ) : (
                          <>
                            <Unlock size={13} /> Mở khóa
                          </>
                        )}
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>
    </AdminLayout>
  );
}
