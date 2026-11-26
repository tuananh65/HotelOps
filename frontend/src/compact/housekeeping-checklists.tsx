import { AlertPopup } from "./alert-popup";
import { useState } from "react";
import { api, fields, message, type Row, timestamp } from "./api";
import { useAuth } from "./auth";
import { Badge, Dialog, Empty, Pager, State, useQuery, label } from "./ui";
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";

type Item = { description: string; isRequired: boolean; weight: number };
export default function HousekeepingChecklists({ embedded = false }: { embedded?: boolean } = {}) {
  const { branch } = useAuth();
  const [revision, bump] = useState(0),
    [search, setSearch] = useState(""),
    [page, setPage] = useState(1),
    [sort, setSort] = useState("name"),
    [id, setId] = useState<number | null>(null);
  const [draft, setDraft] = useState<Row | null>(null),
    [items, setItems] = useState<Item[]>([]),
    [pending, setPending] = useState(false),
    [error, setError] = useState("");
  const query = useQuery(`templates?branchId=${branch}`, revision),
    detail = useQuery(id ? `templates/checklists/${id}` : null, revision);
  const rows = fields(query.data?.checklists)
    .filter(
      (r) =>
        ["CLEANING", "INSPECTION"].includes(r.kind) &&
        `${r.name} ${r.code}`
          .toLocaleLowerCase()
          .includes(search.toLocaleLowerCase()),
    )
    .sort((a, b) =>
      sort === "name"
        ? a.name.localeCompare(b.name, "vi") || b.versionNo - a.versionNo
        : b.checklistTemplateId - a.checklistTemplateId,
    );
  const current = Math.min(page, Math.max(1, Math.ceil(rows.length / 25)));
  function edit(copy?: Row) {
    setError("");
    setDraft(
      copy
        ? { ...copy, copy: true }
        : { code: "", name: "", kind: "INSPECTION" },
    );
    setItems(
      copy
        ? fields(detail.data?.items).map((i) => ({
            description: i.description,
            isRequired: i.isRequired,
            weight: i.weight,
          }))
        : [{ description: "", isRequired: true, weight: 1 }],
    );
  }
  async function save(form: HTMLFormElement) {
    if (pending || !draft) return;
    setPending(true);
    setError("");
    const data = new FormData(form);
    try {
      await api("templates/checklists", {
        branchId: branch,
        code: draft.copy ? draft.code : String(data.get("code") || "").trim(),
        name: String(data.get("name")).trim(),
        kind: draft.copy ? draft.kind : data.get("kind"),
        items,
      });
      setDraft(null);
      setId(null);
      bump((x) => x + 1);
    } catch (e) {
      setError(message(e));
    } finally {
      setPending(false);
    }
  }
  return (
    <>
      {!embedded && <PageTitle>
        <h1>Mẫu checklist</h1>
        <p>Mẫu đã công bố được giữ nguyên; thay đổi bằng phiên bản mới</p>
      </PageTitle>}
      <PageToolbar
        actions={
          <button className="primary" onClick={() => edit()}>
            + Tạo mẫu
          </button>
        }
      >
        <label className="search">
          Tìm mẫu
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
          <select value={sort} onChange={(e) => setSort(e.target.value)}>
            <option value="name">Tên mẫu</option>
            <option value="latest">Mới nhất</option>
          </select>
        </label>
      </PageToolbar>
      <State {...query} retry={() => bump((x) => x + 1)}>
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Mẫu</th>
                <th>Loại</th>
                <th>Phiên bản</th>
                <th>Công bố</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {rows.slice((current - 1) * 25, current * 25).map((r) => (
                <tr key={r.checklistTemplateId}>
                  <td>
                    {r.name}
                    <small>{r.code}</small>
                  </td>
                  <td>{label(r.kind)}</td>
                  <td>{r.versionNo}</td>
                  <td>
                    {timestamp(r.publishedAt)}{" "}
                    <Badge value={r.isActive ? "ACTIVE" : "INACTIVE"} />
                  </td>
                  <td>
                    <button onClick={() => setId(r.checklistTemplateId)}>
                      Chi tiết
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!rows.length && <Empty />}
        <Pager
          page={current}
          total={rows.length}
          pageSize={25}
          onPage={setPage}
        />
      </State>
      {id && !draft && (
        <Dialog
          title="Chi tiết mẫu checklist"
          className="room-drawer"
          close={() => setId(null)}
        >
          <State {...detail} retry={() => bump((x) => x + 1)}>
            {detail.data && (
              <>
                <h3>{detail.data.template.fields.name}</h3>
                <p>
                  {detail.data.template.fields.code} · Phiên bản{" "}
                  {detail.data.template.fields.versionNo}
                </p>
                <ol>
                  {fields(detail.data.items).map((i) => (
                    <li key={i.checklistItemId}>
                      {i.description} · {i.isRequired ? "Bắt buộc" : "Tùy chọn"}{" "}
                      · Trọng số {i.weight}
                    </li>
                  ))}
                </ol>
                <button onClick={() => edit(detail.data.template.fields)}>
                  Tạo phiên bản mới
                </button>
                <p>
                  Mẫu đã công bố không thể sửa hoặc ngừng trực tiếp theo hợp
                  đồng dữ liệu hiện hành.
                </p>
              </>
            )}
          </State>
        </Dialog>
      )}
      {draft && (
        <Dialog
          title={
            draft.copy ? "Tạo phiên bản checklist mới" : "Tạo mẫu checklist"
          }
          close={() => {
            if (!pending) setDraft(null);
          }}
        >
          <form
            onSubmit={(e) => {
              e.preventDefault();
              save(e.currentTarget);
            }}
          >
            <label>
              Mã mẫu
              <input
                name="code"
                required
                maxLength={50}
                defaultValue={draft.code}
                readOnly={!!draft.copy}
              />
            </label>
            <label>
              Tên mẫu
              <input
                name="name"
                required
                maxLength={200}
                defaultValue={draft.name}
              />
            </label>
            <label>
              Loại
              <select
                name="kind"
                defaultValue={draft.kind}
                disabled={!!draft.copy}
              >
                <option value="INSPECTION">Kiểm tra phòng</option>
                <option value="CLEANING">Dọn phòng</option>
              </select>
            </label>
            <h3>Các mục kiểm tra</h3>
            {items.map((item, index) => (
              <fieldset key={index}>
                <legend>Mục {index + 1}</legend>
                <label>
                  Nội dung
                  <input
                    required
                    maxLength={500}
                    value={item.description}
                    onChange={(e) =>
                      setItems(
                        items.map((x, i) =>
                          i === index
                            ? { ...x, description: e.target.value }
                            : x,
                        ),
                      )
                    }
                  />
                </label>
                <label>
                  <input
                    type="checkbox"
                    checked={item.isRequired}
                    onChange={(e) =>
                      setItems(
                        items.map((x, i) =>
                          i === index
                            ? { ...x, isRequired: e.target.checked }
                            : x,
                        ),
                      )
                    }
                  />{" "}
                  Bắt buộc
                </label>
                <label>
                  Trọng số
                  <input
                    type="number"
                    min={0}
                    step="any"
                    required
                    value={item.weight}
                    onChange={(e) =>
                      setItems(
                        items.map((x, i) =>
                          i === index
                            ? { ...x, weight: Number(e.target.value) }
                            : x,
                        ),
                      )
                    }
                  />
                </label>
                <button
                  type="button"
                  disabled={items.length === 1 || pending}
                  onClick={() => setItems(items.filter((_, i) => i !== index))}
                >
                  Bỏ mục
                </button>
              </fieldset>
            ))}
            <div className="toolbar">
              <button
                type="button"
                disabled={pending || items.length >= 200}
                onClick={() =>
                  setItems([
                    ...items,
                    { description: "", isRequired: true, weight: 1 },
                  ])
                }
              >
                + Thêm mục
              </button>
              <button className="primary" disabled={pending}>
                {pending ? "Đang công bố…" : "Công bố mẫu"}
              </button>
            </div>
            {error && (
              <AlertPopup>
                {error}
              </AlertPopup>
            )}
          </form>
        </Dialog>
      )}
    </>
  );
}
