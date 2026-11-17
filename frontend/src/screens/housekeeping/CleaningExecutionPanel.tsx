import { useEffect, useRef, useState, type FormEvent } from "react";
import { Link, useLocation } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { cleaningBlockerContext } from "../../dev2/cleaningBlockerContext";
import { ApiError } from "../../api/dev1/client";
import { uploadFile } from "../../api/dev1/cleaningProgress";
import {
  readAssignmentPlan,
  type AssignmentPlan,
} from "../../api/dev1/cleaningAssignment";
import {
  readCleaningExecution,
  mutateCleaningExecution,
  reassignmentCommand,
  type CleaningExecution,
  type ReassignmentCommand,
  closureCommand,
  type ClosureCommand,
} from "../../api/dev1/cleaningExecution";
export default function CleaningExecutionPanel(props: { branch: string; task: string }) {
  const { state } = useAuth();
  if (state.status !== "AUTHENTICATED" || state.pending !== null || !state.session) return <p role="status">Confirm your session before opening cleaning work.</p>;
  // A route change must not retarget an unresolved command to another task.
  return <CleaningExecutionContent key={`${state.authorityGeneration}:${props.branch}:${props.task}`} {...props} />;
}

function CleaningExecutionContent({
  branch,
  task,
}: {
  branch: string;
  task: string;
}) {
  const [data, setData] = useState<CleaningExecution | null>(null),
    [plan, setPlan] = useState<AssignmentPlan | null>(null);
  const { state: auth } = useAuth();
  const location = useLocation();
  const context = cleaningBlockerContext(location.state, String(auth.session?.user.id ?? ""));
  const initialBlocker = context?.branch === branch && context.task === task ? context : null;
  const [blockerNote, setBlockerNote] = useState(initialBlocker?.note ?? "");
  const [blockerReason, setBlockerReason] = useState(initialBlocker?.reason ?? "");
  const [incidentLink, setIncidentLink] = useState(initialBlocker?.incident ?? "");
  const [materialLink, setMaterialLink] = useState(initialBlocker?.material ?? "");
  const [error, setError] = useState(""),
    [notice, setNotice] = useState(""),
    [busy, setBusy] = useState(false),
    [refresh, setRefresh] = useState(0);
  const [candidatePage, setCandidatePage] = useState(1);
  const [resolutionNote, setResolutionNote] = useState("");
  const [closurePending, setClosurePending] = useState<{
    branch: string;
    task: string;
    action: "blockers" | "complete";
    command: ClosureCommand;
    tag: string;
    key: string;
  } | null>(null);
  const [pending, setPending] = useState<{
    branch: string;
    task: string;
    command: ReassignmentCommand;
    tag: string;
    key: string;
  } | null>(null);
  const alive = useRef(true);
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
    };
  }, []);
  useEffect(() => {
    const abort = new AbortController();
    setData(null);
    setPlan(null);
    setError("");
    readCleaningExecution(branch, task, abort.signal)
      .then(async (d) => {
        if (abort.signal.aborted) return;
        setData(d);
        if (d.reassignCapability === "PROVISIONAL_NOT_ACCEPTED") {
          const candidates = await readAssignmentPlan(
            branch,
            task,
            candidatePage,
            "",
            abort.signal,
          );
          if (!abort.signal.aborted) setPlan(candidates);
        }
      })
      .catch((e) => {
        if (!abort.signal.aborted) setError(message(e));
      });
    return () => abort.abort();
  }, [branch, task, refresh, candidatePage]);
  function message(e: unknown) {
    if (e instanceof ApiError) {
      if (e.code === "BLOCKER_LINK_NOT_AVAILABLE") return "MSG02: Linked work is unavailable. Check source read permission, the Incident room, and the Material Request's explicit cleaning-task link.";
      if (e.code === "TIME_WORKDAY_REQUIRED") return "MSG30: Start a Working workday in this branch, then return and select Start.";
      if (e.code === "TIME_INTERVAL_ALREADY_OPEN") return "MSG31: Another work interval is open. Open Workday to view the current item before starting this task.";
      if (e.status === 400) return "MSG03: Check a different eligible assignee on shift, a future UTC deadline, and a reason of 10-500 characters. The task must be Assigned, In Progress or On Hold.";
      if (e.status === 401) return "Phiên đăng nhập đã hết hạn.";
      if (e.status === 403)
        return "Không có quyền hoặc bạn không còn là người phụ trách.";
      if (e.status === 412)
        return "Công việc đã thay đổi. Tải lại nguồn trước lệnh mới.";
      if (e.status === 409)
        return "Kiểm tra ca làm việc đang mở, công việc khác đang thực hiện và trạng thái hiện tại.";
      if (e.status === 503)
        return "Chức năng tạm thời chưa sẵn sàng. Vui lòng thử lại sau.";
    }
    return "Không xác minh được kết quả. Tải lại nguồn; với Reassign hãy thử đúng lệnh cũ.";
  }
  async function start(resume = false) {
    if (
      !data ||
      busy ||
      pending ||
      closurePending ||
      (resume
        ? data.resumeCapability !== "PROVISIONAL_V1"
        : data.startCapability !== "AVAILABLE")
    )
      return;
    setBusy(true);
    setError("");
    setNotice("");
    try {
      const result = await mutateCleaningExecution(branch, task, "start", data.sourceETag, {
        resolveBlocker: resume && data.holdReason !== "END_OF_SHIFT",
        ...(resume && data.holdReason !== "END_OF_SHIFT" ? { note: resolutionNote.trim() } : {}),
      });
      if (alive.current) {
        setNotice(result.hasEffectiveBlock
          ? "Đã bắt đầu dọn phòng và ghi nhận thời gian. Phòng vẫn bị chặn sử dụng."
          : "Đã bắt đầu dọn phòng; thời gian được ghi nhận bởi máy chủ.");
        setRefresh((n) => n + 1);
      }
    } catch (e) {
      if (alive.current) setError(message(e));
    } finally {
      if (alive.current) setBusy(false);
    }
  }
  async function execute(c: NonNullable<typeof pending>) {
    if (busy || c.branch !== branch || c.task !== task) return;
    setBusy(true);
    setError("");
    setNotice("");
    try {
      await mutateCleaningExecution(
        c.branch,
        c.task,
        "reassign",
        c.tag,
        c.command,
        c.key,
      );
      if (alive.current) {
        setPending(null);
        setNotice("MSG08: Reassignment saved. Notifications queued for both employees; previous effort remains attributed to the previous assignee.");
        setRefresh((n) => n + 1);
      }
    } catch (e) {
      if (alive.current) {
        setError(message(e));
        // These responses reject this command. Keep the same key only when
        // delivery/commit is ambiguous (network/server failure).
        if (e instanceof ApiError && [400, 409, 412, 422].includes(e.status)) {
          setPending(null);
          setNotice(message(e));
          setRefresh((n) => n + 1);
        }
      }
    } finally {
      if (alive.current) setBusy(false);
    }
  }
  function reassign(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!data || busy || pending || closurePending) return;
    try {
      const f = new FormData(e.currentTarget);
      const command = reassignmentCommand(
        data,
        String(f.get("assigneeId") ?? ""),
        String(f.get("dueAt") ?? ""),
        String(f.get("reason") ?? ""),
      );
      if (!plan?.candidates.some((c) => c.id === command.assigneeId))
        throw Error("INVALID_CANDIDATE");
      const c = { branch, task, command, tag: data.sourceETag, key: crypto.randomUUID() };
      setPending(c);
      void execute(c);
    } catch {
      setError(
        "Chọn người mới từ danh sách, hạn UTC tương lai và lý do 10–500 ký tự.",
      );
    }
  }
  async function sendClosure(c: NonNullable<typeof closurePending>) {
    setError("");
    setNotice("");
    try {
      const result = await mutateCleaningExecution(
        c.branch,
        c.task,
        c.action,
        c.tag,
        c.command,
        c.key,
      );
      if (alive.current) {
        setClosurePending(null);
        setNotice(
          (c.action === "blockers"
            ? "Đã ghi nhận trở ngại và tạm dừng dọn phòng."
            : "Đã hoàn tất dọn phòng, chờ kiểm tra.") +
            (result.hasEffectiveBlock ? " Phòng vẫn bị chặn sử dụng."
              : result.effectiveReadiness === "PENDING_CLEANING"
                ? " Trạng thái phòng: chờ dọn theo quy tắc hiện hành."
                : result.effectiveReadiness === "PENDING_INSPECTION"
                  ? " Trạng thái phòng: chờ kiểm tra."
                  : " Chưa xác minh được trạng thái phòng; tải lại bảng trạng thái."),
        );
        setRefresh((n) => n + 1);
      }
    } catch (e) {
      if (alive.current) {
        setError(message(e));
        if (e instanceof ApiError && [400, 409, 412].includes(e.status)) {
          setClosurePending(null);
          setNotice(message(e));
          setRefresh((n) => n + 1);
        }
      }
    }
  }
  async function submitClosure(
    event: FormEvent<HTMLFormElement>,
    action: "blockers" | "complete",
  ) {
    event.preventDefault();
    if (!data || busy || pending || closurePending) return;
    const form = new FormData(event.currentTarget);
    setBusy(true);
    try {
      const files = form
        .getAll("photos")
        .filter((f): f is File => f instanceof File && f.size > 0);
      if (files.length > 5) throw Error("TOO_MANY_PHOTOS");
      const photos = await Promise.all(files.map(uploadFile));
      const command = closureCommand(
        data,
        action,
        String(form.get("note") ?? ""),
        String(form.get("reasonId") ?? ""),
        photos,
      );
      if (action === "blockers") {
        for (const [name, value] of [["incidentId", incidentLink], ["materialRequestId", materialLink]] as const) {
          if (!value) continue;
          if (!/^[1-9]\d*$/.test(value) || !Number.isSafeInteger(Number(value))) throw Error("INVALID_BLOCKER_LINK");
          command[name] = Number(value);
        }
      }
      const c = {
        branch,
        task,
        action,
        command,
        tag: data.sourceETag,
        key: crypto.randomUUID(),
      };
      setClosurePending(c);
      await sendClosure(c);
    } catch {
      if (alive.current)
        setError(
          "Kiểm tra lý do, ghi chú, checklist và tệp JPG/PNG/PDF tối đa 10 MiB.",
        );
    } finally {
      if (alive.current) setBusy(false);
    }
  }
  return (
    <section className="panel">
      <h1>Vòng đời dọn phòng</h1>
      {error && <p role="alert">{error}</p>}
      {notice && <p role="status">{notice}</p>}
      {busy && <p role="status">Đang lưu…</p>}
      {!data && !error && <p role="status">Đang tải công việc…</p>}
      {data && (
        <>
          <h2>{data.taskNumber}</h2>
          {data.startDetails && <section aria-label="Cleaning instructions">
            <p>Room {data.startDetails.roomNumber} - {data.startDetails.cleaningType}</p>
            <p>Due (UTC): {data.startDetails.dueAtUtc ?? "N/A"}</p>
            <p>{data.startDetails.instructions ?? "No additional instructions."}</p>
            <ul>{data.startDetails.items.map(i => <li key={i.id}>{i.label}{i.isRequired ? " (required)" : ""}</li>)}</ul>
          </section>}
          {(error.startsWith("MSG30") || error.startsWith("MSG31")) && <Link to={"/workday?branchId=" + branch}>Open Workday</Link>}
          <p>
            {data.state} · Nhân viên {data.assigneeName ?? data.assigneeId ?? "—"}
          </p>
          <button
            disabled={
              busy ||
              !!pending ||
              !!closurePending ||
              data.startCapability !== "AVAILABLE"
            }
            onClick={() => void start()}
          >
            Bắt đầu dọn phòng
          </button>
          {data.startCapability === "BLOCKER_RESOLUTION_CONTRACT_REQUIRED" && (
            <>
              {data.resumeCapability === "PROVISIONAL_V1" ? (
                data.holdReason === "END_OF_SHIFT" ? (
                  <button disabled={busy || !!pending || !!closurePending} onClick={() => void start(true)}>Resume after end of shift</button>
                ) : (<form
                  onSubmit={(event) => {
                    event.preventDefault();
                    void start(true);
                  }}
                >
                  <label>
                    Ghi chú xử lý trở ngại
                    <input
                      required
                      maxLength={500}
                      value={resolutionNote}
                      disabled={busy || !!pending}
                      onChange={(event) =>
                        setResolutionNote(event.target.value)
                      }
                    />
                  </label>
                  <p>Tiếp tục xác nhận rằng các trở ngại đã được xử lý.</p>
                  <button
                    disabled={busy || !!pending || !resolutionNote.trim()}
                  >
                    Tiếp tục dọn phòng
                  </button>
                </form>)
              ) : (
                <>
                  <button disabled>Tiếp tục dọn phòng</button>
                  <p>
                    Chưa thể tiếp tục: chức năng xác nhận xử lý trở ngại chưa
                    sẵn sàng.
                  </p>
                </>
              )}
            </>
          )}
          {data.state === "IN_PROGRESS" && (
            <>
              <Link to={`/cleaning/tasks/${task}?branchId=${branch}`}>
                Mở tiến độ dọn phòng
              </Link>
              {data.reportBlockerCapability === "PROVISIONAL_V1" && <fieldset disabled={busy || !!pending || !!closurePending}>
                <legend>Linked blocker work (optional)</legend>
                <label>Incident ID for this room<input inputMode="numeric" value={incidentLink} onChange={e => setIncidentLink(e.target.value)} /></label>
                <label>Material Request ID linked to this task<input inputMode="numeric" value={materialLink} onChange={e => setMaterialLink(e.target.value)} /></label>
                <p>Source read permissions are required. Link Material Requests to this cleaning task in their linked-work section first.</p>
                <p>After returning, select any blocker photos again before submitting.</p>
                {data.startDetails && !busy && !pending && !closurePending && ["incidents", "material-requests"].map(module => <Link key={module}
                  to={`/operations/${module}?branchId=${branch}&action=${module === "incidents" ? "A004A-021" : "A004A-043"}`}
                  state={{ cleaningBlocker: { actor: String(auth.session?.user.id), branch, task, room: data.startDetails!.roomId,
                    note: blockerNote, reason: blockerReason, incident: incidentLink, material: materialLink } }}>
                  {module === "incidents" ? "Report room incident" : "Create material request"}
                </Link>)}
              </fieldset>}
              {data.reportBlockerCapability === "PROVISIONAL_V1" ? (
                <form onSubmit={(e) => void submitClosure(e, "blockers")}>
                  <h3>Báo cáo trở ngại</h3>
                  <label>
                    Lý do trở ngại
                    <select
                      aria-label="Lý do trở ngại"
                      name="reasonId"
                      required
                      disabled={busy || !!pending || !!closurePending}
                      value={blockerReason}
                      onChange={e => setBlockerReason(e.target.value)}
                    >
                      <option value="">Chọn lý do</option>
                      {data.blockerReasons?.map((r) => (
                        <option key={r.id} value={r.id}>
                          {r.name}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Ghi chú trở ngại
                    <input
                      name="note"
                      value={blockerNote}
                      onChange={e => setBlockerNote(e.target.value)}
                      required
                      maxLength={500}
                      disabled={busy || !!pending || !!closurePending}
                    />
                  </label>
                  <label>
                    Ảnh hoặc PDF trở ngại
                    <input
                      name="photos"
                      type="file"
                      multiple
                      accept="image/jpeg,image/png,application/pdf"
                      disabled={busy || !!pending || !!closurePending}
                    />
                  </label>
                  <button disabled={busy || !!pending || !!closurePending}>
                    Báo cáo trở ngại
                  </button>
                </form>
              ) : (
                <button disabled>Báo cáo trở ngại</button>
              )}
              {data.completeCapability === "PROVISIONAL_V1" ? (
                <form onSubmit={(e) => void submitClosure(e, "complete")}>
                  <h3>Hoàn tất dọn phòng</h3>
                  <ul>
                    {data.items?.map((i) => (
                      <li key={i.id}>
                        {i.done ? "✓" : "○"} {i.label}
                        {i.required ? " (bắt buộc)" : ""}
                      </li>
                    ))}
                  </ul>
                  <p>
                    Lưu các hạng mục bắt buộc trong tiến độ rồi tải lại nguồn
                    trước khi hoàn tất.
                  </p>
                  <label>
                    Ghi chú hoàn tất
                    <input
                      name="note"
                      required
                      maxLength={500}
                      disabled={busy || !!pending || !!closurePending}
                    />
                  </label>
                  <label>
                    Ảnh hoặc PDF hoàn tất
                    <input
                      name="photos"
                      type="file"
                      multiple
                      accept="image/jpeg,image/png,application/pdf"
                      disabled={busy || !!pending || !!closurePending}
                    />
                  </label>
                  <button
                    disabled={
                      busy ||
                      !!pending ||
                      !!closurePending ||
                      !data.items?.length ||
                      data.items.some((i) => i.required && !i.done)
                    }
                  >
                    Hoàn tất dọn phòng
                  </button>
                </form>
              ) : (
                <button disabled>Hoàn tất dọn phòng</button>
              )}
              {data.reportBlockerCapability !== "PROVISIONAL_V1" &&
                data.completeCapability !== "PROVISIONAL_V1" && (
                  <p role="status">
                    Báo cáo trở ngại và hoàn tất dọn phòng tạm thời chưa khả
                    dụng. Bạn vẫn có thể lưu tiến độ; đạt 100% chưa đồng nghĩa
                    hoàn tất.
                  </p>
                )}
            </>
          )}
          {data.reassignCapability === "PROVISIONAL_NOT_ACCEPTED" && plan && (
            <form onSubmit={reassign}>
              <h3>Phân công lại</h3>
              <p>
                Thay đổi người phụ trách và gửi thông báo cho hai nhân viên.
              </p>
              <label>
                Người phụ trách mới
                <select
                  aria-label="Người phụ trách mới"
                  name="assigneeId"
                  required
                  disabled={busy || !!pending}
                  defaultValue=""
                >
                  <option value="">Chọn nhân viên</option>
                  {plan.candidates
                    .filter((c) => c.id !== data.assigneeId)
                    .map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name} · {c.openTasks} việc mở
                      </option>
                    ))}
                </select>
              </label>
              <div>
                <button
                  type="button"
                  disabled={busy || !!pending || candidatePage <= 1}
                  onClick={() => setCandidatePage((n) => n - 1)}
                >
                  Trang trước nhân viên
                </button>
                <span>
                  {" "}
                  Trang {candidatePage} /{" "}
                  {Math.max(1, Math.ceil(plan.total / 20))}{" "}
                </span>
                <button
                  type="button"
                  disabled={
                    busy || !!pending || candidatePage * 20 >= plan.total
                  }
                  onClick={() => setCandidatePage((n) => n + 1)}
                >
                  Trang sau nhân viên
                </button>
              </div>
              <label>
                Hạn UTC
                <input
                  name="dueAt"
                  defaultValue={plan.task.dueAt ?? ""}
                  required
                  placeholder="YYYY-MM-DDTHH:mm:ssZ"
                  disabled={busy || !!pending}
                />
              </label>
              <label>
                Lý do
                <input
                  name="reason"
                  required
                  minLength={10}
                  maxLength={500}
                  disabled={busy || !!pending}
                />
              </label>
              <button
                disabled={
                  busy ||
                  !!pending ||
                  !["ASSIGNED", "IN_PROGRESS", "ON_HOLD"].includes(
                    data.state ?? "",
                  )
                }
              >
                Lưu phân công lại
              </button>
            </form>
          )}
        </>
      )}
      {pending && error && (
        <button disabled={busy} onClick={() => void execute(pending)}>
          Thử lại đúng lệnh phân công lại
        </button>
      )}
      {closurePending && error && (
        <button
          disabled={busy}
          onClick={async () => {
            if (busy) return;
            setBusy(true);
            try {
              await sendClosure(closurePending);
            } finally {
              if (alive.current) setBusy(false);
            }
          }}
        >
          Thử lại đúng lệnh cho công việc {closurePending.task}
        </button>
      )}
      <button disabled={busy} onClick={() => setRefresh((n) => n + 1)}>
        Tải lại nguồn
      </button>
    </section>
  );
}
