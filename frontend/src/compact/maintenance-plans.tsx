import { AlertPopup } from "./alert-popup";
import { useState } from "react";
import { SlidersHorizontal } from "lucide-react";
import { useAuth } from "./auth";
import { api, fields, message, timestamp, type Row } from "./api";
import { Dialog, Empty, Pager, State, useQuery, Badge } from "./ui";
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";

const instant = (value: string) =>
  new Date(/Z$|[+-]\d\d:\d\d$/.test(value) ? value : `${value}Z`);

export default function MaintenancePlans() {
  const { branch, can } = useAuth();
  const [revision, bump] = useState(0),
    [search, setSearch] = useState(""),
    [page, setPage] = useState(1),
    [filters, setFilters] = useState(false),
    [filter, setFilter] = useState("active"),
    [sort, setSort] = useState("due");
  const [id, setId] = useState<number | null>(
      Number(new URLSearchParams(location.hash.split("?")[1]).get("id")) ||
        null,
    ),
    [editing, setEditing] = useState<Row | null>(null),
    [disabling, setDisabling] = useState<Row | null>(null),
    [pending, setPending] = useState(false),
    [error, setError] = useState("");
  const query = useQuery(`templates?branchId=${branch}`, revision),
    options = useQuery(`workspace/options?branchId=${branch}`),
    detail = useQuery(id ? `templates/schedules/${id}` : null, revision);
  const rows = fields(query.data?.schedules)
    .filter(
      (r) =>
        r.kind === "MAINTENANCE" &&
        `${r.title} ${r.code}`.toLowerCase().includes(search.toLowerCase()) &&
        (filter !== "active" || r.isActive) &&
        (filter !== "due" ||
          (r.isActive && instant(r.nextDueAt).getTime() <= Date.now())),
    )
    .sort((a, b) =>
      sort === "name"
        ? a.title.localeCompare(b.title, "vi")
        : String(a.nextDueAt).localeCompare(String(b.nextDueAt)),
    );
  async function save(path: string, body: Row, method = "POST") {
    if (pending) return;
    setPending(true);
    setError("");
    try {
      await api(path, body, method);
      setEditing(null);
      setDisabling(null);
      bump((x) => x + 1);
    } catch (e) {
      setError(message(e));
      bump((x) => x + 1);
    } finally {
      setPending(false);
    }
  }
  const localDate = (v: string) =>
    v
      ? new Date(instant(v).getTime() - instant(v).getTimezoneOffset() * 60000)
          .toISOString()
          .slice(0, 16)
      : "";
  return (
    <>
      <PageTitle>
        <h1>Bảo trì định kỳ</h1>
        <p>Kế hoạch thiết bị và lịch sử các công việc phát sinh</p>
      </PageTitle>
      <PageToolbar>
        <label>
          Tìm kế hoạch
          <input
            type="search"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
        </label>
        <button aria-expanded={filters} onClick={() => setFilters(!filters)}>
          <SlidersHorizontal size={16} /> Bộ lọc
        </button>
        {can("PM_MANAGE", false) && (
          <button
            className="primary"
            onClick={() => {
              setError("");
              setEditing({});
            }}
          >
            + Tạo kế hoạch
          </button>
        )}
      </PageToolbar>
      {filters && (
        <PageToolbar>
          <label>
            Phạm vi
            <select
              value={filter}
              onChange={(e) => {
                setFilter(e.target.value);
                setPage(1);
              }}
            >
              <option value="all">Tất cả</option>
              <option value="active">Đang áp dụng</option>
              <option value="due">Đến hạn</option>
            </select>
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
              <option value="due">Đến hạn trước</option>
              <option value="name">Tên kế hoạch</option>
            </select>
          </label>
          <button
            onClick={() => {
              setSearch("");
              setFilter("active");
              setSort("due");
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
                <th>Kế hoạch</th>
                <th>Thiết bị</th>
                <th>Chu kỳ</th>
                <th>Lần đến hạn</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {rows.slice((page - 1) * 25, page * 25).map((r) => (
                <tr key={r.workTemplateId}>
                  <td>
                    {r.title}
                    <small>
                      {r.code} · {r.isActive ? "Đang áp dụng" : "Đã dừng"}
                    </small>
                  </td>
                  <td>
                    {options.data?.incidentAssets?.find(
                      (a: Row) => a.assetId === r.assetId,
                    )?.name || "—"}
                  </td>
                  <td>
                    {r.frequencyInterval}{" "}
                    {
                      (
                        {
                          DAILY: "ngày",
                          WEEKLY: "tuần",
                          MONTHLY: "tháng",
                        } as Row
                      )[r.frequency]
                    }
                  </td>
                  <td>{timestamp(r.nextDueAt)}</td>
                  <td>
                    <button onClick={() => setId(r.workTemplateId)}>
                      Chi tiết
                    </button>
                    {r.isActive && can("PM_MANAGE", false) && (
                      <>
                        <button
                          onClick={() => {
                            setError("");
                            setEditing(r);
                          }}
                        >
                          Chỉnh sửa
                        </button>
                        <button
                          onClick={() => {
                            setError("");
                            setDisabling(r);
                          }}
                        >
                          Dừng kế hoạch
                        </button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!rows.length && <Empty />}
        <Pager page={page} pageSize={25} total={rows.length} onPage={setPage} />
      </State>
      {id && (
        <Dialog
          title={detail.data?.template.fields.title || "Kế hoạch PM"}
          className="room-drawer"
          close={() => setId(null)}
        >
          <State {...detail}>
            <p>Đến hạn: {timestamp(detail.data?.template.fields.nextDueAt)}</p>
            <p>
              Kế hoạch đến hạn không đồng nghĩa công việc đã hoàn thành. Công
              việc phát sinh do scheduler hiện có thực hiện.
            </p>
            <h3>Lịch sử bảo trì</h3>
            {detail.data?.occurrences.map((w: Row) => (
              <a
                className="overview-task"
                key={w.workId}
                href={`#work?kind=MAINTENANCE&id=${w.workId}`}
              >
                <span>
                  {w.title}
                  <small>{timestamp(w.occurrenceAt)}</small>
                </span>
                <Badge value={w.status} />
              </a>
            ))}
            {!detail.data?.occurrences.length && <Empty />}
          </State>
        </Dialog>
      )}
      {editing && (
        <Dialog
          title={
            editing.workTemplateId ? "Chỉnh kế hoạch PM" : "Tạo kế hoạch PM"
          }
          close={() => {
            if (!pending) setEditing(null);
          }}
        >
          <State {...options}>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                const f = new FormData(e.currentTarget),
                  num = (key: string) =>
                    f.get(key) ? Number(f.get(key)) : null;
                const plan = {
                  branchId: branch,
                  kind: "MAINTENANCE",
                  code: String(f.get("code") || ""),
                  title: f.get("title"),
                  assetId: num("assetId"),
                  roomId: num("roomId"),
                  departmentId: num("departmentId"),
                  checklistTemplateId: num("checklistTemplateId"),
                  frequency: f.get("frequency"),
                  frequencyInterval: num("frequencyInterval"),
                  nextDueAt: new Date(String(f.get("nextDueAt"))).toISOString(),
                };
                save(
                  `templates/schedules${editing.workTemplateId ? `/${editing.workTemplateId}` : ""}`,
                  editing.workTemplateId
                    ? { rowVersion: editing.rowVersion, plan }
                    : plan,
                  editing.workTemplateId ? "PUT" : "POST",
                );
              }}
            >
              <label>
                Mã kế hoạch
                <input
                  name="code"
                  required
                  maxLength={60}
                  defaultValue={editing.code || ""}
                  readOnly={!!editing.workTemplateId}
                />
              </label>
              <label>
                Tên kế hoạch
                <input
                  name="title"
                  required
                  maxLength={200}
                  defaultValue={editing.title || ""}
                />
              </label>
              {[
                ["assetId", "Thiết bị", "incidentAssets", "assetId"],
                ["roomId", "Phòng", "rooms", "roomId"],
                ["departmentId", "Bộ phận", "departments", "departmentId"],
                [
                  "checklistTemplateId",
                  "Checklist áp dụng",
                  "checklists",
                  "checklistTemplateId",
                ],
              ].map(([key, title, list, value]) => (
                <label key={key}>
                  {title}
                  <select name={key} defaultValue={editing[key] || ""}>
                    <option value="">Không chọn</option>
                    {(options.data?.[list] || [])
                      .filter(
                        (r: Row) =>
                          list !== "checklists" || r.kind === "MAINTENANCE",
                      )
                      .map((r: Row) => (
                        <option key={r[value]} value={r[value]}>
                          {r.name || r.code}
                        </option>
                      ))}
                  </select>
                </label>
              ))}
              <label>
                Chu kỳ
                <select
                  name="frequency"
                  defaultValue={editing.frequency || "MONTHLY"}
                >
                  <option value="DAILY">Ngày</option>
                  <option value="WEEKLY">Tuần</option>
                  <option value="MONTHLY">Tháng</option>
                </select>
              </label>
              <label>
                Khoảng cách chu kỳ
                <input
                  type="number"
                  name="frequencyInterval"
                  min={1}
                  max={365}
                  defaultValue={editing.frequencyInterval || 1}
                  required
                />
              </label>
              <label>
                Lần đến hạn
                <input
                  type="datetime-local"
                  name="nextDueAt"
                  required
                  defaultValue={localDate(editing.nextDueAt)}
                />
              </label>
              {error && (
                <AlertPopup>
                  {error}
                </AlertPopup>
              )}
              <button disabled={pending} className="primary">
                Lưu kế hoạch
              </button>
            </form>
          </State>
        </Dialog>
      )}
      {disabling && (
        <Dialog
          title="Dừng kế hoạch PM"
          close={() => {
            if (!pending) setDisabling(null);
          }}
        >
          <p>
            Dừng phát sinh các lần bảo trì tiếp theo cho {disabling.title}. Lịch
            sử công việc được giữ lại.
          </p>
          {error && (
            <AlertPopup>
              {error}
            </AlertPopup>
          )}
          <button
            disabled={pending}
            onClick={() =>
              save(`templates/schedules/${disabling.workTemplateId}/disable`, {
                rowVersion: disabling.rowVersion,
              })
            }
          >
            Xác nhận dừng
          </button>
        </Dialog>
      )}
    </>
  );
}
