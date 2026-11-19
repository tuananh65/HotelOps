import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import { CollapsibleFilterPanel } from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import {
  createReportApi,
  ReportFailure,
  reportTypes,
  type ReportOptions,
  type ReportSnapshot,
  type ReportType,
} from "./reportApi";

// API admission checks actor and REPORT_VIEW on the same eligible role path.
// A route or a permission string in the browser is never an access grant.
export function ReportAccessLink() {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [allowed, setAllowed] = useState<typeof owner>(null);
  useEffect(() => {
    setAllowed(null);
    if (!owner) return;
    const lifetime = new AbortController();
    void fetch("/api/auth/navigation", {
      credentials: "same-origin",
      cache: "no-store",
      signal: lifetime.signal,
    })
      .then(async (response) => {
        if (response.status === 401) {
          actions.invalidateSession(owner);
          return false;
        }
        if (!response.ok) return false;
        const navigation = await response.json();
        if (
          !Array.isArray(navigation.paths) ||
          !navigation.paths.includes("/reports")
        )
          return false;
        await createReportApi(lifetime.signal).options();
        return true;
      })
      .then((available) => {
        if (available && !lifetime.signal.aborted) setAllowed(owner);
      })
      .catch((e: unknown) => {
        if (
          !lifetime.signal.aborted &&
          e instanceof ReportFailure &&
          e.status === 401
        )
          actions.invalidateSession(owner);
      });
    return () => lifetime.abort();
  }, [owner, actions]);
  return owner && allowed === owner ? <Link to="/reports">Báo cáo</Link> : null;
}

