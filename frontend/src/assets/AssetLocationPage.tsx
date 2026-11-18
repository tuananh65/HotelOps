import { CollapsibleFilterPanel } from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Option = { id: number; name: string };
type Options = {
  branches: Option[];
  branch: number;
  floors: Option[];
  areas: Option[];
  rooms: Option[];
  departments: Option[];
  categories: Option[];
};
type Filters = {
  branch: string;
  floor: string;
  area: string;
  room: string;
  department: string;
  category: string;
  condition: string;
  lifecycle: string;
  groupBy: string;
};
type Group = {
  key: string;
  name: string;
  registeredCount: number;
  activeCount: number | null;
  retiredCount: number;
  states: { lifecycle: string; condition: string; count: number }[];
};
type Asset = {
  id: number;
  code: string;
  name: string;
  roomName: string | null;
  areaName: string | null;
  departmentName: string | null;
  categoryName: string;
  condition: string;
  lifecycle: string;
};
type Result = {
  snapshot: string;
  filters: Filters;
  pageSize: number;
  groupCount: number;
  groups: Group[];
  assetCount: number;
  assets: Asset[];
  missingSources: string[];
};
const initial: Filters = {
  branch: "",
  floor: "",
  area: "",
  room: "",
  department: "",
  category: "",
  condition: "",
  lifecycle: "",
  groupBy: "LOCATION",
};

