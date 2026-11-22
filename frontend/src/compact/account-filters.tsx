import { useState } from "react";
import { SlidersHorizontal } from "lucide-react";
import type { Row } from "./api";

type Employment = {
  branchId: number;
  branchName: string;
  departmentId: number | null;
  departmentName: string | null;
};
type Role = { roleId: number; name: string; branchId: number | null };
type Branch = { branchId: number; branchName: string };
// Older directory responses contain employment only; current responses also
// include valid branch access without inventing a department/employment record.
const accountBranches = (user: Row): Branch[] =>
  user.branches ?? user.employments ?? [];
export type AccountFilter = {
  branch: string;
  department: string;
  role: string;
};
export const emptyAccountFilter: AccountFilter = {
  branch: "",
  department: "",
  role: "",
};

export function matchesAccountFilter(user: Row, filter: AccountFilter) {
  const employments: Employment[] = user.employments || [];
  const roles: Role[] = user.roles || [];
  const matchesRole = (branchId?: number) =>
    !filter.role ||
    roles.some(
      (r) =>
        String(r.roleId) === filter.role &&
        (branchId === undefined ||
          r.branchId === null ||
          r.branchId === branchId),
    );
  if (!filter.branch && !filter.department) return matchesRole();
  if (!filter.department)
    return accountBranches(user).some(
      (b) => String(b.branchId) === filter.branch && matchesRole(b.branchId),
    );
  return employments.some(
    (m) =>
      (!filter.branch || String(m.branchId) === filter.branch) &&
      (!filter.department || String(m.departmentId) === filter.department) &&
      matchesRole(m.branchId),
  );
}

export default function AccountFilters({
  users,
  search,
  setSearch,
  filter,
  setFilter,
  count,
  create,
}: {
  users: Row[];
  search: string;
  setSearch: (value: string) => void;
  filter: AccountFilter;
  setFilter: (value: AccountFilter) => void;
  count: number;
  create?: () => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const employments: Employment[] = users.flatMap((u) => u.employments || []);
  const roles: Role[] = users.flatMap((u) => u.roles || []);
  const options = (items: { id: number; name: string }[]) =>
    [...new Map(items.map((item) => [item.id, item])).values()].sort((a, b) =>
      a.name.localeCompare(b.name, "vi"),
    );
  const branches = options(
    users
      .flatMap(accountBranches)
      .map((b) => ({ id: b.branchId, name: b.branchName })),
  );
  const departments = options(
    employments
      .filter(
        (m) =>
          m.departmentId !== null &&
          (!filter.branch || String(m.branchId) === filter.branch),
      )
      .map((m) => ({
        id: m.departmentId!,
        name: `${m.departmentName}${filter.branch ? "" : ` · ${m.branchName}`}`,
      })),
  );
  const roleOptions = options(
    roles.map((r) => ({ id: r.roleId, name: r.name })),
  );
  const activeCount = Object.values(filter).filter(Boolean).length;
  return (
    <div className="account-filter-panel">
      <div className="account-filters">
        <label>
          Tìm tài khoản
          <input
            type="search"
            placeholder="Tên, mã nhân viên, username hoặc email"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </label>
        <div className="account-filter-actions">
          <button
            aria-expanded={expanded}
            aria-controls="account-filter-details"
            onClick={() => setExpanded(!expanded)}
          >
            <SlidersHorizontal size={18} aria-hidden="true" /> Bộ lọc
            {activeCount ? ` (${activeCount})` : ""}
          </button>
          {create && (
            <button className="primary" onClick={create}>
              + Tạo tài khoản
            </button>
          )}
        </div>
      </div>
      {expanded && (
        <div
          id="account-filter-details"
          className="account-filter-details"
          role="region"
          aria-label="Bộ lọc tài khoản"
        >
          <label>
            Chi nhánh
            <select
              value={filter.branch}
              onChange={(e) =>
                setFilter({ ...filter, branch: e.target.value, department: "" })
              }
            >
              <option value="">Tất cả chi nhánh</option>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Bộ phận
            <select
              value={filter.department}
              onChange={(e) =>
                setFilter({ ...filter, department: e.target.value })
              }
            >
              <option value="">Tất cả bộ phận</option>
              {departments.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Vai trò
            <select
              value={filter.role}
              onChange={(e) => setFilter({ ...filter, role: e.target.value })}
            >
              <option value="">Tất cả vai trò</option>
              {roleOptions.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name}
                </option>
              ))}
            </select>
          </label>
        </div>
      )}
      <div className="account-filter-summary">
        <span role="status" aria-live="polite">
          {count} / {users.length} tài khoản
        </span>
        {(search || activeCount > 0) && (
          <button
            onClick={() => {
              setSearch("");
              setFilter(emptyAccountFilter);
            }}
          >
            Xóa lọc
          </button>
        )}
      </div>
    </div>
  );
}