export function ReportsPage() {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.session &&
    state.pending === null &&
    !state.session.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [admission, setAdmission] = useState<{
    owner: typeof owner;
    options?: ReportOptions;
    error?: string;
  } | null>(null);
  const [kind, setKind] = useState<ReportType>("tasks");
  const [branch, setBranch] = useState("");
  const [department, setDepartment] = useState("");
  const [housekeeper, setHousekeeper] = useState("");
  const [inspector, setInspector] = useState("");
  const [employee, setEmployee] = useState("");
  const [requestType, setRequestType] = useState("");
  const [specific, setSpecific] = useState<Record<string, string>>({});
  const numericFilters: [string, string][] =
    kind === "rooms"
      ? [
          ["floorId", "Mã tầng"],
          ["roomTypeId", "Mã loại phòng"],
        ]
      : kind === "incidents"
        ? [
            ["categoryId", "Mã nhóm sự cố"],
            ["severityId", "Mã mức độ"],
          ]
        : kind === "assets"
          ? [
              ["categoryId", "Mã nhóm tài sản"],
              ["roomId", "Mã phòng"],
              ["areaId", "Mã khu vực"],
            ]
          : kind === "items"
            ? [
                ["warehouseId", "Mã kho"],
                ["itemId", "Mã vật tư"],
              ]
            : [];
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [snapshot, setSnapshot] = useState<ReportSnapshot | null>(null);
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [page, setPage] = useState(0);
  const operation = useRef<AbortController | null>(null);
  useEffect(() => {
    setAdmission(null);
    setSnapshot(null);
    setMessage("");
    setBusy(false);
    operation.current?.abort();
    operation.current = null;
    if (!owner) return;
    const lifetime = new AbortController();
    void createReportApi(lifetime.signal)
      .options()
      .then((options) => {
        if (lifetime.signal.aborted) return;
        const end = new Date();
        const start = new Date(end);
        start.setUTCDate(start.getUTCDate() - options.defaultPeriodDays + 1);
        setFrom(start.toISOString().slice(0, 10));
        setTo(end.toISOString().slice(0, 10));
        setBranch("");
        setDepartment("");
        setEmployee("");
        setRequestType("");
        setSpecific({});
        setAdmission({ owner, options });
      })
      .catch((e: unknown) => {
        if (lifetime.signal.aborted) return;
        setAdmission({
          owner,
          error:
            e instanceof ReportFailure && e.status === 403
              ? "Bạn không có quyền xem báo cáo vận hành."
              : "Quyền xem báo cáo chưa khả dụng.",
        });
        if (e instanceof ReportFailure && e.status === 401)
          actions.invalidateSession(owner);
      });
    return () => {
      lifetime.abort();
      operation.current?.abort();
    };
  }, [owner, actions]);
  const visible = owner && admission?.owner === owner ? admission : null;
  const options = visible?.options;
  function changed() {
    operation.current?.abort();
    operation.current = null;
    setBusy(false);
    setSnapshot(null);
    setMessage("");
    setPage(0);
  }
  async function run(
    exporting: boolean,
    format: "csv" | "xlsx" | "pdf" = "csv",
  ) {
    if (!owner || !options || operation.current) return;
    const controller = new AbortController();
    operation.current = controller;
    const api = createReportApi(controller.signal);
    setBusy(true);
    setMessage("");
    if (!exporting) setSnapshot(null);
    try {
      if (exporting && snapshot) {
        const file = await api.export(snapshot, format);
        if (controller.signal.aborted) return;
        const url = URL.createObjectURL(file.blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = file.name;
        document.body.append(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
        setMessage("MSG25: Đã tạo bản xuất và yêu cầu tải xuống.");
      } else {
        const data = await api.preview(kind, {
          from,
          to,
          ...Object.fromEntries(
            numericFilters
              .filter(([key]) => specific[key])
              .map(([key]) => [key, Number(specific[key])]),
          ),
          ...(kind === "assets" && specific.condition
            ? { condition: specific.condition }
            : {}),
          ...(branch ? { branchId: Number(branch) } : {}),
          ...(department ? { departmentId: Number(department) } : {}),
          ...(kind === "tasks" && employee
            ? { employeeId: Number(employee) }
            : {}),
          ...(kind === "requests" && requestType.trim()
            ? { type: requestType.trim() }
            : {}),
          ...(kind === "inspections" && housekeeper
            ? { housekeeperId: Number(housekeeper) }
            : {}),
          ...(kind === "inspections" && inspector
            ? { inspectorId: Number(inspector) }
            : {}),
        });
        if (!controller.signal.aborted) {
          setSnapshot(data);
          setPage(0);
        }
      }
    } catch (e: unknown) {
      if (controller.signal.aborted) return;
      const status = e instanceof ReportFailure ? e.status : 0;
      if (status === 401 || status === 403) {
        setSnapshot(null);
        setAdmission({ owner, error: "Không còn quyền xem báo cáo." });
        if (status === 401) actions.invalidateSession(owner);
      }
      setMessage(
        status === 422
          ? "MSG26: Thu hẹp bộ lọc báo cáo; dữ liệu vượt giới hạn xuất."
          : exporting
            ? "MSG27: Xuất thất bại. Chưa tải tệp nào. Có thể thử lại hoặc tạo bản xem trước mới."
            : "Không xem trước được. Kiểm tra bộ lọc và quyền; không thay thế dữ liệu thiếu.",
      );
    } finally {
      if (operation.current === controller) {
        operation.current = null;
        setBusy(false);
      }
    }
  }
  return (
    <main aria-label="Báo cáo vận hành">
      <h1>Báo cáo vận hành</h1>
      <Link to="/dashboard">Tổng quan</Link>
      {visible?.error && <p role="alert">{userMessage(visible.error)}</p>}
      {!visible && <p role="status">Đang kiểm tra quyền xem báo cáo…</p>}
      {options && (
        <>
          <p>
            Khoảng thời gian dùng UTC. Giới hạn xuất: {options.exportRowLimit}{" "}
            dòng. Có thể xuất CSV, XLSX và PDF.
          </p>
          <CollapsibleFilterPanel
            activeCount={
              snapshot
                ? Object.values(snapshot.appliedFilters ?? {}).filter(Boolean)
                    .length +
                  Number(!!snapshot.filters.departmentId) +
                  Number(!!snapshot.filters.from) +
                  Number(!!snapshot.filters.to)
                : 0
            }
          >
            <form
              onSubmit={(e) => {
                e.preventDefault();
                void run(false);
              }}
            >
              <label>
                Loại báo cáo
                <SearchableSelect
                  aria-label="Loại báo cáo"
                  value={kind}
                  onChange={(e) => {
                    changed();
                    setKind(e.target.value as ReportType);
                    setDepartment("");
                    setHousekeeper("");
                    setEmployee("");
                    setRequestType("");
                    setInspector("");
                    setSpecific({});
                  }}
                >
                  {reportTypes.map((k) => (
                    <option key={k} value={k}>
                      {displayCode(k)}
                    </option>
                  ))}
                </SearchableSelect>
              </label>
              {numericFilters.map(([key, label]) => (
                <label key={key}>
                  {label}
                  <input
                    type="number"
                    min="1"
                    step="1"
                    value={specific[key] ?? ""}
                    onChange={(e) => {
                      changed();
                      setSpecific((s) => ({ ...s, [key]: e.target.value }));
                    }}
                  />
                </label>
              ))}
              {kind === "assets" && (
                <label>
                  Mã tình trạng
                  <input
                    maxLength={40}
                    value={specific.condition ?? ""}
                    onChange={(e) => {
                      changed();
                      setSpecific((s) => ({ ...s, condition: e.target.value }));
                    }}
                  />
                </label>
              )}
              <label>
                Chi nhánh
                <SearchableSelect
                  aria-label="Chi nhánh"
                  value={branch}
                  onChange={(e) => {
                    changed();
                    setBranch(e.target.value);
                    setDepartment("");
                  }}
                >
                  <option value="">Tất cả trong phạm vi được phép</option>
                  {options.branches.map((b) => (
                    <option key={b.id} value={b.id}>
                      {b.code} — {b.name}
                    </option>
                  ))}
                </SearchableSelect>
              </label>
              {kind === "inspections" && (
                <>
                  <label>
                    Mã nhân viên buồng phòng
                    <input
                      type="number"
                      min="1"
                      step="1"
                      value={housekeeper}
                      onChange={(e) => {
                        changed();
                        setHousekeeper(e.target.value);
                      }}
                    />
                  </label>
                  <label>
                    Mã người kiểm tra
                    <input
                      type="number"
                      min="1"
                      step="1"
                      value={inspector}
                      onChange={(e) => {
                        changed();
                        setInspector(e.target.value);
                      }}
                    />
                  </label>
                </>
              )}
              {kind === "tasks" && (
                <label>
                  Mã nhân viên
                  <input
                    type="number"
                    min="1"
                    step="1"
                    value={employee}
                    onChange={(e) => {
                      changed();
                      setEmployee(e.target.value);
                    }}
                  />
                </label>
              )}
              {kind === "tasks" && (
                <p>
                  Dòng công việc hiển thị người được giao hiện tại. Thời gian
                  thực hiện và ngày làm việc vẫn thuộc nhân viên đã làm.
                </p>
              )}
              {kind === "requests" && (
                <label>
                  Mã loại yêu cầu
                  <input
                    maxLength={40}
                    value={requestType}
                    onChange={(e) => {
                      changed();
                      setRequestType(e.target.value);
                    }}
                  />
                </label>
              )}
              {(kind === "tasks" || kind === "requests") && (
                <label>
                  {kind === "requests" ? "Bộ phận tiếp nhận" : "Bộ phận"}
                  <SearchableSelect
                    aria-label="Bộ phận"
                    value={department}
                    onChange={(e) => {
                      changed();
                      setDepartment(e.target.value);
                    }}
                  >
                    <option value="">Tất cả trong phạm vi được phép</option>
                    {options.departments
                      .filter((d) => !branch || d.branchId === Number(branch))
                      .map((d) => (
                        <option key={d.id} value={d.id}>
                          {d.name}
                        </option>
                      ))}
                  </SearchableSelect>
                </label>
              )}
              <label>
                từ (UTC)
                <input
                  required
                  type="date"
                  value={from}
                  max={to}
                  onChange={(e) => {
                    changed();
                    setFrom(e.target.value);
                  }}
                />
              </label>
              <label>
                Đến (UTC)
                <input
                  required
                  type="date"
                  value={to}
                  min={from}
                  onChange={(e) => {
                    changed();
                    setTo(e.target.value);
                  }}
                />
              </label>
              <button disabled={busy}>Xem trước</button>
            </form>
          </CollapsibleFilterPanel>
          {message && <p role="status">{userMessage(message)}</p>}
          {snapshot && (
            <section aria-label="Xem trước báo cáo">
              <p>
                Bản chụp dữ liệu (UTC): {snapshot.snapshot}. Branches:{" "}
                {snapshot.filters.branchIds.join(", ") || "Không có"}; Bộ phận:{" "}
                {snapshot.filters.departmentId ??
                  "Tất cả trong phạm vi được phép"}
                ; Khoảng thời gian: {snapshot.filters.from} —{" "}
                {snapshot.filters.to}.
              </p>
              {Object.entries(snapshot.appliedFilters ?? {}).map(
                ([key, value]) => (
                  <p key={key}>
                    {key}: {value ?? "Tất cả"}
                  </p>
                ),
              )}
              {snapshot.missingSources.length > 0 && (
                <aside aria-label="Giới hạn báo cáo">
                  <strong>
                    Phạm vi báo cáo, bằng chứng và giá trị chưa khả dụng:
                  </strong>
                  <ul>
                    {snapshot.missingSources.map((s, i) => (
                      <li key={i}>{s}</li>
                    ))}
                  </ul>
                </aside>
              )}
              {snapshot.totalCount === 0 && (
                <p role="status">
                  Không có hồ sơ phù hợp. Vẫn có thể xuất tiêu đề cột
                  CSV.
                </p>
              )}
              <p>
                {snapshot.totalCount} dòng. Trang {page + 1}.
              </p>
              {snapshot.sections.map((section) => (
                <section key={section.key}>
                  <h2>{section.key}</h2>
                  <table>
                    <thead>
                      <tr>
                        {section.columns.map((c) => (
                          <th key={c}>{c}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {section.rows
                        .slice(page * 25, (page + 1) * 25)
                        .map((row, i) => (
                          <tr key={i}>
                            {section.columns.map((c) => (
                              <td key={c}>
                                {row[c] === null ? "—" : String(row[c])}
                              </td>
                            ))}
                          </tr>
                        ))}
                    </tbody>
                  </table>
                </section>
              ))}
              <button
                disabled={page === 0}
                onClick={() => setPage((n) => n - 1)}
              >
                Trang trước
              </button>
              <button
                disabled={snapshot.sections.every(
                  (s) => s.rows.length <= (page + 1) * 25,
                )}
                onClick={() => setPage((n) => n + 1)}
              >
                Trang sau
              </button>
              <button
                disabled={busy || !snapshot.exportFormats.includes("csv")}
                onClick={() => void run(true)}
              >
                Xuất CSV
              </button>
              <button
                disabled={busy || !snapshot.exportFormats.includes("xlsx")}
                onClick={() => void run(true, "xlsx")}
              >
                Xuất XLSX
              </button>
              <button
                disabled={busy || !snapshot.exportFormats.includes("pdf")}
                onClick={() => void run(true, "pdf")}
              >
                Xuất PDF
              </button>
            </section>
          )}
        </>
      )}
    </main>
  );
}