export function AssetLocationPage() {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" && state.pending === null
      ? state.authorityGeneration
      : null;
  const [options, setOptions] = useState<Options | null>(null);
  const [filters, setFilters] = useState(initial);
  const [group, setGroup] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<Result | null>(null);
  const [error, setError] = useState("");
  const [generation, setGeneration] = useState(0);
  const [loadedOwner, setLoadedOwner] = useState(owner);
  useEffect(() => {
    const refresh = () => {
      setResult(null);
      setOptions(null);
      setGeneration((v) => v + 1);
    };
    window.addEventListener("focus", refresh);
    const timer = window.setInterval(refresh, 30000);
    return () => {
      window.removeEventListener("focus", refresh);
      window.clearInterval(timer);
    };
  }, []);
  useEffect(() => {
    setResult(null);
    setOptions(null);
    setError("");
    if (!owner) return;
    const controller = new AbortController();
    async function read<T>(path: string): Promise<T> {
      const response = await fetch(path, {
        signal: controller.signal,
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
      });
      if (response.status === 401) actions.invalidateSession(owner!);
      if (!response.ok)
        throw new Error(
          response.status === 403
            ? "ACCESS_DENIED — không còn quyền đọc trong phạm vi này."
            : `Không đọc được tài sản (${response.status}).`,
        );
      return (await response.json()) as T;
    }
    void (async () => {
      const catalog = await read<Options>(
        `/api/asset-location-review/options${filters.branch ? `?branch=${encodeURIComponent(filters.branch)}` : ""}`,
      );
      if (controller.signal.aborted) return;
      setLoadedOwner(owner);
      setOptions(catalog);
      if (!filters.branch) {
        setFilters((v) => ({ ...v, branch: String(catalog.branch) }));
        return;
      }
      const query = new URLSearchParams({ page: String(page) });
      for (const [key, value] of Object.entries(filters))
        if (value) query.set(key, value);
      if (group !== null) query.set("group", group);
      const data = await read<Result>(`/api/asset-location-review?${query}`);
      if (!controller.signal.aborted) setResult(data);
    })().catch((e) => {
      if (!controller.signal.aborted) {
        setResult(null);
        setOptions(null);
        setError(
          e instanceof Error ? e.message : "Nguồn dữ liệu không khả dụng.",
        );
      }
    });
    return () => controller.abort();
  }, [owner, actions, filters, group, page, generation]);
  function change(key: keyof Filters, value: string) {
    setResult(null);
    setGroup(null);
    setPage(1);
    setFilters((v) =>
      key === "branch"
        ? { ...initial, branch: value, groupBy: v.groupBy }
        : { ...v, [key]: value },
    );
  }
  if (!owner || owner !== loadedOwner) return <p>Đang kiểm tra quyền…</p>;
  function select(label: string, key: keyof Filters, choices: Option[]) {
    return (
      <label>
        {label}{" "}
        <select
          value={filters[key]}
          onChange={(e) => change(key, e.target.value)}
        >
          <option value="">
            {key === "branch" ? "Chọn chi nhánh" : "Tất cả"}
          </option>
          {choices.map((o) => (
            <option key={o.id} value={o.id}>
              {o.name}
            </option>
          ))}
        </select>{" "}
      </label>
    );
  }
  return (
    <section>
      <h2>Tài sản theo vị trí</h2>
      <p>
        <Link to="/">Trang chủ</Link>
      </p>
      {error && <p role="alert">{error}</p>}
      {options && (
        <CollapsibleFilterPanel
          activeCount={
            Object.entries(filters).filter(([k, v]) => k !== "groupBy" && !!v)
              .length
          }
        >
          <fieldset>
            <legend>Bộ lọc hiện tại</legend>
            {select("Chi nhánh", "branch", options.branches)}
            {select("Tầng", "floor", options.floors)}
            {select("Khu vực", "area", options.areas)}
            {select("Phòng", "room", options.rooms)}
            {select("Bộ phận giữ tài sản", "department", options.departments)}
            {select("Loại tài sản", "category", options.categories)}
            <label>
              Tình trạng{" "}
              <select
                value={filters.condition}
                onChange={(e) => change("condition", e.target.value)}
              >
                <option value="">Tất cả</option>
                {["GOOD", "FAIR", "POOR", "FAULTY"].map((s) => (
                  <option key={s} value={s}>
                    {displayCode(s)}
                  </option>
                ))}
              </select>
            </label>{" "}
            <label>
              Vòng đời{" "}
              <select
                value={filters.lifecycle}
                onChange={(e) => change("lifecycle", e.target.value)}
              >
                <option value="">Tất cả</option>
                {["IN_USE", "UNDER_REPAIR", "LOST", "RETIRED"].map((s) => (
                  <option key={s} value={s}>
                    {displayCode(s)}
                  </option>
                ))}
              </select>
            </label>{" "}
            <label>
              Nhóm theo{" "}
              <select
                value={filters.groupBy}
                onChange={(e) => change("groupBy", e.target.value)}
              >
                {Object.entries({
                  LOCATION: "Vị trí",
                  BRANCH: "Chi nhánh",
                  FLOOR: "Tầng",
                  AREA: "Khu vực",
                  ROOM: "Phòng",
                  DEPARTMENT: "Bộ phận",
                  CATEGORY: "Loại tài sản",
                  CONDITION: "Tình trạng",
                }).map(([key, name]) => (
                  <option key={key} value={key}>
                    {displayCode(name)}
                  </option>
                ))}
              </select>
            </label>
          </fieldset>
        </CollapsibleFilterPanel>
      )}
      {result && (
        <>
          <p>
            Bản chụp dữ liệu: {result.snapshot}. Thống kê theo vị trí hiện tại;
            không cộng thêm các lần di chuyển lịch sử.
          </p>
          {result.missingSources.includes("LOCATION_SCOPE_INCOMPLETE") && (
            <p role="status">
              Một số vị trí không có quan hệ phạm vi hợp lệ; không đưa vào kết
              quả. Thống kê này chưa đầy đủ.
            </p>
          )}
          {group !== null ? (
            <>
              <button
                onClick={() => {
                  setResult(null);
                  setGroup(null);
                  setPage(1);
                }}
              >
                Quay lại các nhóm
              </button>
              <h3>Nhóm: {group}</h3>
              <table>
                <thead>
                  <tr>
                    <th>Mã</th>
                    <th>Tài sản</th>
                    <th>Vị trí</th>
                    <th>Bộ phận</th>
                    <th>Loại</th>
                    <th>Tình trạng</th>
                    <th>Vòng đời</th>
                  </tr>
                </thead>
                <tbody>
                  {result.assets.map((a) => (
                    <tr key={a.id}>
                      <td>
                        <Link to={`/assets/${a.id}`}>{a.code}</Link>
                      </td>
                      <td>{a.name}</td>
                      <td>{a.roomName ?? a.areaName ?? "Chưa phân công"}</td>
                      <td>{a.departmentName ?? "N/A"}</td>
                      <td>{a.categoryName}</td>
                      <td>{a.condition}</td>
                      <td>{a.lifecycle}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {result.assets.length === 0 && (
                <p>MSG10 — Không có tài sản phù hợp.</p>
              )}
            </>
          ) : (
            <>
              <table>
                <thead>
                  <tr>
                    <th>Nhóm</th>
                    <th>Tổng bản ghi</th>
                    <th>Đang hoạt động</th>
                    <th>Đã ngừng sử dụng</th>
                    <th>Lifecycle / Tình trạng</th>
                  </tr>
                </thead>
                <tbody>
                  {result.groups.map((g) => (
                    <tr key={g.key}>
                      <td>
                        <button
                          onClick={() => {
                            setResult(null);
                            setGroup(g.key);
                            setPage(1);
                          }}
                        >
                          {g.name}
                        </button>
                      </td>
                      <td>{g.registeredCount}</td>
                      <td>{g.activeCount ?? "N/A"}</td>
                      <td>{g.retiredCount}</td>
                      <td>
                        {g.states.map((s) => (
                          <div key={`${s.lifecycle}-${s.condition}`}>
                            {s.lifecycle} / {s.condition}: {s.count}
                          </div>
                        ))}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {result.groups.length === 0 && (
                <p>MSG10 — Không có nhóm phù hợp.</p>
              )}
            </>
          )}
          <p>
            Trang {page};{" "}
            {group === null
              ? result.groupCount + " nhóm"
              : result.assetCount + " tài sản"}
            .
          </p>
          <button
            disabled={page === 1}
            onClick={() => {
              setResult(null);
              setPage((v) => v - 1);
            }}
          >
            Trước
          </button>{" "}
          <button
            disabled={
              page * result.pageSize >=
              (group === null ? result.groupCount : result.assetCount)
            }
            onClick={() => {
              setResult(null);
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
