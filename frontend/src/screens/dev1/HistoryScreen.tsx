import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Dev1Navigation } from "./Dev1Screen";
import { useDev1QueryController } from "../../controllers/dev1";
import {
  read,
  queryPath,
  id,
  download,
} from "../../controllers/dev1/transportController";
import type {
  HistoryInspectionDto,
  HistoryCycleDto,
  OperationalHistoryPage,
  HistoryInspectionDetailDto,
  HistoryCycleDetailDto,
} from "../../controllers/dev1/contracts";
import { useCallback } from "react";
import { obj } from "./model";
export default function HistoryScreen({ turnover }: { turnover: boolean }) {
  const [q, setQ] = useSearchParams();
  const b = Number(q.get("branchId"));
  const room = Number(q.get("roomId"));
  const page = Number(q.get("page") ?? 1);
  const [branch, setBranch] = useState(q.get("branchId") ?? "");
  const [roomInput, setRoomInput] = useState(q.get("roomId") ?? "");
  const [detail, setDetail] = useState<
    HistoryInspectionDetailDto | HistoryCycleDetailDto
  >();
  const [error, setError] = useState("");
  const valid =
    Number.isSafeInteger(b) &&
    b > 0 &&
    (!turnover || (Number.isSafeInteger(room) && room > 0));
  const key = `${b}:${room}:${page}:${turnover}`;
  useEffect(() => {
    setDetail(undefined);
    setError("");
  }, [key]);
  const fetchPage = useCallback(
    async (signal: AbortSignal) =>
      valid
        ? read<OperationalHistoryPage<HistoryInspectionDto | HistoryCycleDto>>(
            queryPath(
              turnover
                ? `/api/rooms/${id(room)}/turnover-history`
                : "/api/inspection-history",
              { branchId: b, page, pageSize: 20 },
            ),
            { signal },
          )
        : null,
    [key, valid],
  );
  const empty = useCallback(
    (
      v: OperationalHistoryPage<HistoryInspectionDto | HistoryCycleDto> | null,
    ) => !v || v.items.length === 0,
    [],
  );
  const state = useDev1QueryController(key, fetchPage, empty);
  const [detailId, setDetailId] = useState<number>();
  useEffect(() => setDetailId(undefined), [key]);
  useEffect(() => {
    const c = new AbortController();
    setDetail(undefined);
    if (valid && detailId)
      void read<HistoryInspectionDetailDto | HistoryCycleDetailDto>(
        queryPath(
          turnover
            ? `/api/rooms/${id(room)}/turnover-history/${id(detailId)}`
            : `/api/inspection-history/${id(detailId)}`,
          { branchId: b },
        ),
        { signal: c.signal },
      )
        .then((v) => {
          if (!c.signal.aborted) setDetail(v);
        })
        .catch((e) => {
          if (!c.signal.aborted) setError(e.message);
        });
    return () => c.abort();
  }, [key, detailId]);
  async function evidence(attachment: number) {
    try {
      if (!detailId) return;
      const file = await download(
        queryPath(
          turnover
            ? `/api/rooms/${id(room)}/turnover-history/${id(detailId)}/evidence/${id(attachment)}`
            : `/api/inspection-history/${id(detailId)}/evidence/${id(attachment)}`,
          { branchId: b },
        ),
      );
      const url = URL.createObjectURL(file);
      const a = document.createElement("a");
      a.href = url;
      a.download = `evidence-${attachment}`;
      a.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không tải được bằng chứng");
    }
  }
  return (
    <section className="dev1-page">
      <Dev1Navigation />
      <h1>{turnover ? "Lịch sử làm phòng" : "Lịch sử kiểm tra phòng"}</h1>
      <form
        className="dev1-context"
        onSubmit={(e) => {
          e.preventDefault();
          setQ({
            branchId: branch,
            ...(turnover ? { roomId: roomInput } : {}),
          });
        }}
      >
        <label>
          ID chi nhánh
          <input
            required
            type="number"
            min="1"
            step="1"
            value={branch}
            onChange={(e) => setBranch(e.target.value)}
          />
        </label>
        {turnover && (
          <label>
            ID phòng
            <input
              required
              type="number"
              min="1"
              step="1"
              value={roomInput}
              onChange={(e) => setRoomInput(e.target.value)}
            />
          </label>
        )}
        <button>Tải dữ liệu</button>
      </form>
      {(error || state.error) && (
        <p role="alert">{error || state.error?.message}</p>
      )}
      {valid && state.status === "loading" && <p role="status">Đang tải…</p>}
      {valid && state.status === "empty" && <p>Danh sách trống.</p>}
      {state.data && (
        <>
          <table className="dev1-table">
            <thead>
              <tr>
                <th>ID</th>
                <th>Phòng</th>
                <th>Trạng thái</th>
                <th>Kết quả</th>
                <th>Chi tiết</th>
              </tr>
            </thead>
            <tbody>
              {state.data.items.map((r) => (
                <tr key={r.id}>
                  <td>{r.id}</td>
                  <td>{r.roomNumber}</td>
                  <td>{r.status}</td>
                  <td>{r.result ?? "Chưa có"}</td>
                  <td>
                    <button onClick={() => setDetailId(r.id)}>
                      Mở chi tiết
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <button
            disabled={page <= 1}
            onClick={() => {
              const n = new URLSearchParams(q);
              n.set("page", String(page - 1));
              setQ(n);
            }}
          >
            Trang trước
          </button>{" "}
          <span>Trang {page}</span>{" "}
          <button
            disabled={page * 20 >= state.data.total}
            onClick={() => {
              const n = new URLSearchParams(q);
              n.set("page", String(page + 1));
              setQ(n);
            }}
          >
            Trang sau
          </button>
        </>
      )}
      {detail && state.status !== "error" && (
        <section>
          <h2>Chi tiết #{detailId}</h2>
          {Object.entries(detail)
            .filter(([k]) => k !== "evidence")
            .map(([k, v]) => (
              <details key={k}>
                <summary>{k}</summary>
                {(Array.isArray(v) ? v : [v]).map((row, i) => (
                  <dl className="dev1-details" key={i}>
                    {Object.entries(obj(row))
                      .filter(([, v]) => typeof v !== "object")
                      .map(([key, value]) => (
                        <div key={key}>
                          <dt>{key}</dt>
                          <dd>{String(value ?? "—")}</dd>
                        </div>
                      ))}
                  </dl>
                ))}
              </details>
            ))}
          {detail.evidence.map((e) => (
            <button key={e.id} onClick={() => void evidence(e.id)}>
              Tải {e.name}
            </button>
          ))}
        </section>
      )}
    </section>
  );
}
