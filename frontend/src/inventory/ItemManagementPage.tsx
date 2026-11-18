import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";

type Item = {
  itemId: number;
  itemCode: string;
  itemName: string;
  categoryId: number;
  baseUomId: number;
  isActive: boolean;
  isConsumable: boolean;
  description: string | null;
  specification: string | null;
  rowVersion: string;
};
type Lookup = { id: number; code: string; name: string; isActive: boolean };
type Snapshot = {
  items: { items: Item[]; totalCount: number; page: number; pageSize: number };
  categories: Lookup[];
  units: Lookup[];
};
type Edit = { item: Item; wasActive: boolean };

export function ItemManagementPage() {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" && !state.pending
      ? state.authorityGeneration
      : null;
  const current = useRef(owner);
  current.current = owner;
  const [params, setParams] = useSearchParams();
  const page = params.get("page") ?? "1";
  const [loaded, setLoaded] = useState<{
    owner: AuthorityGeneration;
    page: string;
    data: Snapshot;
  } | null>(null);
  const [edit, setEdit] = useState<Edit | null>(null),
    [notice, setNotice] = useState("");
  const [revision, setRevision] = useState(0),
    [busy, setBusy] = useState(false);
  const writer = useRef<AbortController | null>(null);
  const data =
    owner && loaded?.owner === owner && loaded.page === page
      ? loaded.data
      : null;
  useEffect(() => {
    setEdit(null);
    setNotice("");
    writer.current?.abort();
    setBusy(false);
  }, [owner, page]);
  useEffect(() => {
    setLoaded(null);
    if (!owner) return;
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch(
          `/api/item-management?page=${encodeURIComponent(page)}&pageSize=25`,
          {
            credentials: "same-origin",
            cache: "no-store",
            redirect: "error",
            signal: abort.signal,
          },
        );
        if (response.status === 401) actions.invalidateSession(owner);
        if (!response.ok) {
          const body = await response.json().catch(() => null);
          throw new Error(
            body?.detail ?? `Danh mục chưa khả dụng (${response.status}).`,
          );
        }
        const body = (await response.json()) as Snapshot;
        if (
          body.items?.page !== Number(page) ||
          !Array.isArray(body.items.items) ||
          !Array.isArray(body.categories) ||
          !Array.isArray(body.units)
        )
          throw new Error("Phản hồi danh mục không hợp lệ.");
        if (!abort.signal.aborted && current.current === owner)
          setLoaded({ owner, page, data: body });
      } catch (e) {
        if (!abort.signal.aborted) {
          setEdit(null);
          setNotice(e instanceof Error ? e.message : "Danh mục chưa khả dụng.");
        }
      }
    })();
    const refresh = () => {
      setLoaded(null);
      setRevision((v) => v + 1);
    };
    const timer = window.setInterval(refresh, 30000);
    window.addEventListener("focus", refresh);
    return () => {
      abort.abort();
      window.clearInterval(timer);
      window.removeEventListener("focus", refresh);
    };
  }, [owner, page, revision, actions]);
  useEffect(() => () => writer.current?.abort(), []);
  async function save(value: Edit) {
    if (!owner || !data || busy) return;
    const item = value.item;
    if (
      item.itemId > 0 &&
      value.wasActive &&
      !item.isActive &&
      !window.confirm(
        "MSG33: Vô hiệu hóa vật tư này? Vật tư không dùng được trong chứng từ mới. Tồn kho và lịch sử giữ nguyên.",
      )
    )
      return;
    const abort = new AbortController();
    writer.current = abort;
    setBusy(true);
    setNotice("");
    try {
      const bootstrap = await fetch("/api/auth/csrf", {
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
      });
      const token = await bootstrap.json();
      if (
        !bootstrap.ok ||
        token.headerName !== "X-CSRF-TOKEN" ||
        typeof token.csrfToken !== "string" ||
        !token.csrfToken
      )
        throw new Error("Chưa khởi tạo được bảo vệ phiên (CSRF).");
      if (current.current !== owner || abort.signal.aborted) return;
      const response = await fetch(
        `/api/inventory/items${item.itemId ? `/${item.itemId}` : ""}`,
        {
          method: item.itemId ? "PUT" : "POST",
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: abort.signal,
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": token.csrfToken,
          },
          body: JSON.stringify({
            itemCode: item.itemCode,
            itemName: item.itemName,
            categoryId: item.categoryId,
            baseUomId: item.baseUomId,
            isConsumable: item.isConsumable,
            isActive: item.isActive,
            description: item.description,
            specification: item.specification,
            expectedRowVersion: item.itemId ? item.rowVersion : null,
          }),
        },
      );
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok) {
        const body = await response.json().catch(() => null);
        throw new Error(body?.detail ?? `Lưu thất bại (${response.status}).`);
      }
      if (!abort.signal.aborted && current.current === owner) {
        setEdit(null);
        (announceWorkflowSuccess(
          "MSG08: Đã lưu vật tư. Tồn kho và lịch sử hiện có giữ nguyên.",
          "inventory/ItemManagementPage.tsx",
        ),
          setNotice(
            "MSG08: Đã lưu vật tư. Tồn kho và lịch sử hiện có giữ nguyên.",
          ));
        setRevision((v) => v + 1);
      }
    } catch (e) {
      if (!abort.signal.aborted && current.current === owner)
        setNotice(
          e instanceof Error
            ? e.message
            : "Chưa xác định kết quả lưu. Tải lại trước khi thử lại.",
        );
    } finally {
      if (writer.current === abort) {
        writer.current = null;
        setBusy(false);
      }
    }
  }
  const field = (value: Partial<Item>) => {
    if (edit) setEdit({ ...edit, item: { ...edit.item, ...value } });
  };
  return (
    <section>
      <h1>Quản lý vật tư kho</h1>
      <Link to="/inventory/items">Xem danh mục</Link> ·{" "}
      <Link to="/inventory/units">Đơn vị và quy đổi</Link> ·{" "}
      <Link to="/inventory/stock">Tồn kho</Link>
      <p>
        Đổi danh mục dùng chung cần quyền thủ kho hiện tại và ITEM_MASTER_MANAGE
        toàn hệ thống. Không cấp quyền tại đây.
      </p>
      {notice && <p role="status">{notice}</p>}
      {data && (
        <>
          <button
            disabled={busy}
            onClick={() =>
              setEdit({
                wasActive: false,
                item: {
                  itemId: 0,
                  itemCode: "",
                  itemName: "",
                  categoryId: 0,
                  baseUomId: 0,
                  isActive: true,
                  isConsumable: false,
                  description: null,
                  specification: null,
                  rowVersion: "",
                },
              })
            }
          >
            Tạo vật tư
          </button>
          <button
            disabled={busy}
            onClick={() => {
              setEdit(null);
              setRevision((v) => v + 1);
            }}
          >
            Tải lại / bỏ chỉnh sửa
          </button>
          <table>
            <thead>
              <tr>
                <th>Mã</th>
                <th>Tên</th>
                <th>Loại</th>
                <th>Đơn vị cơ sở</th>
                <th>Trạng thái</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {data.items.items.map((i) => (
                <tr key={i.itemId}>
                  <td>{i.itemCode}</td>
                  <td>{i.itemName}</td>
                  <td>
                    {data.categories.find((c) => c.id === i.categoryId)?.name ??
                      i.categoryId}
                  </td>
                  <td>
                    {data.units.find((u) => u.id === i.baseUomId)?.name ??
                      i.baseUomId}
                  </td>
                  <td>{i.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}</td>
                  <td>
                    <button
                      disabled={busy}
                      onClick={() =>
                        setEdit({ item: { ...i }, wasActive: i.isActive })
                      }
                    >
                      Chỉnh sửa
                    </button>
                    {i.isActive && (
                      <button
                        disabled={busy}
                        onClick={() =>
                          void save({
                            item: { ...i, isActive: false },
                            wasActive: true,
                          })
                        }
                      >
                        Vô hiệu hóa
                      </button>
                    )}
                    <Link to={`/inventory/units?itemId=${i.itemId}`}>
                      Quy đổi
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {data.items.items.length === 0 && <p>MSG10: Chưa có vật tư.</p>}
          <button
            disabled={busy || !!edit || data.items.page <= 1}
            onClick={() => setParams({ page: String(data.items.page - 1) })}
          >
            Trước
          </button>
          <button
            disabled={
              busy ||
              !!edit ||
              data.items.page * data.items.pageSize >= data.items.totalCount
            }
            onClick={() => setParams({ page: String(data.items.page + 1) })}
          >
            Tiếp
          </button>
          {edit && (
            <ActionDialog
              scope="inventory/ItemManagementPage.tsx"
              open
              title={edit.item.itemId ? "Chỉnh sửa vật tư" : "Tạo vật tư"}
              onClose={() => setEdit(null)}
              busy={busy}
              locked={false}
              error={notice}
            >
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  void save(edit);
                }}
              >
                <label>
                  Mã{" "}
                  <input
                    required
                    maxLength={50}
                    value={edit.item.itemCode}
                    onChange={(e) => field({ itemCode: e.target.value })}
                  />
                </label>
                <label>
                  Tên{" "}
                  <input
                    required
                    maxLength={200}
                    value={edit.item.itemName}
                    onChange={(e) => field({ itemName: e.target.value })}
                  />
                </label>
                <label>
                  Loại{" "}
                  <select
                    aria-label="Loại"
                    required
                    value={edit.item.categoryId || ""}
                    onChange={(e) =>
                      field({ categoryId: Number(e.target.value) })
                    }
                  >
                    <option value="">Chọn nhóm</option>
                    {data.categories
                      .filter(
                        (c) => c.isActive || c.id === edit.item.categoryId,
                      )
                      .map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.name}
                          {c.isActive ? "" : " (inactive)"}
                        </option>
                      ))}
                  </select>
                </label>
                <label>
                  Đơn vị cơ sở{" "}
                  <select
                    aria-label="Đơn vị cơ sở"
                    required
                    value={edit.item.baseUomId || ""}
                    onChange={(e) =>
                      field({ baseUomId: Number(e.target.value) })
                    }
                  >
                    <option value="">Chọn đơn vị cơ sở</option>
                    {data.units
                      .filter((u) => u.isActive || u.id === edit.item.baseUomId)
                      .map((u) => (
                        <option key={u.id} value={u.id}>
                          {u.name}
                          {u.isActive ? "" : " (inactive)"}
                        </option>
                      ))}
                  </select>
                </label>
                <p>
                  Không đổi đơn vị cơ sở sau khi có giao dịch kho. Cần vô hiệu
                  hóa quy đổi đang hoạt động trước khi đổi đơn vị cơ sở đủ điều
                  kiện.
                </p>
                <label>
                  Mô tả{" "}
                  <textarea
                    value={edit.item.description ?? ""}
                    onChange={(e) =>
                      field({ description: e.target.value || null })
                    }
                  />
                </label>
                <label>
                  Thông số{" "}
                  <textarea
                    value={edit.item.specification ?? ""}
                    onChange={(e) =>
                      field({ specification: e.target.value || null })
                    }
                  />
                </label>
                <label>
                  <input
                    type="checkbox"
                    checked={edit.item.isConsumable}
                    onChange={(e) => field({ isConsumable: e.target.checked })}
                  />
                  Vật tư tiêu hao
                </label>
                <label>
                  <input
                    type="checkbox"
                    checked={edit.item.isActive}
                    onChange={(e) => field({ isActive: e.target.checked })}
                  />
                  Đang hoạt động
                </label>
                <button data-dialog-action disabled={busy}>
                  Lưu vật tư
                </button>
              </form>
            </ActionDialog>
          )}
        </>
      )}
    </section>
  );
}
