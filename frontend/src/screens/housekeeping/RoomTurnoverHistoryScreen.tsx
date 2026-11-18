import { CollapsibleFilterPanel } from "../../components/workspace-interactions";
import {
  useRoomWorkspace,
  useRoomChoices,
  RoomBranchField,
  RoomNumberField,
  roomLabel,
  hotelTime,
} from "./RoomWorkspace";
import { LoadingIndicator } from "../../components/LoadingIndicator";
import { displayCode } from "../../i18n/vi";
import { useEffect, useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import {
  listTurnovers,
  readTurnover,
  turnoverError,
  type TurnoverPage,
  type TurnoverDetail,
  type Coverage,
} from "../../api/dev1/turnoverHistory";
function CoverageNote({ value }: { value: Coverage }) {
  return (
    <p className="room-note">
      Lịch sử vệ sinh và kiểm tra phòng được hiển thị trong phạm vi bạn được
      phép xem. Kết quả kiểm tra trước đây không thay thế trạng thái phòng hiện
      tại.
      {value.inspection === "SOURCE_READ_DENIED" &&
        " Bạn chưa có quyền xem chi tiết kiểm tra."}
    </p>
  );
}
export default function RoomTurnoverHistoryScreen() {
  const scope = useRoomWorkspace("rooms");
  const choices = useRoomChoices(scope);
  const [params, setParams] = useSearchParams(),
    [retry, setRetry] = useState(0);
  const key = params.toString(),
    room = params.get("roomId") || params.get("room") || "",
    cycle = params.get("cycleId") || "";
  const [result, setResult] = useState<{
      key: string;
      owner: typeof scope.owner;
      page?: TurnoverPage;
      detail?: TurnoverDetail;
    } | null>(null),
    [error, setError] = useState(""),
    [loading, setLoading] = useState(false);
  useEffect(() => {
    const abort = new AbortController();
    setResult(null);
    setError("");
    setLoading(!!room);
    if (room && scope.branch && scope.owner)
      (cycle
        ? readTurnover(
            room,
            cycle,
            new URLSearchParams(key),
            abort.signal,
          ).then((detail) => ({ key, owner: scope.owner, detail }))
        : listTurnovers(room, new URLSearchParams(key), abort.signal).then(
            (page) => ({ key, owner: scope.owner, page }),
          )
      )
        .then((d) => {
          if (!abort.signal.aborted) setResult(d);
        })
        .catch((e) => {
          if (!abort.signal.aborted) setError(turnoverError(e));
        })
        .finally(() => {
          if (!abort.signal.aborted) setLoading(false);
        });
    return () => abort.abort();
  }, [room, cycle, key, retry, scope.branch?.id, scope.owner]);
  function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const next = new URLSearchParams();
    new FormData(e.currentTarget).forEach((v, k) => {
      if (String(v).trim()) next.set(k, String(v).trim());
    });
    if (!next.get("roomId")) {
      setError("Chọn một số phòng trong danh sách gợi ý.");
      return;
    }
    const from = next.get("fromDate"),
      to = next.get("toDate");
    if (from && to && from > to) {
      setError("Ngày kết thúc phải từ ngày bắt đầu trở đi.");
      return;
    }
    next.delete("fromDate");
    next.delete("toDate");
    if (from)
      next.set("from", new Date(from + "T00:00:00+07:00").toISOString());
    if (to) next.set("to", new Date(to + "T23:59:59+07:00").toISOString());
    next.set("page", "1");
    setParams(next);
    setRetry((v) => v + 1);
  }
  function href(id?: number) {
    const next = new URLSearchParams(params);
    next.delete("room");
    next.set("roomId", room);
    next.delete("timelinePage");
    if (id) next.set("cycleId", String(id));
    else next.delete("cycleId");
    return `/rooms/turnover?${next}`;
  }
  function pageChange(name: string, value: number) {
    const next = new URLSearchParams(params);
    next.set(name, String(value));
    setParams(next);
  }
  const data =
    scope.branch &&
    scope.owner &&
    result?.owner === scope.owner &&
    result?.key === key
      ? result
      : null;
  const p = data?.page,
    d = data?.detail;
  return (
    <div className="room-workspace">
      <div className="page-header">
        <div>
          <span className="eyebrow">Buồng phòng</span>
          <h1>Lịch sử luân chuyển phòng</h1>
          <p>Chọn số phòng để xem các lần vệ sinh, kiểm tra và làm lại.</p>
        </div>
      </div>
      <RoomBranchField scope={scope} />
      {choices.error && (
        <p role="alert">
          {choices.error}{" "}
          <button onClick={choices.reload}>Tải lại danh sách phòng</button>
        </p>
      )}
      <form
        key={key + (choices.items ? ":ready" : "")}
        className="filter-bar"
        onSubmit={submit}
      >
        <div className="ds-quick-search">
          <RoomNumberField
            items={choices.items ?? []}
            selected={room}
            onSelect={(id) => {
              const next = new URLSearchParams(params);
              next.delete("cycleId");
              next.delete("timelinePage");
              next.delete("room");
              next.set("roomId", id);
              next.set("page", "1");
              setParams(next);
            }}
          />
          <button disabled={!scope.branch || !choices.items?.length}>
            Xem lịch sử
          </button>
        </div>
        <CollapsibleFilterPanel
          resetNative
          activeCount={
            ["from", "to", "result"].filter((k) => !!params.get(k)).length
          }
        >
          <input type="hidden" name="branchId" value={scope.branch?.id ?? ""} />

          <label>
            Từ ngày
            <input
              type="date"
              name="fromDate"
              defaultValue={localDate(params.get("from"))}
            />
          </label>
          <label>
            Đến ngày
            <input
              type="date"
              name="toDate"
              defaultValue={localDate(params.get("to"))}
            />
          </label>
          <label>
            Kết quả kiểm tra
            <select
              aria-label="Kết quả kiểm tra"
              name="result"
              defaultValue={params.get("result") || ""}
            >
              <option value="">Tất cả</option>
              <option value="PASS">Đạt</option>
              <option value="FAIL">Không đạt</option>
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
        </CollapsibleFilterPanel>
      </form>
      {!room && (
        <p className="room-empty">
          Nhập số phòng rồi chọn phòng trong danh sách gợi ý để xem lịch sử.
        </p>
      )}
      {scope.branch && !choices.items && !choices.error && (
        <LoadingIndicator label="Đang tải danh sách phòng" />
      )}
      {loading && scope.branch && <LoadingIndicator label="Đang tải lịch sử" />}
      {error && (
        <p role="alert">
          {error}{" "}
          <button onClick={() => setRetry((v) => v + 1)}>Thử lại</button>
        </p>
      )}
      {p && (
        <>
          <CoverageNote value={p.coverage} />
          <p>
            {p.total} lần vệ sinh · Phòng{" "}
            {choices.items?.find((r) => r.id === p.roomId)?.roomNumber ??
              p.items[0]?.roomNumber}
          </p>
          {!p.items.length ? (
            <p>Chưa có lịch sử phù hợp. Hãy thử chọn khoảng ngày khác.</p>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    {[
                      "Chu kỳ",
                      "Loại",
                      "Trạng thái",
                      "Hiện tại",
                      "Nhân viên",
                      "Bắt đầu",
                      "Kết thúc",
                      "Kết quả kiểm tra",
                      "Làm lại",
                      "Lý do hủy",
                    ].map((x) => (
                      <th key={x}>{x}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {p.items.map((c) => (
                    <tr key={c.id}>
                      <td>
                        <Link to={href(c.id)}>{c.number}</Link>
                      </td>
                      <td>{roomLabel(c.cleaningType)}</td>
                      <td>{roomLabel(c.workflowState ?? c.recordedStatus)}</td>
                      <td>
                        {c.isCurrentCycle === null
                          ? "Chưa xác định"
                          : c.isCurrentCycle
                            ? "Có"
                            : "Lịch sử"}
                      </td>
                      <td>
                        {c.assignees.map((a) => a.name).join(", ") ||
                          "Chưa phân công"}
                      </td>
                      <td>{hotelTime(c.startUtc)}</td>
                      <td>{hotelTime(c.endUtc)}</td>
                      <td>
                        {c.result
                          ? roomLabel(c.result)
                          : "Chưa có kết quả hoặc không có quyền xem"}
                      </td>
                      <td>{c.reworkCount ?? "Chưa xác định"}</td>
                      <td>{c.cancellationReason || "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <button
            disabled={p.filters.page <= 1}
            onClick={() => pageChange("Trang", p.filters.page - 1)}
          >
            Trang trước
          </button>{" "}
          Trang {p.filters.page}{" "}
          <button
            disabled={p.filters.page * p.filters.pageSize >= p.total}
            onClick={() => pageChange("Trang", p.filters.page + 1)}
          >
            Trang sau
          </button>
        </>
      )}
      {d && (
        <>
          <h2>
            {d.cycle.number} · Phòng {d.cycle.roomNumber}
          </h2>
          <Link to={href()}>Danh sách lần vệ sinh</Link>
          <CoverageNote value={d.coverage} />
          <p>
            {d.cycle.isCurrentCycle === null
              ? "Chưa xác định chu kỳ hiện tại"
              : d.cycle.isCurrentCycle
                ? "Chu kỳ hiện tại"
                : "Chu kỳ trước"}{" "}
            · {roomLabel(d.cycle.workflowState ?? d.cycle.recordedStatus)}
          </p>
          <h2>Dòng thời gian ({d.timelineTotal})</h2>
          {!d.timeline.length && <p>Không có sự kiện trên trang này.</p>}
          <ol>
            {d.timeline.map((e) => (
              <li key={`${displayCode(e.sourceType)}:${e.sourceId}:${e.event}`}>
                <time>{hotelTime(e.atUtc)}</time> · {displayCode(e.sourceType)}{" "}
                #{e.sourceId} · {e.event} · {displayCode(e.state)} · {e.note} ·
                Người thực hiện {e.actorId ?? "—"}
              </li>
            ))}
          </ol>
          <button
            disabled={d.timelinePage <= 1}
            onClick={() => pageChange("timelinePage", d.timelinePage - 1)}
          >
            Sự kiện trước
          </button>{" "}
          Trang {d.timelinePage}{" "}
          <button
            disabled={d.timelinePage * d.timelinePageSize >= d.timelineTotal}
            onClick={() => pageChange("timelinePage", d.timelinePage + 1)}
          >
            Dòng thời gian tiếp theo
          </button>
          <h2>Kiểm tra phòng</h2>
          <ul>
            {d.inspections.map((i) => (
              <li key={i.id}>
                Kiểm tra {i.id} · Phiên bản danh sách kiểm tra {i.versionNumber}{" "}
                (mã {i.versionId}) · {displayCode(i.recordedStatus)} ·{" "}
                {hotelTime(i.atUtc)} ·{" "}
                {i.result ? roomLabel(i.result) : "Chưa kết luận"}{" "}
                <Link
                  to={`/inspection-history/${i.id}?branchId=${d.cycle.branchId}`}
                >
                  Xem tiêu chí, lỗi và ảnh kiểm tra
                </Link>
              </li>
            ))}
          </ul>
          <h2>Công việc làm lại đã ghi nhận</h2>
          <ul>
            {d.reworks.map((w) => (
              <li key={w.id}>
                Làm lại {w.id} · Kiểm tra {w.inspectionId} ·{" "}
                {roomLabel(w.workflowState ?? w.recordedStatus)} ·{" "}
                {hotelTime(w.requestedAtUtc)} · {w.note}
              </li>
            ))}
          </ul>
          <h2>Bằng chứng</h2>
          <ul>
            {d.evidence.map((e) => (
              <li key={e.id}>
                <a href={e.downloadPath} target="_blank" rel="noreferrer">
                  {e.name}
                </a>{" "}
                · {e.entityType} #{e.entityId}
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}

function localDate(value: string | null) {
  return value && Number.isFinite(Date.parse(value))
    ? new Intl.DateTimeFormat("en-CA", {
        timeZone: "Asia/Ho_Chi_Minh",
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
      }).format(new Date(value))
    : "";
}
