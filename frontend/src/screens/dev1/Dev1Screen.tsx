import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { ApiError } from "../../api/http";
import { currentSession, subscribe } from "../../services";
import {
  actionLabels,
  fields,
  load,
  obj,
  routeKinds,
  save,
  specialFields,
  titles,
  type Action,
  type Field,
  type Kind,
  type Loaded,
  type RecordValue,
} from "./model";
import "./dev1.css";
const links: [string, string][] = [
  ["/users", "Quản lý tài khoản"],
  ["/branches", "Chi nhánh"],
  ["/locations", "Tầng / khu vực"],
  ["/room-types", "Loại phòng"],
  ["/rooms", "Phòng"],
  ["/departments", "Phòng ban"],
  ["/checklists", "Checklist"],
  ["/cleaning/assignment", "Phân công"],
  ["/inspections/queue", "Kiểm tra phòng"],
  ["/cleaning/tasks", "Tiến độ cleaning"],
  ["/dev1/task-progress", "Tiến độ công việc"],
  ["/rooms/turnover", "Lịch sử làm phòng"],
  ["/inspections/history", "Lịch sử kiểm tra"],
];
export function Dev1Navigation() {
  const [q] = useSearchParams();
  const b = q.get("branchId");
  return (
    <nav aria-label="Nghiệp vụ khách sạn" className="dev1-nav">
      {links.map(([url, label]) => (
        <Link
          key={url}
          to={url + (b ? `?branchId=${encodeURIComponent(b)}` : "")}
        >
          {label}
        </Link>
      ))}
    </nav>
  );
}
const integer = (v: string | null) => {
  const n = Number(v);
  return Number.isSafeInteger(n) && n > 0 ? n : undefined;
};
function errorText(e: unknown) {
  if (e instanceof ApiError) {
    const explanation: Record<number, string> = {
      401: "Phiên đăng nhập không còn hợp lệ. Vui lòng đăng nhập lại.",
      403: "Tài khoản không có quyền thực hiện thao tác trong chi nhánh này.",
      404: "Không tìm thấy dữ liệu hoặc không có quyền xem.",
      409: "Không thể thực hiện do trạng thái hoặc dữ liệu liên quan.",
      412: "Dữ liệu đã thay đổi. Tải lại, kiểm tra và nhập lại thay đổi.",
      428: "Chưa nhận được phiên bản dữ liệu. Hãy tải lại chi tiết.",
      503: "Backend chưa sẵn sàng. Kiểm tra schema và log backend.",
    };
    return `${explanation[e.status] ?? e.message} ${e.problem.detail ?? ""} ${JSON.stringify(e.fieldErrors ?? {}) === "{}" ? "" : JSON.stringify(e.fieldErrors)}${e.outcomeUnknown ? " Chưa xác định thao tác đã được lưu hay chưa; kiểm tra dữ liệu trước khi thử lại." : ""}`;
  }
  return e instanceof Error ? e.message : "Không tải được dữ liệu";
}
function Details({ value }: { value: unknown }) {
  if (value === null || value === undefined)
    return <span>Chưa có dữ liệu</span>;
  if (Array.isArray(value))
    return value.length ? (
      <div className="dev1-records">
        {value.map((v, i) => (
          <section key={i}>
            <Details value={v} />
          </section>
        ))}
      </div>
    ) : (
      <p>Danh sách trống.</p>
    );
  if (typeof value === "object")
    return (
      <dl className="dev1-details">
        {Object.entries(obj(value)).map(([k, v]) => (
          <div key={k}>
            <dt>{k}</dt>
            <dd>
              {typeof v === "object" ? <Details value={v} /> : String(v ?? "—")}
            </dd>
          </div>
        ))}
      </dl>
    );
  return <span>{String(value)}</span>;
}
export default function Dev1Screen({
  screenId,
  recordId,
  kindOverride,
}: {
  screenId: number;
  recordId?: string;
  kindOverride?: Kind;
}) {
  const [q, setQ] = useSearchParams();
  const baseKind = kindOverride ?? routeKinds[screenId];
  const kind: Kind =
    baseKind === "floors" && q.get("kind") === "areas" ? "areas" : baseKind;
  const b = integer(
    q.get("branchId") ?? (screenId === 14 ? (recordId ?? null) : null),
  );
  const n = integer(
    q.get("recordId") ?? (screenId === 14 ? null : (recordId ?? null)),
  );
  const page = integer(q.get("page")) ?? 1;
  const [branch, setBranch] = useState(
    q.get("branchId") ?? (screenId === 14 ? (recordId ?? "") : ""),
  );
  const [record, setRecord] = useState(n ? String(n) : "");
  const [data, setData] = useState<Loaded>();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [action, setAction] = useState<Action>();
  const [busy, setBusy] = useState(false);
  const lock = useRef(false);
  const [revision, setRevision] = useState(0);
  const generation = useRef(0);
  const key = `${kind}:${b}:${n}:${page}`;
  const [loadedKey, setLoadedKey] = useState("");
  useEffect(() => {
    setBranch(b ? String(b) : "");
    setRecord(n ? String(n) : "");
  }, [b, n]);
  useEffect(() => {
    const controller = new AbortController();
    const g = ++generation.current;
    setData(undefined);
    setAction(undefined);
    setError("");
    setLoadedKey("");
    if (!b) {
      setLoading(false);
      return () => controller.abort();
    }
    setLoading(true);
    load(kind, b, n, page, controller.signal)
      .then((v) => {
        if (g === generation.current && !controller.signal.aborted) {
          setData(v);
          setLoadedKey(key);
        }
      })
      .catch((e) => {
        if (g === generation.current && !controller.signal.aborted)
          setError(errorText(e));
      })
      .finally(() => {
        if (g === generation.current && !controller.signal.aborted)
          setLoading(false);
      });
    return () => {
      controller.abort();
      generation.current++;
    };
  }, [key, revision]);
  useEffect(() => {
    const original = JSON.stringify(currentSession());
    return subscribe(() => {
      if (JSON.stringify(currentSession()) !== original) {
        generation.current++;
        setData(undefined);
        setAction(undefined);
        setLoadedKey("");
        setError("Phiên đăng nhập đã thay đổi. Hãy tải lại trang.");
      }
    });
  }, []);
  const current = loadedKey === key ? data : undefined;
  const native = obj(current?.data);
  const rows = Array.isArray(native.items) ? native.items : [];
  const total = Number(native.totalCount ?? native.total ?? 0);
  const change = (values: Record<string, string | undefined>) => {
    if (lock.current) return;
    setNotice("");
    const next = new URLSearchParams(q);
    for (const [k, v] of Object.entries(values)) {
      if (v) next.set(k, v);
      else next.delete(k);
    }
    setQ(next);
  };
  async function submit(input: RecordValue) {
    if (lock.current || !action || !b) return;
    lock.current = true;
    setBusy(true);
    setError("");
    const g = generation.current;
    try {
      const result = await save(kind, action, b, n, current?.version, input);
      if (g !== generation.current) return;
      setAction(undefined);
      setNotice(
        obj(result).notification === "NOT_INTEGRATED"
          ? "Đã lưu. Chức năng gửi thông báo chưa được backend tích hợp."
          : "Đã lưu. Đang tải dữ liệu mới nhất.",
      );
      setData(undefined);
      setLoadedKey("");
      const created = integer(String(obj(result).id ?? ""));
      if (action === "create" && created) changeAfterWrite(created);
      else setRevision((x) => x + 1);
    } catch (e) {
      if (g === generation.current) {
        setError(errorText(e));
        if (
          e instanceof ApiError &&
          ([401, 403, 412, 428].includes(e.status) || e.outcomeUnknown)
        ) {
          setAction(undefined);
          setData(undefined);
          setLoadedKey("");
        }
      }
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  function changeAfterWrite(id: number) {
    const next = new URLSearchParams(q);
    next.set("recordId", String(id));
    setQ(next);
  }
  const actions: Action[] = current?.version
    ? fields[kind]
      ? ["edit"]
      : []
    : [];
  if (current?.version) {
    if (kind === "roomTypes") actions.push("deactivate");
    if (kind === "checklists") actions.push("publish", "retire");
    if (kind === "planning") actions.push("assign", "assignInspection");
    if (kind === "inspection")
      actions.push("start", "saveInspection", "defect", "finish", "rework");
    if (kind === "cleaningProgress" || kind === "taskProgress")
      actions.push("saveProgress");
  }
  const unavailableList =
    [
      "checklists",
      "planning",
      "inspection",
      "cleaningProgress",
      "taskProgress",
    ].includes(kind) && !n;
  return (
    <div className="dev1-page">
      <Dev1Navigation />
      <h1>{titles[kind]}</h1>
      {[25, 28, 29].includes(screenId) && (
        <p>
          Backend chưa cung cấp thao tác tạo cleaning task hoặc danh sách rework
          cho route này. Dùng ID cleaning task để xem tiến độ, hoặc ID
          inspection để yêu cầu làm lại.
        </p>
      )}
      <p>
        Quyền truy cập và thao tác được kiểm tra bởi máy chủ. Chọn chi nhánh và
        dữ liệu thực tế để tiếp tục.
      </p>
      <form
        className="dev1-context"
        onSubmit={(e) => {
          e.preventDefault();
          change({ branchId: branch, recordId: record, page: undefined });
        }}
      >
        <label>
          ID chi nhánh
          <input
            aria-label="ID chi nhánh"
            type="number"
            min="1"
            step="1"
            required
            value={branch}
            onChange={(e) => setBranch(e.target.value)}
            disabled={busy}
          />
        </label>
        {kind !== "structure" && (
          <label>
            {kind === "planning" || kind === "cleaningProgress"
              ? "ID cleaning task"
              : kind === "inspection"
                ? "ID inspection"
                : kind === "taskProgress"
                  ? "ID công việc"
                  : "ID bản ghi (để trống để xem danh sách)"}
            <input
              aria-label="ID bản ghi"
              type="number"
              min="1"
              step="1"
              value={record}
              onChange={(e) => setRecord(e.target.value)}
              disabled={busy}
            />
          </label>
        )}
        <button className="primary" disabled={busy}>
          Tải dữ liệu
        </button>
        {baseKind === "floors" && (
          <select
            aria-label="Tầng hoặc khu vực"
            disabled={busy}
            value={kind}
            onChange={(e) =>
              change({
                kind: e.target.value,
                recordId: undefined,
                page: undefined,
              })
            }
          >
            <option value="floors">Tầng</option>
            <option value="areas">Khu vực</option>
          </select>
        )}
      </form>
      {!b && (
        <p>
          Nhập ID chi nhánh bạn được cấp quyền. Backend hiện chưa cung cấp danh
          sách chi nhánh cho bộ màn hình này.
        </p>
      )}
      {notice && <p role="status">{notice}</p>}
      {error && (
        <div role="alert">
          <p>{error}</p>
          <button
            disabled={busy}
            onClick={() => {
              setAction(undefined);
              setRevision((x) => x + 1);
            }}
          >
            Tải lại
          </button>{" "}
          <Link to="/login">Đăng nhập</Link>
        </div>
      )}
      {loading && <p role="status">Đang tải…</p>}
      {b && !loading && !error && (
        <>
          <div className="dev1-actions">
            {fields[kind] && (
              <button disabled={busy} onClick={() => setAction("create")}>
                Thêm mới
              </button>
            )}
            {actions.map((a) => (
              <button
                key={a}
                disabled={
                  busy || (!current?.version?.etag && a !== "assignInspection")
                }
                onClick={() => setAction(a)}
              >
                {actionLabels[a]}
              </button>
            ))}
          </div>
          {current?.version && !current.version.etag && (
            <p>
              Không có ETag; các thao tác yêu cầu phiên bản dữ liệu đang bị
              khóa.
            </p>
          )}
          {unavailableList && (
            <p>
              Backend chưa có API danh sách cho màn hình này. Nhập ID thật để mở
              dữ liệu. Không có dữ liệu mẫu thay thế.
            </p>
          )}
          {!n && Array.isArray(native.items) ? (
            <>
              <table className="dev1-table">
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>Mã / số phòng</th>
                    <th>Tên</th>
                    <th>Trạng thái</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r, i) => {
                    const a = obj(r);
                    const row = obj(a.details ?? r);
                    return (
                      <tr key={String(row.id ?? i)}>
                        <td>{String(row.id)}</td>
                        <td>{String(row.code ?? row.roomNumber ?? "—")}</td>
                        <td>{String(row.name ?? "—")}</td>
                        <td>
                          {row.isActive === true
                            ? "Hoạt động"
                            : row.isActive === false
                              ? "Ngừng hoạt động"
                              : "—"}
                        </td>
                        <td>
                          <button
                            disabled={busy}
                            onClick={() => change({ recordId: String(row.id) })}
                          >
                            Mở chi tiết
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
              {!rows.length && <p>Danh sách trống.</p>}
              <div className="dev1-actions">
                <button
                  disabled={page <= 1 || busy}
                  onClick={() => change({ page: String(page - 1) })}
                >
                  Trang trước
                </button>
                <span>
                  Trang {page} · Tổng {total}
                </span>
                <button
                  disabled={
                    page * Number(native.pageSize ?? 20) >= total || busy
                  }
                  onClick={() => change({ page: String(page + 1) })}
                >
                  Trang sau
                </button>
              </div>
            </>
          ) : (
            current?.data !== null &&
            current && <Details value={current.data} />
          )}
        </>
      )}
      {action && (
        <Editor
          key={`${key}:${action}:${revision}`}
          kind={kind}
          action={action}
          initial={native}
          busy={busy}
          submit={submit}
          cancel={() => setAction(undefined)}
        />
      )}
      {(kind === "inspection" ||
        kind === "cleaningProgress" ||
        kind === "taskProgress") && (
        <p>
          100% tiến độ không tự hoàn thành công việc. Gửi kết quả kiểm tra không
          đồng nghĩa với phê duyệt phòng.
        </p>
      )}
    </div>
  );
}
function Editor({
  kind,
  action,
  initial,
  busy,
  submit,
  cancel,
}: {
  kind: Kind;
  action: Action;
  initial: RecordValue;
  busy: boolean;
  submit: (v: RecordValue) => Promise<void>;
  cancel: () => void;
}) {
  const fs =
    action === "create" || action === "edit"
      ? (fields[kind]?.[action] ?? [])
      : (specialFields[action] ?? []);
  const initialRows = () => {
    if (action === "saveInspection")
      return (Array.isArray(initial.criteria) ? initial.criteria : []).map(
        (x) => ({ ...obj(x), itemId: obj(x).itemId, photos: null }),
      );
    if (action === "saveProgress")
      return (Array.isArray(initial.items) ? initial.items : []).map((x) => ({
        ...obj(x),
      }));
    if (kind === "checklists" && action === "edit") {
      const versions = Array.isArray(initial.versions) ? initial.versions : [];
      return ((obj(versions.at(-1)).criteria as RecordValue[]) ?? []).map(
        (x) => ({ ...x }),
      );
    }
    return [
      {
        text: "",
        group: null,
        mandatory: true,
        passCondition: null,
        description: null,
      },
    ];
  };
  const [values, setValues] = useState<RecordValue>(
    action === "edit" || action === "saveProgress" ? { ...initial } : {},
  );
  const [rows, setRows] = useState<RecordValue[]>(initialRows);
  const [error, setError] = useState("");
  const checklist =
    kind === "checklists" && (action === "create" || action === "edit");
  const changeRow = (i: number, k: string, v: unknown) =>
    setRows((prev) => prev.map((r, j) => (i === j ? { ...r, [k]: v } : r)));
  return (
    <section className="dev1-editor" aria-label={actionLabels[action]}>
      <h2>{actionLabels[action]}</h2>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          setError("");
          let input = { ...values };
          if (checklist)
            input.criteria = rows.map((r) => ({
              text: r.text,
              group: r.group || null,
              mandatory: !!r.mandatory,
              passCondition: r.passCondition || null,
              description: r.description || null,
            }));
          if (action === "saveInspection")
            input.results = rows.map((r) => ({
              itemId: r.itemId,
              outcome: r.outcome,
              note: r.note || null,
              photos: null,
            }));
          if (action === "saveProgress")
            input.items = rows.map((r) => ({
              id: r.id,
              done: kind === "cleaningProgress" ? !!r.done : null,
              outcome: kind === "taskProgress" ? r.outcome : null,
              note: r.note || null,
            }));
          if (action === "saveProgress") input.note = input.note || null;
          void submit(input).catch((e) => setError(errorText(e)));
        }}
      >
        <fieldset disabled={busy}>
          <div className="dev1-fields">
            {fs.map((f) => (
              <Input
                key={f.key}
                field={f}
                value={values[f.key]}
                change={(v) => setValues({ ...values, [f.key]: v })}
              />
            ))}
          </div>
          {(checklist ||
            action === "saveProgress" ||
            action === "saveInspection") && (
            <div>
              {rows.map((r, i) => (
                <fieldset key={i}>
                  <legend>
                    {checklist
                      ? `Tiêu chí ${i + 1}`
                      : String(r.label ?? r.criterion ?? r.itemId ?? r.id)}
                  </legend>
                  {checklist ? (
                    <>
                      <Input
                        field={{ key: "text", label: "Nội dung" }}
                        value={r.text}
                        change={(v) => changeRow(i, "text", v)}
                      />
                      <Input
                        field={{ key: "group", label: "Nhóm", optional: true }}
                        value={r.group}
                        change={(v) => changeRow(i, "group", v)}
                      />
                      <Input
                        field={{
                          key: "mandatory",
                          label: "Bắt buộc",
                          type: "checkbox",
                          optional: true,
                        }}
                        value={r.mandatory}
                        change={(v) => changeRow(i, "mandatory", v)}
                      />
                      <Input
                        field={{
                          key: "passCondition",
                          label: "Điều kiện đạt",
                          optional: true,
                        }}
                        value={r.passCondition}
                        change={(v) => changeRow(i, "passCondition", v)}
                      />
                      <Input
                        field={{
                          key: "description",
                          label: "Mô tả tiêu chí",
                          optional: true,
                        }}
                        value={r.description}
                        change={(v) => changeRow(i, "description", v)}
                      />
                      <button
                        type="button"
                        onClick={() => setRows(rows.filter((_, j) => j !== i))}
                      >
                        Bỏ tiêu chí
                      </button>
                    </>
                  ) : (
                    <>
                      {action === "saveProgress" &&
                      kind === "cleaningProgress" ? (
                        <Input
                          field={{
                            key: "done",
                            label: "Đã thực hiện",
                            type: "checkbox",
                            optional: true,
                          }}
                          value={r.done}
                          change={(v) => changeRow(i, "done", v)}
                        />
                      ) : (
                        <label>
                          Kết quả
                          <select
                            required
                            value={String(r.outcome ?? "")}
                            onChange={(e) =>
                              changeRow(i, "outcome", e.target.value)
                            }
                          >
                            <option value="">Chọn kết quả</option>
                            <option value="PASS">Đạt</option>
                            <option value="FAIL">Không đạt</option>
                          </select>
                        </label>
                      )}
                      <Input
                        field={{
                          key: "note",
                          label: "Ghi chú tiêu chí",
                          optional: true,
                        }}
                        value={r.note}
                        change={(v) => changeRow(i, "note", v)}
                      />
                    </>
                  )}
                </fieldset>
              ))}
              {checklist && (
                <button
                  type="button"
                  onClick={() =>
                    setRows([
                      ...rows,
                      { text: "", group: null, mandatory: true },
                    ])
                  }
                >
                  Thêm tiêu chí
                </button>
              )}
            </div>
          )}
          {error && <p role="alert">{error}</p>}
          <div className="dev1-actions">
            <button
              className="primary"
              disabled={busy || (checklist && !rows.length)}
            >
              {busy ? "Đang lưu…" : "Lưu"}
            </button>
            <button type="button" onClick={cancel}>
              Hủy
            </button>
          </div>
        </fieldset>
      </form>
    </section>
  );
}
function Input({
  field: f,
  value,
  change,
}: {
  field: Field;
  value: unknown;
  change: (v: unknown) => void;
}) {
  return (
    <label>
      {f.label}
      {!f.optional && " *"}
      {f.type === "textarea" ? (
        <textarea
          required={!f.optional}
          value={String(value ?? "")}
          onChange={(e) => change(e.target.value || null)}
        />
      ) : f.type === "checkbox" ? (
        <input
          type="checkbox"
          required={!f.optional}
          checked={!!value}
          onChange={(e) => change(e.target.checked)}
        />
      ) : (
        <input
          type={f.type ?? "text"}
          step={f.type === "number" ? 1 : undefined}
          min={f.min ?? (f.key.endsWith("Id") ? 1 : undefined)}
          max={f.max}
          required={!f.optional}
          value={String(value ?? "")}
          onChange={(e) =>
            change(
              f.type === "number"
                ? e.target.value === ""
                  ? null
                  : Number(e.target.value)
                : e.target.value || null,
            )
          }
        />
      )}
    </label>
  );
}
