import { userMessage } from "../i18n/userMessage";
import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { ShiftPlanningContext, usePlanningBusy } from "./shiftPlanningData";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { useContext, useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";

type Definition = {
  id: number;
  code: string;
  name: string;
  start: string;
  end: string;
  crossesMidnight: boolean;
  breakMinutes: number;
  isActive: boolean;
  version: string;
};
const positive = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
function valid(v: unknown): v is Definition {
  return (
    isObject(v) &&
    positive(v.id) &&
    typeof v.code === "string" &&
    typeof v.name === "string" &&
    typeof v.start === "string" &&
    typeof v.end === "string" &&
    typeof v.crossesMidnight === "boolean" &&
    typeof v.breakMinutes === "number" &&
    Number.isInteger(v.breakMinutes) &&
    typeof v.isActive === "boolean" &&
    typeof v.version === "string" &&
    /^"[A-F0-9]{64}"$/.test(v.version)
  );
}
export function ShiftDefinitionsPanel({ active = true }: { active?: boolean }) {
  const planning = useContext(ShiftPlanningContext);
  const { state } = useAuth(),
    owner =
      state.status === "AUTHENTICATED" && !state.pending
        ? state.authorityGeneration
        : null;
  const current = useRef(owner);
  current.current = owner;
  const abort = useRef<AbortController | null>(null),
    sending = useRef(false);
  const [rows, setRows] = useState<Definition[]>([]),
    [busy, setBusy] = useState(false),
    [loaded, setLoaded] = useState(false),
    [message, setMessage] = useState("");
  const [selected, setSelected] = useState<Definition | null>(null),
    [label, setLabel] = useState(""),
    [response, setResponse] = useState(""),
    [resolution, setResolution] = useState("");
  const branch = String(planning?.scope.branchId ?? ""),
    department = String(planning?.scope.departmentId ?? "");
  const [code, setCode] = useState(""),
    [overnight, setOvernight] = useState(false),
    [editing, setEditing] = useState(false);
  usePlanningBusy("templates", busy);
  const endpoint =
    "/api/branches/" +
    branch +
    "/departments/" +
    department +
    "/shift-definitions";
  const [uncertain, setUncertain] = useState(false);
  useEffect(() => {
    if (active) void load();
  }, [active]);
  useEffect(() => {
    setRows([]);
    setLoaded(false);
    setSelected(null);
    setMessage("");
    return () => abort.current?.abort();
  }, [owner]);
  async function load(after = 0) {
    if (!owner || sending.current) return;
    if (!positive(Number(branch)) || !positive(Number(department))) {
      setMessage("Chọn chi nhánh và bộ phận hợp lệ.");
      return;
    }
    const controller = new AbortController();
    abort.current = controller;
    sending.current = true;
    setBusy(true);
    setMessage("");
    try {
      const result = await fetch(endpoint + "?afterId=" + after, {
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: controller.signal,
      });
      if (!result.ok)
        throw Error(
          "Không tải được mẫu ca. Vui lòng thử lại hoặc kiểm tra quyền của bạn.",
        );
      const data: unknown = await result.json();
      if (!Array.isArray(data) || !data.every(valid))
        throw Error("Không xác minh được phản hồi cấu hình.");
      if (!controller.signal.aborted && current.current === owner) {
        setRows(data);
        setLoaded(true);
        setSelected(null);
        setEditing(false);
        setUncertain(false);
      }
    } catch (e) {
      if (!controller.signal.aborted && current.current === owner) {
        setRows([]);
        setLoaded(false);
        setSelected(null);
        setMessage(String(e));
      }
    } finally {
      sending.current = false;
      if (!controller.signal.aborted && current.current === owner)
        setBusy(false);
    }
  }
  async function save() {
    if (!owner || !loaded || !editing || sending.current || uncertain) return;
    const controller = new AbortController();
    abort.current = controller;
    sending.current = true;
    setBusy(true);
    setMessage("");
    let submitted = false;
    try {
      const first = await fetch("/api/auth/csrf", {
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: controller.signal,
      });
      if (!first.ok) throw Error("Xác minh phiên đăng nhập thất bại.");
      const csrf: unknown = await first.json();
      if (
        !isObject(csrf) ||
        csrf.headerName !== "X-CSRF-TOKEN" ||
        typeof csrf.csrfToken !== "string" ||
        !/^[\x21-\x7e]+$/.test(csrf.csrfToken) ||
        current.current !== owner
      )
        throw Error("Phiên đăng nhập đã thay đổi. Vui lòng tải lại.");
      submitted = true;
      const result = await fetch(
        endpoint + (selected ? "/" + selected.id : ""),
        {
          method: selected ? "PUT" : "POST",
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: controller.signal,
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            ...(selected ? { "If-Match": selected.version } : {}),
          },
          body: JSON.stringify({
            code,
            name: label,
            start: response.length === 5 ? response + ":00" : response,
            end: resolution.length === 5 ? resolution + ":00" : resolution,
            crossesMidnight: overnight,
          }),
        },
      );
      if (!result.ok) {
        const problem: unknown = await result.json();
        const detail =
          isObject(problem) && problem.title === "MSG35"
            ? "Mã ca đã tồn tại."
            : "Thông tin ca không hợp lệ hoặc đã thay đổi.";
        if ([400, 409].includes(result.status)) {
          if (!controller.signal.aborted && current.current === owner)
            setMessage("" + detail + " Sửa thông tin mẫu ca và lưu lại.");
          return;
        }
        throw Error(detail);
      }
      const data: unknown = await result.json();
      if (!valid(data) || (selected !== null && data.id !== selected.id))
        throw Error("Không xác minh được phản hồi lưu.");
      if (!controller.signal.aborted && current.current === owner) {
        setRows([]);
        setLoaded(false);
        setSelected(null);
        setEditing(false);
        (announceWorkflowSuccess(
          "Đã lưu. Thời gian ca đã lên lịch giữ nguyên. Bấm Tải mẫu ca để xem kết quả.",
          "workday/ShiftDefinitionsPanel.tsx",
        ),
          setMessage(
            "Đã lưu. Thời gian ca đã lên lịch giữ nguyên. Bấm Tải mẫu ca để xem kết quả.",
          ));
      }
    } catch (e) {
      if (!controller.signal.aborted && current.current === owner) {
        setMessage(
          "Chưa xác định được kết quả lưu. Tải lại mẫu ca trước khi thử lại.",
        );
        if (submitted) setUncertain(true);
      }
    } finally {
      sending.current = false;
      if (!controller.signal.aborted && current.current === owner)
        setBusy(false);
    }
  }
  if (!owner) return null;
  function resetScope() {
    setRows([]);
    setLoaded(false);
    setSelected(null);
    setEditing(false);
    setMessage("");
  }
  function edit(row: Definition | null) {
    setSelected(row);
    setCode(row?.code ?? "");
    setLabel(row?.name ?? "");
    setResponse(row?.start ?? "");
    setResolution(row?.end ?? "");
    setOvernight(row?.crossesMidnight ?? false);
    setEditing(true);
  }
  return (
    <section className="shift-card">
      <h2>Mẫu ca làm việc</h2>
      <p>
        Thời gian áp dụng khi lập ca mới. Thời gian phân ca hiện có giữ nguyên.
      </p>
      <div className="shift-actions">
        <button disabled={busy} onClick={() => void load()}>
          Tải mẫu ca
        </button>
        {busy && <LoadingIndicator compact label="Đang xử lý mẫu ca" />}
      </div>
      {message && <p role="status">{userMessage(message)}</p>}
      {loaded && (
        <>
          <button disabled={busy || uncertain} onClick={() => edit(null)}>
            Tạo mẫu ca
          </button>
          <div className="shift-table-scroll">
            <table>
              <thead>
                <tr>
                  <th>Mã ca</th>
                  <th>Tên</th>
                  <th>Bắt đầu</th>
                  <th>Kết thúc</th>
                  <th>Qua nửa đêm</th>
                  <th>Trạng thái</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={r.id}>
                    <td>{r.code}</td>
                    <td>{r.name}</td>
                    <td>{r.start.slice(0, 5)}</td>
                    <td>{r.end.slice(0, 5)}</td>
                    <td>{r.crossesMidnight ? "Có" : "Không"}</td>
                    <td>{r.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}</td>
                    <td>
                      <button
                        disabled={busy || uncertain}
                        onClick={() => edit(r)}
                      >
                        Chỉnh sửa
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {rows.length === 100 && (
            <button
              disabled={busy}
              onClick={() => void load(rows[rows.length - 1].id)}
            >
              Trang sau
            </button>
          )}
        </>
      )}
      {editing && (
        <ActionDialog
          scope="workday/ShiftDefinitionsPanel.tsx"
          open
          title="Mẫu ca làm việc"
          onClose={() => setEditing(false)}
          busy={busy}
          locked={uncertain}
          error={message}
        >
          <form
            onSubmit={(e) => {
              e.preventDefault();
              void save();
            }}
          >
            <fieldset disabled={busy || uncertain}>
              <legend>{selected ? "Chỉnh sửa mẫu ca" : "Mẫu ca mới"}</legend>
              <label>
                Mã
                <input
                  required
                  maxLength={30}
                  value={code}
                  onChange={(e) => setCode(e.target.value)}
                />
              </label>
              <label>
                Tên
                <input
                  required
                  maxLength={100}
                  value={label}
                  onChange={(e) => setLabel(e.target.value)}
                />
              </label>
              <label>
                Bắt đầu
                <input
                  required
                  type="time"
                  step="1"
                  value={response}
                  onChange={(e) => setResponse(e.target.value)}
                />
              </label>
              <label>
                Kết thúc
                <input
                  required
                  type="time"
                  step="1"
                  value={resolution}
                  onChange={(e) => setResolution(e.target.value)}
                />
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={overnight}
                  onChange={(e) => setOvernight(e.target.checked)}
                />
                Kết thúc vào ngày hôm sau
              </label>
              <button data-dialog-action>Lưu mẫu ca</button>
              <button type="button" onClick={() => setEditing(false)}>
                Hủy
              </button>
            </fieldset>
          </form>
        </ActionDialog>
      )}
    </section>
  );
}
