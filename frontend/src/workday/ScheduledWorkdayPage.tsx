import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { CalendarDays, Building2 } from "lucide-react";
import { createHttpClient, isObject } from "../auth/http";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { useAuth } from "../auth/AuthProvider";
import { safeId } from "./contracts";
import { ScheduledWorkdayPanel } from "./ScheduledWorkdayPanel";
import "./scheduled-workday.css";
import { useWorkspacePermissions } from "../screens/housekeeping/useWorkspacePermissions";

type Branch = { branchId: number; name: string; isPrimary: boolean };
export function ScheduledWorkdayPage() {
  const { state, actions } = useAuth();
  const [query] = useSearchParams();
  const [branches, setBranches] = useState<Branch[] | null>(null);
  const [selected, setSelected] = useState("");
  const access = useWorkspacePermissions(selected);
  const [error, setError] = useState("");
  const [reload, setReload] = useState(0);
  const [busy, setBusy] = useState(false);
  const requestedBranch = Number(query.get("branchId"));
  useEffect(() => {
    const abort = new AbortController();
    setBranches(null);
    setError("");
    setSelected("");
    void createHttpClient()({
      method: "GET",
      url: "/api/workdays/branch-options",
      expectedStatus: 200,
      signal: abort.signal,
      decode(v) {
        if (
          !Array.isArray(v) ||
          !v.every(
            (b) =>
              isObject(b) &&
              safeId(b.branchId) &&
              typeof b.name === "string" &&
              typeof b.isPrimary === "boolean",
          )
        )
          return;
        if (new Set(v.map((b) => b.branchId)).size !== v.length) return;
        return v as Branch[];
      },
    }).then((result) => {
      if (abort.signal.aborted) return;
      if (!result.ok) {
        if (result.error.kind === "http" && result.error.status === 401) {
          actions.invalidateSession(state.authorityGeneration);
          return;
        }
        setError("Chưa tải được chi nhánh làm việc. Vui lòng thử lại.");
        return;
      }
      setBranches(result.data);
      const branch =
        result.data.find((b) => b.branchId === requestedBranch) ??
        result.data.find((b) => b.isPrimary) ??
        (result.data.length === 1 ? result.data[0] : undefined);
      setSelected(branch ? String(branch.branchId) : "");
    });
    return () => abort.abort();
  }, [reload, requestedBranch, state.authorityGeneration, actions]);
  const branch = branches?.find((b) => String(b.branchId) === selected);
  return (
    <main className="scheduled-workday">
      <header className="workday-heading">
        <div>
          <span className="workday-eyebrow">CA LÀM VIỆC CỦA TÔI</span>
          <h1>Bắt đầu / kết thúc ca</h1>
          <p>Xem ca hôm nay và ghi nhận thời gian làm việc của bạn.</p>
        </div>
        <CalendarDays size={36} aria-hidden="true" />
      </header>
      <section
        className="workday-card workday-branch"
        aria-label="Chi nhánh làm việc"
      >
        <Building2 aria-hidden="true" />
        <div>
          <h2>Chi nhánh làm việc</h2>
          {error ? (
            <>
              <p role="alert">{userMessage(error)}</p>
              <button onClick={() => setReload((n) => n + 1)}>Thử lại</button>
            </>
          ) : branches === null ? (
            <LoadingIndicator compact label="Đang tải chi nhánh làm việc" />
          ) : branches.length === 0 ? (
            <p>
              Chưa có chi nhánh khả dụng cho việc ghi nhận ca. Vui lòng liên hệ
              quản lý.
            </p>
          ) : branches.length === 1 ? (
            <p className="workday-branch-name">{branches[0].name}</p>
          ) : (
            <label>
              Chọn chi nhánh
              <SearchableSelect
                aria-label="Chọn chi nhánh"
                value={selected}
                disabled={busy}
                onChange={(e) => setSelected(e.target.value)}
              >
                <option value="">Chọn nơi làm việc</option>
                {branches.map((b) => (
                  <option key={b.branchId} value={b.branchId}>
                    {b.name}
                  </option>
                ))}
              </SearchableSelect>
            </label>
          )}
        </div>
      </section>
      {branch && (
        <ScheduledWorkdayPanel
          key={branch.branchId}
          branchId={branch.branchId}
          branchName={branch.name}
          dispatchEnabled
          onBusyChange={setBusy}
        />
      )}
      <nav className="workday-links" aria-label="Liên kết ca làm việc">
        <Link to="/workday">Lịch ca &amp; ngày làm việc</Link>
        {access.allows("SHIFT_HANDOVER_VIEW", true) && (
          <Link to={`/workday/handovers?branchId=${selected}`}>
            Lịch sử bàn giao ca
          </Link>
        )}
        <Link to="/home">Trang chủ</Link>
      </nav>
    </main>
  );
}
