import { CollapsibleFilterPanel } from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useState } from "react";
import { Link, useParams, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Option = { id: number; name: string };
type Catalog = {
  branches: Option[];
  branch: number;
  rooms: Option[];
  areas: Option[];
  departments: Option[];
  categories: Option[];
};
type Asset = {
  assetId: number;
  assetCode: string;
  assetName: string;
  category: string;
  room: string | null;
  area: string | null;
  department: string | null;
  custodian: string | null;
  conditionStatus: string;
  lifecycleStatus: string;
};
type List = { items: Asset[]; total: number; pageSize: number };
type Profile = {
  profile: {
    assetId: number;
    assetCode: string;
    assetName: string;
    [key: string]: string | number | null;
  };
  canEdit: boolean;
  canRetire: boolean;
  snapshot: string;
  timeZone: string;
  pageSize: number;
  history: {
    auditId: number;
    eventType: string;
    occurredAt: string;
    fromLocationType: string | null;
    fromLocationId: number | null;
    toLocationType: string | null;
    toLocationId: number | null;
    fromDepartmentId: number | null;
    toDepartmentId: number | null;
    fromUserId: number | null;
    toUserId: number | null;
    status: string | null;
  }[];
  historyTotal: number;
  historyIncomplete: boolean;
  incidents: {
    incidentId: number;
    incidentNo: string;
    incidentTitle: string;
    status: string;
  }[];
  incidentTotal: number;
  incidentAccess: string;
  schedules: {
    scheduleId: number;
    scheduleCode: string;
    frequency: string;
    frequencyInterval: number;
    nextDueAt: string;
    isActive: boolean;
  }[];
  scheduleTotal: number;
  scheduleAccess: string;
  documents: {
    attachmentId: number;
    name: string;
    mimeType: string | null;
    fileSizeBytes: number | null;
    uploadedAt: string;
  }[];
  documentTotal: number;
};
const initial = {
  branchId: "",
  assetTypeId: "",
  roomId: "",
  areaId: "",
  departmentId: "",
  conditionStatus: "",
  lifecycleStatus: "",
  search: "",
};

export function AssetProfilePage() {
  const { id } = useParams();
  const location = useLocation();
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" && state.pending === null
      ? state.authorityGeneration
      : null;
  const [filters, setFilters] = useState(initial);
  const [catalog, setCatalog] = useState<Catalog | null>(null);
  const [list, setList] = useState<List | null>(null);
  const [profile, setProfile] = useState<Profile | null>(null);
  function time(value: string) {
    try {
      return (
        new Intl.DateTimeFormat("vi-VN", {
          timeZone: profile?.timeZone,
          dateStyle: "short",
          timeStyle: "medium",
        }).format(
          new Date(/[zZ]|[+-]\d{2}:\d{2}$/.test(value) ? value : value + "Z"),
        ) +
        " " +
        profile?.timeZone
      );
    } catch {
      return "Chưa có thông tin thời điểm / múi giờ";
    }
  }
  const [page, setPage] = useState(1);
  const [error, setError] = useState("");
  const [revision, setRevision] = useState(0);
  const [loadedOwner, setLoadedOwner] = useState(owner);
  useEffect(() => {
    setPage(1);
    setProfile(null);
    setList(null);
  }, [id]);
  useEffect(() => {
    const refresh = () => {
      setProfile(null);
      setList(null);
      setCatalog(null);
      setRevision((v) => v + 1);
    };
    const timer = window.setInterval(refresh, 30000);
    window.addEventListener("focus", refresh);
    return () => {
      window.clearInterval(timer);
      window.removeEventListener("focus", refresh);
    };
  }, []);
  useEffect(() => {
    setProfile(null);
    setList(null);
    setCatalog(null);
    setError("");
    if (!owner) return;
    const controller = new AbortController();
    async function read<T>(url: string): Promise<T> {
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
            ? "ACCESS_DENIED — không đủ quyền hiện hành."
            : `Không đọc được tài sản (${response.status}).`,
        );
      return (await response.json()) as T;
    }
    void (async () => {
      if (id) {
        if (!/^[1-9]\d*$/.test(id) || !Number.isSafeInteger(Number(id)))
          throw new Error("Mã tài sản không hợp lệ.");
        const data = await read<Profile>(
          `/api/asset-profiles/${id}?page=${page}`,
        );
        if (!controller.signal.aborted) {
          setProfile(data);
          setLoadedOwner(owner);
        }
      } else {
        const options = await read<Catalog>(
          `/api/asset-profiles/catalog${filters.branchId ? `?branch=${encodeURIComponent(filters.branchId)}` : ""}`,
        );
        if (controller.signal.aborted) return;
        setCatalog(options);
        setLoadedOwner(owner);
        if (!filters.branchId) {
          setFilters((v) => ({ ...v, branchId: String(options.branch) }));
          return;
        }
        const query = new URLSearchParams({ page: String(page) });
        for (const [key, value] of Object.entries(filters))
          if (value) query.set(key, value);
        const data = await read<List>(`/api/asset-profiles?${query}`);
        if (!controller.signal.aborted) setList(data);
      }
    })().catch((e) => {
      if (!controller.signal.aborted) {
        setList(null);
        setProfile(null);
        setCatalog(null);
        setError(e instanceof Error ? e.message : "Không đọc được dữ liệu.");
      }
    });
    return () => controller.abort();
  }, [id, owner, actions, filters, page, revision]);
  function change(key: keyof typeof filters, value: string) {
    setList(null);
    setPage(1);
    setFilters((v) =>
      key === "branchId"
        ? { ...initial, branchId: value }
        : { ...v, [key]: value },
    );
  }
  function select(label: string, key: keyof typeof filters, items: Option[]) {
    return (
      <label>
        {label}{" "}
        <select
          value={filters[key]}
          onChange={(e) => change(key, e.target.value)}
        >
          <option value="">Tất cả</option>
          {items.map((o) => (
            <option key={o.id} value={o.id}>
              {o.name}
            </option>
          ))}
        </select>{" "}
      </label>
    );
  }
  if (!owner || loadedOwner !== owner) return <p>Đang kiểm tra quyền…</p>;
  const total = profile
    ? Math.max(
        profile.historyTotal,
        profile.incidentTotal,
        profile.scheduleTotal,
        profile.documentTotal,
      )
    : (list?.total ?? 0);
  const size = profile?.pageSize ?? list?.pageSize ?? 20;
  return (
    <section>
      <h2>{id ? "Hồ sơ tài sản" : "Tài sản"}</h2>
      <p>
        <Link to="/">Trang chủ</Link>
        {" · "}
        <Link to="/assets">Danh sách tài sản</Link>
      </p>
      {!id && (
        <p>
          <Link to="/assets/new">Đăng ký tài sản</Link>
        </p>
      )}
      {error && <p role="alert">{error}</p>}
      {typeof location.state?.message === "string" && (
        <p role="status">{location.state.message}</p>
      )}
      {!id && catalog && (
        <>
          <label>
            Tìm kiếm{" "}
            <input
              maxLength={200}
              value={filters.search}
              onChange={(e) => change("search", e.target.value)}
            />
          </label>
          <CollapsibleFilterPanel
            activeCount={
              Object.entries(filters).filter(([k, v]) => k !== "search" && !!v)
                .length
            }
          >
            <fieldset>
              <legend>Bộ lọc</legend>
              {select("Chi nhánh", "branchId", catalog.branches)}
              {select("Loại", "assetTypeId", catalog.categories)}
              {select("Phòng", "roomId", catalog.rooms)}
              {select("Khu vực", "areaId", catalog.areas)}
              {select(
                "Bộ phận giữ tài sản",
                "departmentId",
                catalog.departments,
              )}
              <label>
                Tình trạng{" "}
                <select
                  value={filters.conditionStatus}
                  onChange={(e) => change("conditionStatus", e.target.value)}
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
                  value={filters.lifecycleStatus}
                  onChange={(e) => change("lifecycleStatus", e.target.value)}
                >
                  <option value="">Tất cả</option>
                  {["IN_USE", "UNDER_REPAIR", "LOST", "RETIRED"].map((s) => (
                    <option key={s} value={s}>
                      {displayCode(s)}
                    </option>
                  ))}
                </select>
              </label>{" "}
            </fieldset>
          </CollapsibleFilterPanel>
        </>
      )}
      {list && (
        <>
          <table>
            <thead>
              <tr>
                <th>Mã / Tên</th>
                <th>Vị trí</th>
                <th>Bộ phận / Người giữ</th>
                <th>Tình trạng</th>
                <th>Vòng đời</th>
              </tr>
            </thead>
            <tbody>
              {list.items.map((a) => (
                <tr key={a.assetId}>
                  <td>
                    <Link to={`/assets/${a.assetId}`}>
                      {a.assetCode} — {a.assetName}
                    </Link>
                  </td>
                  <td>{a.room ?? a.area ?? "Chưa phân công"}</td>
                  <td>
                    {a.department ?? "N/A"} / {a.custodian ?? "N/A"}
                  </td>
                  <td>{displayCode(a.conditionStatus)}</td>
                  <td>{displayCode(a.lifecycleStatus)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {list.items.length === 0 && (
            <p>MSG10 — Không có tài sản phù hợp bộ lọc.</p>
          )}
        </>
      )}
      {profile && (
        <>
          <h3>
            {profile.profile.assetCode} — {profile.profile.assetName}
          </h3>
          <p>Bản chụp dữ liệu: {time(profile.snapshot)}</p>
          <p>
            <Link to={`/assets/${id}/condition`}>Cập nhật tình trạng</Link>
          </p>
          <p>
            <Link to={`/assets/${id}/transfer`}>Điều chuyển vị trí</Link>
          </p>
          <p>
            <Link to={`/assets/${id}/transfer/cross-branch`}>
              Bàn giao liên chi nhánh
            </Link>
          </p>
          <p>
            <Link to={`/assets/${id}/custody`}>
              Bàn giao trách nhiệm quản lý tài sản
            </Link>
          </p>
          {profile.canEdit && (
            <p>
              <Link to={`/assets/${id}/placement`}>
                Phân công vị trí ban đầu
              </Link>
            </p>
          )}
          {profile.canEdit && (
            <p>
              <Link to={`/assets/${id}/edit`}>Chỉnh sửa thông tin</Link>
            </p>
          )}
          {profile.canRetire && (
            <p>
              <Link to={`/assets/${id}/retire`}>Kiểm tra điều kiện Retire</Link>
            </p>
          )}
          <dl>
            {Object.entries({
              "Chi nhánh": "branchName",
              "Loại tài sản": "assetTypeName",
              Phòng: "roomNumber",
              "Khu vực": "areaName",
              "Bộ phận giữ": "departmentName",
              "Người giữ": "custodianName",
              "Tình trạng": "conditionStatus",
              Lifecycle: "lifecycleStatus",
              Serial: "serialNumber",
              Hãng: "brand",
              Model: "model",
              "Ngày mua": "purchaseDate",
              "Hết bảo hành": "warrantyExpiryDate",
              "Mô tả": "description",
            }).map(([label, key]) => (
              <div key={key}>
                <dt>{label}</dt>
                <dd>{profile.profile[key] ?? "N/A"}</dd>
              </div>
            ))}
          </dl>
          <h4>Di chuyển / Bàn giao</h4>
          {profile.historyIncomplete && (
            <p>
              N/A: có lịch sử thiếu bằng chứng hoặc không đủ quyền ở hai đầu
              giao dịch.
            </p>
          )}
          {profile.history.map((h) => (
            <p key={h.auditId}>
              {time(h.occurredAt)} · {h.eventType} · {displayCode(h.status)} ·{" "}
              {h.fromLocationType} {h.fromLocationId} → {h.toLocationType}{" "}
              {h.toLocationId}; bộ phận {h.fromDepartmentId ?? "N/A"} →{" "}
              {h.toDepartmentId ?? "N/A"}; người giữ {h.fromUserId ?? "N/A"} →{" "}
              {h.toUserId ?? "N/A"}
            </p>
          ))}
          {profile.history.length === 0 && (
            <p>Không có lịch sử khả dụng trên trang này.</p>
          )}
          <h4>Sự cố đang mở</h4>
          {profile.incidentAccess === "ACCESS_DENIED" ? (
            <p>ACCESS_DENIED — cần quyền đọc Incident hiện hành.</p>
          ) : profile.incidents.length ? (
            profile.incidents.map((i) => (
              <p key={i.incidentId}>
                {i.incidentNo}: {i.incidentTitle} — {displayCode(i.status)}
              </p>
            ))
          ) : (
            <p>Không có sự cố khả dụng trên trang này.</p>
          )}
          <h4>Lịch bảo trì</h4>
          {profile.scheduleAccess === "ACCESS_DENIED" ? (
            <p>ACCESS_DENIED — cần quyền đọc lịch bảo trì hiện hành.</p>
          ) : profile.schedules.length ? (
            profile.schedules.map((s) => (
              <p key={s.scheduleId}>
                {s.scheduleCode} · {s.frequencyInterval} {s.frequency} ·{" "}
                {time(s.nextDueAt)} ·{" "}
                {s.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}
              </p>
            ))
          ) : (
            <p>Không có lịch trên trang này.</p>
          )}
          <h4>Tài liệu</h4>
          {profile.documents.map((d) => (
            <p key={d.attachmentId}>
              <a
                href={`/api/asset-profiles/${id}/files/${d.attachmentId}`}
                download
              >
                {d.name}
              </a>{" "}
              · {d.mimeType} · {d.fileSizeBytes ?? "N/A"} bytes
            </p>
          ))}
          {profile.documents.length === 0 && (
            <p>Không có tài liệu trên trang này.</p>
          )}
        </>
      )}
      {(list || profile) && (
        <p>
          Trang {page}{" "}
          <button
            disabled={page === 1}
            onClick={() => {
              setList(null);
              setProfile(null);
              setPage((v) => v - 1);
            }}
          >
            Trước
          </button>{" "}
          <button
            disabled={page * size >= total}
            onClick={() => {
              setList(null);
              setProfile(null);
              setPage((v) => v + 1);
            }}
          >
            Sau
          </button>
        </p>
      )}
    </section>
  );
}
