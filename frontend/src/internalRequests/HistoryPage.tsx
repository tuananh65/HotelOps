import { useNavigationAccess } from "../shared-host/useNavigationAccess";
import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import { SearchInput, searchSuggestions } from "../components/SearchInput";
import { WorkflowAvailability } from "./WorkflowAvailability";
import { CollapsibleFilterPanel } from "../components/workspace-interactions";
import { StatusBadge } from "../components/design-system";
import { displayCode } from "../i18n/vi";
import { staffDate } from "./staffDate";
import { LoadingIndicator } from "../components/LoadingIndicator";
import "./staff-workspace.css";
import "./request-history.css";
import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { InternalRequestDecisionPanel } from "./DecisionPanel";
import { InternalRequestProgressPanel } from "./ProgressPanel";
import { InternalRequestTaskLinksPanel } from "./TaskLinksPanel";
import { InternalRequestCleaningLinksPanel } from "./CleaningLinksPanel";
import { InternalRequestIncidentLinksPanel } from "./IncidentLinksPanel";
import { InternalRequestMaterialLinksPanel } from "./MaterialLinksPanel";
import { InternalRequestWithdrawalPanel } from "./WithdrawalPanel";
import { InternalRequestIntakeLink } from "./IntakePage";
import { InternalRequestCompletionPanel } from "./CompletionPanel";
import { InternalRequestReopenPanel } from "./ReopenPanel";
import { InternalRequestEscalationPanel } from "./EscalationPanel";
import { InternalRequestHandoverPanel } from "./HandoverPanel";
import {
  decodeHistory,
  decodeTimeline,
  decodeHistoryOptions,
  type HistoryOptions,
  HistoryFailure,
  readHistory,
  type HistoryPage as Page,
  type Timeline,
} from "./historyApi";

export function InternalRequestAccessLink() {
  const allows = useNavigationAccess();
  return allows("/internal-requests") ? (
    <Link to="/internal-requests">Yêu cầu nội bộ</Link>
  ) : null;
}

