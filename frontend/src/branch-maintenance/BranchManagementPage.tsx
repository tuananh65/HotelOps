import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  Building2,
  CheckCircle2,
  CirclePause,
  Plus,
  RefreshCw,
  Search,
  ArrowRight,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";
import { useAuth } from "../auth/AuthProvider";
import { AppButton } from "../components/design-system";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { CollapsibleFilterPanel } from "../components/workspace-interactions";
import { branchReadError } from "./branch-ui";

type BranchOptions = {
  timezone: string;
  pageSize: number;
  branches: { id: number; code: string; name: string; isActive: boolean }[];
};
export function useBranchManagementOptions() {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    !state.pending &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [attempt, setAttempt] = useState(0);
  const [result, setResult] = useState<{
    owner: typeof owner;
    data?: BranchOptions;
    error?: string;
  } | null>(null);
  useEffect(() => {
    setResult(null);
    if (!owner) return;
    const abort = new AbortController();
    void (async () => {
      const response = await fetch("/api/branches/management-options", {
        method: "GET",
        credentials: "include",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
      });
      if (abort.signal.aborted) return;
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok) throw new Error(await branchReadError(response));
      const data = (await response.json()) as BranchOptions;
      if (
        !data ||
        typeof data.timezone !== "string" ||
        !data.timezone ||
        !Array.isArray(data.branches) ||
        !Number.isInteger(data.pageSize) ||
        data.pageSize < 1 ||
        data.pageSize > 100 ||
        !data.branches.every(
          (b) =>
            b &&
            Number.isSafeInteger(b.id) &&
            b.id > 0 &&
            typeof b.code === "string" &&
            typeof b.name === "string" &&
            typeof b.isActive === "boolean",
        )
      )
        throw new Error("Dữ liệu danh mục không hợp lệ.");
      if (!abort.signal.aborted) setResult({ owner, data });
    })().catch((e) => {
      if (!abort.signal.aborted)
        setResult({
          owner,
          error:
            e instanceof TypeError
              ? "Không kết nối được máy chủ. Vui lòng thử lại."
              : e instanceof Error
                ? e.message
                : "Không tải được danh mục.",
        });
    });
    return () => abort.abort();
  }, [owner, actions, attempt]);
  return {
    ...(owner && result?.owner === owner ? result : {}),
    loading: !!owner && (!result || result.owner !== owner),
    retry: () => setAttempt((v) => v + 1),
  };
}

