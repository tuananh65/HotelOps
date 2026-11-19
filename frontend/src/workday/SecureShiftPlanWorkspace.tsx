import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { CalendarDays, CalendarPlus, Clock3 } from "lucide-react";
import { useAuth } from "../auth/AuthProvider";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { ShiftDefinitionsPanel } from "./ShiftDefinitionsPanel";
import { ShiftRosterPanel } from "./ShiftRosterPanel";
import { ShiftPlanPreviewPanel } from "./ShiftPlanPreviewPanel";
import { createShiftPlanApi } from "./shiftPlanApi";
import {
  ShiftPlanningContext,
  validPlanningBranches,
  type PlanningBranch,
} from "./shiftPlanningData";
import "./shift-planning.css";

export function SecureShiftPlanWorkspace({
  saveEnabled = false,
}: {
  saveEnabled?: boolean;
}) {
  const { state } = useAuth();
  const identity = useRef(state.authorityGeneration),
    generation = useRef(0);
  if (identity.current !== state.authorityGeneration) {
    identity.current = state.authorityGeneration;
    generation.current++;
  }
  if (
    state.status !== "AUTHENTICATED" ||
    state.pending ||
    state.session?.mustChangePassword
  )
    return <LoadingIndicator />;
  return <Workspace key={generation.current} saveEnabled={saveEnabled} />;
}
function Workspace({ saveEnabled }: { saveEnabled: boolean }) {
  const { state, actions } = useAuth();
  const [branches, setBranches] = useState<PlanningBranch[]>([]),
    [branch, setBranch] = useState(""),
    [department, setDepartment] = useState("");
  const [loading, setLoading] = useState(true),
    [error, setError] = useState(""),
    [reload, setReload] = useState(0);
  const [tab, setTab] = useState("plan"),
    [rosterRevision, setRosterRevision] = useState(0),
    [dirty, setDirty] = useState(false);
  const [busyPanels, setBusyPanels] = useState<Set<string>>(new Set());
  const reportBusy = useCallback(
    (panel: string, value: boolean) =>
      setBusyPanels((previous) => {
        if (previous.has(panel) === value) return previous;
        const next = new Set(previous);
        if (value) next.add(panel);
        else next.delete(panel);
        return next;
      }),
    [],
  );
  const api = useMemo(
    () => createShiftPlanApi(globalThis.fetch.bind(globalThis), saveEnabled),
    [saveEnabled],
  );
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError("");
    void (async () => {
      try {
        const response = await fetch("/api/shift-plans/options", {
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: controller.signal,
        });
        if (response.status === 401)
          actions.invalidateSession(state.authorityGeneration);
        if (!response.ok) throw Error();
        const value: unknown = await response.json();
        if (!validPlanningBranches(value)) throw Error();
        if (controller.signal.aborted) return;
        setBranches(value);
        if (value.length === 1) {
          setBranch(String(value[0].id));
          if (value[0].departments.length === 1)
            setDepartment(String(value[0].departments[0].id));
        }
      } catch {
        if (!controller.signal.aborted)
          setError("Không tải được phạm vi lập ca. Vui lòng thử lại.");
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    })();
    return () => controller.abort();
  }, [reload, actions, state.authorityGeneration]);
  const selectedBranch = branches.find((b) => String(b.id) === branch),
    selectedDepartment = selectedBranch?.departments.find(
      (d) => String(d.id) === department,
    );
  const scope =
    selectedBranch && selectedDepartment
      ? {
          branchId: selectedBranch.id,
          branchName: selectedBranch.name,
          departmentId: selectedDepartment.id,
          departmentName: selectedDepartment.name,
          timezone: selectedBranch.timezone,
        }
      : null;
  const busy = busyPanels.size > 0;
  const tabs = [
    { id: "plan", label: "Lập phân công", icon: CalendarPlus },
    { id: "roster", label: "Lịch đã phân", icon: CalendarDays },
    { id: "templates", label: "Mẫu ca", icon: Clock3 },
  ];
  return (
    <main className="shift-planning">
      <header className="shift-heading">
        <div>
          <p className="shift-eyebrow">CA LÀM VIỆC</p>
          <h1>Lập kế hoạch ca</h1>
          <p>Chọn bộ phận, thêm nhân viên vào ca và kiểm tra trước khi lưu.</p>
        </div>
        <CalendarDays size={36} aria-hidden="true" />
      </header>
      <section className="shift-card shift-scope" aria-label="Phạm vi lập ca">
        {loading ? (
          <LoadingIndicator label="Đang tải chi nhánh và bộ phận" />
        ) : error ? (
          <div role="alert">
            {userMessage(error)}{" "}
            <button onClick={() => setReload((v) => v + 1)}>Thử lại</button>
          </div>
        ) : !branches.length ? (
          <p>
            Bạn chưa được phân quyền lập ca cho bộ phận nào. Liên hệ quản trị
            viên để kiểm tra phạm vi quản lý.
          </p>
        ) : (
          <>
            <div className="shift-grid">
              <label>
                Chi nhánh
                <SearchableSelect
                aria-label="Chi nhánh"
                  value={branch}
                  disabled={busy || dirty || branches.length === 1}
                  onChange={(e) => {
                    setBranch(e.target.value);
                    const b = branches.find(
                      (b) => String(b.id) === e.target.value,
                    );
                    setDepartment(
                      b?.departments.length === 1
                        ? String(b.departments[0].id)
                        : "",
                    );
                  }}
                >
                  <option value="">Chọn chi nhánh</option>
                  {branches.map((b) => (
                    <option key={b.id} value={b.id}>
                      {b.name}
                    </option>
                  ))}
                </SearchableSelect>
              </label>
              <label>
                Bộ phận
                <SearchableSelect
                aria-label="Bộ phận"
                  value={department}
                  disabled={!selectedBranch || busy || dirty}
                  onChange={(e) => setDepartment(e.target.value)}
                >
                  <option value="">Chọn bộ phận cần lập ca</option>
                  {selectedBranch?.departments.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
                </SearchableSelect>
              </label>
            </div>
            {dirty && (
              <p className="shift-hint">
                Hoàn tất hoặc xóa danh sách phân công đang soạn trước khi đổi bộ
                phận.
              </p>
            )}
          </>
        )}
      </section>
      {scope ? (
        <ShiftPlanningContext.Provider
          key={`${branch}:${department}`}
          value={{ scope, reportBusy, reportDrafts: setDirty }}
        >
          <nav className="shift-tabs" aria-label="Chức năng kế hoạch ca">
            {tabs.map(({ id, label, icon: Icon }) => (
              <button
                key={id}
                aria-pressed={tab === id}
                disabled={busy}
                onClick={() => setTab(id)}
              >
                <Icon size={18} aria-hidden="true" />
                {label}
              </button>
            ))}
          </nav>
          <div hidden={tab !== "plan"}>
            <ShiftPlanPreviewPanel
              adapter={api.preview}
              saveAdapter={saveEnabled ? api.save : undefined}
              onSaved={() => setRosterRevision((v) => v + 1)}
            />
          </div>
          <div hidden={tab !== "roster"}>
            <ShiftRosterPanel
              active={tab === "roster"}
              revision={rosterRevision}
            />
          </div>
          <div hidden={tab !== "templates"}>
            <ShiftDefinitionsPanel active={tab === "templates"} />
          </div>
        </ShiftPlanningContext.Provider>
      ) : (
        !loading &&
        branches.length > 0 && (
          <div className="shift-empty">
            <CalendarDays size={32} aria-hidden="true" />
            <h2>Chọn bộ phận để bắt đầu</h2>
            <p>
              Danh sách nhân viên và mẫu ca sẽ hiển thị theo phạm vi bạn được
              quản lý.
            </p>
          </div>
        )
      )}
    </main>
  );
}
