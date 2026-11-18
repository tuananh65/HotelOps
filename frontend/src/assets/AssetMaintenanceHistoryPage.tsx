import { CollapsibleFilterPanel } from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Branch = { branchId: number; branchName: string };
type Asset = { assetId: number; assetCode: string; assetName: string };
type Repair = {
  maintenanceWorkId: number;
  workNo: string;
  status: string;
  actualStartAt: string | null;
  resolvedAt: string | null;
  resolutionNote: string | null;
  currentAssignee: string | null;
  technicians: string[];
};
type Material = {
  usageId: number;
  itemCode: string;
  itemName: string;
  quantity: number | string;
  unit: string;
  technician: string;
  at: string;
};
type Row = {
  type: string;
  id: number;
  at: string;
  number: string;
  title: string | null;
  status: string | null;
  technician: string | null;
  result: string | null;
  repairs: Repair[];
  materials: Material[];
};
type History = {
  asset: Asset;
  snapshot: string;
  timeZone: string;
  filters: { from: string; to: string };
  type: string | null;
  page: number;
  pageSize: number;
  totalCount: number;
  items: Row[];
  missingSources: string[];
  totals: {
    incidentCount: number | null;
    recurrences: number | null;
    averageResolutionHours: number | null;
    lastPreventiveAt: string | null;
    resolutionPopulation: string;
    preventivePopulation: string;
  };
};