export function BranchManagementPage() {
  const result = useBranchManagementOptions();
  const [page, setPage] = useState(1),
    [query, setQuery] = useState(""),
    [status, setStatus] = useState("");
  const branches = (result.data?.branches ?? []).filter(
    (b) =>
      (!status || String(b.isActive) === status) &&
      `${b.code} ${b.name}`
        .toLocaleLowerCase("vi")
        .includes(query.trim().toLocaleLowerCase("vi")),
  );
  const size = result.data?.pageSize ?? 20,
    pages = Math.max(1, Math.ceil(branches.length / size)),
    currentPage = Math.min(page, pages);
  const allBranches = result.data?.branches ?? [];
  const activeCount = allBranches.filter((branch) => branch.isActive).length;
  const clearFilters = () => {
    setQuery("");
    setStatus("");
    setPage(1);
  };
  return (
    <section className="branch-workspace">
      <header className="branch-heading">
        <div>
          <span className="branch-eyebrow">Cấu trúc khách sạn</span>
          <h1>Quản lý chi nhánh</h1>
          <p>Tra cứu thông tin, tạo và cập nhật chi nhánh khách sạn.</p>
        </div>
        {result.data && (
          <Link className="ds-button ds-button--primary" to="/branches/new">
            <Plus size={18} aria-hidden="true" />
            Tạo chi nhánh
          </Link>
        )}
      </header>
      {result.loading ? (
        <LoadingIndicator label="Đang tải danh sách chi nhánh" />
      ) : result.error ? (
        <div className="branch-card">
          <p role="alert" className="branch-feedback">
            {result.error}
          </p>
          <AppButton onClick={result.retry}>Thử lại</AppButton>
        </div>
      ) : result.data ? (
        <>
          <dl className="branch-metrics" aria-label="Tổng quan chi nhánh">
            {[
              {
                label: "Tổng số chi nhánh",
                value: allBranches.length,
                Icon: Building2,
                tone: "total",
              },
              {
                label: "Đang hoạt động",
                value: activeCount,
                Icon: CheckCircle2,
                tone: "active",
              },
              {
                label: "Ngừng hoạt động",
                value: allBranches.length - activeCount,
                Icon: CirclePause,
                tone: "inactive",
              },
            ].map(({ label, value, Icon, tone }) => (
              <div className={`branch-metric is-${tone}`} key={tone}>
                <span className="branch-metric-icon">
                  <Icon size={21} aria-hidden="true" />
                </span>
                <div>
                  <dt>{label}</dt>
                  <dd>{value}</dd>
                </div>
              </div>
            ))}
          </dl>
          <div className="branch-card branch-directory">
            <div className="branch-section-heading">
              <div>
                <h2>Danh sách chi nhánh</h2>
                <p className="branch-muted">
                  Chọn một chi nhánh để xem và cập nhật thông tin.
                </p>
              </div>
              <AppButton icon={RefreshCw} onClick={result.retry}>
                Tải lại danh sách
              </AppButton>
            </div>
            <div className="branch-toolbar">
              <label className="branch-search">
                <span className="branch-sr-only">Tìm chi nhánh</span>
                <Search size={18} aria-hidden="true" />
                <input
                  type="search"
                  placeholder="Nhập mã hoặc tên chi nhánh"
                  value={query}
                  onChange={(e) => {
                    setQuery(e.target.value);
                    setPage(1);
                  }}
                />
              </label>
            </div>
            <CollapsibleFilterPanel activeCount={Number(!!status)}>
              <label>
                Trạng thái
                <select
                  aria-label="Trạng thái"
                  value={status}
                  onChange={(e) => {
                    setStatus(e.target.value);
                    setPage(1);
                  }}
                >
                  <option value="">Tất cả</option>
                  <option value="true">Đang hoạt động</option>
                  <option value="false">Ngừng hoạt động</option>
                </select>
              </label>
            </CollapsibleFilterPanel>
            <div className="branch-results" aria-live="polite">
              <p className="branch-muted">{branches.length} chi nhánh</p>
              {(query || status) && (
                <AppButton variant="ghost" onClick={clearFilters}>
                  Xóa bộ lọc
                </AppButton>
              )}
            </div>
            {branches.length ? (
              <div className="branch-table-wrap">
                <table className="branch-table">
                  <thead>
                    <tr>
                      <th>Mã chi nhánh</th>
                      <th>Tên chi nhánh</th>
                      <th>Trạng thái</th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {branches
                      .slice((currentPage - 1) * size, currentPage * size)
                      .map((b) => (
                        <tr key={b.id}>
                          <td>
                            <span className="branch-code">{b.code}</span>
                          </td>
                          <td>
                            <Link
                              className="branch-name"
                              to={`/branches/${b.id}/edit`}
                            >
                              <Building2 size={18} aria-hidden="true" />
                              {b.name}
                            </Link>
                          </td>
                          <td>
                            <span
                              className={`branch-status ${b.isActive ? "is-active" : ""}`}
                            >
                              {b.isActive
                                ? "Đang hoạt động"
                                : "Ngừng hoạt động"}
                            </span>
                          </td>
                          <td>
                            <Link
                              to={`/branches/${b.id}/edit`}
                              aria-label={`Xem chi nhánh ${b.name}`}
                              className="branch-detail-link"
                            >
                              Xem chi tiết{" "}
                              <ArrowRight size={16} aria-hidden="true" />
                            </Link>
                          </td>
                        </tr>
                      ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <div className="branch-empty">
                <Building2 size={32} aria-hidden="true" />
                <h3>
                  {allBranches.length
                    ? "Không tìm thấy chi nhánh phù hợp."
                    : "Chưa có chi nhánh"}
                </h3>
                <p>
                  {allBranches.length
                    ? "Thử tên hoặc mã khác, hoặc xóa bộ lọc để xem tất cả."
                    : "Tạo chi nhánh đầu tiên để bắt đầu quản lý thông tin khách sạn."}
                </p>
              </div>
            )}
            <nav
              aria-label="Phân trang chi nhánh"
              className="branch-pagination"
            >
              <AppButton
                icon={ChevronLeft}
                disabled={currentPage <= 1}
                onClick={() => setPage(currentPage - 1)}
              >
                Trang trước
              </AppButton>
              <span>
                Trang {currentPage} / {pages}
              </span>
              <AppButton
                icon={ChevronRight}
                disabled={currentPage >= pages}
                onClick={() => setPage(currentPage + 1)}
              >
                Trang sau
              </AppButton>
            </nav>
          </div>
        </>
      ) : (
        <p role="alert">
          Vui lòng đăng nhập bằng tài khoản có quyền quản lý chi nhánh.
        </p>
      )}
    </section>
  );
}
