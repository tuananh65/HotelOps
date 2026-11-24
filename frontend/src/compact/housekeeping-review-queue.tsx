import { useState } from "react";
import { SlidersHorizontal } from "lucide-react";
import { type Row, timestamp } from "./api";
import { useAuth } from "./auth";
import { Badge, Empty, Pager, State, useQuery } from "./ui";
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";

export default function HousekeepingReviewQueue({
  defects = false,
}: {
  defects?: boolean;
}) {
  const { branch, can } = useAuth();
  const [search, setSearch] = useState(""),
    [page, setPage] = useState(1),
    [status, setStatus] = useState(""),
    [sort, setSort] = useState(defects ? "latest" : "oldest"),
    [filters, setFilters] = useState(false),
    [revision, bump] = useState(0);
  const query = useQuery(
    `work/${defects ? "defects" : "inspection-queue"}?branchId=${branch}&page=${page}&search=${encodeURIComponent(search)}&sort=${sort}${status ? `&status=${status}` : ""}`,
    revision,
  );
  return (
    <>
      <PageTitle>
        <h1>{defects ? "Lỗi kiểm tra" : "Phòng chờ kiểm tra"}</h1>
        <p>
          {defects
            ? "Theo dõi lỗi và công việc làm lại liên quan"
            : "Kiểm tra theo checklist giấy và xác nhận kết quả trên công việc"}
        </p>
      </PageTitle>
      <PageToolbar>
        <label className="search">
          Tìm phòng / nội dung
          <input
            type="search"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
        </label>
        <label>
          Sắp xếp
          <select
            value={sort}
            onChange={(e) => {
              setSort(e.target.value);
              setPage(1);
            }}
          >
            <option value="oldest">Cũ nhất</option>
            <option value="latest">Mới nhất</option>
            <option value="title">Nội dung</option>
          </select>
        </label>
        {defects && (
          <button aria-expanded={filters} onClick={() => setFilters(!filters)}>
            <SlidersHorizontal size={16} /> Bộ lọc
          </button>
        )}
      </PageToolbar>
      {filters && defects && (
        <PageToolbar>
          <label>
            Trạng thái
            <select
              value={status}
              onChange={(e) => {
                setStatus(e.target.value);
                setPage(1);
              }}
            >
              <option value="">Tất cả</option>
              <option value="OPEN">Chưa xử lý</option>
              <option value="RESOLVED">Đã xử lý</option>
              <option value="CANCELLED">Đã hủy</option>
            </select>
          </label>
          <button
            onClick={() => {
              setStatus("");
              setSearch("");
              setPage(1);
            }}
          >
            Đặt lại
          </button>
        </PageToolbar>
      )}
      <State {...query} retry={() => bump((x) => x + 1)}>
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Phòng</th>
                <th>{defects ? "Lỗi" : "Công việc vệ sinh"}</th>
                <th>Trạng thái</th>
                <th>{defects ? "Làm lại" : "Hoàn thành dọn"}</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {query.data?.items.map((r: Row) => (
                <tr key={r.findingId || r.workId}>
                  <td>{r.room}</td>
                  <td>
                    {r.description || r.title}
                  </td>
                  <td>
                    <Badge value={r.status} />
                  </td>
                  <td>
                    {defects ? (
                      r.reworkWorkId ? (
                        <a href={`#work?kind=REWORK&id=${r.reworkWorkId}`}>
                          Xem việc làm lại
                        </a>
                      ) : (
                        "Chưa liên kết"
                      )
                    ) : (
                      timestamp(r.completedAt)
                    )}
                  </td>
                  <td>
                    {defects ? (
                      <a
                        className="button-link"
                        href={`#work?kind=INSPECTION&id=${r.inspectionWorkId}`}
                      >
                        Xem kết quả & xử lý lỗi
                      </a>
                    ) : (
                      <>
                        {can("INSPECTION_PERFORM") && (
                          <a
                            className="button-link"
                            href={`#work?kind=CLEANING&id=${r.workId}`}
                          >
                            Xác nhận hoàn thành
                          </a>
                        )}{" "}
                        <a
                          className="button-link"
                          href={`#work?kind=CLEANING&id=${r.workId}`}
                        >
                          Chi tiết
                        </a>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!query.data?.items.length && <Empty />}
        <Pager
          page={page}
          total={query.data?.total || 0}
          pageSize={25}
          onPage={setPage}
        />
      </State>
    </>
  );
}
