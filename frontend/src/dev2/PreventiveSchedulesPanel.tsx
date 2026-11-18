import { ActionDialog } from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { PreventiveTechnicianSelect } from "./PreventiveTechnicianSelect";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";

type Check = { key: string; description: string; required: boolean };
type Schedule = {
  scheduleId: number;
  scheduleCode: string;
  title: string | null;
  instruction: string | null;
  frequency: string;
  interval: number;
  nextDueAt: string;
  assetId: number | null;
  roomId: number | null;
  areaId: number | null;
  defaultAssigneeId: number | null;
  isActive: boolean;
  version: string;
  checkItems: Check[] | null;
};
type Target = { id: number; kind: string; label: string };
class ScheduleRequestError extends Error {
  constructor(
    readonly status: number,
    readonly code: string | null,
    readonly title: string | null,
    message: string,
  ) {
    super(message);
  }
}
const positive = (x: unknown): x is number =>
  typeof x === "number" && Number.isSafeInteger(x) && x > 0;
function schedule(x: unknown): x is Schedule {
  return (
    isObject(x) &&
    positive(x.scheduleId) &&
    typeof x.scheduleCode === "string" &&
    (x.title === null || typeof x.title === "string") &&
    (x.instruction === null || typeof x.instruction === "string") &&
    typeof x.frequency === "string" &&
    positive(x.interval) &&
    typeof x.nextDueAt === "string" &&
    Number.isFinite(Date.parse(x.nextDueAt)) &&
    [x.assetId, x.roomId, x.areaId, x.defaultAssigneeId].every(
      (v) => v === null || positive(v),
    ) &&
    typeof x.isActive === "boolean" &&
    typeof x.version === "string" &&
    /^"[0-9A-F]{16}"$/.test(x.version) &&
    (x.checkItems === null ||
      (Array.isArray(x.checkItems) &&
        x.checkItems.every(
          (c) =>
            isObject(c) &&
            typeof c.key === "string" &&
            typeof c.description === "string" &&
            typeof c.required === "boolean",
        )))
  );
}
export function PreventiveSchedulesPanel({ branchId }: { branchId: number }) {
  const { state } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" && !state.pending
      ? state.authorityGeneration
      : null;
  const mounted = useRef(owner).current,
    current = useRef(owner);
  current.current = owner;
  const abort = useRef(new AbortController());
  useEffect(() => {
    const controller = new AbortController();
    abort.current = controller;
    return () => controller.abort();
  }, []);
  const [rows, setRows] = useState<Schedule[]>([]),
    [targets, setTargets] = useState<Target[]>([]);
  const [selected, setSelected] = useState<Schedule>();
  const [kind, setKind] = useState("ROOM"),
    [target, setTarget] = useState("");
  const [title, setTitle] = useState(""),
    [instruction, setInstruction] = useState("");
  const [frequency, setFrequency] = useState("DAYS"),
    [interval, setInterval] = useState("1");
  const [due, setDue] = useState(""),
    [assignee, setAssignee] = useState("");
  const [checks, setChecks] = useState<Check[]>([]),
    [reason, setReason] = useState("");
  const [confirmDeactivation, setConfirmDeactivation] = useState(false);
  const [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false),
    [authorized, setAuthorized] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const sending = useRef(false);
  const [cursor, setCursor] = useState(0);
  const [editorOpen, setEditorOpen] = useState(false);
  const root = `/api/branches/${branchId}/preventive-schedules`;
  const live = () =>
    owner !== null &&
    current.current === owner &&
    !abort.current.signal.aborted;
  async function request(url: string, init?: RequestInit) {
    const response = await fetch(url, {
      ...init,
      credentials: "same-origin",
      cache: "no-store",
      redirect: "error",
      signal: abort.current.signal,
    });
    if (!response.ok) {
      const problem: unknown = await response.json().catch(() => null);
      const code =
        isObject(problem) && typeof problem.code === "string"
          ? problem.code
          : null;
      const title =
        isObject(problem) && typeof problem.title === "string"
          ? problem.title
          : null;
      const knownValidation =
        response.status === 400 &&
        (title === "Invalid preventive schedule" ||
          title === "Invalid deactivation reason");
      throw new ScheduleRequestError(
        response.status,
        code,
        title,
        code === "DUPLICATE_ACTIVE_SCHEDULE"
          ? "MSG35: Đối tượng đã có lịch hoạt động cùng tiêu đề. Đổi tiêu đề và lưu lại."
          : knownValidation &&
              isObject(problem) &&
              typeof problem.detail === "string"
            ? problem.detail
            : response.status === 412
              ? "Lịch đã đổi. Tải lại trước khi sửa."
              : `Yêu cầu chưa khả dụng (${response.status}). Kiểm tra quyền và cấu hình hiện tại.`,
      );
    }
    return (await response.json()) as unknown;
  }
  async function load(more = false) {
    if (!live() || sending.current || (more && uncertain)) return;
    sending.current = true;
    setBusy(true);
    setMessage("");
    try {
      const raw = await request(
        `${root}?afterId=${more ? cursor : 0}&limit=50`,
      );
      if (!Array.isArray(raw) || !raw.every(schedule))
        throw Error("Phản hồi lịch không hợp lệ.");
      if (live()) {
        setRows(
          more
            ? [
                ...rows.filter(
                  (r) => !raw.some((n) => n.scheduleId === r.scheduleId),
                ),
                ...raw,
              ].sort((a, b) => a.scheduleId - b.scheduleId)
            : raw,
        );
        setCursor(raw.at(-1)?.scheduleId ?? (more ? cursor : 0));
        setAuthorized(true);
        if (!more) {
          setUncertain(false);
          edit();
          setTargets([]);
          setTarget("");
        }
      }
    } catch (e) {
      if (live()) {
        setAuthorized(false);
        setRows([]);
        setSelected(undefined);
        setMessage(String(e));
      }
    } finally {
      sending.current = false;
      if (live()) setBusy(false);
    }
  }
  async function loadTargets(more = false) {
    if (!live() || sending.current) return;
    sending.current = true;
    setBusy(true);
    try {
      const raw = await request(
        `${root}/targets?kind=${kind}&afterId=${more ? (targets.at(-1)?.id ?? 0) : 0}`,
      );
      if (
        !Array.isArray(raw) ||
        !raw.every(
          (x) =>
            isObject(x) &&
            positive(x.id) &&
            x.kind === kind &&
            typeof x.label === "string",
        )
      )
        throw Error("Mục tiêu không hợp lệ.");
      if (live())
        setTargets(
          more ? [...targets, ...(raw as Target[])] : (raw as Target[]),
        );
    } catch (e) {
      if (live()) {
        setTargets([]);
        setTarget("");
        setMessage(String(e));
      }
    } finally {
      sending.current = false;
      if (live()) setBusy(false);
    }
  }
  function edit(row?: Schedule) {
    setSelected(row);
    setTitle(row?.title ?? "");
    setInstruction(row?.instruction ?? "");
    setFrequency(row?.frequency ?? "DAYS");
    setInterval(String(row?.interval ?? 1));
    setAssignee(row?.defaultAssigneeId?.toString() ?? "");
    setChecks(row?.checkItems ?? []);
    setReason("");
    setDue("");
    setConfirmDeactivation(false);
  }
  async function save(deactivate = false) {
    if (!live() || sending.current || uncertain) return;
    if (
      !deactivate &&
      (!title.trim() ||
        !instruction.trim() ||
        !positive(Number(interval)) ||
        (!selected &&
          (!targets.some((t) => t.id === Number(target)) ||
            !due ||
            !Number.isFinite(Date.parse(due)))) ||
        (assignee !== "" && !positive(Number(assignee))))
    ) {
      setMessage("Nhập đầy đủ các trường bắt buộc của lịch.");
      return;
    }
    if (
      deactivate &&
      (!selected || !confirmDeactivation || reason.trim().length < 10)
    ) {
      setMessage("Xác nhận vô hiệu hóa và nhập lý do ít nhất 10 ký tự.");
      return;
    }
    sending.current = true;
    setBusy(true);
    setMessage("");
    let submitted = false;
    try {
      const csrf = await request("/api/auth/csrf");
      if (
        !isObject(csrf) ||
        csrf.headerName !== "X-CSRF-TOKEN" ||
        typeof csrf.csrfToken !== "string" ||
        !/^[\x21-\x7e]+$/.test(csrf.csrfToken) ||
        !live()
      )
        throw Error("Phiên đăng nhập đã thay đổi. Vui lòng tải lại.");
      const body = deactivate
        ? { reason }
        : {
            title,
            instruction,
            frequency,
            interval: Number(interval),
            firstDueAt:
              selected?.nextDueAt ?? new Date(due + "+07:00").toISOString(),
            assetId: selected
              ? selected.assetId
              : kind === "ASSET"
                ? Number(target)
                : null,
            roomId: selected
              ? selected.roomId
              : kind === "ROOM"
                ? Number(target)
                : null,
            areaId: selected
              ? selected.areaId
              : kind === "AREA"
                ? Number(target)
                : null,
            defaultAssigneeId: assignee === "" ? null : Number(assignee),
            checkItems: checks,
          };
      submitted = true;
      const raw = await request(
        root +
          (selected ? `/${selected.scheduleId}` : "") +
          (deactivate ? "/deactivate" : ""),
        {
          method: selected && !deactivate ? "PUT" : "POST",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            ...(selected ? { "If-Match": selected.version } : {}),
          },
          body: JSON.stringify(body),
        },
      );
      if (!schedule(raw)) throw Error("Không xác minh được phản hồi lưu.");
      if (live()) {
        setRows((old) =>
          [...old.filter((r) => r.scheduleId !== raw.scheduleId), raw].sort(
            (a, b) => a.scheduleId - b.scheduleId,
          ),
        );
        edit(raw);
        setEditorOpen(false);
        setMessage(
          "Đã lưu lịch. Các lần thực hiện hiện có giữ định nghĩa ban đầu.",
        );
      }
    } catch (e) {
      if (live()) {
        const rejected =
          e instanceof ScheduleRequestError &&
          ((e.status === 409 && e.code === "DUPLICATE_ACTIVE_SCHEDULE") ||
            (e.status === 400 &&
              (e.title === "Invalid preventive schedule" ||
                e.title === "Invalid deactivation reason")));
        setMessage(
          String(e) +
            (submitted && !rejected
              ? "Tải lại lịch trước khi thay đổi tiếp."
              : ""),
        );
        if (submitted && !rejected) setUncertain(true);
        if (
          e instanceof ScheduleRequestError &&
          (e.status === 401 || e.status === 403)
        ) {
          setAuthorized(false);
          setRows([]);
          setSelected(undefined);
        }
      }
    } finally {
      sending.current = false;
      if (live()) setBusy(false);
    }
  }
  if (!owner || owner !== mounted || !positive(branchId)) return null;
  return (
    <section aria-label="Lịch bảo trì định kỳ">
      <h2>Lịch bảo trì định kỳ</h2>
      <button disabled={busy} onClick={() => void load()}>
        Tải lịch
      </button>
      {rows.length > 0 && (
        <button disabled={busy || uncertain} onClick={() => void load(true)}>
          Tải trang sau
        </button>
      )}
      {message && <p role="status">{message}</p>}
      <ul>
        {rows.map((r) => (
          <li key={r.scheduleId}>
            <button
              disabled={busy || uncertain}
              onClick={() => {
                edit(r);
                setEditorOpen(true);
              }}
            >
              {r.title ?? r.scheduleCode}
            </button>{" "}
            — {r.isActive ? "Đang hoạt động" : "Ngừng hoạt động"} —{" "}
            {new Date(r.nextDueAt).toLocaleString("vi-VN", {
              timeZone: "Asia/Ho_Chi_Minh",
            })}{" "}
            (UTC+7)
          </li>
        ))}
      </ul>
      {authorized && (
        <>
          <button
            disabled={busy || uncertain}
            onClick={() => {
              edit();
              setEditorOpen(true);
            }}
          >
            Lịch mới
          </button>
          <ActionDialog
            open={editorOpen}
            title="Lịch bảo trì định kỳ"
            onClose={() => setEditorOpen(false)}
            busy={busy}
            error={message}
            drawer
          >
            {" "}
            <fieldset
              disabled={
                busy ||
                uncertain ||
                selected?.isActive === false ||
                selected?.title === null
              }
            >
              {!selected && (
                <>
                  <label>
                    Loại đối tượng
                    <select
                      value={kind}
                      onChange={(e) => {
                        setKind(e.target.value);
                        setTargets([]);
                        setTarget("");
                      }}
                    >
                      <option value="ROOM">Phòng</option>
                      <option value="AREA">Khu vực</option>
                      <option value="ASSET">Tài sản</option>
                    </select>
                  </label>
                  <button onClick={() => void loadTargets()}>
                    Tải mục tiêu
                  </button>
                  <button
                    disabled={!targets.length}
                    onClick={() => void loadTargets(true)}
                  >
                    Thêm đối tượng
                  </button>
                  <label>
                    Đối tượng
                    <select
                      value={target}
                      onChange={(e) => setTarget(e.target.value)}
                    >
                      <option value="">Chọn đối tượng</option>
                      {targets.map((t) => (
                        <option key={t.id} value={t.id}>
                          {t.label}
                        </option>
                      ))}
                    </select>
                  </label>
                </>
              )}
              <label>
                Tiêu đề
                <input
                  maxLength={200}
                  disabled={!!selected}
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                />
              </label>
              <label>
                Mô tả công việc
                <textarea
                  maxLength={16000}
                  value={instruction}
                  onChange={(e) => setInstruction(e.target.value)}
                />
              </label>
              <label>
                Lặp mỗi
                <input
                  type="number"
                  min="1"
                  value={interval}
                  onChange={(e) => setInterval(e.target.value)}
                />
              </label>
              <select
                aria-label="Đơn vị khoảng thời gian"
                value={frequency}
                onChange={(e) => setFrequency(e.target.value)}
              >
                <option>DAYS</option>
                <option>WEEKS</option>
                <option>MONTHS</option>
              </select>
              {!selected && (
                <label>
                  Hạn đầu tiên (UTC+7)
                  <input
                    type="datetime-local"
                    value={due}
                    onChange={(e) => setDue(e.target.value)}
                  />
                </label>
              )}
              <PreventiveTechnicianSelect
                url={`${root}/technicians`}
                value={assignee}
                onChange={setAssignee}
                optional
              />
              <p>
                Nhân viên đã chọn phải có quyền xem và thực hiện bảo trì trong
                chi nhánh hiện tại.
              </p>
              {checks.map((c, i) => (
                <div key={c.key}>
                  <input
                    aria-label={`Kiểm tra ${i + 1}`}
                    maxLength={1000}
                    value={c.description}
                    onChange={(e) =>
                      setChecks(
                        checks.map((x, j) =>
                          i === j ? { ...x, description: e.target.value } : x,
                        ),
                      )
                    }
                  />
                  <label>
                    <input
                      type="checkbox"
                      checked={c.required}
                      onChange={(e) =>
                        setChecks(
                          checks.map((x, j) =>
                            i === j ? { ...x, required: e.target.checked } : x,
                          ),
                        )
                      }
                    />
                    Bắt buộc
                  </label>
                  <button
                    onClick={() => setChecks(checks.filter((_, j) => j !== i))}
                  >
                    Xóa hạng mục kiểm tra
                  </button>
                </div>
              ))}
              <button
                disabled={checks.length >= 100}
                onClick={() =>
                  setChecks([
                    ...checks,
                    {
                      key: crypto.randomUUID(),
                      description: "",
                      required: true,
                    },
                  ])
                }
              >
                Thêm hạng mục kiểm tra
              </button>
              <button data-dialog-action onClick={() => void save()}>
                Lưu lịch
              </button>
            </fieldset>
            {selected?.isActive && (
              <fieldset disabled={busy || uncertain}>
                <label>
                  Lý do vô hiệu hóa
                  <textarea
                    maxLength={4000}
                    value={reason}
                    onChange={(e) => {
                      setReason(e.target.value);
                      setConfirmDeactivation(false);
                    }}
                  />
                </label>
                <label>
                  <input
                    type="checkbox"
                    checked={confirmDeactivation}
                    onChange={(e) => setConfirmDeactivation(e.target.checked)}
                  />
                  Xác nhận vô hiệu hóa. Các lần thực hiện đã tạo giữ nguyên.
                </label>
                <button
                  disabled={!confirmDeactivation || reason.trim().length < 10}
                  onClick={() => void save(true)}
                >
                  Vô hiệu hóa lịch
                </button>
              </fieldset>
            )}
          </ActionDialog>{" "}
        </>
      )}
    </section>
  );
}
