import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Link,
  Navigate,
  useLocation,
  useParams,
  useSearchParams,
} from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import {
  routeItemId,
  type InventoryItem,
  type InventoryPage,
  type InventoryResult,
} from "./contracts";
import { createInventoryApi } from "./inventoryApi";

const bindingKeys = new WeakMap<object, number>();
let nextKey = 0;
function bindingKey(binding: object) {
  if (!bindingKeys.has(binding)) bindingKeys.set(binding, ++nextKey);
  return bindingKeys.get(binding)!;
}

/** Authentication admits a GET attempt; the backend alone decides Inventory permission. */
export function InventoryRoute({ mode }: { mode: "list" | "detail" }) {
  const { state, actions } = useAuth();
  const location = useLocation();
  const { id = "" } = useParams();
  const [blocked, setBlocked] = useState(false);
  const blockedRef = useRef(false);
  const binding = useMemo(
    () => ({}),
    [state.session, state.status, state.pending, mode, location.key],
  );
  const latest = useRef(binding);
  latest.current = binding;
  const current = useCallback(() => latest.current === binding, [binding]);
  const sessionFailure = useCallback(() => {
    if (!current() || blockedRef.current) return;
    blockedRef.current = true;
    setBlocked(true);
    void actions.revalidate();
  }, [actions, current]);

  if (state.status === "RESTRICTED" || state.session?.mustChangePassword)
    return <Navigate replace to="/change-password" />;
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate replace to="/login" />;
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.pending !== null
  )
    return (
      <main className="inventory-page">
        <p role="status">
          Dữ liệu kho được ẩn khi phiên chưa được xác minh.
        </p>
        <button onClick={() => void actions.revalidate()}>Kiểm tra phiên đăng nhập</button>
        <Link to="/profile">Hồ sơ cá nhân</Link>
      </main>
    );
  if (blocked)
    return (
      <main className="inventory-page">
        <h1>Khôi phục phiên quản lý kho</h1>
        <p role="alert">
          Dữ liệu kho đã ẩn. Phiên đã được kiểm tra; chủ động tải lại để thử tiếp.
        </p>
        <button
          onClick={() => {
            blockedRef.current = false;
            setBlocked(false);
          }}
        >
          Tải lại kho vật tư
        </button>
        <Link to="/profile">Hồ sơ cá nhân</Link>
      </main>
    );
  if (mode === "detail" && routeItemId(id) === undefined)
    return (
      <main className="inventory-page">
        <p role="alert">Mã vật tư không hợp lệ.</p>
        <Link to="/inventory/items">Quay lại vật tư</Link>
      </main>
    );
  return (
    <InventoryRead
      key={bindingKey(binding)}
      mode={mode}
      id={Number(id)}
      current={current}
      sessionFailure={sessionFailure}
    />
  );
}

function InventoryRead({
  mode,
  id,
  current,
  sessionFailure,
}: {
  mode: "list" | "detail";
  id: number;
  current: () => boolean;
  sessionFailure: () => void;
}) {
  const [params] = useSearchParams();
  const rawPage = params.getAll("page");
  const page =
    rawPage.length === 0
      ? 1
      : rawPage.length === 1
        ? routeItemId(rawPage[0])
        : undefined;
  const [api] = useState(() => createInventoryApi());
  const [attempt, setAttempt] = useState(0);
  const [result, setResult] = useState<InventoryResult<
    InventoryPage | InventoryItem
  > | null>(null);
  const valid =
    mode === "detail" || (page !== undefined && page <= 107_374_183);
  useEffect(() => {
    const controller = new AbortController();
    setResult(null);
    void Promise.resolve().then(async () => {
      if (!valid || controller.signal.aborted || !current()) return;
      const next =
        mode === "list"
          ? await api.list(page!, 20, controller.signal)
          : await api.item(id, controller.signal);
      if (controller.signal.aborted || !current()) return;
      setResult(next);
      if (!next.ok && next.status === 401) sessionFailure();
    });
    return () => controller.abort();
  }, [api, mode, id, page, valid, attempt, current, sessionFailure]);

  return (
    <main className="inventory-page">
      <nav aria-label="Điều hướng kho">
        <Link to="/dashboard">Không gian làm việc</Link>
        <Link to="/inventory/items">Vật tư</Link> · <Link to="/inventory/stocktakes">Kiểm kê</Link>{" | "}<Link to="/inventory/transfers">Điều chuyển kho</Link>{" | "}<Link to="/inventory/receipts">Phiếu nhập kho</Link> | <Link to="/inventory/warehouses">Kho</Link> | <Link to="/inventory/catalog">Quản lý vật tư</Link> · <Link to="/inventory/stock">Tồn kho và sổ kho</Link> · <Link to="/inventory/units">Đơn vị và quy đổi</Link>
        <Link to="/profile">Hồ sơ cá nhân</Link>
      </nav>
      <header>
        <p className="inventory-eyebrow">Kho vật tư · Chỉ xem</p>
        <h1>{mode === "list" ? "Vật tư trong kho" : "Chi tiết vật tư"}</h1>
        <p>
          Tra cứu danh mục vật tư. Số dư tồn kho được xem riêng.
        </p>
      </header>
      {!valid ? (
        <p role="alert">Số trang không hợp lệ.</p>
      ) : !result ? (
        <p role="status">Đang tải vật tư...</p>
      ) : !result.ok ? (
        <section>
          <p role="alert">{errorMessage(result)}</p>
          {result.status !== 401 && (
            <button
              onClick={() => {
                setResult(null);
                setAttempt((x) => x + 1);
              }}
            >
              Thử lại
            </button>
          )}
        </section>
      ) : mode === "list" ? (
        <Items page={result.data as InventoryPage} />
      ) : (
        <Item item={result.data as InventoryItem} />
      )}
      {mode === "detail" && <Link to="/inventory/items">Quay lại vật tư</Link>}
    </main>
  );
}