export function AssetMaintenanceHistoryPage() {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" && state.pending === null
      ? state.authorityGeneration
      : null;
  const [params, setParams] = useSearchParams();
  const asset = params.get("asset");
  const [branches, setBranches] = useState<Branch[]>([]);
  const [branch, setBranch] = useState("");
  const [search, setSearch] = useState("");
  const [after, setAfter] = useState(0);
  const [assets, setAssets] = useState<Asset[]>([]);
  const [more, setMore] = useState(false);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [type, setType] = useState("");
  const [page, setPage] = useState(1);
  const [history, setHistory] = useState<History | null>(null);
  const date = (value: string | null) => {
    if (!value || !history) return "Chưa có thông tin";
    try {
      const instant = new Date(
        /[zZ]|[+-]\d{2}:\d{2}$/.test(value) ? value : value + "Z",
      );
      return (
        new Intl.DateTimeFormat("vi-VN", {
          timeZone: history.timeZone,
          dateStyle: "short",
          timeStyle: "medium",
        }).format(instant) +
        " " +
        history.timeZone
      );
    } catch {
      return "Chưa có thông tin múi giờ / thời điểm";
    }
  };
  const [error, setError] = useState("");
  const [refresh, setRefresh] = useState(0);
  const [loadedOwner, setLoadedOwner] = useState(owner);
  useEffect(() => {
    const reload = () => {
      setHistory(null);
      setAssets([]);
      setBranches([]);
      setRefresh((v) => v + 1);
    };
    window.addEventListener("focus", reload);
    const timer = window.setInterval(reload, 30000);
    return () => {
      window.removeEventListener("focus", reload);
      window.clearInterval(timer);
    };
  }, []);
  useEffect(() => {
    setHistory(null);
    setAssets([]);
    setBranches([]);
    setError("");
    if (!owner) return;
    const controller = new AbortController();
    async function get<T>(url: string): Promise<T> {
      const response = await fetch(url, {
        signal: controller.signal,
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
      });
      if (response.status === 401) actions.invalidateSession(owner!);
      if (!response.ok)
        throw new Error(
          response.status === 403
            ? "ACCESS_DENIED: thiếu quyền đọc tài sản hoặc nguồn bảo trì trong chi nhánh."
            : `Không đọc được lịch sử (${response.status}).`,
        );
      return (await response.json()) as T;
    }
    void (async () => {
      const options = await get<Branch[]>(
        "/api/asset-maintenance-history/options",
      );
      if (controller.signal.aborted) return;
      setBranches(options);
      setLoadedOwner(owner);
      if (!branch && options[0]) {
        setBranch(String(options[0].branchId));
        return;
      }
      if (branch && !options.some((b) => String(b.branchId) === branch))
        throw new Error("ACCESS_DENIED: chi nhánh không còn khả dụng.");
      if (branch) {
        const list = await get<{ items: Asset[]; hasMore: boolean }>(
          `/api/asset-maintenance-history/assets?${new URLSearchParams({ branch, search, after: String(after) })}`,
        );
        if (controller.signal.aborted) return;
        setAssets(list.items);
        setMore(list.hasMore);
      }
      if (asset) {
        if (!/^[1-9]\d*$/.test(asset) || !Number.isSafeInteger(Number(asset)))
          throw new Error("Mã tài sản không hợp lệ.");
        const query = new URLSearchParams({ page: String(page) });
        if (from) query.set("from", from);
        if (to) query.set("to", to);
        if (type) query.set("type", type);
        const data = await get<History>(
          `/api/asset-maintenance-history/${asset}?${query}`,
        );
        if (controller.signal.aborted) return;
        setHistory(data);
      }
    })().catch((e) => {
      if (!controller.signal.aborted) {
        setHistory(null);
        setAssets([]);
        setError(e instanceof Error ? e.message : "Không đọc được dữ liệu.");
      }
    });
    return () => controller.abort();
  }, [
    owner,
    actions,
    branch,
    search,
    after,
    asset,
    from,
    to,
    type,
    page,
    refresh,
  ]);
  if (!owner || owner !== loadedOwner) return <p>Đang kiểm tra quyền…</p>;
  return (
    <section>
      <h2>Lịch sử bảo trì tài sản</h2>
      <p>
        <Link to="/">Trang chủ</Link>
      </p>
      {error && <p role="alert">{error}</p>}
      <label>
        Chi nhánh{" "}
        <select
          value={branch}
          onChange={(e) => {
            setBranch(e.target.value);
            setAfter(0);
            setPage(1);
            setHistory(null);
            setParams({});
          }}
        >
          <option value="">Chọn chi nhánh</option>
          {branches.map((b) => (
            <option key={b.branchId} value={b.branchId}>
              {displayCode(b.branchName)}
            </option>
          ))}
        </select>
      </label>{" "}
      <label>
        Tìm tài sản{" "}
        <input
          value={search}
          maxLength={200}
          onChange={(e) => {
            setSearch(e.target.value);
            setAfter(0);
          }}
        />
      </label>
      <ul>
        {assets.map((a) => (
          <li key={a.assetId}>
            <button
              onClick={() => {
                setParams({ asset: String(a.assetId) });
                setPage(1);
                setHistory(null);
              }}
            >
              {a.assetCode} — {a.assetName}
            </button>
          </li>
        ))}
      </ul>
      {after > 0 && (
        <button onClick={() => setAfter(0)}>Đầu danh sách tài sản</button>
      )}
      {more && assets.length > 0 && (
        <button onClick={() => setAfter(assets[assets.length - 1].assetId)}>
          Tài sản tiếp theo
        </button>
      )}
      <CollapsibleFilterPanel
        activeCount={[from, to, type].filter(Boolean).length}
      >
        <fieldset>
          <legend>Bộ lọc lịch sử (UTC)</legend>
          <label>
            Từ{" "}
            <input
              type="date"
              value={from}
              onChange={(e) => {
                setFrom(e.target.value);
                setPage(1);
                setHistory(null);
              }}
            />
          </label>{" "}
          <label>
            Đến{" "}
            <input
              type="date"
              value={to}
              onChange={(e) => {
                setTo(e.target.value);
                setPage(1);
                setHistory(null);
              }}
            />
          </label>{" "}
          <label>
            Loại{" "}
            <select
              value={type}
              onChange={(e) => {
                setType(e.target.value);
                setPage(1);
                setHistory(null);
              }}
            >
              <option value="">Tất cả</option>
              <option value="INCIDENT">Sự cố / sửa chữa</option>
              <option value="PREVENTIVE">Bảo trì định kỳ</option>
            </select>
          </label>
        </fieldset>
      </CollapsibleFilterPanel>
      {history && (
        <>
          <h3>
            <Link to={`/assets/${history.asset.assetId}`}>
              {history.asset.assetCode} — {history.asset.assetName}
            </Link>
          </h3>
          <p>
            Bản chụp dữ liệu: {date(history.snapshot)}. Kỳ:{" "}
            {history.filters.from} → {history.filters.to} UTC.
          </p>
          <dl>
            <dt>Số sự cố</dt>
            <dd>{history.totals.incidentCount ?? "N/A"}</dd>
            <dt>Số lần tái mở</dt>
            <dd>{history.totals.recurrences ?? "N/A"}</dd>
            <dt>Thời gian giải quyết trung bình (giờ)</dt>
            <dd>{history.totals.averageResolutionHours ?? "N/A"}</dd>
            <dt>Bảo trì định kỳ gần nhất trong tập lọc</dt>
            <dd>{date(history.totals.lastPreventiveAt)}</dd>
          </dl>
          <p>
            {history.totals.resolutionPopulation}{" "}
            {history.totals.preventivePopulation}
          </p>
          {history.missingSources.length > 0 && (
            <p role="status">
              Dữ liệu chưa đủ: {history.missingSources.join(", ")}
            </p>
          )}
          {history.items.length === 0 && (
            <p>MSG10 — Không có bản ghi phù hợp bộ lọc.</p>
          )}
          {history.items.map((row) => (
            <article key={`${row.type}-${row.id}`}>
              <h4>
                {row.number}: {row.title ?? "N/A"}
              </h4>
              <p>
                {displayCode(row.type)} · {date(row.at)} ·{" "}
                {displayCode(row.status ?? "N/A")} · {row.technician ?? "N/A"} ·{" "}
                {row.result ?? "N/A"}
              </p>
              {row.repairs.map((r) => (
                <div key={r.maintenanceWorkId}>
                  <strong>{r.workNo}</strong> — {displayCode(r.status)}
                  <p>
                    Được giao hiện tại: {r.currentAssignee ?? "N/A"}. Người thực
                    hiện có work log: {r.technicians.join(", ") || "N/A"}.
                  </p>
                  <p>
                    {date(r.actualStartAt)} → {date(r.resolvedAt)}.{" "}
                    {r.resolutionNote}
                  </p>
                </div>
              ))}
              <h5>Vật tư đã ghi nhận sử dụng</h5>
              {row.materials.length === 0 ? (
                <p>Chưa có bằng chứng tiêu hao hợp lệ.</p>
              ) : (
                <ul>
                  {row.materials.map((m) => (
                    <li key={m.usageId}>
                      {m.itemCode} — {m.itemName}: {m.quantity} {m.unit};{" "}
                      {m.technician}; {date(m.at)}
                    </li>
                  ))}
                </ul>
              )}
            </article>
          ))}
          <p>
            {history.totalCount} bản ghi. Trang {history.page}.
          </p>
          <button
            disabled={page <= 1}
            onClick={() => {
              setHistory(null);
              setPage((v) => v - 1);
            }}
          >
            Trước
          </button>{" "}
          <button
            disabled={page * history.pageSize >= history.totalCount}
            onClick={() => {
              setHistory(null);
              setPage((v) => v + 1);
            }}
          >
            Sau
          </button>
        </>
      )}
    </section>
  );
}
