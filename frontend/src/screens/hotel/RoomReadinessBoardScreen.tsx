import { BedDouble } from "lucide-react";
import { useEffect, useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { errorMessage } from "../../api/dev1/client";
import {
  listRooms,
  positiveId,
  roomStatusCodes,
  type RoomDirectoryResponse,
} from "../../api/dev1/rooms";
import { PageHeader } from "../../ui";

export default function RoomReadinessBoardScreen() {
  const [params, setParams] = useSearchParams();
  const branch = params.get("branchId") || "";
  const status = params.get("status") || "";
  const search = params.get("search") || "";
  const page = Number(params.get("page") || "1");
  const [draftBranch, setDraftBranch] = useState(branch);
  const [draftSearch, setDraftSearch] = useState(search);
  const [draftStatus, setDraftStatus] = useState(status);
  const [result, setResult] = useState<RoomDirectoryResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [retry, setRetry] = useState(0);
  const requestKey = JSON.stringify([branch, page, status, search, retry]);
  const [resultKey, setResultKey] = useState("");
  useEffect(() => {
    const controller = new AbortController();
    setResult(null);
    setError("");
    setLoading(Boolean(branch));
    if (branch) {
      listRooms(branch, page, status, search, controller.signal)
        .then((data) => {
          if (!controller.signal.aborted) {
            setResult(data);
            setResultKey(JSON.stringify([branch, page, status, search, retry]));
          }
        })
        .catch((reason) => {
          if (!controller.signal.aborted) setError(errorMessage(reason));
        })
        .finally(() => {
          if (!controller.signal.aborted) setLoading(false);
        });
    }
    return () => controller.abort();
  }, [branch, page, status, search, retry]);
  useEffect(() => {
    setDraftBranch(branch);
    setDraftSearch(search);
    setDraftStatus(status);
  }, [branch, search, status]);

  function apply(event: FormEvent) {
    event.preventDefault();
    try {
      positiveId(draftBranch);
    } catch {
      setError("Nhập ID chi nhánh là số nguyên dương hợp lệ.");
      return;
    }
    setResult(null);
    setParams({
      branchId: draftBranch,
      status: draftStatus,
      search: draftSearch,
      page: "1",
    });
    setRetry((value) => value + 1);
  }
  const floors = [
    ...new Map(
      (result?.items || []).map((room) => [room.floor.id, room.floor]),
    ).values(),
  ];
  return (
    <>
      <PageHeader id={23} />
      <p>
        Trạng thái phòng từ máy chủ. Chưa xác nhận room readiness theo chu kỳ
        dọn phòng/kiểm tra. Dữ liệu chỉ cập nhật khi tải lại.
      </p>
      <form className="filter-bar" onSubmit={apply}>
        <label>
          Chi nhánh (ID)
          <input
            aria-label="Chi nhánh (ID)"
            inputMode="numeric"
            required
            value={draftBranch}
            onChange={(event) => setDraftBranch(event.target.value)}
          />
        </label>
        <label>
          Số phòng
          <input
            maxLength={200}
            value={draftSearch}
            onChange={(event) => setDraftSearch(event.target.value)}
          />
        </label>
        <label>
          Trạng thái phòng
          <select
            aria-label="Trạng thái phòng"
            value={draftStatus}
            onChange={(event) => setDraftStatus(event.target.value)}
          >
            <option value="">Tất cả trạng thái</option>
            {roomStatusCodes.map((code) => (
              <option key={code} value={code}>
                {code}
              </option>
            ))}
          </select>
        </label>
        <button type="submit">Tải dữ liệu</button>
      </form>
      {!branch && (
        <p role="status">
          Nhập chi nhánh để tải phòng. Máy chủ kiểm tra quyền truy cập chi
          nhánh.
        </p>
      )}
      {loading && <p role="status">Đang tải phòng…</p>}
      {error && (
        <div role="alert">
          <p>{error}</p>
          <button onClick={() => setRetry((value) => value + 1)}>
            Thử lại
          </button>
        </div>
      )}
      {result && resultKey === requestKey && !loading && !error && (
        <>
          <p role="status">
            {result.totalCount} phòng phù hợp · Trang {result.page}. Các nhóm
            tầng bên dưới chỉ gồm phòng trên trang này.
          </p>
          {result.items.length === 0 && <p>Không có phòng phù hợp bộ lọc.</p>}
          {floors.map((floor) => (
            <section className="floor-section" key={floor.id}>
              <div className="section-header">
                <h2>{floor.name}</h2>
                <span>Chi nhánh {branch}</span>
              </div>
              <div className="room-grid">
                {result.items
                  .filter((room) => room.floor.id === floor.id)
                  .map((room) => (
                    <article className="room-tile" key={room.id}>
                      <BedDouble size={24} strokeWidth={1} />
                      <strong>{room.roomNumber}</strong>
                      <span>{room.roomType.name}</span>
                      <span>{room.operationalStatusCode}</span>
                      <Link
                        to={`/cleaning/tasks?branchId=${branch}&search=${encodeURIComponent(room.roomNumber)}`}
                      >
                        Xem công việc vệ sinh
                      </Link>
                      {!room.isActive && <span>Ngừng hoạt động</span>}
                    </article>
                  ))}
              </div>
            </section>
          ))}
          <div className="filter-bar">
            <button
              disabled={page <= 1}
              onClick={() =>
                setParams({
                  branchId: branch,
                  status,
                  search,
                  page: String(page - 1),
                })
              }
            >
              Trang trước
            </button>
            <button
              disabled={page * result.pageSize >= result.totalCount}
              onClick={() =>
                setParams({
                  branchId: branch,
                  status,
                  search,
                  page: String(page + 1),
                })
              }
            >
              Trang sau
            </button>
          </div>
        </>
      )}
    </>
  );
}
