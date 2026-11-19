import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { LoadingIndicator } from "../components/LoadingIndicator";
import "./staff-workspace.css";

type Option = { id: number; name: string };
type Options = {
  branches: Option[];
  types: (Option & {
    materialNeed: boolean;
    departmentId?: number | null;
    allowAnyDepartment?: boolean;
  })[];
  departments: Option[];
  requesterDepartments: Option[];
  priorities: Option[];
  rooms: Option[];
  areas: Option[];
};
class Failure extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
  ) {
    super(code);
  }
}
async function json(
  path: string,
  signal: AbortSignal,
  init?: RequestInit,
): Promise<unknown> {
  const response = await fetch(path, {
    ...init,
    signal,
    credentials: "same-origin",
    cache: "no-store",
    redirect: "error",
  });
  const body = response.headers.get("content-type")?.includes("json")
    ? await response.json()
    : null;
  if (!response.ok)
    throw new Failure(
      response.status,
      typeof body?.code === "string"
        ? body.code
        : typeof body?.title === "string"
          ? body.title
          : "REQUEST_FAILED",
    );
  return body;
}
function options(raw: unknown): Options {
  const value = raw as Options;
  if (
    !value ||
    ![
      value.branches,
      value.types,
      value.departments,
      value.requesterDepartments,
      value.priorities,
      value.rooms,
      value.areas,
    ].every(
      (list) =>
        Array.isArray(list) &&
        list.every(
          (v) =>
            Number.isSafeInteger(v.id) &&
            v.id > 0 &&
            typeof v.name === "string",
        ),
    ) ||
    value.types.some(
      (v) =>
        typeof v.materialNeed !== "boolean" ||
        (v.departmentId != null &&
          (!Number.isSafeInteger(v.departmentId) || v.departmentId <= 0)) ||
        (v.allowAnyDepartment !== undefined &&
          typeof v.allowAnyDepartment !== "boolean"),
    )
  )
    throw new Error("Lựa chọn tiếp nhận không hợp lệ");
  return value;
}
export function InternalRequestIntakeLink() {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    !state.pending &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [allowed, setAllowed] = useState<typeof owner>(null);
  useEffect(() => {
    setAllowed(null);
    const abort = new AbortController();
    if (owner)
      void json("/api/internal-requests/intake", abort.signal)
        .then(options)
        .then(() => {
          if (!abort.signal.aborted) setAllowed(owner);
        })
        .catch((e) => {
          if (!abort.signal.aborted && e instanceof Failure && e.status === 401)
            actions.invalidateSession(owner);
        });
    return () => abort.abort();
  }, [owner, actions]);
  return owner && allowed === owner ? (
    <Link to="/internal-requests/new">Tạo yêu cầu nội bộ</Link>
  ) : null;
}
export function InternalRequestIntakePage() {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    !state.pending &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [branch, setBranch] = useState("");
  const [revision, setRevision] = useState(0);
  const [loaded, setLoaded] = useState<{
    owner: typeof owner;
    branch: string;
    value: Options;
  } | null>(null);
  const [type, setType] = useState("");
  const [department, setDepartment] = useState("");
  const [priority, setPriority] = useState("");
  const [requesterDepartment, setRequesterDepartment] = useState("");
  const [subject, setSubject] = useState("");
  const [description, setDescription] = useState("");
  const [deadline, setDeadline] = useState("");
  const [location, setLocation] = useState("");
  const [files, setFiles] = useState<File[]>([]);
  const [message, setMessage] = useState("");
  const [created, setCreated] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  // Preserve the submission key after uncertain outcomes; the server's unique request number prevents duplicate creation.
  const submission = useRef(crypto.randomUUID());
  useEffect(() => {
    setLoaded(null);
    setType("");
    setDepartment("");
    setRequesterDepartment("");
    setPriority("");
    setLocation("");
    setFiles([]);
    setMessage("");
    setBusy(false);
    sending.current = false;
    const abort = new AbortController();
    controller.current = abort;
    if (owner)
      void json(
        `/api/internal-requests/intake${branch ? `?branchId=${branch}` : ""}`,
        abort.signal,
      )
        .then(options)
        .then((value) => {
          if (!abort.signal.aborted) {
            if (!branch && value.branches.length === 1) {
              setBranch(String(value.branches[0].id));
              return;
            }
            setLoaded({ owner, branch, value });
            setRequesterDepartment(
              value.requesterDepartments.length === 1
                ? String(value.requesterDepartments[0].id)
                : "",
            );
          }
        })
        .catch((e) => {
          if (!abort.signal.aborted) {
            setMessage(
              e instanceof Failure && e.code === "IR_INTAKE_SCHEMA_REQUIRED"
                ? "Chức năng tạo yêu cầu chưa sẵn sàng do hệ thống chưa cập nhật dữ liệu cần thiết. Vui lòng liên hệ quản trị viên."
                : e instanceof Failure && e.status === 403
                  ? "Bạn chưa được cấp quyền tạo yêu cầu nội bộ trong chi nhánh này."
                  : "Chưa tải được biểu mẫu. Vui lòng thử lại.",
            );
            if (e instanceof Failure && e.status === 401)
              actions.invalidateSession(owner);
          }
        });
    return () => abort.abort();
  }, [owner, branch, revision, actions]);
  const view =
    owner && loaded?.owner === owner && loaded.branch === branch
      ? loaded.value
      : null;
  const material = view?.types.find((v) => v.id === Number(type))?.materialNeed;
  const requestType = view?.types.find((v) => v.id === Number(type));
  const receivingDepartments = requestType?.allowAnyDepartment
    ? view!.departments
    : requestType?.departmentId
      ? view!.departments.filter((d) => d.id === requestType.departmentId)
      : [];
  function changeType(value: string) {
    setType(value);
    const selected = view?.types.find((t) => t.id === Number(value));
    setDepartment(
      selected?.departmentId &&
        view?.departments.some((d) => d.id === selected.departmentId)
        ? String(selected.departmentId)
        : "",
    );
  }
  async function submit() {
    const abort = controller.current;
    if (
      !owner ||
      !view ||
      !branch ||
      material ||
      sending.current ||
      !abort ||
      abort.signal.aborted
    )
      return;
    if (!receivingDepartments.some((d) => d.id === Number(department))) {
      setMessage("Chọn bộ phận tiếp nhận phù hợp với loại yêu cầu.");
      return;
    }
    if (
      !type ||
      !department ||
      !priority ||
      !subject.trim() ||
      !description.trim() ||
      !deadline ||
      !Number.isFinite(Date.parse(deadline))
    ) {
      setMessage("MSG03: Nhập đủ các trường bắt buộc.");
      return;
    }
    if (
      files.length > 5 ||
      files.some((f) => f.size < 1 || f.size > 10 * 1024 * 1024)
    ) {
      setMessage(
        "MSG03: Tải tối đa 5 tệp JPG, PNG hoặc PDF, mỗi tệp tối đa 10 MiB.",
      );
      return;
    }
    sending.current = true;
    setBusy(true);
    setMessage("");
    try {
      const uploads = await Promise.all(
        files.map(async (file) => {
          const bytes = new Uint8Array(await file.arrayBuffer());
          let binary = "";
          for (let offset = 0; offset < bytes.length; offset += 8192)
            binary += String.fromCharCode(
              ...bytes.subarray(offset, offset + 8192),
            );
          return { name: file.name, mimeType: file.type, base64: btoa(binary) };
        }),
      );
      const csrf = (await json("/api/auth/csrf", abort.signal)) as {
        csrfToken?: unknown;
      };
      if (
        typeof csrf?.csrfToken !== "string" ||
        !csrf.csrfToken ||
        abort.signal.aborted
      )
        throw new Error("Kiểm tra bảo mật thất bại");
      const result = (await json(
        "/api/internal-requests/intake",
        abort.signal,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
          },
          body: JSON.stringify({
            submissionId: submission.current,
            branchId: Number(branch),
            typeId: Number(type),
            departmentId: Number(department),
            subject: subject.trim(),
            description: description.trim(),
            priorityId: Number(priority),
            desiredDeadline: new Date(`${deadline}:00+07:00`).toISOString(),
            roomId: location.startsWith("room:")
              ? Number(location.slice(5))
              : null,
            areaId: location.startsWith("area:")
              ? Number(location.slice(5))
              : null,
            requesterDepartmentId: requesterDepartment
              ? Number(requesterDepartment)
              : null,
            files: uploads,
          }),
        },
      )) as { requestId: number; status: string; message: string };
      if (
        !result ||
        !Number.isSafeInteger(result.requestId) ||
        result.requestId <= 0 ||
        result.status !== "PENDING_ACCEPTANCE" ||
        result.message !== "MSG09"
      )
        throw new Error("Chưa xác nhận tạo mới");
      if (!abort.signal.aborted) {
        setCreated(result.requestId);
        setSubject("");
        setDescription("");
        setFiles([]);
        setLoaded(null);
        setMessage("MSG09: Đã tạo yêu cầu. Đang chờ tiếp nhận.");
      }
    } catch (e) {
      if (!abort.signal.aborted) {
        setLoaded(null);
        setFiles([]);
        setMessage(
          e instanceof Failure && e.code === "MATERIAL_REQUEST_REQUIRED"
            ? "MSG03: Hãy tạo yêu cầu vật tư thay thế (UC091)."
            : e instanceof Failure && e.code === "IR_SUBMISSION_ALREADY_USED"
              ? "Lần gửi đã được sử dụng. Kiểm tra yêu cầu đã gửi trước khi tạo thêm."
              : e instanceof Failure &&
                  e.code === "IR_REQUESTER_MEMBERSHIP_CHANGED"
                ? "Phân công bộ phận của bạn đã đổi. Tải lại để chọn bộ phận hiện khả dụng."
                : e instanceof Failure &&
                    e.code === "IR_CURRENT_HEAD_AUTHORITY_REQUIRED"
                  ? "Bộ phận tiếp nhận chưa có một quản lý đủ quyền xử lý. Vui lòng nhờ quản trị viên kiểm tra phân công quản lý."
                  : e instanceof Failure && e.status === 409
                    ? "Điều kiện yêu cầu đã đổi. Tải lại lựa chọn hiện tại trước khi gửi lại."
                    : e instanceof Failure && e.status === 400
                      ? "MSG03: Kiểm tra trường bắt buộc, hạn tương lai, tham chiếu chi nhánh và định dạng tệp. Tải lại để sửa yêu cầu."
                      : "Chưa xác nhận tạo mới. Kiểm tra yêu cầu đã gửi rồi tải lại quyền. Không tự gửi lại.",
        );
        if (e instanceof Failure && e.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      if (!abort.signal.aborted) {
        sending.current = false;
        setBusy(false);
      }
    }
  }
  if (!owner) return null;
  const select = (
    label: string,
    value: string,
    items: Option[],
    change: (value: string) => void,
  ) => (
    <label>
      {label}
      <SearchableSelect
        required
        aria-label={label}
        value={value}
        disabled={busy}
        onChange={(e) => change(e.target.value)}
      >
        <option value="">Chọn</option>
        {items.map((v) => (
          <option key={v.id} value={v.id}>
            {v.name}
          </option>
        ))}
      </SearchableSelect>
    </label>
  );
  return (
    <main className="staff-workspace">
      <header className="staff-heading">
        <div>
          <h1>Tạo yêu cầu nội bộ</h1>
          <p className="staff-subtitle">
            Gửi đề nghị hỗ trợ tới bộ phận trong chi nhánh của bạn.
          </p>
        </div>
        <Link to="/internal-requests">Danh sách yêu cầu</Link>
      </header>
      {message && (
        <p className={created ? "staff-success" : "staff-notice"} role="status">
          {userMessage(message)}
        </p>
      )}
      {created ? (
        <Link to={`/internal-requests?requestId=${created}`}>
          Mở yêu cầu vừa tạo
        </Link>
      ) : (
        <>
          <div className="staff-toolbar">
            <button disabled={busy} onClick={() => setRevision((v) => v + 1)}>
              Tải lại biểu mẫu
            </button>
          </div>
          {!view && !message && <LoadingIndicator label="Đang tải biểu mẫu" />}
          {view && (
            <form
              className="staff-form"
              aria-label="Tạo yêu cầu nội bộ"
              onSubmit={(e) => {
                e.preventDefault();
                void submit();
              }}
            >
              {view.branches.length === 1 ? (
                <label>
                  Chi nhánh
                  <span className="staff-branch">{view.branches[0].name}</span>
                </label>
              ) : (
                select("Chi nhánh", branch, view.branches, setBranch)
              )}
              {branch && (
                <>
                  {view.requesterDepartments.length > 0 && (
                    <label>
                      Bộ phận của bạn
                      <SearchableSelect
                        aria-label="Bộ phận của bạn"
                        value={requesterDepartment}
                        disabled={busy}
                        onChange={(e) => setRequesterDepartment(e.target.value)}
                      >
                        {view.requesterDepartments.length > 1 && (
                          <option value="">Chưa chỉ định</option>
                        )}
                        {view.requesterDepartments.map((v) => (
                          <option key={v.id} value={v.id}>
                            {v.name}
                          </option>
                        ))}
                      </SearchableSelect>
                    </label>
                  )}
                  {select(
                    "Loại yêu cầu",
                    type,
                    view.types.filter((t) => !t.materialNeed),
                    changeType,
                  )}
                  {select(
                    "Bộ phận tiếp nhận",
                    department,
                    receivingDepartments,
                    setDepartment,
                  )}
                  {type && !receivingDepartments.length && (
                    <p role="status">
                      Chưa có bộ phận tiếp nhận được cấu hình cho loại yêu cầu
                      này.
                    </p>
                  )}
                  {select("Độ ưu tiên", priority, view.priorities, setPriority)}
                  <label className="staff-wide">
                    Tiêu đề
                    <input
                      required
                      maxLength={200}
                      placeholder="Ví dụ: Hỗ trợ di chuyển bàn trong phòng"
                      value={subject}
                      disabled={busy}
                      onChange={(e) => setSubject(e.target.value)}
                    />
                  </label>
                  <label className="staff-wide">
                    Mô tả
                    <textarea
                      required
                      maxLength={4000}
                      placeholder="Nêu rõ nội dung cần hỗ trợ và kết quả mong muốn"
                      value={description}
                      disabled={busy}
                      onChange={(e) => setDescription(e.target.value)}
                    />
                  </label>
                  <label>
                    Thời hạn mong muốn (UTC+7)
                    <input
                      type="datetime-local"
                      required
                      value={deadline}
                      disabled={busy}
                      onChange={(e) => setDeadline(e.target.value)}
                    />
                  </label>
                  <label>
                    Vị trí (không bắt buộc)
                    <SearchableSelect
                      value={location}
                      disabled={busy}
                      onChange={(e) => setLocation(e.target.value)}
                    >
                      <option value="">Không chọn vị trí</option>
                      {view.rooms.map((v) => (
                        <option key={`room:${v.id}`} value={`room:${v.id}`}>
                          Phòng {v.name}
                        </option>
                      ))}
                      {view.areas.map((v) => (
                        <option key={`area:${v.id}`} value={`area:${v.id}`}>
                          Khu vực {v.name}
                        </option>
                      ))}
                    </SearchableSelect>
                  </label>
                  <label>
                    Tệp đính kèm
                    <input
                      key={`${branch}:${revision}`}
                      type="file"
                      multiple
                      accept="image/jpeg,image/png,application/pdf"
                      disabled={busy}
                      onChange={(e) =>
                        setFiles(Array.from(e.target.files ?? []))
                      }
                    />
                  </label>
                  {material && (
                    <p className="staff-wide staff-notice" role="alert">
                      Loại này cần cấp vật tư. Hãy dùng chức năng{" "}
                      <Link to="/material-requests/new">
                        Tạo yêu cầu vật tư
                      </Link>
                      .
                    </p>
                  )}
                  <div className="staff-wide">
                    <button disabled={busy || !!material} type="submit">
                      {busy ? "Đang gửi…" : "Gửi yêu cầu"}
                    </button>
                  </div>
                </>
              )}
            </form>
          )}
        </>
      )}
    </main>
  );
}
