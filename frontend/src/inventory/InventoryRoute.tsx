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
          Inventory data is hidden while your session is unconfirmed.
        </p>
        <button onClick={() => void actions.revalidate()}>Check session</button>
        <Link to="/profile">Profile</Link>
      </main>
    );
  if (blocked)
    return (
      <main className="inventory-page">
        <h1>Inventory session recovery</h1>
        <p role="alert">
          Inventory data has been cleared. Your session was checked; reload
          explicitly to try again.
        </p>
        <button
          onClick={() => {
            blockedRef.current = false;
            setBlocked(false);
          }}
        >
          Reload inventory
        </button>
        <Link to="/profile">Profile</Link>
      </main>
    );
  if (mode === "detail" && routeItemId(id) === undefined)
    return (
      <main className="inventory-page">
        <p role="alert">Invalid item ID.</p>
        <Link to="/inventory/items">Back to items</Link>
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
      <nav aria-label="Inventory navigation">
        <Link to="/dashboard">Workspace</Link>
        <Link to="/inventory/items">Items</Link> · <Link to="/inventory/catalog">Manage items</Link> · <Link to="/inventory/stock">Stock balance and ledger</Link> · <Link to="/inventory/units">Units and conversions</Link>
        <Link to="/profile">Profile</Link>
      </nav>
      <header>
        <p className="inventory-eyebrow">Inventory · Read only</p>
        <h1>{mode === "list" ? "Inventory items" : "Item detail"}</h1>
        <p>
          Browse the item catalogue. Stock balances are available separately.
        </p>
      </header>
      {!valid ? (
        <p role="alert">Invalid page number.</p>
      ) : !result ? (
        <p role="status">Loading items...</p>
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
              Retry
            </button>
          )}
        </section>
      ) : mode === "list" ? (
        <Items page={result.data as InventoryPage} />
      ) : (
        <Item item={result.data as InventoryItem} />
      )}
      {mode === "detail" && <Link to="/inventory/items">Back to items</Link>}
    </main>
  );
}

function errorMessage(error: { kind: string; status?: number }) {
  if (error.status === 401)
    return "Your session must be checked. Inventory data has been cleared.";
  if (error.status === 403)
    return "Access denied. Your account does not have access to this Inventory data.";
  if (error.status === 404) return "Item not found.";
  if (error.kind === "invalid-response")
    return "The server response could not be validated.";
  return "Inventory data is currently unavailable. Please try again.";
}

function Items({ page }: { page: InventoryPage }) {
  const pages = Math.max(1, Math.ceil(page.totalCount / page.pageSize));
  return (
    <>
      <p>
        {page.totalCount} items · Page {page.page} of {pages}
      </p>
      {page.items.length === 0 ? (
        <p>No items found on this page.</p>
      ) : (
        <div className="inventory-table">
          <table>
            <caption>Item catalogue</caption>
            <thead>
              <tr>
                <th>Code</th>
                <th>Name</th>
                <th>Category ID</th>
                <th>Base unit ID</th>
                <th>Status</th>
                <th>Details</th>
              </tr>
            </thead>
            <tbody>
              {page.items.map((item) => (
                <tr key={item.itemId}>
                  <td>{item.itemCode}</td>
                  <td>{item.itemName}</td>
                  <td>{item.categoryId}</td>
                  <td>{item.baseUomId}</td>
                  <td>{item.isActive ? "Active" : "Inactive"}</td>
                  <td>
                    <Link to={`/inventory/items/${item.itemId}`}>
                      View {item.itemCode}
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <nav aria-label="Items pagination">
        {page.page > 1 && <Link to={`?page=${page.page - 1}`}>Previous</Link>}
        {page.page < pages && <Link to={`?page=${page.page + 1}`}>Next</Link>}
      </nav>
    </>
  );
}

function Item({ item }: { item: InventoryItem }) {
  return (
    <section>
      <h2>{item.itemName}</h2>
      <dl>
        <dt>Item ID</dt>
        <dd>{item.itemId}</dd>
        <dt>Code</dt>
        <dd>{item.itemCode}</dd>
        <dt>Category ID</dt>
        <dd>{item.categoryId}</dd>
        <dt>Base unit ID</dt>
        <dd>{item.baseUomId}</dd>
        <dt>Status</dt>
        <dd>{item.isActive ? "Active" : "Inactive"}</dd>
        <dt>Consumable</dt>
        <dd>{item.isConsumable ? "Yes" : "No"}</dd>
        <dt>Description</dt>
        <dd>{item.description ?? "Not supplied"}</dd>
        <dt>Specification</dt>
        <dd>{item.specification ?? "Not supplied"}</dd>
      </dl>
    </section>
  );
}
