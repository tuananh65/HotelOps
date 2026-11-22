import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import { useState, type ReactNode } from "react";
import { SlidersHorizontal } from "lucide-react";
import {
  action,
  api,
  fields,
  message,
  type Row,
  type Page,
  timestamp,
  downloadEvidence,
} from "./api";
import { useAuth } from "./auth";
import {
  Badge,
  Dialog,
  Empty,
  History,
  Pager,
  State,
  useQuery,
  label,
} from "./ui";
import CleaningQueue from "./cleaning-queue";
import {
  commands,
  kinds,
  nextCommands,
  permission,
  terminal,
} from "./workflow";
export default function Work({
  onlyKind,
  allowedKinds,
  title,
  personal = false,
  coordinator = false,
  reviewOnly = false,
  technicalStaff = false,
  historyMine = false,
  readScope,
}: {
  onlyKind?: string;
  allowedKinds?: string[];
  title?: string;
  personal?: boolean;
  coordinator?: boolean;
  reviewOnly?: boolean;
  technicalStaff?: boolean;
  historyMine?: boolean;
  readScope?: number;
}) {
  const { branch, can, profile } = useAuth(),
    visible = kinds.filter(
      (k) =>
        can(k[2]) &&
        (!onlyKind || k[0] === onlyKind) &&
        (!allowedKinds || allowedKinds.includes(k[0])),
    );
  const serverFilters = !!onlyKind || !!allowedKinds || personal;
  const [mine, setMine] = useState(
    readScope !== undefined
      ? false
      : personal ||
          (!allowedKinds &&
            !["TASK_MANAGE", "CLEANING_ASSIGN", "MAINTENANCE_ASSIGN"].some(
              (p) => can(p, false),
            )),
  );
  const [kind, setKind] = useState(
      onlyKind ||
        visible.find(
          (k) =>
            k[0] ===
            new URLSearchParams(location.hash.split("?")[1]).get("kind"),
        )?.[0] ||
        (can("MAINTENANCE_EXECUTE") && !can("MAINTENANCE_ASSIGN")
          ? "MAINTENANCE"
          : null) ||
        visible[0]?.[0] ||
        "CLEANING",
    ),
    [page, setPage] = useState(1),
    [revision, bump] = useState(0),
    [id, setId] = useState<number | null>(
      Number(new URLSearchParams(location.hash.split("?")[1]).get("id")) ||
        null,
    ),
    [creating, setCreating] = useState(
      new URLSearchParams(location.hash.split("?")[1]).get("create") === "1",
    ),
    [command, setCommand] = useState(""),
    [error, setError] = useState(""),
    [pending, setPending] = useState(false),
    [search, setSearch] = useState(
      new URLSearchParams(location.hash.split("?")[1]).get("search") || "",
    ),
    [parentDraft, setParentDraft] = useState<Row | null>(null);
  const [filters, setFilters] = useState(false),
    [status, setStatus] = useState(reviewOnly ? "COMPLETED" : ""),
    [department, setDepartment] = useState("");
  const [sort, setSort] = useState("latest"),
    [from, setFrom] = useState(""),
    [to, setTo] = useState("");
  const assignedOnly =
    personal &&
    !historyMine &&
    ["CLEANING", "REWORK", "OPERATIONAL", "MAINTENANCE"].includes(kind);
  const [deriving, setDeriving] = useState(false),
    [deriveKey, setDeriveKey] = useState("");
  const modalActions = coordinator || technicalStaff;
  const technical =
    readScope !== undefined ||
    coordinator ||
    (technicalStaff && ["MAINTENANCE", "INCIDENT"].includes(kind));
  const [priority, setPriority] = useState("");
  const [queue, setQueue] = useState(
    new URLSearchParams(location.hash.split("?")[1]).get("queue") || "",
  );
  const commandText = (command: string) =>
    coordinator && kind === "MAINTENANCE"
      ? command === "verify"
        ? "Chấp nhận"
        : command === "reopen"
          ? "Yêu cầu xử lý lại"
          : commands[command]
      : commands[command];
  const dateFilter = (day: string, offset = 0) => {
    const value = new Date(`${day}T00:00:00`);
    value.setDate(value.getDate() + offset);
    return encodeURIComponent(value.toISOString());
  };
  const statusOptions =
    kind === "INTERNAL_REQUEST"
      ? [
          "DRAFT",
          "SUBMITTED",
          "PENDING_ACCEPTANCE",
          "ACCEPTED",
          "IN_PROGRESS",
          "COMPLETED",
          "CLOSED",
          "REJECTED",
          "CANCELLED",
        ]
      : kind === "INCIDENT"
        ? ["OPEN", "IN_PROGRESS", "RESOLVED", "CLOSED", "CANCELLED"]
        : kind === "INSPECTION"
          ? [
              "ASSIGNED",
              "INSPECTING",
              "PENDING_CONCLUSION",
              "PASSED",
              "FAILED",
              "CANCELLED",
            ]
          : kind === "MAINTENANCE"
            ? [
                "PENDING",
                "IN_PROGRESS",
                "ON_HOLD",
                "COMPLETED",
                "CLOSED",
                "CANCELLED",
              ]
            : kind === "CLEANING"
              ? [
                  "PENDING_ASSIGNMENT",
                  "ASSIGNED",
                  "IN_PROGRESS",
                  "ON_HOLD",
                  "PENDING_INSPECTION",
                  "REWORK_REQUIRED",
                  "COMPLETED",
                  "CLOSED",
                  "CANCELLED",
                ]
              : kind === "REWORK"
                ? [
                    "PENDING_ACCEPTANCE",
                    "ACCEPTED",
                    "IN_PROGRESS",
                    "ON_HOLD",
                    "PENDING_REINSPECTION",
                    "COMPLETED",
                    "CLOSED",
                    "CANCELLED",
                  ]
                : [
                    "PENDING_ASSIGNMENT",
                    "ASSIGNED",
                    "ACCEPTED",
                    "IN_PROGRESS",
                    "PAUSED",
                    "PENDING_VERIFICATION",
                    "CLOSED",
                    "CANCELLED",
                  ];
  const supervisor =
    kind === "CLEANING" && can("CLEANING_ASSIGN", false) && !mine;
  const technicalQuery = `${historyMine ? "&historyMine=true" : ""}${priority ? `&priorityId=${priority}` : ""}${queue && kind === "MAINTENANCE" ? `&queue=${queue}` : ""}`;
  const query = useQuery<Page>(
      supervisor
        ? null
        : `work?${readScope === 0 ? "" : `branchId=${readScope ?? branch}&`}kind=${kind}&page=${page}${technicalQuery}&mine=${assignedOnly || historyMine ? false : mine}${assignedOnly ? "&assignedToMe=true" : ""}${serverFilters ? `&search=${encodeURIComponent(search)}&sort=${sort}${from ? `&from=${dateFilter(from)}` : ""}${to ? `&to=${dateFilter(to, 1)}` : ""}${department ? `&departmentId=${department}` : ""}${status === "OVERDUE" ? "&overdue=true" : status ? `&status=${status}` : ""}` : ""}`,
      revision,
    ),
    detail = useQuery(id ? `work/${id}` : null, revision),
    options = useQuery(`workspace/options?branchId=${branch}`);
  const related = useQuery(id ? `work/${id}/related` : null, revision);
  const ownTime = useQuery(
    can("TASK_VIEW") ? `insights/time?branchId=${branch}` : null,
    revision,
  );
  const activeTimer = fields(ownTime.data).find((t) => !t.closedAt);
  const candidates = useQuery<Row[]>(
    id && ["assign", "handover-offer"].includes(command)
      ? `work/candidates?branchId=${branch}&kind=${kind}${command === "handover-offer" ? "&workId=" + id : ""}`
      : null,
  );
  const parents = useQuery<Page>(
    creating && ["INSPECTION", "REWORK"].includes(kind)
      ? `work?branchId=${branch}&kind=CLEANING&pageSize=100`
      : null,
  );
  const w = detail.data?.work?.fields,
    assignments = fields(detail.data?.assignments),
    ownAssignment = assignments.find(
      (a) =>
        a.assigneeUserId === profile.user.fields.userId &&
        a.assignmentStatus === "ACTIVE",
    );
  const events = fields(detail.data?.events);
  const offer = events.find(
    (e) =>
      e.eventType === "HANDOVER_OFFERED" &&
      e.recipientUserId === profile.user.fields.userId &&
      !events.some(
        (x) =>
          x.relatedEventId === e.workEventId &&
          ["HANDOVER_ACCEPTED", "HANDOVER_REJECTED"].includes(x.eventType),
      ),
  );
  const choices = w
    ? nextCommands(
        kind,
        w.status,
        !!ownAssignment,
        !!ownAssignment?.acceptedAt,
        w.createdBy === profile.user.fields.userId,
      ).filter(
        (a) =>
          !(
            w.kind === "INCIDENT" &&
            ["assign", "cancel", "verify", "reopen"].includes(a) &&
            (!can("INCIDENT_TRIAGE", false) ||
              detail.data?.capabilities?.canCoordinateIncident === false)
          ) &&
          !(
            w.kind === "INTERNAL_REQUEST" &&
            ((["route", "assign"].includes(a) &&
              detail.data?.capabilities?.canCoordinateRequest === false) ||
              (["cancel", "reject"].includes(a) &&
                detail.data?.capabilities?.canHandleRequest === false))
          ) &&
          can(
            permission(kind, a),
            !!ownAssignment || w.createdBy === profile.user.fields.userId,
          ) &&
          !(
            (a === "verify" || (w.kind === "MAINTENANCE" && a === "reopen")) &&
            assignments.some(
              (x) => x.assigneeUserId === profile.user.fields.userId,
            )
          ),
      )
    : [];
  const canExecute =
    w && !!ownAssignment && can(permission(w.kind, "execute"), true);
  if (canExecute && ["IN_PROGRESS", "INSPECTING"].includes(w.status))
    choices.push("progress");
  if (w && !terminal.includes(w.status)) {
    if (
      ownAssignment &&
      can(permission(kind, "handover-offer")) &&
      !(
        ["MAINTENANCE", "INCIDENT"].includes(kind) &&
        !can(
          kind === "MAINTENANCE" ? "MAINTENANCE_ASSIGN" : "INCIDENT_TRIAGE",
          false,
        )
      )
    )
      choices.push("handover-offer");
    if (
      offer &&
      (!["MAINTENANCE", "INCIDENT"].includes(kind) ||
        can(
          kind === "MAINTENANCE" ? "MAINTENANCE_ASSIGN" : "INCIDENT_TRIAGE",
          false,
        ))
    )
      choices.push("handover-accept", "handover-reject");
  }
  async function mutate(path: string, body: Row, method = "POST") {
    if (pending) return;
    setPending(true);
    setError("");
    try {
      const result = await api(path, body, method);
      setCommand("");
      setCreating(false);
      if (path === "work") setId(result.fields.workId);
      if (path.endsWith("/maintenance")) {
        setDeriving(false);
        location.hash = `work?kind=MAINTENANCE&id=${result.fields.workId}`;
      }
      bump((x) => x + 1);
    } catch (e) {
      setError(message(e));
      if (!creating) bump((x) => x + 1);
    } finally {
      setPending(false);
    }
  }
  const close = () => {
    if (!pending) {
      setId(null);
      setCommand("");
      setError("");
    }
  };
  const refs = (type: string) =>
    options.data?.references?.filter((r: Row) => r.kind === type) || [];
  const visibleRows = fields(query.data?.items).filter(
    (r) =>
      serverFilters ||
      r.title.toLocaleLowerCase().includes(search.trim().toLocaleLowerCase()),
  );
  const createAction = !reviewOnly &&
    can(visible.find((k) => k[0] === kind)?.[3] || "") && (
      <button
        className="primary"
        onClick={() => {
          setError("");
          setParentDraft(null);
          setCreating(true);
        }}
      >
        + Tạo mới
      </button>
    );
  return (
    <>
      <div className="page-heading">
        <PageTitle>
          <h1>
            {title ||
              (onlyKind === "INTERNAL_REQUEST"
                ? "Yêu cầu nội bộ"
                : onlyKind
                  ? "Công việc"
                  : supervisor
                    ? "Điều phối buồng phòng"
                    : "Công việc & yêu cầu")}
          </h1>
          <p>Theo dõi từ phân công đến kiểm tra và xác nhận</p>
        </PageTitle>
      </div>
      {visible.length > 1 && (
        <div className="tabs">
          {visible.map((k) => (
            <button
              key={k[0]}
              aria-pressed={kind === k[0]}
              onClick={() => {
                setKind(k[0]);
                setPage(1);
                setId(null);
                setSearch("");
                setStatus(reviewOnly ? "COMPLETED" : "");
                setDepartment("");
                setCommand("");
                setCreating(false);
              }}
            >
              {k[1]}
            </button>
          ))}
        </div>
      )}
      <PageToolbar actions={createAction}>
        {!supervisor && (
          <label className="search">
            {serverFilters ? "Tìm công việc / yêu cầu" : "Tìm trong trang"}
            <input
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                if (serverFilters) setPage(1);
              }}
              placeholder="Tên công việc…"
            />
          </label>
        )}
        {serverFilters && (
          <button aria-expanded={filters} onClick={() => setFilters(!filters)}>
            <SlidersHorizontal size={18} /> Bộ lọc
            {status || department ? " •" : ""}
          </button>
        )}
        {!personal && (
          <button
            aria-pressed={mine}
            onClick={() => {
              setMine(!mine);
              setPage(1);
            }}
          >
            {mine ? "Đang xem việc của tôi" : "Đang xem trong phạm vi quyền"}
          </button>
        )}
      </PageToolbar>
      {serverFilters && filters && (
        <PageToolbar>
          <label>
            Trạng thái
            <select
              disabled={reviewOnly}
              value={status}
              onChange={(e) => {
                setStatus(e.target.value);
                setPage(1);
              }}
            >
              <option value="">Tất cả trạng thái</option>
              <option value="OVERDUE">Quá hạn</option>
              {statusOptions.map((s) => (
                <option key={s} value={s}>
                  {label(s)}
                </option>
              ))}
            </select>
          </label>
          {readScope !== 0 && (
            <label>
              Bộ phận xử lý
              <select
                value={department}
                onChange={(e) => {
                  setDepartment(e.target.value);
                  setPage(1);
                }}
              >
                <option value="">Tất cả bộ phận</option>
                {(options.data?.departments || []).map((d: Row) => (
                  <option key={d.departmentId} value={d.departmentId}>
                    {d.name}
                  </option>
                ))}
              </select>
            </label>
          )}
          {technicalStaff && kind === "MAINTENANCE" && (
            <>
              <label>
                Nhóm công việc
                <select
                  value={queue}
                  onChange={(e) => {
                    setQueue(e.target.value);
                    setPage(1);
                  }}
                >
                  <option value="">Tất cả công việc</option>
                  <option value="awaiting">Chờ tiếp nhận</option>
                  <option value="active">Đang xử lý</option>
                  <option value="review">Chờ nghiệm thu</option>
                  <option value="rework">Cần xử lý lại</option>
                  <option value="scheduled">Bảo trì định kỳ</option>
                </select>
              </label>
              <label>
                Mức độ ưu tiên
                <select
                  value={priority}
                  onChange={(e) => {
                    setPriority(e.target.value);
                    setPage(1);
                  }}
                >
                  <option value="">Tất cả mức ưu tiên</option>
                  {refs("PRIORITY").map((r: Row) => (
                    <option key={r.referenceId} value={r.referenceId}>
                      {r.name}
                    </option>
                  ))}
                </select>
              </label>
            </>
          )}
          <label>
            Sắp xếp
            <select
              value={sort}
              onChange={(e) => {
                setSort(e.target.value);
                setPage(1);
              }}
            >
              <option value="latest">Mới nhất</option>
              <option value="due">Hạn gần nhất</option>
              <option value="title">Tên công việc</option>
            </select>
          </label>
          <label>
            Tạo từ ngày
            <input
              type="date"
              value={from}
              max={to || undefined}
              onChange={(e) => {
                setFrom(e.target.value);
                setPage(1);
              }}
            />
          </label>
          <label>
            Đến ngày
            <input
              type="date"
              value={to}
              min={from || undefined}
              onChange={(e) => {
                setTo(e.target.value);
                setPage(1);
              }}
            />
          </label>
          <button
            onClick={() => {
              setStatus(reviewOnly ? "COMPLETED" : "");
              setPriority("");
              setQueue("");
              setDepartment("");
              setSearch("");
              setSort("latest");
              setFrom("");
              setTo("");
              setPage(1);
            }}
          >
            Đặt lại
          </button>
        </PageToolbar>
      )}
      {activeTimer && (
        <section className="active-shift">
          <div>
            <strong>Bạn đang thực hiện công việc #{activeTimer.workId}</strong>
            <p>
              Tạm dừng công việc đang chạy trước khi bắt đầu một công việc khác.
            </p>
          </div>
          <button
            className="primary"
            onClick={async () => {
              try {
                const d = await api(`work/${activeTimer.workId}`);
                if (personal) {
                  location.hash = `work?kind=${d.work.fields.kind}&id=${activeTimer.workId}`;
                  return;
                }
                setKind(d.work.fields.kind);
                setId(activeTimer.workId);
                setCommand("");
                setError("");
              } catch (e) {
                setError(message(e));
              }
            }}
          >
            Mở việc đang chạy
          </button>
        </section>
      )}
      {supervisor ? (
        <CleaningQueue
          revision={revision}
          open={(r) => {
            setId(r.workId);
            setError("");
            setCommand(
              [
                "PENDING_ASSIGNMENT",
                "ASSIGNED",
                "IN_PROGRESS",
                "ON_HOLD",
              ].includes(r.status)
                ? "assign"
                : "",
            );
          }}
        />
      ) : (
        <State {...query} retry={() => bump((x) => x + 1)}>
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th>Công việc</th>
                  {readScope !== undefined && <th>Chi nhánh</th>}
                  {technical && <th>Ưu tiên / Phụ trách</th>}
                  <th>Trạng thái</th>
                  <th>Hạn xử lý</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {visibleRows.map((r) => (
                  <tr key={r.workId}>
                    <td>
                      <strong>{r.title}</strong>
                      <small>
                        {r.workNo || `#${r.workId}`}
                        {r.roomId
                          ? " · Phòng " +
                            (r.roomCode ||
                              options.data?.rooms?.find(
                                (x: Row) => x.roomId === r.roomId,
                              )?.code ||
                              r.roomId)
                          : ""}
                      </small>
                      {technical && r.assetName && <small>{r.assetName}</small>}
                    </td>
                    {readScope !== undefined && (
                      <td>
                        {profile.branches.find((b) => b.branchId === r.branchId)
                          ?.name || "—"}
                      </td>
                    )}
                    {technical && (
                      <td>
                        {r.priorityName || "—"}
                        <small>{r.assigneeName || "Chưa phân công"}</small>
                      </td>
                    )}
                    <td>
                      <Badge
                        value={
                          r.kind === "MAINTENANCE" && r.status === "COMPLETED"
                            ? "Chờ nghiệm thu"
                            : r.status
                        }
                      />
                    </td>
                    <td>{timestamp(r.dueAt)}</td>
                    <td>
                      <button
                        onClick={() => {
                          setId(r.workId);
                          setError("");
                          setCommand("");
                        }}
                      >
                        {allowedKinds || personal || coordinator
                          ? "Chi tiết"
                          : "Xử lý"}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {!visibleRows.length && (
            <Empty>
              {search.trim() ? (
                <>
                  {serverFilters
                    ? "Không có công việc khớp tìm kiếm."
                    : "Không có công việc khớp tìm kiếm trong trang này."}{" "}
                  <button onClick={() => setSearch("")}>Xóa tìm kiếm</button>
                </>
              ) : (
                "Chưa có công việc trong phạm vi đã chọn."
              )}
            </Empty>
          )}
          <Pager page={page} total={query.data?.total || 0} onPage={setPage} />
        </State>
      )}
      {creating && can(visible.find((k) => k[0] === kind)?.[3] || "") && (
        <Dialog
          title={`Tạo ${visible.find((k) => k[0] === kind)?.[1].toLowerCase()}`}
          close={() => {
            if (!pending) setCreating(false);
          }}
        >
          <State {...options}>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                const f = new FormData(e.currentTarget);
                const num = (k: string) => (f.get(k) ? Number(f.get(k)) : null);
                const parent =
                  parentDraft ||
                  fields(parents.data?.items).find(
                    (p) => p.workId === num("parentWorkId"),
                  );
                mutate("work", {
                  branchId: branch,
                  kind,
                  title: f.get("title"),
                  description: f.get("description") || null,
                  roomId: parent?.roomId || num("roomId"),
                  parentWorkId: parent?.workId || num("parentWorkId"),
                  departmentId: num("departmentId"),
                  requesterDepartmentId:
                    kind === "INTERNAL_REQUEST"
                      ? num("requesterDepartmentId")
                      : null,
                  checklistTemplateId: num("checklistTemplateId"),
                  priorityId: num("priorityId"),
                  severityId: num("severityId"),
                  assetId: kind === "INCIDENT" ? num("assetId") : null,
                  dueAt: f.get("dueAt")
                    ? new Date(String(f.get("dueAt"))).toISOString()
                    : null,
                });
              }}
            >
              <label>
                Tiêu đề
                <input
                  name="title"
                  required
                  maxLength={250}
                  defaultValue={
                    parentDraft
                      ? `${kind === "INSPECTION" ? "Kiểm tra" : kind === "REWORK" ? "Dọn lại" : "Bảo trì"} · ${parentDraft.title}`
                      : ""
                  }
                />
              </label>
              <label>
                Mô tả
                <textarea name="description" maxLength={4000} />
              </label>
              {["INSPECTION", "REWORK"].includes(kind) ? (
                <label>
                  Công việc dọn phòng gốc
                  {parentDraft ? (
                    <>
                      <input
                        type="hidden"
                        name="parentWorkId"
                        value={parentDraft.workId}
                      />
                      <p>
                        {parentDraft.title} · #{parentDraft.workId}
                      </p>
                    </>
                  ) : (
                    <select name="parentWorkId" required defaultValue="">
                      <option value="">Chọn công việc</option>
                      {fields(parents.data?.items)
                        .filter((p) =>
                          ["PENDING_INSPECTION", "REWORK_REQUIRED"].includes(
                            p.status,
                          ),
                        )
                        .map((p) => (
                          <option key={p.workId} value={p.workId}>
                            {p.title} · #{p.workId}
                          </option>
                        ))}
                    </select>
                  )}
                </label>
              ) : (
                <label>
                  Phòng
                  <select
                    name="roomId"
                    required={kind === "CLEANING"}
                    defaultValue={
                      new URLSearchParams(location.hash.split("?")[1]).get(
                        "room",
                      ) || ""
                    }
                  >
                    <option value="">Không gắn phòng</option>
                    {options.data?.rooms.map((r: Row) => (
                      <option key={r.roomId} value={r.roomId}>
                        {r.code}
                      </option>
                    ))}
                  </select>
                </label>
              )}
              {kind === "INCIDENT" && (
                <>
                  <label>
                    Tài sản liên quan
                    <select name="assetId" defaultValue="">
                      <option value="">Không gắn tài sản</option>
                      {(options.data?.incidentAssets || []).map((a: Row) => (
                        <option key={a.assetId} value={a.assetId}>
                          {a.assetCode} · {a.name}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Mức độ nghiêm trọng
                    <select name="severityId" defaultValue="">
                      <option value="">Chưa chọn</option>
                      {refs("SEVERITY").map((r: Row) => (
                        <option key={r.referenceId} value={r.referenceId}>
                          {r.name}
                        </option>
                      ))}
                    </select>
                  </label>
                </>
              )}
              <label>
                Bộ phận xử lý
                <select name="departmentId" defaultValue="">
                  <option value="">Chưa chọn</option>
                  {options.data?.departments.map((r: Row) => (
                    <option key={r.departmentId} value={r.departmentId}>
                      {r.name}
                    </option>
                  ))}
                </select>
              </label>
              {kind === "INTERNAL_REQUEST" && (
                <label>
                  Bộ phận yêu cầu
                  <select name="requesterDepartmentId" required defaultValue="">
                    <option value="">Chọn bộ phận của bạn</option>
                    {options.data?.departments
                      .filter(
                        (d: Row) =>
                          profile.roles.some(
                            (r) =>
                              ["OPERATION_MANAGER", "DEPT_MANAGER"].includes(
                                r.code,
                              ) && r.branchId === branch,
                          ) ||
                          profile.memberships.some(
                            (m) =>
                              m.branchId === branch &&
                              m.kind === "EMPLOYMENT" &&
                              m.departmentId === d.departmentId,
                          ),
                      )
                      .map((d: Row) => (
                        <option key={d.departmentId} value={d.departmentId}>
                          {d.name}
                        </option>
                      ))}
                  </select>
                </label>
              )}
              {options.data?.checklists && (
                <label>
                  Mẫu checklist
                  <select
                    name="checklistTemplateId"
                    defaultValue=""
                    required={kind === "INSPECTION"}
                  >
                    <option value="">Không chọn</option>
                    {options.data.checklists
                      .filter(
                        (t: Row) =>
                          t.kind === (kind === "REWORK" ? "CLEANING" : kind),
                      )
                      .map((t: Row) => (
                        <option
                          key={t.checklistTemplateId}
                          value={t.checklistTemplateId}
                        >
                          {t.name}
                        </option>
                      ))}
                  </select>
                </label>
              )}
              <label>
                Hạn hoàn thành
                <input type="datetime-local" name="dueAt" />
              </label>
              <button className="primary" disabled={pending}>
                Tạo công việc
              </button>
            </form>
          </State>
          {error && (
            <p className="alert" role="alert">
              {error}
            </p>
          )}
        </Dialog>
      )}
      {deriving && w && (
        <Dialog
          title="Tạo công việc bảo trì"
          close={() => {
            if (!pending) setDeriving(false);
          }}
        >
          <form
            onSubmit={(e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              mutate(`work/${id}/maintenance`, {
                rowVersion: w.rowVersion,
                operationKey: deriveKey,
                title: f.get("title"),
                reason: f.get("reason"),
                dueAt: f.get("dueAt")
                  ? new Date(String(f.get("dueAt"))).toISOString()
                  : null,
              });
            }}
          >
            <label>
              Tên công việc
              <input
                name="title"
                required
                maxLength={250}
                defaultValue={`Xử lý: ${w.title}`}
              />
            </label>
            <label>
              Hạn xử lý
              <input name="dueAt" type="datetime-local" />
            </label>
            <label>
              Lý do
              <textarea name="reason" minLength={10} required />
            </label>
            {error && (
              <p role="alert" className="alert">
                {error}
              </p>
            )}
            <button disabled={pending} className="primary">
              Tạo công việc
            </button>
          </form>
        </Dialog>
      )}
      {id && (
        <Dialog
          className={
            personal || coordinator || technicalStaff || readScope !== undefined
              ? "room-drawer"
              : undefined
          }
          title={w?.title || "Chi tiết công việc"}
          close={close}
        >
          <State {...detail}>
            {w && (
              <>
                <div className="toolbar">
                  <Badge
                    value={
                      w.kind === "MAINTENANCE" && w.status === "COMPLETED"
                        ? "Chờ nghiệm thu"
                        : w.status
                    }
                  />
                  <span>#{w.workId}</span>
                </div>
                <p>{w.description}</p>
                {(personal || coordinator || readScope !== undefined) && (
                  <>
                    <p>
                      {detail.data?.context?.branch} ·{" "}
                      {detail.data?.context?.floor}{" "}
                      {detail.data?.context?.room &&
                        `· Phòng ${detail.data.context.room}`}
                    </p>
                    <p>
                      Số công việc: {w.workNo} · Tiến độ: {w.progressPercent}% ·
                      Hạn: {timestamp(w.dueAt)}
                    </p>
                    {(detail.data?.assignmentDetails || []).map((a: Row) => (
                      <p className="hint" key={a.assignmentId}>
                        Người giao: {a.assignedBy} · {timestamp(a.assignedAt)} ·{" "}
                        {label(a.assignmentStatus)}
                      </p>
                    ))}
                    {w.roomId && can("INCIDENT_CREATE") && (
                      <a
                        className="button-link"
                        href={`#work?kind=INCIDENT&room=${w.roomId}&create=1`}
                      >
                        Báo sự cố
                      </a>
                    )}
                  </>
                )}
                <section className="workflow-next" aria-label="Bước tiếp theo">
                  {w.kind === "CLEANING" &&
                    ["PENDING_INSPECTION", "REWORK_REQUIRED"].includes(
                      w.status,
                    ) && (
                      <>
                        <h3>
                          {w.status === "REWORK_REQUIRED"
                            ? "Dọn lại và kiểm tra lại"
                            : "Kiểm tra phòng"}
                        </h3>
                        <p>
                          {personal
                            ? "Công việc đã được gửi kiểm tra. Xem kết quả và việc làm lại được giao bên dưới."
                            : "Mở phiếu đang xử lý bên dưới hoặc tạo phiếu mới. Người kiểm tra phải độc lập với người dọn."}
                        </p>
                        {[
                          [
                            "INSPECTION",
                            "Tạo phiếu kiểm tra",
                            "INSPECTION_PERFORM",
                          ],
                          ["REWORK", "Tạo việc dọn lại", "CLEANING_ASSIGN"],
                        ]
                          .filter(
                            ([k, , p]) =>
                              can(p) &&
                              !(
                                k === "REWORK" && w.status !== "REWORK_REQUIRED"
                              ) &&
                              !fields(related.data).some(
                                (r) =>
                                  r.kind === k &&
                                  !terminal.includes(r.status) &&
                                  r.status !== "PENDING_REINSPECTION",
                              ),
                          )
                          .map(([k, n]) => (
                            <button
                              key={k}
                              className="primary"
                              onClick={() => {
                                setParentDraft(w);
                                setKind(k);
                                setId(null);
                                setCommand("");
                                setCreating(true);
                              }}
                            >
                              {n}
                            </button>
                          ))}
                      </>
                    )}
                  {!coordinator &&
                    w.kind === "INCIDENT" &&
                    can("MAINTENANCE_ASSIGN") &&
                    !terminal.includes(w.status) && (
                      <button
                        onClick={() => {
                          setParentDraft(w);
                          setKind("MAINTENANCE");
                          setId(null);
                          setCommand("");
                          setCreating(true);
                        }}
                      >
                        Tạo công việc bảo trì
                      </button>
                    )}
                  <State {...related} retry={() => bump((n) => n + 1)}>
                    {fields(related.data)
                      .filter((r) => r.workId !== id)
                      .map((r) => (
                        <button
                          key={r.workId}
                          onClick={() => {
                            if (coordinator && r.kind === "MAINTENANCE") {
                              location.hash = `work?kind=MAINTENANCE&id=${r.workId}`;
                              return;
                            }
                            setKind(r.kind);
                            setId(r.workId);
                            setCommand("");
                            setError("");
                          }}
                        >
                          {r.title} · <Badge value={r.status} />
                        </button>
                      ))}
                  </State>
                </section>
                <div className="toolbar">
                  {choices.map((a) => (
                    <button
                      key={a}
                      className={a === choices[0] ? "primary" : ""}
                      disabled={pending}
                      onClick={() => {
                        setCommand(a);
                        setError("");
                      }}
                    >
                      {commandText(a)}
                    </button>
                  ))}
                </div>
                {command && (
                  <WorkActionModal
                    modal={modalActions}
                    title={commandText(command)}
                    close={() => {
                      if (!pending) setCommand("");
                    }}
                  >
                    <form
                      className="action-form"
                      onSubmit={(e) => {
                        e.preventDefault();
                        const f = new FormData(e.currentTarget);
                        mutate(
                          `work/${id}/actions/${command}`,
                          action(w.rowVersion, {
                            reason: f.get("reason") || null,
                            progress:
                              command === "progress"
                                ? Number(f.get("progress"))
                                : null,
                            userId: f.get("userId")
                              ? Number(f.get("userId"))
                              : null,
                            relatedId: command.startsWith("handover-")
                              ? offer?.workEventId
                              : f.get("relatedId")
                                ? Number(f.get("relatedId"))
                                : null,
                            priorityId: f.get("priorityId")
                              ? Number(f.get("priorityId"))
                              : null,
                            dueAt: f.get("dueAt")
                              ? new Date(String(f.get("dueAt"))).toISOString()
                              : null,
                          }),
                        );
                      }}
                    >
                      <h3>{commandText(command)}</h3>
                      {command === "progress" && (
                        <label>
                          Tiến độ (%)
                          <input
                            type="number"
                            name="progress"
                            min={0}
                            max={100}
                            required
                            defaultValue={w.progressPercent}
                          />
                        </label>
                      )}
                      {["assign", "handover-offer"].includes(command) && (
                        <State {...candidates}>
                          <label>
                            Nhân viên
                            <select name="userId" required defaultValue="">
                              <option value="">Chọn nhân viên</option>
                              {candidates.data?.map((u) => (
                                <option key={u.userId} value={u.userId}>
                                  {u.fullName}
                                </option>
                              ))}
                            </select>
                          </label>
                        </State>
                      )}
                      {command === "route" && (
                        <label>
                          Bộ phận nhận
                          <select name="relatedId" required defaultValue="">
                            <option value="">Chọn bộ phận</option>
                            {options.data?.departments.map((d: Row) => (
                              <option
                                key={d.departmentId}
                                value={d.departmentId}
                              >
                                {d.name}
                              </option>
                            ))}
                          </select>
                        </label>
                      )}
                      {command === "triage" && (
                        <>
                          <label>
                            Ưu tiên
                            <select name="priorityId" required defaultValue="">
                              <option value="">Chọn mức ưu tiên</option>
                              {refs("PRIORITY").map((r: Row) => (
                                <option
                                  key={r.referenceId}
                                  value={r.referenceId}
                                >
                                  {r.name}
                                </option>
                              ))}
                            </select>
                          </label>
                          <label>
                            Hạn xử lý
                            <input
                              type="datetime-local"
                              name="dueAt"
                              required
                            />
                          </label>
                        </>
                      )}
                      <label>
                        Lý do / ghi chú
                        <textarea
                          name="reason"
                          minLength={10}
                          maxLength={2000}
                          required={
                            command.startsWith("handover-") ||
                            [
                              "pause",
                              "cancel",
                              "reopen",
                              "reject",
                              "fail",
                              "triage",
                              "escalate",
                            ].includes(command) ||
                            (command === "assign" &&
                              assignments.some(
                                (a) => a.assignmentStatus === "ACTIVE",
                              ))
                          }
                        />
                      </label>
                      <button className="primary" disabled={pending}>
                        Xác nhận
                      </button>
                      <button type="button" onClick={() => setCommand("")}>
                        Quay lại
                      </button>
                    </form>
                    {modalActions && error && (
                      <p className="alert" role="alert">
                        {error}
                      </p>
                    )}
                  </WorkActionModal>
                )}
                {error && !(modalActions && command) && (
                  <p className="alert" role="alert">
                    {error}
                  </p>
                )}
                {technical && detail.data?.maintenance && (
                  <section>
                    <p>
                      Ưu tiên: {detail.data.maintenance.priority || "—"} · Mức
                      độ: {detail.data.maintenance.severity || "—"}
                    </p>
                    {detail.data.maintenance.asset && (
                      <p>
                        Thiết bị: {detail.data.maintenance.asset.name} ·{" "}
                        {detail.data.maintenance.asset.assetCode}
                      </p>
                    )}
                    {detail.data.maintenance.assignees.map(
                      (a: Row, i: number) => (
                        <p key={i}>
                          {a.fullName} · {label(a.assignmentStatus)} ·{" "}
                          {timestamp(a.assignedAt)}
                        </p>
                      ),
                    )}
                    {w.completionNote && (
                      <p>Kết quả thực hiện: {w.completionNote}</p>
                    )}
                    {technicalStaff &&
                      w.kind === "MAINTENANCE" &&
                      ownAssignment &&
                      !terminal.includes(w.status) &&
                      can("MR_CREATE") && (
                        <a
                          className="button-link"
                          href={`#documents?kind=MATERIAL_REQUEST&create=1&sourceWorkId=${w.workId}`}
                        >
                          Yêu cầu vật tư cho công việc
                        </a>
                      )}
                    <h3>Lịch sử nghiệm thu</h3>
                    <History
                      rows={workHistory(
                        detail.data.maintenance.reviews,
                        w.kind,
                      )}
                    />
                    {detail.data.maintenance.materials?.map((d: Row) => (
                      <a
                        className="overview-task"
                        key={d.documentId}
                        href={`#documents?kind=MATERIAL_REQUEST&id=${d.documentId}`}
                      >
                        {d.documentNo} · {label(d.status)}
                      </a>
                    ))}
                    {w.kind === "INCIDENT" &&
                      !terminal.includes(w.status) &&
                      !fields(related.data).some(
                        (r) =>
                          r.kind === "MAINTENANCE" &&
                          !["CLOSED", "CANCELLED"].includes(r.status),
                      ) &&
                      can("MAINTENANCE_ASSIGN", false) && (
                        <button
                          onClick={() => {
                            setError("");
                            setDeriveKey(crypto.randomUUID());
                            setDeriving(true);
                          }}
                        >
                          Tạo công việc bảo trì
                        </button>
                      )}
                  </section>
                )}
                <h3>Checklist</h3>
                {fields(detail.data?.checklist).map((c) => (
                  <div className="check-item" key={c.workChecklistItemId}>
                    <div>
                      {c.descriptionSnapshot}
                      {c.requiredSnapshot && <small>Bắt buộc</small>}
                    </div>
                    <select
                      aria-label={`Kết quả ${c.descriptionSnapshot}`}
                      disabled={
                        pending ||
                        !canExecute ||
                        !["IN_PROGRESS", "INSPECTING"].includes(w.status)
                      }
                      value={c.outcome || ""}
                      onChange={(e) =>
                        mutate(
                          `work/${id}/checklist/${c.workChecklistItemId}`,
                          {
                            rowVersion: c.rowVersion,
                            outcome: e.target.value,
                            note: c.note || null,
                          },
                          "PUT",
                        )
                      }
                    >
                      <option value="" disabled>
                        Chưa đánh giá
                      </option>
                      <option value="PASS">Đạt</option>
                      <option value="FAIL">Không đạt</option>
                      {!c.requiredSnapshot && (
                        <option value="NA">Không áp dụng</option>
                      )}
                    </select>
                  </div>
                ))}
                {canExecute &&
                  ["IN_PROGRESS", "INSPECTING"].includes(w.status) &&
                  fields(detail.data?.checklist)
                    .filter((c) => c.outcome)
                    .map((c) => (
                      <form
                        key={`note-${c.workChecklistItemId}-${c.rowVersion}`}
                        onSubmit={(e) => {
                          e.preventDefault();
                          mutate(
                            `work/${id}/checklist/${c.workChecklistItemId}`,
                            {
                              rowVersion: c.rowVersion,
                              outcome: c.outcome,
                              note:
                                new FormData(e.currentTarget).get("note") ||
                                null,
                            },
                            "PUT",
                          );
                        }}
                      >
                        <label>
                          Ghi chú: {c.descriptionSnapshot}
                          <textarea
                            name="note"
                            maxLength={2000}
                            defaultValue={c.note || ""}
                          />
                        </label>
                        <button disabled={pending}>
                          Lưu ghi chú checklist
                        </button>
                      </form>
                    ))}
                {!detail.data?.checklist?.length && (
                  <p className="hint">Công việc không có checklist đính kèm.</p>
                )}
                <h3>Minh chứng</h3>
                {(detail.data?.attachments || []).map((file: Row) => (
                  <button
                    key={file.attachmentId}
                    disabled={pending}
                    onClick={async () => {
                      setPending(true);
                      setError("");
                      try {
                        await downloadEvidence(
                          file.attachmentId,
                          file.originalName,
                        );
                      } catch (e) {
                        setError(message(e));
                      } finally {
                        setPending(false);
                      }
                    }}
                  >
                    {file.originalName}
                  </button>
                ))}
                {!detail.data?.attachments?.length && (
                  <p className="hint">Chưa có tệp minh chứng.</p>
                )}
                {!terminal.includes(w.status) &&
                  ((canExecute &&
                    ["IN_PROGRESS", "INSPECTING"].includes(w.status)) ||
                    (w.kind === "INCIDENT" &&
                      w.createdBy === profile.user.fields.userId &&
                      can("INCIDENT_CREATE"))) && (
                    <form
                      onSubmit={async (e) => {
                        e.preventDefault();
                        if (pending) return;
                        const form = e.currentTarget;
                        const data = new FormData(form);
                        data.set("branchId", String(branch));
                        data.set("workId", String(id));
                        setPending(true);
                        setError("");
                        try {
                          await api("evidence", data);
                          form.reset();
                          bump((n) => n + 1);
                        } catch (e) {
                          setError(message(e));
                        } finally {
                          setPending(false);
                        }
                      }}
                    >
                      <label>
                        Ảnh / tệp minh chứng
                        <input
                          name="file"
                          type="file"
                          accept="image/png,image/jpeg,application/pdf"
                          required
                        />
                      </label>
                      <small>PNG, JPEG hoặc PDF; tối đa 10 MB.</small>
                      <button disabled={pending}>Tải minh chứng</button>
                    </form>
                  )}
                {kind === "INSPECTION" &&
                  ownAssignment &&
                  ["INSPECTING", "PENDING_CONCLUSION"].includes(w.status) && (
                    <form
                      onSubmit={(e) => {
                        e.preventDefault();
                        const form = e.currentTarget;
                        mutate(`work/${id}/findings`, {
                          rowVersion: w.rowVersion,
                          description: new FormData(form).get("description"),
                        });
                      }}
                    >
                      <label>
                        Ghi nhận lỗi cần làm lại
                        <textarea name="description" required minLength={10} />
                      </label>
                      <button disabled={pending}>Ghi nhận lỗi</button>
                    </form>
                  )}
                {fields(detail.data?.findings).map((f) => (
                  <section className="panel" key={f.findingId}>
                    <p>
                      {f.description} · <Badge value={f.status} />
                    </p>
                    {f.status === "OPEN" &&
                      can("INSPECTION_PERFORM") &&
                      w.status !== "PASSED" && (
                        <form
                          onSubmit={(e) => {
                            e.preventDefault();
                            const data = new FormData(e.currentTarget);
                            mutate(
                              `work/${id}/findings/${f.findingId}/resolve`,
                              action(f.rowVersion, {
                                relatedId: Number(data.get("relatedId")),
                                reason: data.get("reason"),
                              }),
                            );
                          }}
                        >
                          <label>
                            Công việc đã dọn lại
                            <select name="relatedId" required defaultValue="">
                              <option value="">
                                Chọn việc đã gửi kiểm tra lại
                              </option>
                              {fields(related.data)
                                .filter(
                                  (r) =>
                                    r.kind === "REWORK" &&
                                    r.status === "PENDING_REINSPECTION",
                                )
                                .map((r) => (
                                  <option key={r.workId} value={r.workId}>
                                    {r.title}
                                  </option>
                                ))}
                            </select>
                          </label>
                          <label>
                            Kết quả xác minh
                            <textarea
                              name="reason"
                              required
                              minLength={10}
                              maxLength={2000}
                            />
                          </label>
                          <button disabled={pending}>
                            Xác nhận lỗi đã xử lý
                          </button>
                        </form>
                      )}
                  </section>
                ))}
                <h3>Lịch sử xử lý</h3>
                {personal &&
                  (detail.data?.reviews || []).map((review: Row) => (
                    <section className="panel" key={review.workId}>
                      <h3>Kết quả kiểm tra #{review.workId}</h3>
                      <Badge value={review.status} />
                      <p>{review.completionNote}</p>
                      {(review.findings || []).map(
                        (finding: Row, i: number) => (
                          <p key={i}>
                            {finding.description} ·{" "}
                            <Badge value={finding.status} />
                            {finding.resolutionNote && (
                              <small>{finding.resolutionNote}</small>
                            )}
                          </p>
                        ),
                      )}
                    </section>
                  ))}
                <History
                  rows={workHistory(fields(detail.data?.events), w.kind)}
                />
              </>
            )}
          </State>
        </Dialog>
      )}
    </>
  );
}
function workHistory(rows: Row[], kind: string) {
  if (kind !== "MAINTENANCE") return rows;
  const labels: Record<string, string> = {
    ASSIGN: "Phân công công việc",
    ACCEPT: "Đã nhận công việc",
    START: "Bắt đầu công việc",
    PAUSE: "Tạm dừng công việc",
    RESUME: "Tiếp tục công việc",
    PROGRESS: "Cập nhật tiến độ",
    COMPLETE: "Gửi hoàn thành",
    VERIFY: "Đã nghiệm thu",
    REOPEN: "Yêu cầu xử lý lại",
    CANCEL: "Hủy công việc",
  };
  return rows.map((row) => ({
    ...row,
    action: labels[row.action || row.eventType] || row.action || row.eventType,
  }));
}
function WorkActionModal({
  modal,
  title,
  close,
  children,
}: {
  modal: boolean;
  title: string;
  close: () => void;
  children: ReactNode;
}) {
  return modal ? (
    <Dialog title={title} close={close}>
      {children}
    </Dialog>
  ) : (
    <>{children}</>
  );
}