function errorMessage(error: { kind: string; status?: number }) {
  if (error.status === 401)
    return "Cần kiểm tra phiên. Dữ liệu kho đã ẩn.";
  if (error.status === 403)
    return "Tài khoản của bạn không có quyền xem dữ liệu kho này.";
  if (error.status === 404) return "Không tìm thấy vật tư.";
  if (error.kind === "invalid-response")
    return "Không xác thực được phản hồi máy chủ.";
  return "Dữ liệu kho hiện chưa khả dụng. Vui lòng thử lại.";
}

function Items({ page }: { page: InventoryPage }) {
  const pages = Math.max(1, Math.ceil(page.totalCount / page.pageSize));
  return (
    <>
      <p>
        {page.totalCount} vật tư · Trang {page.page} trên {pages}
      </p>
      {page.items.length === 0 ? (
        <p>Không tìm thấy vật tư trên trang này.</p>
      ) : (
        <div className="inventory-table">
          <table>
            <caption>Danh mục vật tư</caption>
            <thead>
              <tr>
                <th>Mã</th>
                <th>Tên</th>
                <th>Mã nhóm</th>
                <th>Mã đơn vị cơ sở</th>
                <th>Trạng thái</th>
                <th>Chi tiết</th>
              </tr>
            </thead>
            <tbody>
              {page.items.map((item) => (
                <tr key={item.itemId}>
                  <td>{item.itemCode}</td>
                  <td>{item.itemName}</td>
                  <td>{item.categoryId}</td>
                  <td>{item.baseUomId}</td>
                  <td>{item.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}</td>
                  <td>
                    <Link to={`/inventory/items/${item.itemId}`}>
                      Xem {item.itemCode}
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <nav aria-label="Phân trang vật tư">
        {page.page > 1 && <Link to={`?page=${page.page - 1}`}>Trước</Link>}
        {page.page < pages && <Link to={`?page=${page.page + 1}`}>Tiếp</Link>}
      </nav>
    </>
  );
}

function Item({ item }: { item: InventoryItem }) {
  return (
    <section>
      <h2>{item.itemName}</h2>
      <dl>
        <dt>Mã vật tư</dt>
        <dd>{item.itemId}</dd>
        <dt>Mã</dt>
        <dd>{item.itemCode}</dd>
        <dt>Mã nhóm</dt>
        <dd>{item.categoryId}</dd>
        <dt>Mã đơn vị cơ sở</dt>
        <dd>{item.baseUomId}</dd>
        <dt>Trạng thái</dt>
        <dd>{item.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}</dd>
        <dt>Vật tư tiêu hao</dt>
        <dd>{item.isConsumable ? "Có" : "Không"}</dd>
        <dt>Mô tả</dt>
        <dd>{item.description ?? "Chưa cung cấp"}</dd>
        <dt>Thông số</dt>
        <dd>{item.specification ?? "Chưa cung cấp"}</dd>
      </dl>
    </section>
  );
}
