import { CollapsibleFilterPanel } from "../../components/workspace-interactions";
import {
  useRoomWorkspace,
  RoomBranchField,
  NamedFilter,
  roomLabel,
  hotelTime,
  hotelDate,
} from "./RoomWorkspace";
import { getJson } from "../../api/dev1/client";
import { LoadingIndicator } from "../../components/LoadingIndicator";
import { displayCode } from "../../i18n/vi";
import OwnHousekeepingList from "./OwnHousekeepingList";
import { useEffect, useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import {
  listCleaning,
  monitorError,
  type MonitorPage,
} from "../../api/dev1/cleaningMonitor";
import { useAuth } from "../../auth/AuthProvider";
export default function CleaningTaskListScreen(props: { kindOnly?: "rework" }) {
  return <CleaningMonitorScreen {...props} />;
}
function CleaningMonitorScreen({ kindOnly }: { kindOnly?: "rework" } = {}) {
  const { state } = useAuth();
  const scope = useRoomWorkspace("cleaning");
  const owner =
    state.status === "AUTHENTICATED" && !state.pending
      ? state.authorityGeneration
      : null;
  const [params, setParams] = useSearchParams();
  const monitorMode = scope.branch?.canMonitor && params.get("view") !== "mine";
  const effectiveParams = new URLSearchParams(params);
  if (kindOnly) effectiveParams.set("kind", kindOnly);
  const key = effectiveParams.toString(),
    branch = params.get("branchId") || "";
  type Options = {
    floors: { id: number; name: string }[];
    assignees: { id: number; name: string }[];
    priorities: { id: number; name: string }[];
    statuses: string[];
  };
  const [optionResult, setOptionResult] = useState<{
    branch: string;
    owner: typeof owner;
    data?: Options;
    error?: string;
  } | null>(null);
  const [optionRetry, setOptionRetry] = useState(0);
  useEffect(() => {
    const abort = new AbortController();
    setOptionResult(null);
    if (branch && monitorMode && owner)
      void getJson(`/branches/${branch}/cleaning-monitor/options`, abort.signal)
        .then((v) => {
          const d = v as Options;
          if (
            !d ||
            ![d.floors, d.assignees, d.priorities].every(
              (a) =>
                Array.isArray(a) &&
                a.every(
                  (i) =>
                    Number.isSafeInteger(i.id) &&
                    i.id > 0 &&
                    typeof i.name === "string",
                ),
            ) ||
            !Array.isArray(d.statuses) ||
            d.statuses.some((x) => typeof x !== "string")
          )
            throw new Error();
          if (!abort.signal.aborted)
            setOptionResult({ branch, owner, data: d });
        })
        .catch(() => {
          if (!abort.signal.aborted)
            setOptionResult({
              branch,
              owner,
              error: "Chưa tải được bộ lọc công việc.",
            });
        });
    return () => abort.abort();
  }, [branch, monitorMode, owner, optionRetry]);
  const options =
    optionResult?.branch === branch && optionResult?.owner === owner
      ? optionResult.data
      : undefined;
  const [result, setResult] = useState<{
    key: string;
    owner: typeof owner;
    data: MonitorPage;
  } | null>(null);
  const [error, setError] = useState<{ key: string; message: string } | null>(
    null,
  );
  const [retry, setRetry] = useState(0),
    [loading, setLoading] = useState(false);
  useEffect(() => {
    const abort = new AbortController();
    setResult(null);
    setError(null);
    setLoading(Boolean(branch));
    if (branch && owner !== null && monitorMode)
      listCleaning(branch, new URLSearchParams(key), abort.signal)
        .then((data) => {
          if (!abort.signal.aborted) setResult({ key, owner, data });
        })
        .catch((e) => {
          if (!abort.signal.aborted)
            setError({ key, message: monitorError(e) });
        })
        .finally(() => {
          if (!abort.signal.aborted) setLoading(false);
        });
    return () => abort.abort();
  }, [branch, key, retry, owner, monitorMode]);
  function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const next = new URLSearchParams();
    new FormData(e.currentTarget).forEach((v, k) => {
      if (String(v).trim()) next.set(k, String(v).trim());
    });
    next.set("page", "1");
    setParams(next);
    setRetry((v) => v + 1);
  }
  const data =
    scope.branch?.canMonitor &&
    owner !== null &&
    result?.owner === owner &&
    result.key === key
      ? result.data
      : null;
  function changePage(page: number) {
    const next = new URLSearchParams(params);
    next.set("page", String(page));
    setParams(next);
  }
  if (
    scope.branch &&
    (!scope.branch.canMonitor || params.get("view") === "mine")
  )
    return (
      <div className="room-workspace">
        <div className="page-header">
          <div>
            <span className="eyebrow">Buồng phòng</span>
            <h1>
              {kindOnly
                ? "Công việc làm lại của tôi"
                : "Công việc buồng phòng của tôi"}
            </h1>
          </div>
        </div>
        <RoomBranchField scope={scope} />
        {scope.branch.canMonitor && (
          <div className="room-actions">
            <Link to={`/cleaning/tasks?branchId=${branch}`}>
              Theo dõi công việc trong chi nhánh
            </Link>
          </div>
        )}
        <OwnHousekeepingList scope={scope} kind={kindOnly} />
      </div>
    );
  return (
    <div className="room-workspace">
      <div className="page-header">
        <div>
          <span className="eyebrow">Buồng phòng</span>
          <h1>{kindOnly ? "Công việc làm lại" : "Công việc buồng phòng"}</h1>
          <p>
            {kindOnly
              ? "Theo dõi các công việc cần khắc phục sau kiểm tra phòng."
              : "Theo dõi công việc, nhân viên thực hiện và tiến độ vệ sinh phòng."}
          </p>
        </div>
      </div>
      <RoomBranchField scope={scope} />
      <div className="room-actions">
        {!kindOnly && scope.branch?.canReadOwn && (
          <Link
            to={
              "/cleaning/tasks?view=mine&branchId=" + encodeURIComponent(branch)
            }
          >
            Công việc vệ sinh được giao
          </Link>
        )}
        {!kindOnly && scope.branch?.canCreate && (
          <Link
            to={`/cleaning/tasks/new?branchId=${encodeURIComponent(branch)}`}
          >
            Tạo công việc vệ sinh
          </Link>
        )}
      </div>
      <form
        key={key + (options ? ":ready" : "")}
        className="filter-bar"
        onSubmit={submit}
      >
        <div className="ds-quick-search">
          <label>
            Tìm công việc
            <input
              name="search"
              type="search"
              placeholder="Số phòng hoặc mã công việc"
              maxLength={150}
              defaultValue={params.get("search") || ""}
            />
          </label>
          <button type="submit">Tìm kiếm</button>
        </div>
        <CollapsibleFilterPanel
          resetNative
          activeCount={
            [
              "fromDate",
              "toDate",
              "floorId",
              "assigneeId",
              "priorityId",
              "status",
              "overdue",
            ].filter((k) => !!params.get(k)).length
          }
        >
          <input type="hidden" name="branchId" value={scope.branch?.id ?? ""} />
          <label>
            Từ ngày
            <input
              name="fromDate"
              type="date"
              defaultValue={params.get("fromDate") || ""}
            />
          </label>
          <label>
            Đến ngày
            <input
              name="toDate"
              type="date"
              defaultValue={params.get("toDate") || ""}
            />
          </label>

          <NamedFilter
            name="floorId"
            label="Tầng"
            value={params.get("floorId") || ""}
            items={options?.floors ?? []}
          />
          <NamedFilter
            name="assigneeId"
            label="Nhân viên"
            value={params.get("assigneeId") || ""}
            items={options?.assignees ?? []}
          />
          <NamedFilter
            name="priorityId"
            label="Độ ưu tiên"
            value={params.get("priorityId") || ""}
            items={options?.priorities ?? []}
          />
          <label>
            Trạng thái
            <select name="status" defaultValue={params.get("status") || ""}>
              <option value="">Tất cả</option>
              {options?.statuses.map((s) => (
                <option key={s} value={s}>
                  {roomLabel(s)}
                </option>
              ))}
            </select>
          </label>
          <label>
            Loại
            <select
              name="kind"
              value={kindOnly || undefined}
              defaultValue={kindOnly ? undefined : params.get("kind") || ""}
              disabled={!!kindOnly}
            >
              <option value="">Tất cả</option>
              <option value="cleaning">Vệ sinh</option>
              <option value="rework">Làm lại</option>
            </select>
            {kindOnly && <input type="hidden" name="kind" value={kindOnly} />}
          </label>
          <label>
            Quá hạn
            <select name="overdue" defaultValue={params.get("overdue") || ""}>
              <option value="">Tất cả</option>
              <option value="true">Có</option>
              <option value="false">Không</option>
            </select>
          </label>
          <label>
            Số dòng
            <select
              name="pageSize"
              defaultValue={params.get("pageSize") || "20"}
            >
              {[10, 20, 50, 100].map((n) => (
                <option key={n} value={n}>
                  {displayCode(n)}
                </option>
              ))}
            </select>
          </label>
          <button className="primary" disabled={!scope.branch?.canMonitor}>
            Lọc công việc
          </button>
        </CollapsibleFilterPanel>
      </form>
      {optionResult?.branch === branch &&
        optionResult?.owner === owner &&
        optionResult.error && (
          <p role="alert">
            {optionResult.error}{" "}
            <button onClick={() => setOptionRetry((v) => v + 1)}>
              Tải lại bộ lọc
            </button>
          </p>
        )}
      {scope.branch && !scope.branch.canMonitor && (
        <p className="room-empty">
          Bạn có thể xem{" "}
          <Link to={`/cleaning/tasks?view=mine&branchId=${branch}`}>
            công việc được giao cho mình
          </Link>
          .
        </p>
      )}
      {loading && scope.branch?.canMonitor && (
        <LoadingIndicator label="Đang tải công việc" />
      )}
      {error?.key === key && (
        <p role="alert">
          {error.message}{" "}
          <button onClick={() => setRetry((v) => v + 1)}>Thử lại</button>
        </p>
      )}
      <p className="room-note">
        Mặc định hiển thị công việc hôm nay. Chọn khoảng ngày để xem lịch sử.
        Thời gian theo giờ Việt Nam (UTC+7).
      </p>
      {data && (
        <>
          <p>
            {data.total} công việc · {hotelDate(data.fromDate)} –{" "}
            {hotelDate(data.toDate)}
          </p>
          {data.items.length === 0 ? (
            <p>
              Không có công việc phù hợp. Bạn có thể đổi khoảng ngày hoặc xóa bộ
              lọc.
            </p>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    {[
                      "Công việc",
                      "Phòng",
                      "Loại",
                      "Trạng thái",
                      "Nhân viên",
                      "Ưu tiên",
                      "Thời hạn",
                      "Quá hạn",
                      "Tiến độ",
                      "Chu kỳ hiện tại",
                      "Trở ngại",
                      "Trạng thái phòng",
                    ].map((s) => (
                      <th key={displayCode(s)}>{displayCode(s)}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((r) => (
                    <tr key={`${roomLabel(r.kind)}:${r.id}`}>
                      <td>
                        <Link
                          to={`/cleaning/tasks/${r.id}?branchId=${branch}&monitor=1&kind=${roomLabel(r.kind)}`}
                        >
                          {r.taskNumber}
                        </Link>
                      </td>
                      <td>{r.roomNumber}</td>
                      <td>{roomLabel(r.kind)}</td>
                      <td>{roomLabel(r.workflowState ?? r.recordedStatus)}</td>
                      <td>{r.assigneeName ?? "Chưa phân công"}</td>
                      <td>{roomLabel(r.priorityCode)}</td>
                      <td>{hotelTime(r.dueAtUtc)}</td>
                      <td>
                        {r.isOverdue === null
                          ? "Chưa xác định"
                          : r.isOverdue
                            ? "Có"
                            : "Không"}
                      </td>
                      <td>
                        {r.progressPercent === null
                          ? "—"
                          : `${r.progressPercent}%`}
                      </td>
                      <td>
                        {r.isCurrentCycle == null
                          ? "Chưa xác định"
                          : r.isCurrentCycle
                            ? "Có"
                            : "Không"}
                      </td>
                      <td>
                        {r.hasUnresolvedBlocker == null
                          ? "Chưa xác định"
                          : r.hasUnresolvedBlocker
                            ? "Chưa xử lý"
                            : "Không"}{" "}
                        {r.blockerCode}
                      </td>
                      <td>{roomLabel(r.effectiveReadiness ?? "UNKNOWN")}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <div className="room-pagination">
            <button
              disabled={data.page <= 1}
              onClick={() => changePage(data.page - 1)}
            >
              Trang trước
            </button>
            <span>Trang {data.page}</span>
            <button
              disabled={data.page * data.pageSize >= data.total}
              onClick={() => changePage(data.page + 1)}
            >
              Trang sau
            </button>
          </div>
        </>
      )}
    </div>
  );
}
