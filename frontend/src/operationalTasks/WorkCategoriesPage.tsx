import { SearchInput, searchSuggestions } from "../components/SearchInput";
import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import {
  announceWorkflowSuccess,
  ActionDialog,
  CollapsibleFilterPanel,
} from "../components/workspace-interactions";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { useStructureOptions } from "../hotel/useStructureOptions";
import "./work-categories.css";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";

type Category = {
  id: number;
  code: string;
  name: string;
  description: string | null;
  departmentId: number | null;
  isActive: boolean;
  usageCount: number;
  version: string;
};
type Department = { id: number; name: string };
const positive = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
function valid(v: unknown): v is Category {
  return (
    isObject(v) &&
    positive(v.id) &&
    typeof v.code === "string" &&
    typeof v.name === "string" &&
    (v.description === null || typeof v.description === "string") &&
    (v.departmentId === null || positive(v.departmentId)) &&
    typeof v.isActive === "boolean" &&
    typeof v.usageCount === "number" &&
    Number.isSafeInteger(v.usageCount) &&
    v.usageCount >= 0 &&
    typeof v.version === "string" &&
    /^"[A-F0-9]{64}"$/.test(v.version)
  );
}
export function WorkCategoriesPage() {
  const { state } = useAuth();
  const generation = useRef({ authority: state.authorityGeneration, key: 0 });
  if (generation.current.authority !== state.authorityGeneration)
    generation.current = {
      authority: state.authorityGeneration,
      key: generation.current.key + 1,
    };
  if (
    state.status !== "AUTHENTICATED" ||
    state.pending ||
    state.session?.mustChangePassword
  )
    return null;
  return <BoundWorkCategoriesPage key={generation.current.key} />;
}
function BoundWorkCategoriesPage() {
  const { state } = useAuth();
  const branches = useStructureOptions("branches");
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const owner =
    state.status === "AUTHENTICATED" && !state.pending
      ? state.authorityGeneration
      : null;
  const current = useRef(owner);
  current.current = owner;
  const abort = useRef<AbortController | null>(null),
    sending = useRef(false);
  const [branch, setBranch] = useState(""),
    [loaded, setLoaded] = useState(false),
    [busy, setBusy] = useState(false),
    [message, setMessage] = useState("");
  const [rows, setRows] = useState<Category[]>([]),
    [departments, setDepartments] = useState<Department[]>([]),
    [hasMore, setHasMore] = useState(false);
  const [selected, setSelected] = useState<Category | null>(null),
    [editing, setEditing] = useState(false),
    [uncertain, setUncertain] = useState(false);
  const [code, setCode] = useState(""),
    [name, setName] = useState(""),
    [description, setDescription] = useState(""),
    [department, setDepartment] = useState("");
  const [active, setActive] = useState(true),
    [confirmed, setConfirmed] = useState(false);
  function clear() {
    setLoaded(false);
    setRows([]);
    setDepartments([]);
    setEditing(false);
    setHasMore(false);
  }
  useEffect(() => {
    clear();
    setMessage("");
    return () => abort.current?.abort();
  }, [owner]);
  function endpoint(id?: number) {
    if (branch && !positive(Number(branch)))
      throw Error("Nhập mã chi nhánh hợp lệ.");
    return (
      "/api/work-categories" +
      (id ? "/" + id : "") +
      (branch ? "?branchId=" + Number(branch) : "")
    );
  }
  async function load(more = false) {
    if (!owner || sending.current || (more && uncertain)) return;
    const controller = new AbortController();
    abort.current = controller;
    sending.current = true;
    setBusy(true);
    setMessage("");
    try {
      const url =
        endpoint() +
        (branch ? "&" : "?") +
        "afterId=" +
        (more ? (rows.at(-1)?.id ?? 0) : 0);
      const response = await fetch(url, {
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: controller.signal,
      });
      if (!response.ok)
        throw Error(
          "Danh mục nhóm chưa khả dụng (" +
            response.status +
            "). Kiểm tra quyền và phạm vi quản trị hiện tại.",
        );
      const value: unknown = await response.json();
      if (
        !isObject(value) ||
        !Array.isArray(value.items) ||
        !value.items.every(valid) ||
        !Array.isArray(value.departments) ||
        !value.departments.every(
          (d) => isObject(d) && positive(d.id) && typeof d.name === "string",
        )
      )
        throw Error("Phản hồi danh mục không hợp lệ.");
      if (!controller.signal.aborted && current.current === owner) {
        setRows(more ? [...rows, ...value.items] : value.items);
        setDepartments(value.departments as Department[]);
        setLoaded(true);
        setHasMore(value.items.length === 100);
        setEditing(false);
        setUncertain(false);
      }
    } catch (e) {
      if (!controller.signal.aborted && current.current === owner) {
        clear();
        setMessage(
          e instanceof Error ? e.message : "Không tải được nhóm công việc.",
        );
      }
    } finally {
      sending.current = false;
      if (!controller.signal.aborted && current.current === owner)
        setBusy(false);
    }
  }
  function edit(row: Category | null) {
    setSelected(row);
    setCode(row?.code ?? "");
    setName(row?.name ?? "");
    setDescription(row?.description ?? "");
    setDepartment(row?.departmentId?.toString() ?? "");
    setActive(row?.isActive ?? true);
    setConfirmed(false);
    setEditing(true);
  }
  async function save() {
    if (!owner || sending.current || uncertain || !loaded) return;
    const controller = new AbortController();
    abort.current = controller;
    sending.current = true;
    setBusy(true);
    setMessage("");
    let submitted = false;
    try {
      const response = await fetch("/api/auth/csrf", {
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: controller.signal,
      });
      if (!response.ok) throw Error("Xác minh phiên đăng nhập thất bại.");
      const csrf: unknown = await response.json();
      if (
        !isObject(csrf) ||
        csrf.headerName !== "X-CSRF-TOKEN" ||
        typeof csrf.csrfToken !== "string" ||
        !/^[\x21-\x7e]+$/.test(csrf.csrfToken) ||
        current.current !== owner
      )
        throw Error("Phiên đăng nhập đã thay đổi. Vui lòng tải lại.");
      const url = endpoint(selected?.id);
      submitted = true;
      const saved = await fetch(url, {
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
          name,
          description: description.trim() || null,
          departmentId: branch ? Number(department) : null,
          isActive: active,
          confirmation: confirmed,
        }),
      });
      if (!saved.ok) {
        const error: unknown = await saved.json();
        const code =
          isObject(error) && typeof error.code === "string" ? error.code : null;
        // These responses reject the write before commit. Keep the form for correction.
        if (
          (saved.status === 409 && code === "MSG35") ||
          (saved.status === 400 &&
            code !== null &&
            [
              "INVALID_CATEGORY_INPUT",
              "CATEGORY_SCOPE_MISMATCH",
              "DEACTIVATION_CONFIRMATION_REQUIRED",
              "NEW_CATEGORY_MUST_BE_ACTIVE",
              "ACTIVE_DEPARTMENT_REQUIRED",
            ].includes(code))
        ) {
          if (!controller.signal.aborted && current.current === owner) {
            setMessage(
              code === "MSG35"
                ? "MSG35: Mã nhóm đã tồn tại. Nhập mã khác và lưu lại."
                : "Thông tin nhóm bị từ chối. Kiểm tra trường bắt buộc, bộ phận và xác nhận vô hiệu hóa.",
            );
          }
          return;
        }
        throw Error(
          saved.status === 403
            ? "Bạn không có quyền lưu nhóm trong phạm vi này."
            : saved.status === 412
              ? "Nhóm đã được người khác cập nhật. Tải lại danh sách trước khi sửa."
              : "Chưa xác nhận được kết quả lưu. Tải lại danh sách để kiểm tra.",
        );
      }
      const value: unknown = await saved.json();
      if (!valid(value)) throw Error("Không xác minh được phản hồi lưu.");
      if (!controller.signal.aborted && current.current === owner) {
        clear();
        (announceWorkflowSuccess(
          "Đã lưu nhóm. Tải lại danh mục để tiếp tục.",
          "operationalTasks/WorkCategoriesPage.tsx",
        ),
          setMessage("Đã lưu nhóm. Tải lại danh mục để tiếp tục."));
      }
    } catch (e) {
      if (!controller.signal.aborted && current.current === owner) {
        setMessage(
          e instanceof Error ? e.message : "Chưa xác nhận được kết quả lưu.",
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
  const filtered = rows.filter(
    (row) =>
      (!status || String(row.isActive) === status) &&
      (!search.trim() ||
        `${row.code} ${row.name}`
          .toLocaleLowerCase("vi")
          .includes(search.trim().toLocaleLowerCase("vi"))),
  );
  return (
    <section className="work-categories">
      <header className="work-categories__heading">
        <div>
          <p>Quản lý danh mục</p>
          <h1>Nhóm công việc</h1>
          <p>
            Quản lý các nhóm để phân loại công việc dùng chung hoặc trong từng
            chi nhánh.
          </p>
        </div>
        {loaded && (
          <button
            className="work-categories__primary"
            disabled={busy || uncertain}
            onClick={() => edit(null)}
          >
            Tạo nhóm
          </button>
        )}
      </header>
      <section
        className="work-categories__card"
        aria-label="Phạm vi nhóm công việc"
      >
        <div className="work-categories__scope">
          <label>
            Phạm vi danh mục
            <SearchableSelect
              disabled={busy || uncertain}
              value={branch}
              onChange={(e) => {
                setBranch(e.target.value);
                clear();
                setMessage("");
                setSearch("");
                setStatus("");
              }}
            >
              <option value="">Nhóm dùng chung</option>
              {branches.items?.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.code} — {b.name}
                </option>
              ))}
            </SearchableSelect>
          </label>
          <button
            className="work-categories__primary"
            disabled={busy}
            onClick={() => void load()}
          >
            {loaded ? "Tải lại danh sách" : "Xem danh sách"}
          </button>
        </div>
        <p className="work-categories__hint">
          {branch
            ? "Nhóm tại chi nhánh được gắn với một bộ phận phụ trách."
            : "Nhóm dùng chung được quản lý ở phạm vi toàn hệ thống."}
        </p>
        {branches.loading && (
          <LoadingIndicator label="Đang tải chi nhánh" compact />
        )}
        {branches.error && (
          <p role="alert">
            {userMessage(branches.error)}{" "}
            <button type="button" onClick={branches.reload}>
              Tải lại chi nhánh
            </button>
          </p>
        )}
      </section>
      {message && (
        <p
          className={
            message.startsWith("Đã lưu")
              ? "work-categories__success"
              : "work-categories__message"
          }
          role="status"
        >
          {userMessage(message)}
        </p>
      )}
      {busy && <LoadingIndicator label="Đang xử lý nhóm công việc" />}
      {!loaded && !busy && !message && (
        <section className="work-categories__empty">
          <h2>Chọn danh mục để bắt đầu</h2>
          <p>Chọn nhóm dùng chung hoặc chi nhánh, sau đó bấm Xem danh sách.</p>
        </section>
      )}
      {loaded && (
        <section className="work-categories__card">
          <div className="work-categories__result-heading">
            <h2>
              Danh sách nhóm{" "}
              <span>
                {rows.length}
                {hasMore ? "+" : ""}
              </span>
            </h2>
          </div>
          <CollapsibleFilterPanel
            activeCount={Number(!!search) + Number(!!status)}
          >
            <div className="work-categories__filters">
              <label>
                Tìm trong danh sách đã tải
                <SearchInput suggestions={searchSuggestions(rows)}
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Nhập mã hoặc tên nhóm"
                />
              </label>
              <label>
                Trạng thái
                <SearchableSelect
                  value={status}
                  onChange={(e) => setStatus(e.target.value)}
                >
                  <option value="">Tất cả</option>
                  <option value="true">Đang hoạt động</option>
                  <option value="false">Ngừng hoạt động</option>
                </SearchableSelect>
              </label>
              <button
                type="button"
                onClick={() => {
                  setSearch("");
                  setStatus("");
                }}
              >
                Xóa bộ lọc
              </button>
            </div>
          </CollapsibleFilterPanel>
          {!rows.length ? (
            <div className="work-categories__empty">
              <h3>Chưa có nhóm công việc</h3>
              <p>Bấm Tạo nhóm để thêm nhóm đầu tiên trong phạm vi này.</p>
            </div>
          ) : !filtered.length ? (
            <p>Không có nhóm phù hợp bộ lọc trong danh sách đã tải.</p>
          ) : (
            <div
              className="work-categories__table"
              tabIndex={0}
              role="region"
              aria-label="Danh sách nhóm công việc"
            >
              <table>
                <thead>
                  <tr>
                    <th>Mã</th>
                    <th>Tên</th>
                    <th>Bộ phận</th>
                    <th>Trạng thái</th>
                    <th>Lượt sử dụng</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((r) => (
                    <tr key={r.id}>
                      <td>{r.code}</td>
                      <td>
                        <strong>{r.name}</strong>
                        {r.description && <small>{r.description}</small>}
                      </td>
                      <td>
                        {r.departmentId
                          ? (departments.find((d) => d.id === r.departmentId)
                              ?.name ?? "Chưa có thông tin")
                          : "Dùng chung"}
                      </td>
                      <td>
                        <span
                          className={`work-categories__badge ${r.isActive ? "is-active" : ""}`}
                        >
                          {r.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}
                        </span>
                      </td>
                      <td>{r.usageCount}</td>
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
          )}
          {hasMore && (
            <button
              disabled={busy || uncertain}
              onClick={() => void load(true)}
            >
              Tải thêm
            </button>
          )}
        </section>
      )}
      {editing && (
        <ActionDialog
          scope="operationalTasks/WorkCategoriesPage.tsx"
          open
          title={selected ? "Cập nhật nhóm công việc" : "Tạo nhóm công việc"}
          onClose={() => setEditing(false)}
          busy={busy}
          locked={uncertain}
          error={message}
        >
          <form
            className="work-category-form"
            onSubmit={(e) => {
              e.preventDefault();
              void save();
            }}
          >
            <fieldset disabled={busy || uncertain}>
              <legend>{selected ? "Chỉnh sửa nhóm" : "Tạo nhóm"}</legend>
              {selected && selected.usageCount > 0 && (
                <p>
                  Nhóm đã sử dụng giữ mã, tên và mô tả. Có thể đổi trạng thái
                  hoạt động.
                </p>
              )}
              <label>
                Mã nhóm
                <input
                  required
                  maxLength={40}
                  readOnly={!!selected?.usageCount}
                  value={code}
                  onChange={(e) => setCode(e.target.value)}
                />
              </label>
              <label>
                Tên nhóm
                <input
                  required
                  maxLength={150}
                  readOnly={!!selected?.usageCount}
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                />
              </label>
              <label>
                Mô tả
                <textarea
                  maxLength={4000}
                  readOnly={!!selected?.usageCount}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                />
              </label>
              {branch && (
                <label>
                  Bộ phận
                  <SearchableSelect
                    aria-label="Bộ phận"
                    required
                    disabled={selected !== null}
                    value={department}
                    onChange={(e) => setDepartment(e.target.value)}
                  >
                    <option value="">Chọn bộ phận</option>
                    {departments.map((d) => (
                      <option key={d.id} value={d.id}>
                        {d.name}
                      </option>
                    ))}
                  </SearchableSelect>
                </label>
              )}
              {selected && (
                <label>
                  <input
                    type="checkbox"
                    checked={active}
                    onChange={(e) => {
                      setActive(e.target.checked);
                      setConfirmed(false);
                    }}
                  />
                  Đang hoạt động
                </label>
              )}
              {!active && (
                <label>
                  <input
                    required
                    type="checkbox"
                    checked={confirmed}
                    onChange={(e) => setConfirmed(e.target.checked)}
                  />
                  Xác nhận vô hiệu hóa. Công việc hiện có giữ nguyên nhóm này.
                </label>
              )}
              <button data-dialog-action type="submit">
                Lưu nhóm công việc
              </button>
            </fieldset>
          </form>
        </ActionDialog>
      )}
    </section>
  );
}
