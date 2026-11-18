import { useMemo, useState } from "react";
import { Lock, Search, Unlock } from "lucide-react";
import { platformDemoApi } from "../../api/platformDemo";
import type { AdminUserItem, UserRole } from "../../api/types";
import { StatusBadge } from "../../components/ui/StatusBadge";
import { AdminLayout } from "./AdminLayout";

const ROLE_LABELS: Record<UserRole, string> = {
  customer: "Khách hàng",
  staff: "Soát vé (Staff)",
  organizer: "Ban tổ chức",
  admin: "Quản trị viên",
};

export function AdminUsersPage() {
  const [users, setUsers] = useState<AdminUserItem[]>(() => platformDemoApi.getUsers());
  const [query, setQuery] = useState("");

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return users;
    return users.filter(
      (u) => u.name.toLowerCase().includes(q) || u.email.toLowerCase().includes(q),
    );
  }, [users, query]);

  const handleRoleChange = (id: string, role: UserRole) => {
    setUsers(platformDemoApi.updateUserRole(id, role));
  };

  const handleToggleLock = (id: string) => {
    setUsers(platformDemoApi.toggleUserStatus(id));
  };

  return (
    <AdminLayout
      title="Quản trị tài khoản"
      subtitle="Phân quyền vai trò (Customer, Staff, Organizer, Admin) và khóa/mở khóa tài khoản vi phạm."
    >
      <div className="mb-5 flex items-center gap-2.5 rounded-2xl border border-[#E6E4DD] bg-white p-4">
        <Search size={16} className="text-[#647075]" />
        <input
          aria-label="Tìm tài khoản theo tên hoặc email"
          placeholder="Tìm theo tên hoặc email người dùng..."
          className="w-full bg-transparent text-sm text-[#182226] outline-none"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
      </div>

      <div className="overflow-x-auto rounded-2xl border border-[#E6E4DD] bg-white">
        <table className="w-full min-w-[720px] text-left text-sm">
          <thead className="border-b border-[#E6E4DD] bg-[#F9F8F4] text-xs font-bold uppercase tracking-wider text-[#647075]">
            <tr>
              <th className="px-5 py-3.5">Người dùng</th>
              <th className="px-5 py-3.5">Vai trò</th>
              <th className="px-5 py-3.5">Ngày tạo</th>
              <th className="px-5 py-3.5">Trạng thái</th>
              <th className="px-5 py-3.5 text-right">Thao tác</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[#EEECE6]">
            {filtered.map((user) => (
              <tr key={user.id} className="hover:bg-[#F9F8F4]/60">
                <td className="px-5 py-4">
                  <p className="font-bold text-[#182226]">{user.name}</p>
                  <p className="text-xs text-[#647075]">{user.email}</p>
                </td>
                <td className="px-5 py-4">
                  <select
                    aria-label={`Vai trò của ${user.name}`}
                    value={user.role}
                    onChange={(e) => handleRoleChange(user.id, e.target.value as UserRole)}
                    className="rounded-xl border border-[#E6E4DD] bg-[#F9F8F4] px-3 py-1.5 text-xs font-semibold text-[#182226]"
                  >
                    {(Object.keys(ROLE_LABELS) as UserRole[]).map((role) => (
                      <option key={role} value={role}>
                        {ROLE_LABELS[role]}
                      </option>
                    ))}
                  </select>
                </td>
                <td className="px-5 py-4 text-xs text-[#647075]">{user.createdAt}</td>
                <td className="px-5 py-4">
                  <StatusBadge tone={user.status === "Active" ? "success" : "danger"}>
                    {user.status === "Active" ? "Hoạt động" : "Đã khóa"}
                  </StatusBadge>
                </td>
                <td className="px-5 py-4 text-right">
                  <button
                    type="button"
                    onClick={() => handleToggleLock(user.id)}
                    className="btn-outline !py-1.5 !px-3 text-xs"
                  >
                    {user.status === "Active" ? (
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
            ))}
          </tbody>
        </table>
      </div>
    </AdminLayout>
  );
}