export function InternalRequestHistoryPage() {
  const { state, actions } = useAuth();
  const allowsNavigation = useNavigationAccess();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [search, setSearch] = useState("");
  const [overdue, setOverdue] = useState(false);
  const [atRisk, setAtRisk] = useState(false);
  const [filters, setFilters] = useState({
    status: "",
    direction: "",
    requestTypeId: "",
    priorityId: "",
    receivingDepartmentId: "",
  });
  const [page, setPage] = useState(1);
  const [searchParams, setSearchParams] = useSearchParams();
  const requestIds = searchParams.getAll("requestId");
  const selected =
    requestIds.length === 1 &&
    /^[1-9][0-9]*$/.test(requestIds[0]) &&
    Number.isSafeInteger(Number(requestIds[0]))
      ? Number(requestIds[0])
      : null;
  const setSelected = (id: number | null) =>
    setSearchParams(id === null ? {} : { requestId: String(id) });
  const [completionNotice, setCompletionNotice] = useState<number | null>(null);
  const [escalationNotice, setEscalationNotice] = useState<number | null>(null);
  const [workNotice, setWorkNotice] = useState<{
    owner: typeof owner;
    id: number;
    message: string;
    receiptId?: number;
  } | null>(null);
  useEffect(() => setWorkNotice(null), [selected, owner]);
  useEffect(() => setEscalationNotice(null), [selected, owner]);
  useEffect(() => setCompletionNotice(null), [selected, owner]);
  const [eventPage, setEventPage] = useState(1);
  const [assignmentPage, setAssignmentPage] = useState(1);
  const [revision, setRevision] = useState(0);
  function refreshWork(id: number, message: string, receiptId?: number) {
    setWorkNotice({ owner, id, message, receiptId });
    setRevision((v) => v + 1);
  }
  const query = new URLSearchParams({
    page: String(page),
    pageSize: "25",
    ...(search ? { search } : {}),
    ...(overdue ? { overdue: "true" } : {}),
    ...(atRisk ? { atRisk: "true" } : {}),
    ...Object.fromEntries(
      Object.entries(filters).filter(([, value]) => value !== ""),
    ),
  });
  const path =
    selected === null
      ? `?${query}`
      : `/${selected}/timeline?eventPage=${eventPage}&assignmentPage=${assignmentPage}&pageSize=25`;
  const key = `${path}:${revision}`;
  const [result, setResult] = useState<{
    owner: typeof owner;
    key: string;
    list?: Page;
    timeline?: Timeline;
    options?: HistoryOptions;
    error?: string;
  } | null>(null);
  useEffect(() => {
    setResult(null);
    if (!owner) return;
    const controller = new AbortController();
    void Promise.all([
      readHistory(path, controller.signal),
      selected === null
        ? readHistory("/options", controller.signal).then(decodeHistoryOptions)
        : Promise.resolve(undefined),
    ])
      .then(([value, options]) => {
        const data =
          selected === null
            ? { list: decodeHistory(value) }
            : { timeline: decodeTimeline(value, selected) };
        if (!controller.signal.aborted)
          setResult({ owner, key, ...data, options });
      })
      .catch((e: unknown) => {
        if (controller.signal.aborted) return;
        setResult({
          owner,
          key,
          error:
            e instanceof HistoryFailure && e.status === 403
              ? "Bạn không có quyền xem lịch sử yêu cầu nội bộ."
              : e instanceof HistoryFailure && e.status === 404
                ? "Yêu cầu chưa khả dụng hoặc ngoài quyền truy cập hiện tại."
                : "Chưa tải được lịch sử. Thử tải lại dữ liệu hiện tại.",
        });
        if (e instanceof HistoryFailure && e.status === 401)
          actions.invalidateSession(owner);
      });
    return () => controller.abort();
  }, [owner, actions, path, key, selected]);
  const visible =
    owner && result?.owner === owner && result.key === key ? result : null;
  if (!owner) return null;
  return (
    <main className="staff-workspace ir-workspace">
      <header className="staff-heading">
        <div>
          <h1>Theo dõi yêu cầu nội bộ</h1>
          <p className="staff-subtitle">
            Xem yêu cầu đã gửi và công việc cần xử lý. Thời gian theo giờ Việt
            Nam (UTC+7).
          </p>
        </div>
        <InternalRequestIntakeLink />
      </header>
      <div className="staff-toolbar">
        <Link to="/home">Trang chủ</Link>
        <button onClick={() => setRevision((v) => v + 1)}>
          Tải lại lịch sử
        </button>
      </div>
      {selected === null ? (
        <>
          <div className="ds-quick-search">
            <label>
              Tìm yêu cầu
              <SearchInput
                suggestions={searchSuggestions(visible?.list)}
                maxLength={200}
                value={search}
                onChange={(e) => {
                  setSearch(e.target.value);
                  setPage(1);
                }}
              />
            </label>
          </div>
          <CollapsibleFilterPanel
            activeCount={
              Object.values(filters).filter(Boolean).length +
              Number(overdue) +
              Number(atRisk)
            }
          >
            <section className="staff-filters" aria-label="Bộ lọc yêu cầu">
              {visible?.options && (
                <>
                  <label>
                    Trạng thái
                    <SearchableSelect
                      aria-label="Trạng thái"
                      value={filters.status}
                      onChange={(e) => {
                        setFilters((v) => ({ ...v, status: e.target.value }));
                        setPage(1);
                      }}
                    >
                      <option value="">Tất cả trạng thái</option>
                      {visible.options.statuses.map((value) => (
                        <option key={value} value={value}>
                          {displayCode(value)}
                        </option>
                      ))}
                    </SearchableSelect>
                  </label>
                  {(
                    [
                      ["requestTypeId", "Loại yêu cầu", visible.options.types],
                      ["priorityId", "Độ ưu tiên", visible.options.priorities],
                      [
                        "receivingDepartmentId",
                        "Bộ phận tiếp nhận",
                        visible.options.departments,
                      ],
                    ] as const
                  ).map(([field, label, values]) => (
                    <label key={field}>
                      {label}
                      <SearchableSelect
                        aria-label={label}
                        value={filters[field]}
                        onChange={(e) => {
                          setFilters((v) => ({
                            ...v,
                            [field]: e.target.value,
                          }));
                          setPage(1);
                        }}
                      >
                        <option value="">Tất cả</option>
                        {values.map((v) => (
                          <option key={v.id} value={v.id}>
                            {v.name}
                          </option>
                        ))}
                      </SearchableSelect>
                    </label>
                  ))}
                  <button
                    onClick={() => {
                      setFilters({
                        status: "",
                        direction: "",
                        requestTypeId: "",
                        priorityId: "",
                        receivingDepartmentId: "",
                      });
                      setSearch("");
                      setOverdue(false);
                      setAtRisk(false);
                      setPage(1);
                    }}
                  >
                    Xóa bộ lọc
                  </button>
                </>
              )}
              <label>
                Phạm vi yêu cầu
                <SearchableSelect
                  aria-label="Phạm vi yêu cầu"
                  value={filters.direction}
                  onChange={(e) => {
                    setFilters((current) => ({
                      ...current,
                      direction: e.target.value,
                    }));
                    setPage(1);
                  }}
                >
                  <option value="">Tất cả yêu cầu được phép xem</option>
                  <option value="SENT">Đã gửi</option>
                  <option value="RECEIVED">Đã nhận</option>
                </SearchableSelect>
              </label>
              <label className="staff-check">
                <input
                  type="checkbox"
                  checked={overdue}
                  onChange={(e) => {
                    setOverdue(e.target.checked);
                    if (e.target.checked) setAtRisk(false);
                    setPage(1);
                  }}
                />
                Chỉ hiện quá hạn
              </label>
              <label className="staff-check">
                <input
                  type="checkbox"
                  checked={atRisk}
                  onChange={(e) => {
                    setAtRisk(e.target.checked);
                    if (e.target.checked) setOverdue(false);
                    setPage(1);
                  }}
                />
                Chỉ hiện có nguy cơ trễ hạn
              </label>
            </section>
          </CollapsibleFilterPanel>
        </>
      ) : (
        <button onClick={() => setSelected(null)}>Quay lại yêu cầu</button>
      )}
      {!visible && <LoadingIndicator label="Đang tải yêu cầu" />}
      {workNotice?.owner === owner && workNotice.id === selected && (
        <p role="status">
          {userMessage(workNotice.message)}{" "}
          {workNotice.receiptId && (
            <Link
              to={`/internal-requests/${workNotice.id}/handovers/${workNotice.receiptId}`}
            >
              Mở biên nhận bàn giao của tôi
            </Link>
          )}
        </p>
      )}
      {visible?.error && (
        <p role="alert">
          {userMessage(visible.error)}{" "}
          <button onClick={() => setRevision((value) => value + 1)}>
            Tải lại lịch sử
          </button>
        </p>
      )}
      {visible?.list && (
        <section aria-label="Danh sách yêu cầu">
          <p>{visible.list.totalCount} yêu cầu</p>
          {visible.list.totalCount === 0 ? (
            <p role="status">
              Không có yêu cầu phù hợp với quyền và bộ lọc hiện tại. Đổi hoặc
              xóa bộ lọc để tìm lại.
            </p>
          ) : (
            <div
              className="staff-table-scroll"
              tabIndex={0}
              role="region"
              aria-label="Bảng yêu cầu nội bộ"
            >
              <table>
                <thead>
                  <tr>
                    <th>Yêu cầu</th>

                    <th>Trạng thái</th>

                    <th>Tiếp nhận / xử lý</th>

                    <th>Thời hạn</th>
                  </tr>
                </thead>
                <tbody>
                  {visible.list.items.map((row) => (
                    <tr key={row.id}>
                      <td>
                        <button
                          onClick={() => {
                            setSelected(row.id);
                            setEventPage(1);
                            setAssignmentPage(1);
                          }}
                        >
                          {row.number}
                        </button>
                        <strong className="ir-subject">{row.subject}</strong>
                        <small>
                          {row.branchName ?? "Chưa có tên chi nhánh"}
                        </small>
                      </td>
                      <td>
                        <StatusBadge>{displayCode(row.status)}</StatusBadge>
                      </td>
                      <td>
                        {row.receivingDepartmentName ??
                          visible.options?.departments.find(
                            (d) => d.id === row.receivingDepartmentId,
                          )?.name ??
                          "Chưa phân công"}
                        <small>
                          {row.currentAssigneeName ??
                            "Chưa phân công người xử lý"}
                        </small>
                      </td>
                      <td>
                        {staffDate(row.deadline)}
                        <small>
                          {row.timingStatus === "AT_RISK"
                            ? "Sắp đến hạn"
                            : row.timingStatus === "OVERDUE"
                              ? "Quá hạn"
                              : row.timingStatus === "ON_TIME"
                                ? "Trong hạn"
                                : row.timingStatus === "TERMINAL"
                                  ? "Đã hoàn tất"
                                  : "Chưa xác định tiến độ thời hạn"}
                        </small>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <nav className="staff-pagination" aria-label="Phân trang yêu cầu">
            <button disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
              Trang trước
            </button>
            <span> Trang {page} </span>
            <button
              disabled={page * 25 >= visible.list.totalCount}
              onClick={() => setPage((p) => p + 1)}
            >
              Trang sau
            </button>
          </nav>
        </section>
      )}
      {visible?.timeline && (
        <section aria-label="Dòng thời gian yêu cầu">
          <InternalRequestDecisionPanel
            id={visible.timeline.requestId}
            onChanged={(message) =>
              refreshWork(visible.timeline!.requestId, message)
            }
          />

          <WorkflowAvailability key={visible.timeline.requestId}>
            {visible.timeline.canReopen === true && (
              <InternalRequestReopenPanel
                id={visible.timeline.requestId}
                onReopened={() => {
                  setCompletionNotice(null);
                  refreshWork(
                    visible.timeline!.requestId,
                    "MSG08: Đã mở lại yêu cầu với người xử lý cuối cùng.",
                  );
                }}
              />
            )}
            {completionNotice === visible.timeline.requestId && (
              <p role="status">Đã đóng yêu cầu.</p>
            )}
            <InternalRequestCompletionPanel
              id={visible.timeline.requestId}
              onCompleted={() => {
                setCompletionNotice(visible.timeline?.requestId ?? null);
                setRevision((v) => v + 1);
              }}
            />
            <InternalRequestWithdrawalPanel
              id={visible.timeline.requestId}
              onWithdrawn={() => setRevision((v) => v + 1)}
            />
            <InternalRequestProgressPanel
              id={visible.timeline.requestId}
              onSaved={() =>
                refreshWork(
                  visible.timeline!.requestId,
                  "MSG08: Đã lưu tiến độ. Yêu cầu vẫn đang thực hiện.",
                )
              }
            />
            {escalationNotice === visible.timeline.requestId && (
              <p role="status">Đã chuyển cấp yêu cầu.</p>
            )}
            {visible.timeline.canEscalate === true && (
              <InternalRequestEscalationPanel
                id={visible.timeline.requestId}
                onSaved={() => {
                  setEscalationNotice(visible.timeline?.requestId ?? null);
                  setRevision((v) => v + 1);
                }}
              />
            )}
            <InternalRequestHandoverPanel
              id={visible.timeline.requestId}
              onSaved={(handoverState, receiptId) =>
                refreshWork(
                  visible.timeline!.requestId,
                  `MSG08: Bàn giao ${handoverState}.`,
                  receiptId,
                )
              }
            />
          </WorkflowAvailability>
          <details className="ir-card">
            <summary>Công việc liên quan</summary>
            <InternalRequestTaskLinksPanel
              key={visible.timeline.requestId}
              id={visible.timeline.requestId}
              onLinked={() =>
                refreshWork(
                  visible.timeline!.requestId,
                  "MSG08: Đã liên kết công việc.",
                )
              }
            />
            <InternalRequestCleaningLinksPanel
              key={`Vệ sinh:${visible.timeline.requestId}`}
              id={visible.timeline.requestId}
              onLinked={() =>
                refreshWork(
                  visible.timeline!.requestId,
                  "MSG08: Đã liên kết công việc vệ sinh.",
                )
              }
            />
            <InternalRequestIncidentLinksPanel
              key={`Sự cố:${visible.timeline.requestId}`}
              id={visible.timeline.requestId}
              onLinked={() =>
                refreshWork(
                  visible.timeline!.requestId,
                  "MSG08: Đã liên kết sự cố.",
                )
              }
            />
            {allowsNavigation("/operations/material-requests") && (
              <InternalRequestMaterialLinksPanel
                key={`Vật tư:${visible.timeline.requestId}`}
                id={visible.timeline.requestId}
                onLinked={() =>
                  refreshWork(
                    visible.timeline!.requestId,
                    "MSG08: Đã liên kết yêu cầu vật tư.",
                  )
                }
              />
            )}
          </details>
          <section className="ir-card">
            <h3>Lịch sử xử lý</h3>
            {visible.timeline.events.length === 0 && (
              <p>Không có sự kiện trên trang này.</p>
            )}
            <ol className="ir-timeline">
              {visible.timeline.events.map((e) => (
                <li key={e.id}>
                  {staffDate(e.occurredAt)} — {requestEventLabel(e.type)}:{" "}
                  {e.oldStatus ? displayCode(e.oldStatus) : "—"} →{" "}
                  {e.newStatus ? displayCode(e.newStatus) : "—"}; Người thực
                  hiện: {e.actorName ?? "Chưa có tên"}
                  {e.note && <p>{e.note}</p>}
                </li>
              ))}
            </ol>
            {visible.timeline.eventTotalCount > 25 && (
              <nav aria-label="Phân trang lịch sử">
                <button
                  disabled={eventPage <= 1}
                  onClick={() => setEventPage((p) => p - 1)}
                >
                  Sự kiện trước
                </button>
                <span> Trang {eventPage} </span>
                <button
                  disabled={eventPage * 25 >= visible.timeline.eventTotalCount}
                  onClick={() => setEventPage((p) => p + 1)}
                >
                  Sự kiện tiếp theo
                </button>
              </nav>
            )}
          </section>
          <details className="ir-card">
            <summary>Lịch sử phân công</summary>
            {visible.timeline.assignments.length === 0 && (
              <p>Không có phân công trên trang này.</p>
            )}
            <ol>
              {visible.timeline.assignments.map((a) => (
                <li key={a.id}>
                  Người xử lý: {a.assigneeName ?? "Chưa có tên"}; người phân
                  công: {a.assignedByName ?? "Chưa có tên"};{" "}
                  {staffDate(a.assignedAt)} →{" "}
                  {a.endedAt ? staffDate(a.endedAt) : "Đang mở"};{" "}
                  {displayCode(a.status)}
                  {a.note && <p>{a.note}</p>}
                </li>
              ))}
            </ol>
            {visible.timeline.assignmentTotalCount > 25 && (
              <nav aria-label="Phân trang phân công">
                <button
                  disabled={assignmentPage <= 1}
                  onClick={() => setAssignmentPage((p) => p - 1)}
                >
                  Phân công trước
                </button>
                <span> Trang {assignmentPage} </span>
                <button
                  disabled={
                    assignmentPage * 25 >= visible.timeline.assignmentTotalCount
                  }
                  onClick={() => setAssignmentPage((p) => p + 1)}
                >
                  Phân công tiếp theo
                </button>
              </nav>
            )}
          </details>
          <section className="ir-card">
            <h3>Phản hồi</h3>
            {visible.timeline.feedback ? (
              <p>
                Đánh giá: {visible.timeline.feedback.rating}/5 —{" "}
                {visible.timeline.feedback.comment}; Đã nộp{" "}
                {staffDate(visible.timeline.feedback.submittedAt)}
              </p>
            ) : (
              <p>Chưa có phản hồi đã ghi nhận.</p>
            )}
          </section>
        </section>
      )}
    </main>
  );
}

function requestEventLabel(code: string) {
  const labels: Record<string, string> = {
    STATUS_CHANGE: "Cập nhật trạng thái",
    CREATED: "Tạo yêu cầu",
    ASSIGNED: "Phân công xử lý",
    REASSIGNED: "Đổi người xử lý",
    PROGRESS_UPDATED: "Cập nhật tiến độ",
    WITHDRAWN: "Rút yêu cầu",
    ESCALATED: "Chuyển cấp",
    CLOSED: "Đóng yêu cầu",
    REOPENED: "Mở lại yêu cầu",
  };
  return labels[code] ?? displayCode(code);
}
