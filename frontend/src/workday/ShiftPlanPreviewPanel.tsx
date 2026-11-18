import { ActionDialog } from "../components/workspace-interactions";
import { ShiftPlanApiError, type SavePlanAdapter } from "./shiftPlanApi";
import { useContext, useEffect, useRef, useState } from "react";
import { LoadingIndicator } from "../components/LoadingIndicator";
import {
  ShiftPlanningContext,
  usePlanningBusy,
  usePlanningChoices,
  localDate,
  planningDateTime,
} from "./shiftPlanningData";
import { useAuth } from "../auth/AuthProvider";
import {
  decodePreview,
  draftSnapshot,
  issueLabels,
  type Preview,
  type PreviewAdapter,
  type ShiftDraft,
} from "./shiftPlanPreview";
/** Review and save future assignments; all scope and conflict checks are enforced by the API. */
export function ShiftPlanPreviewPanel({
  adapter,
  saveAdapter,
  onSaved,
}: {
  adapter?: PreviewAdapter;
  saveAdapter?: SavePlanAdapter;
  onSaved?: () => void;
}) {
  const { state, actions } = useAuth();
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.pending !== null ||
    state.session.mustChangePassword
  )
    return (
      <p role="alert">Cần phiên đăng nhập đã xác minh và không bị hạn chế.</p>
    );
  return (
    <Bound
      key={`${identityKey(state.session)}:${adapter ? identityKey(adapter) : 0}:${saveAdapter ? identityKey(saveAdapter) : 0}`}
      adapter={adapter}
      saveAdapter={saveAdapter}
      onSaved={onSaved}
      recover={() => void actions.revalidate()}
    />
  );
}
const keys = new WeakMap<object, number>();
let serial = 0;
function identityKey(o: object) {
  if (!keys.has(o)) keys.set(o, ++serial);
  return keys.get(o)!;
}
function Bound({
  adapter,
  saveAdapter,
  recover,
  onSaved,
}: {
  adapter?: PreviewAdapter;
  saveAdapter?: SavePlanAdapter;
  recover: () => void;
  onSaved?: () => void;
}) {
  const planning = useContext(ShiftPlanningContext);
  const choices = usePlanningChoices(planning?.scope);
  const [drafts, setDrafts] = useState<readonly ShiftDraft[]>([]),
    [fields, setFields] = useState({
      employeeId: "",
      branchId: String(planning?.scope.branchId ?? ""),
      departmentId: String(planning?.scope.departmentId ?? ""),
      templateId: "",
      shiftDate: localDate(),
    });
  const [preview, setPreview] = useState<Preview | null>(null),
    [message, setMessage] = useState(""),
    [messageSucceeded, setMessageSucceeded] = useState(false),
    [pending, setPending] = useState(false);
  const [saveReview, setSaveReview] = useState(false),
    [sessionBlocked, setSessionBlocked] = useState(false);
  const saveAttempted = useRef(false);
  const [canStartNew, setCanStartNew] = useState(false);
  usePlanningBusy("plan", pending);
  const reportDrafts = planning?.reportDrafts;
  useEffect(() => {
    reportDrafts?.(drafts.length > 0);
    return () => reportDrafts?.(false);
  }, [reportDrafts, drafts.length]);
  const epoch = useRef(0),
    alive = useRef(true),
    busy = useRef(false),
    abort = useRef<AbortController | null>(null),
    latest = useRef(adapter);
  latest.current = adapter;
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
      epoch.current++;
      abort.current?.abort();
    };
  }, []);
  useEffect(() => {
    epoch.current++;
    abort.current?.abort();
    busy.current = false;
    setPending(false);
    setPreview(null);
  }, [adapter]);
  function invalidate() {
    setMessageSucceeded(false);
    setSaveReview(false);
    epoch.current++;
    abort.current?.abort();
    busy.current = false;
    setPending(false);
    setPreview(null);
    setMessage("");
  }
  async function run() {
    if (!adapter || busy.current || sessionBlocked) return;
    setMessageSucceeded(false);
    const snapshot = draftSnapshot(drafts);
    if (!snapshot) {
      setMessage("Thêm bản nháp hợp lệ trước khi xem trước.");
      return;
    }
    busy.current = true;
    setPending(true);
    setPreview(null);
    setMessage("");
    const generation = ++epoch.current;
    const controller = new AbortController();
    abort.current = controller;
    const current = () =>
      alive.current &&
      epoch.current === generation &&
      latest.current === adapter &&
      !controller.signal.aborted;
    try {
      const raw = await adapter(snapshot, controller.signal);
      if (!current()) return;
      const result = decodePreview(raw, snapshot.length);
      if (result) setPreview(result);
      else
        setMessage(
          "Không xem trước được: phản hồi máy chủ không hợp lệ. Không được phép lưu.",
        );
    } catch (error) {
      if (current()) {
        if (error instanceof ShiftPlanApiError && error.status === 401) {
          setSessionBlocked(true);
          setDrafts([]);
          setPreview(null);
        }
        setMessage(
          "Không kiểm tra được phân công. Vui lòng tải lại danh sách hoặc kiểm tra kết nối.",
        );
      }
    } finally {
      if (current()) {
        busy.current = false;
        setPending(false);
      }
    }
  }
  async function savePlan() {
    if (
      !saveAdapter ||
      !preview ||
      preview.rows.some((r) => r.issues.length) ||
      busy.current ||
      saveAttempted.current ||
      sessionBlocked
    )
      return;
    const snapshot = draftSnapshot(drafts);
    if (!snapshot) return;
    saveAttempted.current = true;
    setMessageSucceeded(false);
    busy.current = true;
    setPending(true);
    setSaveReview(false);
    const generation = ++epoch.current;
    const controller = new AbortController();
    abort.current = controller;
    try {
      const result = await saveAdapter(snapshot, controller.signal);
      if (
        !alive.current ||
        generation !== epoch.current ||
        controller.signal.aborted
      )
        return;
      setPreview(null);
      setDrafts([]);
      if (result.ok) {
        setMessageSucceeded(true);
        setCanStartNew(true);
        setMessage(
          `Đã lưu ${result.createdAssignments} phân công; ${result.createdShifts} ca mới đã tạo. Thông báo nhân viên đang chờ gửi.`,
        );
        onSaved?.();
      } else {
        setCanStartNew(
          result.outcome !== "OUTCOME_UNKNOWN" && result.status !== 401,
        );
        if (result.status === 401) setSessionBlocked(true);
        setMessage(
          result.outcome === "OUTCOME_UNKNOWN"
            ? "Chưa xác định được kết quả lưu. Kiểm tra mục Lịch đã phân trước khi lập lại để tránh trùng ca."
            : result.status === 409
              ? "Có phân công bị trùng hoặc đã thay đổi. Tải lại danh sách ca: nhân viên có thể đã được giao, phân công đã hủy hoặc ca theo ngày khác định nghĩa hiện tại."
              : "Chưa xác nhận kế hoạch ca. Không tự gửi lại.",
        );
      }
    } catch {
      if (alive.current && generation === epoch.current) {
        setPreview(null);
        setDrafts([]);
        setMessage(
          "Chưa xác định được kết quả lưu. Kiểm tra mục Lịch đã phân trước khi lập lại để tránh trùng ca.",
        );
      }
    } finally {
      if (alive.current && generation === epoch.current) {
        busy.current = false;
        setPending(false);
      }
    }
  }
  const locked = pending || sessionBlocked || saveAttempted.current;
  const employees = choices.data?.employees ?? [],
    templates = choices.data?.templates ?? [];
  const employeeName = (id: number) =>
    employees.find((e) => e.id === id)?.name ?? "Nhân viên đã chọn";
  const templateName = (id: number) =>
    templates.find((t) => t.id === id)?.name ?? "Ca đã chọn";
  return (
    <section className="shift-card" aria-label="Lập phân công ca">
      <div className="shift-section-heading">
        <div>
          <h2>Phân công nhân viên</h2>
          <p>Thêm từng người vào ca, sau đó kiểm tra toàn bộ danh sách.</p>
        </div>
        <span className="shift-badge">{drafts.length} phân công đang soạn</span>
      </div>
      {!adapter && (
        <p role="alert">Chức năng lập phân công hiện chưa khả dụng.</p>
      )}
      <button
        className="shift-quiet"
        disabled={locked || choices.loading || drafts.length > 0}
        onClick={choices.reload}
      >
        Cập nhật danh sách ca và nhân viên
      </button>
      {choices.loading && (
        <LoadingIndicator label="Đang tải mẫu ca và nhân viên" />
      )}
      {choices.error && (
        <div className="shift-notice" role="alert">
          {choices.error}{" "}
          <button disabled={locked} onClick={choices.reload}>
            Tải lại danh sách
          </button>
        </div>
      )}
      {choices.data && !employees.length && (
        <p className="shift-notice">
          Bộ phận này chưa có nhân viên đủ điều kiện phân ca. Cần khai báo nhân
          viên thuộc bộ phận trước khi lập kế hoạch.
        </p>
      )}
      {choices.data && !templates.length && (
        <p className="shift-notice">
          Chi nhánh chưa có mẫu ca đang hoạt động. Mở mục Mẫu ca để tạo trước.
        </p>
      )}
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (
            locked ||
            !planning ||
            !employees.some((e) => e.id === Number(fields.employeeId)) ||
            !templates.some((t) => t.id === Number(fields.templateId))
          )
            return;
          const draft = {
            employeeId: Number(fields.employeeId),
            branchId: planning.scope.branchId,
            departmentId: planning.scope.departmentId,
            templateId: Number(fields.templateId),
            shiftDate: fields.shiftDate,
          };
          if (!draftSnapshot([draft]) || drafts.length >= 500) {
            setMessage(
              "Kiểm tra ngày làm việc và danh sách phân công. Mỗi lần lưu tối đa 500 phân công.",
            );
            return;
          }
          if (
            drafts.some(
              (d) =>
                d.employeeId === draft.employeeId &&
                d.templateId === draft.templateId &&
                d.shiftDate === draft.shiftDate,
            )
          ) {
            setMessage(
              "Nhân viên này đã có trong danh sách cho cùng ca và ngày.",
            );
            return;
          }
          invalidate();
          setDrafts((v) => [...v, draft]);
          setFields((v) => ({ ...v, employeeId: "" }));
        }}
      >
        <fieldset
          className="shift-form-grid"
          disabled={locked || choices.loading || !choices.data}
        >
          <label>
            Ngày làm việc
            <input
              required
              type="date"
              min={localDate()}
              value={fields.shiftDate}
              onChange={(e) => {
                invalidate();
                setFields((v) => ({ ...v, shiftDate: e.target.value }));
              }}
            />
          </label>
          <label>
            Ca làm việc
            <select
              aria-label="Ca làm việc"
              required
              value={fields.templateId}
              onChange={(e) => {
                invalidate();
                setFields((v) => ({ ...v, templateId: e.target.value }));
              }}
            >
              <option value="">Chọn ca làm việc</option>
              {templates.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name} · {t.start.slice(0, 5)} – {t.end.slice(0, 5)}
                  {t.crossesMidnight ? " (hôm sau)" : ""}
                </option>
              ))}
            </select>
          </label>
          <label>
            Nhân viên
            <select
              aria-label="Nhân viên"
              required
              value={fields.employeeId}
              onChange={(e) => {
                invalidate();
                setFields((v) => ({ ...v, employeeId: e.target.value }));
              }}
            >
              <option value="">Chọn nhân viên</option>
              {employees.map((e) => (
                <option key={e.id} value={e.id}>
                  {e.name}
                </option>
              ))}
            </select>
          </label>
          <button
            className="shift-primary"
            disabled={!employees.length || !templates.length}
          >
            Thêm vào danh sách
          </button>
        </fieldset>
      </form>
      <div className="shift-draft-heading">
        <h3>Danh sách chờ lưu</h3>
        {drafts.length > 0 && (
          <button
            className="shift-quiet"
            disabled={locked}
            onClick={() => {
              invalidate();
              setDrafts([]);
            }}
          >
            Xóa danh sách
          </button>
        )}
      </div>
      {!drafts.length ? (
        <p className="shift-empty-small">
          Chưa có phân công. Chọn ngày, ca và nhân viên ở phía trên.
        </p>
      ) : (
        <div className="shift-table-scroll">
          <table>
            <thead>
              <tr>
                <th>Nhân viên</th>
                <th>Ca làm việc</th>
                <th>Ngày</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {drafts.map((d, i) => (
                <tr key={`${d.employeeId}:${d.templateId}:${d.shiftDate}`}>
                  <td>
                    <strong>{employeeName(d.employeeId)}</strong>
                  </td>
                  <td>{templateName(d.templateId)}</td>
                  <td>{d.shiftDate.split("-").reverse().join("/")}</td>
                  <td>
                    <button
                      className="shift-quiet"
                      aria-label={`Xóa phân công ${i + 1}`}
                      disabled={locked}
                      onClick={() => {
                        invalidate();
                        setDrafts((v) => v.filter((_, n) => n !== i));
                      }}
                    >
                      Xóa
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <div className="shift-actions">
        <button
          className="shift-primary"
          disabled={!adapter || !drafts.length || locked}
          onClick={() => void run()}
        >
          Kiểm tra phân công
        </button>
        {pending && <LoadingIndicator compact label="Đang xử lý kế hoạch ca" />}
      </div>
      {message && (
        <p
          className={`shift-notice${messageSucceeded ? " shift-notice-success" : ""}`}
          role={messageSucceeded ? "status" : "alert"}
        >
          {message}
        </p>
      )}
      {canStartNew && !pending && !sessionBlocked && (
        <button
          onClick={() => {
            saveAttempted.current = false;
            setCanStartNew(false);
            invalidate();
            setDrafts([]);
            setFields((v) => ({ ...v, employeeId: "" }));
          }}
        >
          Lập phân công khác
        </button>
      )}
      {sessionBlocked && (
        <button onClick={recover}>Kiểm tra phiên đăng nhập</button>
      )}
      {preview && (
        <section
          className="shift-review"
          aria-label="Kết quả kiểm tra phân công"
        >
          <h3>Kết quả kiểm tra</h3>
          <p>
            Giờ hiển thị theo chi nhánh. Hệ thống kiểm tra lại xung đột khi lưu.
          </p>
          <ul>
            {preview.rows.map((r) => (
              <li key={r.index}>
                <strong>
                  {employeeName(drafts[r.index].employeeId)} ·{" "}
                  {templateName(drafts[r.index].templateId)}
                </strong>
                <p>
                  {r.startUtc
                    ? planningDateTime(r.startUtc, planning?.scope.timezone)
                    : "Chưa xác định giờ bắt đầu"}
                  {r.endUtc &&
                    ` — ${planningDateTime(r.endUtc, planning?.scope.timezone)}`}
                </p>
                {r.issues.length ? (
                  <ul>
                    {r.issues.map((issue) => (
                      <li key={issue}>{issueLabels[issue]}</li>
                    ))}
                  </ul>
                ) : (
                  <span className="shift-success">Hợp lệ để tiếp tục</span>
                )}
              </li>
            ))}
          </ul>
        </section>
      )}
      {saveAdapter &&
        preview &&
        !sessionBlocked &&
        !saveAttempted.current &&
        preview.rows.every((r) => r.issues.length === 0) && (
          <div className="shift-save">
            <button
              className="shift-primary"
              disabled={pending}
              onClick={() => setSaveReview(true)}
            >
              Tiếp tục lưu kế hoạch
            </button>
            {saveReview && (
              <ActionDialog
                open
                title="Xác nhận lưu kế hoạch ca"
                onClose={() => setSaveReview(false)}
                busy={pending}
              >
                <div
                  className="shift-confirm"
                  role="region"
                  aria-label="Xác nhận lưu kế hoạch"
                >
                  <h3>Lưu {drafts.length} phân công?</h3>
                  <p>
                    {planning?.scope.departmentName} ·{" "}
                    {planning?.scope.branchName}. Nhân viên sẽ được thông báo
                    sau khi lưu thành công.
                  </p>
                  <div className="shift-actions">
                    <button
                      data-dialog-action
                      className="shift-primary"
                      disabled={pending}
                      onClick={() => void savePlan()}
                    >
                      Xác nhận lưu kế hoạch
                    </button>
                    <button
                      disabled={pending}
                      onClick={() => setSaveReview(false)}
                    >
                      Quay lại kiểm tra
                    </button>
                  </div>
                </div>
              </ActionDialog>
            )}
          </div>
        )}
    </section>
  );
}
