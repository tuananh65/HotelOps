import { AlertPopup } from "./alert-popup";
import PageTitle from "./page-title";
import "./housekeeping-mobile.css";
import "./housekeeping-workspace.css";
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
  ConfirmationActions,
  Dialog,
  Empty,
  History,
  Pager,
  State,
  useQuery,
  label,
} from "./ui";
import CleaningQueue from "./cleaning-queue";
import CleaningRooms from "./cleaning-rooms";
import PaperInspection from "./paper-inspection";
import RequestRecipientFields from "./request-recipient-fields";
import CleaningIncidents from "./cleaning-incidents";
import CleaningSupplyReconciliation from "./cleaning-supply-reconciliation";
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
  housekeeping = false,
  hideKindTabs = false,
  initialKind,
  readScope,
  initialStatus,
  cleaningCurrent = false,
  detailOnly = false,
  onCloseDetail,
}: {
  onlyKind?: string;
  allowedKinds?: string[];
  title?: string;
  personal?: boolean;
  coordinator?: boolean;
  reviewOnly?: boolean;
  technicalStaff?: boolean;
  historyMine?: boolean;
  housekeeping?: boolean;
  hideKindTabs?: boolean;
  initialKind?: string;
  readScope?: number;
  initialStatus?: string;
  cleaningCurrent?: boolean;
  detailOnly?: boolean;
  onCloseDetail?: () => void;
}) {
  const { branch, can, profile } = useAuth();
  const branchRoles = profile.roles.filter(r => r.branchId === null || r.branchId === branch);
  const housekeepingStaff = branchRoles.some(r => r.code === "HK_STAFF");
  const receptionOnly = branchRoles.length > 0 && branchRoles.every(r => r.code === "RECEPTION");
  const operationsManager = branchRoles.some(r => r.code === "OPERATION_MANAGER");
  const maintenanceSupervisor = branchRoles.some(r => r.code === "MAINTENANCE_SUPERVISOR");
  const requestCoordinator = branchRoles.some(r => ["OPERATION_MANAGER", "OWNER"].includes(r.code))
    || profile.memberships.some(m => m.branchId === branch && m.kind === "DEPARTMENT_AUTHORITY");
  const
    visible = kinds.filter(
      (k) =>
        can(k[2]) &&
        !(housekeepingStaff && k[0] === "INCIDENT") &&
        (!receptionOnly || k[0] === "INTERNAL_REQUEST") &&
        (!onlyKind || k[0] === onlyKind) &&
        (!allowedKinds || allowedKinds.includes(k[0])),
    );
  const serverFilters = !!onlyKind || !!allowedKinds || personal;
  const reception = profile.roles.some(
    (r) =>
      r.code === "RECEPTION" && (r.branchId === null || r.branchId === branch),
  );
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
      (initialKind && visible.some(k => k[0] === initialKind) ? initialKind : null) ||
        (onlyKind && visible.some(k => k[0] === onlyKind) ? onlyKind : null) ||
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
    [command, setCommand] = useState(
      housekeeping &&
        new URLSearchParams(location.hash.split("?")[1]).get("assign") === "1"
        ? "assign"
        : "",
    ),
    [error, setError] = useState(""),
    [pending, setPending] = useState(false),
    [search, setSearch] = useState(
      new URLSearchParams(location.hash.split("?")[1]).get("search") || "",
    ),
    [parentDraft, setParentDraft] = useState<Row | null>(null);
  const [cleaningRoom, setCleaningRoom] = useState<Row | null>(null);
  const requestTypes=useQuery(kind==="INTERNAL_REQUEST"&&creating?`work/internal-request-types?branchId=${branch}`:null,revision);
  const [requestedRoom, setRequestedRoom] = useState(
    Number(new URLSearchParams(location.hash.split("?")[1]).get("room")) ||
      undefined,
  );
  const [filters, setFilters] = useState(false),
    [status, setStatus] = useState(initialStatus || (reviewOnly ? "COMPLETED" : "")),
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
  const modalActions = true;
  const technical =
    readScope !== undefined ||
    coordinator ||
    (technicalStaff && ["MAINTENANCE", "INCIDENT"].includes(kind));
  const [priority, setPriority] = useState("");
  const [queue, setQueue] = useState(
    new URLSearchParams(location.hash.split("?")[1]).get("queue") || "",
  );
  const commandText = (command: string) =>
    ["CLEANING", "REWORK"].includes(kind) && command === "assign" && assignments.some(a => a.assignmentStatus === "ACTIVE")
      ? "Phân công lại"
      : coordinator && kind === "MAINTENANCE"
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
    (housekeeping || kind === "CLEANING") && can("CLEANING_ASSIGN", false) && !mine;
  const requestPersonal = kind === "INTERNAL_REQUEST" && !requestCoordinator;
  const technicalQuery = `${cleaningCurrent ? "&cleaningCurrent=true" : ""}${historyMine ? "&historyMine=true" : ""}${priority ? `&priorityId=${priority}` : ""}${queue && kind === "MAINTENANCE" ? `&queue=${queue}` : ""}`;
  const query = useQuery<Page>(
      supervisor || detailOnly
        ? null
        : `work?${readScope === 0 ? "" : `branchId=${readScope ?? branch}&`}kind=${kind}&page=${page}${technicalQuery}&mine=${requestPersonal ? true : assignedOnly || historyMine ? false : mine}${assignedOnly ? "&assignedToMe=true" : ""}${serverFilters ? `&search=${encodeURIComponent(search)}&sort=${sort}${from ? `&from=${dateFilter(from)}` : ""}${to ? `&to=${dateFilter(to, 1)}` : ""}${department ? `&departmentId=${department}` : ""}${status === "OVERDUE" ? "&overdue=true" : status ? `&status=${status}` : ""}` : ""}`,
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
  const cleaningOptions = useQuery<{ canCreate: boolean; candidates: Row[] }>(
    creating && ["CLEANING", "REWORK"].includes(kind) && can("CLEANING_ASSIGN")
      ? `work/cleaning-creation-options?branchId=${branch}`
      : null,
    revision,
  );
  const parents = useQuery<Page>(
    creating && ["INSPECTION", "REWORK"].includes(kind)
      ? `work?branchId=${branch}&kind=CLEANING&pageSize=100`
      : null,
  );
  const parentId = Number(
    new URLSearchParams(location.hash.split("?")[1]).get("parent"),
  );
  const exactParent = useQuery(
    creating && parentId && ["INSPECTION", "REWORK"].includes(kind)
      ? `work/${parentId}`
      : null,
  );
  const creationParent = parentDraft || exactParent.data?.work?.fields;
  const w = receptionOnly && detail.data?.work?.fields?.kind !== "INTERNAL_REQUEST" ? undefined : detail.data?.work?.fields,
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
  const readOnlyWork = operationsManager && ["INCIDENT", "INTERNAL_REQUEST"].includes(w?.kind || kind);
  const choices = w && !readOnlyWork
    ? nextCommands(
        kind,
        w.status,
        !!ownAssignment || (w.kind === "MAINTENANCE" && detail.data?.capabilities?.canClaimMaintenance === true),
        !!ownAssignment?.acceptedAt,
        w.createdBy === profile.user.fields.userId,
      ).map(a => ["CLEANING", "REWORK"].includes(kind) && a === "accept" ? "start" : a).filter(
        (a) =>
          !(a === "pause" && ["CLEANING", "REWORK"].includes(w.kind)) &&
          !(a === "cancel" && ["CLEANING", "REWORK", "INSPECTION"].includes(w.kind)) &&
          !(w.kind === "INSPECTION" && a !== "cancel") &&
          !(maintenanceSupervisor && w.kind === "MAINTENANCE" && ["assign", "verify", "reopen", "cancel"].includes(a)) &&
          !(
            a === "assign" &&
            ((["CLEANING", "REWORK"].includes(w.kind) &&
              [
                "PENDING_INSPECTION",
                "REWORK_REQUIRED",
                "PENDING_REINSPECTION",
              ].includes(w.status)) ||
              (w.kind === "INSPECTION" && w.status === "PENDING_CONCLUSION"))
          ) &&
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
            !(w.kind === "OPERATIONAL" && detail.data?.capabilities?.canSelfVerifyOperational === true) &&
            assignments.some(
              (x) => x.assigneeUserId === profile.user.fields.userId,
            )
          ),
      )
    : [];
  const paperHousekeeping = ["CLEANING", "REWORK", "INSPECTION"].includes(kind);
  const reportIncident = w?.roomId && detail.data?.capabilities?.canReportRoomIssue === true
    ? <a className="button-link incident-action" href={`#room-issues?room=${w.roomId}&create=1&work=${w.workId}&workKind=${w.kind}`}>Báo sự cố</a> : null;
  const canExecute =
    w && !readOnlyWork && !!ownAssignment && can(permission(w.kind, "execute"), true);
  if (canExecute && !paperHousekeeping && ["IN_PROGRESS", "INSPECTING"].includes(w.status))
    choices.push("progress");
  if (w && !readOnlyWork && !(maintenanceSupervisor && w.kind === "MAINTENANCE") && !terminal.includes(w.status) && (!["CLEANING", "REWORK"].includes(w.kind) ||
    (!assignments.some(a => a.assignmentStatus === "ACTIVE" && a.acceptedAt) && !w.actualStartAt && !fields(detail.data?.time).length && ["ASSIGNED", "PENDING_ACCEPTANCE"].includes(w.status)))) {
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
  const roomIssue = w?.kind === "INCIDENT" && events.some(e=>e.eventType==="ROOM_ISSUE_REPORTED");
  if (roomIssue) choices.length=0;
  async function mutate(path: string, body: Row, method = "POST") {
    if (pending) return;
    setPending(true);
    setError("");
    try {
      const result = await api(path, body, method);
      setCommand(housekeepingStaff && ["CLEANING", "REWORK"].includes(kind) && path.endsWith("/actions/start") ? "complete" : "");
      setCreating(false);
      if (path === "work") {
        setId(result.fields.workId);
        if (
          housekeeping && kind === "INSPECTION"
        )
          setCommand("assign");
        if (housekeeping && kind === "INSPECTION" && kind !== initialKind)
          location.hash = `work?kind=${kind}&id=${result.fields.workId}&assign=1`;
        else if (housekeeping)
          history.replaceState(
            null,
            "",
            `#work?kind=${kind}&id=${result.fields.workId}`,
          );
        setCleaningRoom(null);
      }
      if (path.endsWith("/maintenance")) {
        setDeriving(false);
        location.hash = `work?kind=MAINTENANCE&id=${result.fields.workId}`;
      }
      bump((x) => x + 1);
    } catch (e) {
      setError(message(e));
      if (!creating || kind === "CLEANING") bump((x) => x + 1);
    } finally {
      setPending(false);
    }
  }
  const close = () => {
    if (!pending) {
      setId(null);
      setCommand("");
      setError("");
      onCloseDetail?.();
    }
  };
  const refs = (type: string) =>
    options.data?.references?.filter((r: Row) => r.kind === type) || [];
  const visibleRows = fields(query.data?.items).filter(
    (r) =>
      serverFilters ||
      r.title.toLocaleLowerCase().includes(search.trim().toLocaleLowerCase()),
  );
  const mayCreate =
    kind !== "INSPECTION" &&
    !(operationsManager && ["INCIDENT", "INTERNAL_REQUEST"].includes(kind)) &&
    !(maintenanceSupervisor && kind === "MAINTENANCE") &&
    !(reception && kind === "INCIDENT") &&
    can(visible.find((k) => k[0] === kind)?.[3] || "");
  const createAction = !reviewOnly && mayCreate && (
    <button
      className="primary"
      onClick={() => {
        setError("");
        if (supervisor) setKind("CLEANING");
        setParentDraft(null);
        setCleaningRoom(null);
        setRequestedRoom(undefined);
        setCreating(true);
      }}
    >
      + Tạo mới
    </button>
  );
  return (
    <>
      {!detailOnly&&<>
      <div className="page-heading">
        <PageTitle>
          <h1>
            {title ||
              (housekeeping
                ? {
                    CLEANING: "Phân công dọn phòng",
                    INSPECTION: "Kiểm tra phòng",
                    REWORK: "Xử lý làm lại",
                    INCIDENT: "Sự cố & Thông báo",
                  }[kind] || "Buồng phòng"
                : null) ||
              (onlyKind === "INTERNAL_REQUEST"
                ? "Yêu cầu nội bộ"
                : onlyKind
                  ? "Công việc"
                  : supervisor
                    ? "Điều phối buồng phòng"
                    : "Công việc & yêu cầu")}
          </h1>
          <p>{personal && onlyKind === "OPERATIONAL"
            ? "Công việc vận hành được giao cho bạn, ngoài việc dọn phòng · Hoàn thành để chờ xác nhận"
            : "Theo dõi từ phân công đến kiểm tra và xác nhận"}</p>
        </PageTitle>
      </div>
      {!hideKindTabs && visible.length > 1 && (
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
      {!supervisor && <PageToolbar actions={createAction}>
        <label className="search">
          {supervisor
            ? "Tìm phòng / công việc"
            : serverFilters
              ? "Tìm công việc / yêu cầu"
              : "Tìm trong trang"}
          <input
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              if (serverFilters) setPage(1);
            }}
            placeholder={
              supervisor ? "Số phòng hoặc tên công việc…" : "Tên công việc…"
            }
          />
        </label>
        {serverFilters && !supervisor && (
          <button aria-expanded={filters} onClick={() => setFilters(!filters)}>
            <SlidersHorizontal size={18} /> Bộ lọc
            {status || department ? " •" : ""}
          </button>
        )}
        {!personal && !requestPersonal && !receptionOnly && (
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
      </PageToolbar>}
      {serverFilters && !supervisor && filters && (
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
            <strong>Bạn đang thực hiện {paperHousekeeping ? "một công việc" : `công việc #${activeTimer.workId}`}</strong>
            <p>
              Hoàn thành công việc đang thực hiện trước khi bắt đầu công việc khác.
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
          search={search}
          onSearch={setSearch}
          actions={createAction}
          sort={sort}
          onSort={setSort}
          create={(room) => {
            setKind("CLEANING");
            setCleaningRoom(room);
            setParentDraft(null);
            setId(null);
            setCommand("");
            setError("");
            setCreating(true);
          }}
          open={(r) => {
            setKind("CLEANING");
            setId(r.workId);
            setError("");
            setCommand(
              [
                "PENDING_ASSIGNMENT",
                "ASSIGNED",
              ].includes(r.status)
                ? "assign"
                : "",
            );
          }}
        />
      ) : (
        <State {...query} retry={() => bump((x) => x + 1)}>
          <div className={`table-scroll ${housekeepingStaff ? "staff-work-list" : ""}`}>
            <table>
              <thead>
                <tr>
                  <th>Công việc</th>
                  {readScope !== undefined && <th>Chi nhánh</th>}
                  {technical && <th>Ưu tiên / Phụ trách</th>}
                  <th>Trạng thái</th>
                  <th>Hạn xử lý</th>
                  {!housekeepingStaff && <th>Thao tác</th>}
                </tr>
              </thead>
              <tbody>
                {visibleRows.map((r) => (
                  <tr key={r.workId} tabIndex={housekeepingStaff ? 0 : undefined}
                    role={housekeepingStaff ? "button" : undefined}
                    aria-label={housekeepingStaff ? `Mở ${r.title}` : undefined}
                    onClick={housekeepingStaff ? () => { setKind(r.kind); setId(r.workId); setError(""); setCommand(""); } : undefined}
                    onKeyDown={housekeepingStaff ? e => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); setKind(r.kind); setId(r.workId); setError(""); setCommand(""); } } : undefined}>
                    <td>
                      <strong>{r.title}</strong>
                      <small>
                        {!paperHousekeeping && (r.workNo || `#${r.workId}`)}
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
                            : housekeepingStaff && ["ASSIGNED", "PENDING_ACCEPTANCE"].includes(r.status) ? "Chờ nhận" : r.completedWithIncident?"Đã dọn xong — phòng gặp sự cố":r.status
                        }
                      />
                    </td>
                    <td>{timestamp(r.dueAt)}</td>
                    {!housekeepingStaff && <td>
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
                    </td>}
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
      </>}
      {creating && mayCreate && (
        <Dialog
          title={`Tạo ${visible.find((k) => k[0] === kind)?.[1].toLowerCase()}`}
          close={() => {
            if (!pending) { setCreating(false); if (housekeeping && parentDraft) { setKind("CLEANING"); setId(parentDraft.workId); } }
          }}
        >
          <State {...options}>
            {kind === "CLEANING" && can("CLEANING_ASSIGN") && !cleaningRoom ? (
              <CleaningRooms
                revision={revision}
                create={setCleaningRoom}
                roomId={requestedRoom}
              />
            ) : (
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  if (["CLEANING", "REWORK"].includes(kind) &&
                      (cleaningOptions.loading || cleaningOptions.error || !cleaningOptions.data?.canCreate)) return;
                  if(kind==="INTERNAL_REQUEST"&&(requestTypes.loading||requestTypes.error))return;
                  const f = new FormData(e.currentTarget);
                  const num = (k: string) =>
                    f.get(k) ? Number(f.get(k)) : null;
                  const parent =
                    creationParent ||
                    fields(parents.data?.items).find(
                      (p) => p.workId === num("parentWorkId"),
                    );
                  mutate("work", {
                    branchId: branch,
                    kind,
                    title: f.get("title"),
                    description: f.get("description") || null,
                    roomId: parent?.roomId || num("roomId"),
                    roomRowVersion:
                      kind === "CLEANING"
                        ? cleaningRoom?.rowVersion
                        : undefined,
                    parentWorkId: parent?.workId || num("parentWorkId"),
                    departmentId: kind==="INTERNAL_REQUEST" ? (String(f.get("requestRecipient")).startsWith("department:")?Number(String(f.get("requestRecipient")).split(":")[1]):null) : num("departmentId"),
                    categoryId: kind==="INTERNAL_REQUEST"?num("categoryId"):null,
                    assigneeUserId: ["CLEANING", "REWORK"].includes(kind) ? num("assigneeUserId") : kind==="INTERNAL_REQUEST"&&String(f.get("requestRecipient")).startsWith("user:")?Number(String(f.get("requestRecipient")).split(":")[1]):undefined,
                    durationMinutes: ["CLEANING", "REWORK"].includes(kind) ? num("durationMinutes") : undefined,
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
                {kind==="INTERNAL_REQUEST"&&<State {...requestTypes} retry={()=>bump(n=>n+1)}>{requestTypes.data&&<RequestRecipientFields catalog={requestTypes.data} departments={options.data?.departments||[]}/>}</State>}
                <label>
                  Tiêu đề
                  <input
                    name="title"
                    required
                    maxLength={250}
                    defaultValue={
                      cleaningRoom && kind === "CLEANING"
                        ? `Dọn phòng ${cleaningRoom.code}`
                        : creationParent
                          ? `${kind === "INSPECTION" ? "Kiểm tra" : kind === "REWORK" ? "Dọn lại" : "Bảo trì"} · ${creationParent.title}`
                          : ""
                    }
                  />
                </label>
                <label>
                  Mô tả
                  <textarea name="description" maxLength={4000} />
                </label>
                {kind === "CLEANING" && cleaningRoom ? (
                  <section className="panel">
                    <strong>Phòng {cleaningRoom.code}</strong>
                    <p>
                      {cleaningRoom.floor} · {cleaningRoom.roomType} ·{" "}
                      {cleaningRoom.capacity} người
                    </p>
                    <input
                      type="hidden"
                      name="roomId"
                      value={cleaningRoom.roomId}
                    />

                  </section>
                ) : ["INSPECTION", "REWORK"].includes(kind) ? (
                  <label>
                    Công việc dọn phòng gốc
                    {creationParent ? (
                      <>
                        <input
                          type="hidden"
                          name="parentWorkId"
                          value={creationParent.workId}
                        />
                        <p>
                          {creationParent.title}
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
                              {p.title}
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
                {["CLEANING", "REWORK"].includes(kind) ? (
                  <State {...cleaningOptions} retry={() => bump((x) => x + 1)}>
                    {!cleaningOptions.data?.canCreate && (
                      <AlertPopup>
                        Bạn phải bắt đầu ca và đang trong giờ làm việc tại chi nhánh này để tạo yêu cầu dọn phòng.
                      </AlertPopup>
                    )}
                    <label>
                      Nhân viên dọn phòng
                      <select name="assigneeUserId" required defaultValue=""
                        disabled={pending || !cleaningOptions.data?.canCreate}>
                        <option value="">Chọn nhân viên đang trong ca</option>
                        {(cleaningOptions.data?.candidates || []).map((person) => (
                          <option key={person.userId} value={person.userId}>
                            {person.fullName} · {person.employeeCode}
                          </option>
                        ))}
                      </select>
                    </label>
                    {cleaningOptions.data?.candidates.length === 0 && (
                      <Empty>Chưa có nhân viên dọn phòng đang trong ca tại chi nhánh này.</Empty>
                    )}
                  </State>
                ) : (
                  <label>
                    {kind!=="INTERNAL_REQUEST"&&<>Bộ phận xử lý
                    <select name="departmentId" defaultValue="">
                      <option value="">Chưa chọn</option>
                      {options.data?.departments.map((r: Row) => (
                        <option key={r.departmentId} value={r.departmentId}>
                          {r.name}
                        </option>
                      ))}
                    </select></>}
                  </label>
                )}
                {kind === "INTERNAL_REQUEST" && (
                  <label>
                    Bộ phận yêu cầu
                    <select
                      name="requesterDepartmentId"
                      required
                      defaultValue=""
                    >
                      <option value="">Chọn bộ phận của bạn</option>
                      {options.data?.departments
                        .filter(
                          (d: Row) =>
                            profile.roles.some(
                              (r) =>
                                ["OPERATION_MANAGER"].includes(
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
                {!paperHousekeeping && options.data?.checklists && (
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
                {["CLEANING", "REWORK"].includes(kind) ? <CleaningDuration /> : <label>
                  Hạn hoàn thành
                  <input type="datetime-local" name="dueAt" />
                </label>}
                <button
                  className="primary"
                  disabled={pending || !!(parentId && exactParent.loading) ||
                    (["CLEANING", "REWORK"].includes(kind) && (cleaningOptions.loading || !!cleaningOptions.error ||
                      !cleaningOptions.data?.canCreate || !cleaningOptions.data?.candidates.length))}
                >
                  Tạo công việc
                </button>
              </form>
            )}
          </State>
          {error && (
            <AlertPopup>
              {error}
            </AlertPopup>
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
              <AlertPopup>
                {error}
              </AlertPopup>
            )}
            <button disabled={pending} className="primary">
              Tạo công việc
            </button>
          </form>
        </Dialog>
      )}
      {id && (
        <Dialog
          className="room-drawer"
          title={w?.title || "Chi tiết công việc"}
          close={close}
        >
          <State {...detail}>
            {w && (
              <>
                <div className="toolbar work-status">
                  <Badge
                    value={
                      w.kind === "MAINTENANCE" && w.status === "COMPLETED"
                        ? "Chờ nghiệm thu"
                        : housekeepingStaff && ["ASSIGNED", "PENDING_ACCEPTANCE"].includes(w.status) ? "Chờ nhận" : detail.data?.inspectionVariant ? `Chờ kiểm tra - ${detail.data.inspectionVariant.toLowerCase()}` : detail.data?.hasOpenRoomIssues&&["PENDING_INSPECTION","PENDING_REINSPECTION","REWORK_REQUIRED"].includes(w.status)?"Kiểm tra phòng gặp sự cố":w.status
                    }
                  />
                  {!paperHousekeeping && <span>#{w.workId}</span>}
                </div>
                <p>{w.description}</p>
                {(personal ||
                  coordinator ||
                  housekeeping ||
                  readScope !== undefined) && (
                  <>
                    <p>
                      {detail.data?.context?.branch} ·{" "}
                      {detail.data?.context?.floor}{" "}
                      {detail.data?.context?.room &&
                        `· Phòng ${detail.data.context.room}`}
                    </p>
                    <p>
                      {!paperHousekeeping && <>Số công việc: {w.workNo} · Tiến độ: {w.progressPercent}% · </>}
                      Hạn: {timestamp(w.dueAt)}
                    </p>
                    {(detail.data?.assignmentDetails || []).map((a: Row) => (
                      <div className="assignment-summary" key={a.assignmentId}>
                        <p>Người giao: {a.assignedBy} · {timestamp(a.assignedAt)}</p>
                        <p>Người nhận: {a.assignee || "—"} · {a.acceptedAt ? `Giờ nhận: ${timestamp(a.acceptedAt)}` : "Chưa nhận việc"}</p>
                        {!paperHousekeeping && <p>{label(a.assignmentStatus)}</p>}
                      </div>
                    ))}
                  </>
                )}
                <section className="workflow-next" aria-label="Bước tiếp theo">
                  {detail.data?.hasOpenRoomIssues&&["CLEANING","REWORK"].includes(w.kind)&&w.status==="IN_PROGRESS"&&<p>Sự cố đã phát hiện. Bạn có thể hoàn thành phần vệ sinh để gửi giám sát kiểm tra.</p>}
                  {detail.data?.roomIssues?.length>0&&<CleaningIncidents work={w} issues={detail.data.roomIssues} canConfirm={detail.data.capabilities?.canConfirmRoomIssue===true} saved={()=>bump(n=>n+1)}/>}
                  {roomIssue&&<a className="button-link" href={`#room-issues?id=${w.workId}`}>Mở quy trình khắc phục sự cố phòng</a>}
                  {w.kind === "INSPECTION" && !terminal.includes(w.status) && can("INSPECTION_PERFORM") && <a className="button-link" href={`#work?kind=CLEANING&id=${w.parentWorkId}`}>Mở công việc để ghi kết quả kiểm tra</a>}
                  {w.kind === "CLEANING" &&
                    ["PENDING_INSPECTION", "REWORK_REQUIRED"].includes(
                      w.status,
                    ) && (
                      <>
                        <h3>
                          {detail.data?.hasOpenRoomIssues?"Kiểm tra phòng gặp sự cố":w.status === "REWORK_REQUIRED"
                            ? "Dọn lại và kiểm tra lại"
                            : "Kiểm tra phòng"}
                        </h3>
                        <p>
                          {personal
                            ? "Công việc đã được gửi kiểm tra. Xem kết quả và việc làm lại được giao bên dưới."
                            : "Kiểm tra thực tế theo checklist giấy và ghi kết quả Đạt/Không đạt."}
                        </p>
                        <div className="inspection-actions">{related.data && detail.data?.capabilities?.canRecordPaperInspection === true && <PaperInspection work={w} related={fields(related.data)} reviews={detail.data?.reviews || []} saved={() => bump(n => n + 1)} />}{reportIncident}</div>
                        {[
                          ["REWORK", "Tạo việc dọn lại", "CLEANING_ASSIGN"],
                        ]
                          .filter(
                            ([k, , p]) =>
                              can(p) &&
                              !(
                                k === "REWORK" && w.status !== "REWORK_REQUIRED"
                              ) &&
                              !(
                                k === "INSPECTION" &&
                                w.status === "REWORK_REQUIRED" &&
                                !fields(related.data).some(
                                  (r) =>
                                    r.kind === "REWORK" &&
                                    r.status === "PENDING_REINSPECTION" &&
                                    !fields(related.data).some(
                                      (i) =>
                                        i.kind === "INSPECTION" &&
                                        i.status === "FAILED" &&
                                        i.workId > r.workId &&
                                        i.verifiedAt &&
                                        r.completedAt &&
                                        new Date(i.verifiedAt) >=
                                          new Date(r.completedAt),
                                    ),
                                )
                              ) &&
                              !(
                                k === "INSPECTION" &&
                                fields(related.data).some(
                                  (r) =>
                                    r.kind === "REWORK" &&
                                    !terminal.includes(r.status) &&
                                    r.status !== "PENDING_REINSPECTION",
                                )
                              ) &&
                              !fields(related.data).some(
                                (r) =>
                                  r.kind === k &&
                                  !terminal.includes(r.status) &&
                                  !(
                                    r.kind === "REWORK" &&
                                    r.status === "PENDING_REINSPECTION" &&
                                    fields(related.data).some(
                                      (i) =>
                                        i.kind === "INSPECTION" &&
                                        i.status === "FAILED" &&
                                        i.workId > r.workId &&
                                        i.verifiedAt &&
                                        r.completedAt &&
                                        new Date(i.verifiedAt) >=
                                          new Date(r.completedAt),
                                    )
                                  ),
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
                    w.kind === "INCIDENT" && !readOnlyWork && !maintenanceSupervisor &&
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
                            if (
                              housekeeping ||
                              (coordinator && r.kind === "MAINTENANCE")
                            ) {
                              location.hash = `work?kind=${r.kind}&id=${r.workId}`;
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
                  {w.kind !== "CLEANING" || !["PENDING_INSPECTION", "REWORK_REQUIRED"].includes(w.status) ? reportIncident : null}
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
                {detail.data?.capabilities?.canReconcileCleaningSupplies === true && <CleaningSupplyReconciliation key={w.workId} work={w} saved={() => bump(n => n + 1)} />}
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
                            durationMinutes: command === "assign" && ["CLEANING", "REWORK"].includes(kind) ? Number(f.get("durationMinutes")) : undefined,
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
                      {command === "assign" && ["CLEANING", "REWORK"].includes(kind) && <CleaningDuration />}
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
                      {!(paperHousekeeping && ["accept", "start", "complete", "assign"].includes(command)) && <label>
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
                      </label>}
                      <ConfirmationActions pending={pending} cancel={()=>setCommand("")}/>
                    </form>
                    {modalActions && error && (
                      <AlertPopup>
                        {error}
                      </AlertPopup>
                    )}
                  </WorkActionModal>
                )}
                {error && !(modalActions && command) && (
                  <AlertPopup>
                    {error}
                  </AlertPopup>
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
                {!paperHousekeeping && <><h3>Checklist</h3>
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
                </>}
                {!paperHousekeeping && !housekeepingStaff && <><h3>Minh chứng</h3>
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
                {!readOnlyWork && !terminal.includes(w.status) &&
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
                </>}
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
                      can("DEFECT_RESOLVE") &&
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
                {(personal || housekeeping) &&
                  (detail.data?.reviews || []).map((review: Row) => (
                    <section className="panel" key={review.workId}>
                      <h3>Kết quả kiểm tra · {timestamp(review.verifiedAt)}</h3>
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
  if (!["MAINTENANCE", "CLEANING", "REWORK", "INSPECTION"].includes(kind)) return rows;
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
    INSPECTION_PASSED: "Kiểm tra đạt",
    INSPECTION_FAILED: "Kiểm tra không đạt",
    PAPER_INSPECTION_RECORDED: "Ghi nhận kiểm tra theo checklist giấy",
    PASS: "Kiểm tra đạt",
    FAIL: "Kiểm tra không đạt",
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
    <Dialog className="confirmation-popup" title={title} close={close}>
      {children}
    </Dialog>
  ) : (
    <>{children}</>
  );
}

function CleaningDuration() {
  return <fieldset className="duration-options"><legend>Thời gian hoàn thành tính từ lúc giao việc</legend>
    <div>{[20,25,30].map(minutes => <label key={minutes}><input type="radio" name="durationMinutes" value={minutes} defaultChecked={minutes===30} required/>{minutes} phút</label>)}</div>
  </fieldset>;
}
