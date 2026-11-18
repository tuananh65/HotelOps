import {
  Building2,
  RefreshCw,
  ChevronRight,
  ChevronLeft,
  Search,
  FolderOpen,
} from "lucide-react";
import { AppButton } from "../components/design-system";
import { CollapsibleFilterPanel } from "../components/workspace-interactions";
import { LoadingIndicator } from "../components/LoadingIndicator";
import {
  StructureDetails,
  StructureManagementLinks,
  StructureStatus,
  structureIcons,
} from "./structure-presentation";
import "./hotel-structure.css";
import { useEffect, useRef, useState } from "react";
import { Navigate, useLocation, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { PublicSession } from "../auth/authStore";
import { decimalId, type StructureDto } from "./contracts";
import { useHotelFlow } from "./useHotelFlow";
import {
  boundStructure,
  collectionsFor,
  defaultCollection,
  type StructureNode,
  type StructureQuery,
  type StructureCollection,
} from "./structureView";
type Trail = { type: StructureNode; id: number; label: string };
type Request = { owner: PublicSession; query: StructureQuery; trail: Trail[] };
const heading = {
  floors: "Tầng",
  areas: "Khu vực",
  rooms: "Phòng",
  departments: "Bộ phận",
};
const singular = {
  floors: "floor",
  areas: "area",
  rooms: "room",
  departments: "department",
} as const;
export function BranchStructureTree() {
  const { state, actions } = useAuth();
  const params = useParams(),
    location = useLocation();
  const owner =
    state.status === "AUTHENTICATED" && state.pending === null
      ? state.session
      : null;
  const [branch, setBranch] = useState(
    params.branchId ??
      new URLSearchParams(location.search).get("branchId") ??
      "",
  );
  const [active, setActive] = useState("");
  const [request, setRequest] = useState<Request | null>(null);
  const [branchOptions, setBranchOptions] = useState<{
    owner: PublicSession;
    items: { id: number; code: string; name: string; isActive: boolean }[];
  } | null>(null);
  const [branchError, setBranchError] = useState("");
  const [optionsReload, setOptionsReload] = useState(0);
  useEffect(() => {
    setBranchOptions(null);
    setBranchError("");
    if (!owner || owner.mustChangePassword) return;
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch("/api/branches/structure-options", {
          credentials: "include",
          cache: "no-store",
          redirect: "error",
          signal: abort.signal,
          headers: { Accept: "application/json" },
        });
        if (
          !response.ok ||
          response.headers.get("Content-Type")?.split(";")[0] !==
            "application/json"
        )
          throw new Error();
        const items: unknown = await response.json();
        if (
          !Array.isArray(items) ||
          !items.every(
            (item) =>
              item &&
              typeof item === "object" &&
              Number.isSafeInteger(item.id) &&
              item.id > 0 &&
              typeof item.code === "string" &&
              typeof item.name === "string" &&
              typeof item.isActive === "boolean",
          ) ||
          new Set(items.map((item) => item.id)).size !== items.length
        )
          throw new Error();
        if (!abort.signal.aborted) setBranchOptions({ owner, items });
      } catch {
        if (!abort.signal.aborted)
          setBranchError(
            "Không tải được chi nhánh được phép truy cập. Tải lại hoặc kiểm tra phiên đăng nhập.",
          );
      }
    })();
    return () => abort.abort();
  }, [owner, optionsReload]);
  const key = JSON.stringify([branch, active, request?.query]);
  const flow = useHotelFlow(key);
  const load = useRef(flow.load);
  load.current = flow.load;
  useEffect(() => {
    if (request && request.owner === owner)
      void load.current(
        "structure",
        { branchId: request.query.branchId },
        { ...request.query },
      );
  }, [request, owner]);
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate replace to="/login" />;
  if (state.status === "RESTRICTED")
    return <Navigate replace to="/change-password" />;
  if (!owner || state.session?.mustChangePassword)
    return (
      <main>
        <p role="status">Chưa xác minh được phiên. Dữ liệu cấu trúc đã ẩn.</p>
        {state.status !== "INITIALIZING" && (
          <button onClick={() => void actions.revalidate()}>
            Kiểm tra phiên đăng nhập
          </button>
        )}
      </main>
    );
  const selected = request?.owner === owner ? request : null;
  const raw = selected && (flow.snapshot?.data as StructureDto | null);
  const data =
    raw && selected && boundStructure(raw, selected.query) ? raw : null;
  const page = data?.collection
    ? data[data.collection as StructureCollection]
    : null;
  function root() {
    const id = decimalId(branch);
    if (!id) return;
    setRequest({
      owner: owner!,
      query: {
        branchId: id,
        nodeType: "branch",
        collection: "floors",
        isActive: active,
        page: 1,
        pageSize: 20,
      },
      trail: [],
    });
  }
  function select(type: StructureNode, id: number, label: string) {
    if (!selected) return;
    setRequest({
      owner: owner!,
      query: {
        ...selected.query,
        nodeType: type,
        nodeId: id,
        collection: defaultCollection(type),
        page: 1,
      },
      trail: [...selected.trail, { type, id, label }],
    });
  }
  function ancestor(index: number) {
    if (!selected) return;
    if (index < 0) {
      setRequest({
        ...selected,
        query: {
          ...selected.query,
          nodeType: "branch",
          nodeId: undefined,
          collection: "floors",
          page: 1,
        },
        trail: [],
      });
      return;
    }
    const node = selected.trail[index];
    setRequest({
      ...selected,
      query: {
        ...selected.query,
        nodeType: node.type,
        nodeId: node.id,
        collection: defaultCollection(node.type),
        page: 1,
      },
      trail: selected.trail.slice(0, index + 1),
    });
  }
  const branchReady = branchOptions?.owner === owner;
  const validBranch =
    branchReady &&
    branchOptions.items.some((item) => String(item.id) === branch);
  const reading = flow.phase === "reading";
  const collection = data?.collection as StructureCollection | undefined;
  const canManage = data?.visibility === "management";
  const activeLabel =
    active === ""
      ? "Tất cả trạng thái"
      : active === "true"
        ? "Đang hoạt động"
        : "Ngừng hoạt động";
  return (
    <main className="hotel-structure-workspace">
      <header className="hotel-structure-heading">
        <div>
          <span className="hotel-structure-eyebrow">Quản lý khách sạn</span>
          <h1>Cấu trúc khách sạn</h1>
          <p>Xem tầng, khu vực, phòng và bộ phận trong từng chi nhánh.</p>
        </div>
      </header>
      <section
        className="hotel-structure-card hotel-structure-selection"
        aria-label="Chọn chi nhánh"
      >
        <form
          className="hotel-structure-branch-form"
          onSubmit={(event) => {
            event.preventDefault();
            root();
          }}
        >
          <label htmlFor="structure-branch">
            Chi nhánh
            <select
              id="structure-branch"
              aria-label="Chi nhánh"
              name="branchId"
              value={branch}
              disabled={!branchReady}
              onChange={(event) => {
                setBranch(event.target.value);
                setRequest(null);
              }}
            >
              <option value="">Chọn chi nhánh</option>
              {branchReady &&
                branchOptions.items.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.code} — {item.name}
                    {item.isActive ? "" : " (Ngừng hoạt động)"}
                  </option>
                ))}
            </select>
          </label>
          <AppButton
            type="submit"
            variant="primary"
            icon={Search}
            disabled={!validBranch || reading}
          >
            Xem cấu trúc
          </AppButton>
          <AppButton
            icon={RefreshCw}
            onClick={() => setOptionsReload((value) => value + 1)}
            disabled={!branchReady && !branchError}
          >
            Tải lại chi nhánh
          </AppButton>
        </form>
        {!branchReady && !branchError && (
          <LoadingIndicator compact label="Đang tải chi nhánh" />
        )}
        {branchError && (
          <p className="hotel-structure-feedback" role="alert">
            {branchError}
          </p>
        )}
        {branchReady && branchOptions.items.length === 0 && (
          <p className="hotel-structure-muted">
            Tài khoản chưa có chi nhánh để xem.
          </p>
        )}
        <CollapsibleFilterPanel activeCount={Number(active !== "")}>
          <label className="hotel-structure-filter">
            Trạng thái hoạt động
            <select
              aria-label="Bộ lọc hoạt động"
              value={active}
              onChange={(event) => {
                setActive(event.target.value);
                if (selected)
                  setRequest({
                    ...selected,
                    query: {
                      ...selected.query,
                      isActive: event.target.value,
                      page: 1,
                    },
                  });
              }}
            >
              <option value="">Tất cả</option>
              <option value="true">Đang hoạt động</option>
              <option value="false">Ngừng hoạt động</option>
            </select>
          </label>
        </CollapsibleFilterPanel>
      </section>
      {reading && <LoadingIndicator label="Đang tải cấu trúc khách sạn" />}
      {flow.message && (
        <p className="hotel-structure-feedback" role="alert">
          {flow.message}
        </p>
      )}
      {raw && !data && (
        <p className="hotel-structure-feedback" role="alert">
          Chưa xác nhận được dữ liệu của mục đang chọn. Vui lòng chọn lại chi
          nhánh và xem cấu trúc.
        </p>
      )}
      {!selected && !reading && !flow.message && (
        <div className="hotel-structure-card hotel-structure-empty">
          <Building2 size={36} aria-hidden="true" />
          <h2>Chọn chi nhánh để bắt đầu</h2>
          <p>Chọn chi nhánh ở trên rồi bấm Xem cấu trúc.</p>
        </div>
      )}
      {data && selected && (
        <section
          className="hotel-structure-result"
          aria-label="Cấu trúc chi nhánh theo từng cấp"
        >
          <div className="hotel-structure-context">
            <nav
              aria-label="Đường dẫn cấu trúc"
              className="hotel-structure-trail"
            >
              <AppButton
                variant="ghost"
                icon={Building2}
                onClick={() => ancestor(-1)}
              >
                {data.branch.name}
              </AppButton>
              {selected.trail.map((node, index) => (
                <span key={node.type + node.id}>
                  <ChevronRight size={14} aria-hidden="true" />
                  <AppButton
                    variant="ghost"
                    aria-current={
                      index === selected.trail.length - 1
                        ? "location"
                        : undefined
                    }
                    onClick={() => ancestor(index)}
                  >
                    {node.label}
                  </AppButton>
                </span>
              ))}
            </nav>
            <p
              className="hotel-structure-muted"
              aria-label="Bộ lọc cấu trúc đã áp dụng"
            >
              {activeLabel}
            </p>
          </div>
          {canManage && <StructureManagementLinks branchId={data.branch.id} />}
          <div
            className={page ? "hotel-structure-layout" : "hotel-structure-leaf"}
          >
            {page && collection && (
              <section
                className="hotel-structure-card hotel-structure-list"
                aria-label={heading[collection] + " Trang"}
              >
                <nav
                  className="hotel-structure-tabs"
                  aria-label="Danh mục cấu trúc"
                >
                  {collectionsFor(data.nodeType as StructureNode)
                    .filter((item) => data.availableCollections.includes(item))
                    .map((item) => {
                      const Icon = structureIcons[singular[item]];
                      return (
                        <AppButton
                          key={item}
                          variant="ghost"
                          icon={Icon}
                          aria-pressed={collection === item}
                          onClick={() =>
                            setRequest({
                              ...selected,
                              query: {
                                ...selected.query,
                                collection: item,
                                page: 1,
                              },
                            })
                          }
                        >
                          {heading[item]}
                        </AppButton>
                      );
                    })}
                </nav>
                <div className="hotel-structure-list-heading">
                  <h2>{heading[collection]}</h2>
                  <span>{page.totalCount} mục</span>
                </div>
                {page.items.length === 0 ? (
                  <div className="hotel-structure-empty">
                    <FolderOpen size={32} aria-hidden="true" />
                    <h3>Không có mục phù hợp</h3>
                    <p>Thử đổi trạng thái hoặc chọn mục khác trong cấu trúc.</p>
                  </div>
                ) : (
                  <ul
                    aria-label="Các mục cấu trúc"
                    className="hotel-structure-items"
                  >
                    {page.items.map((item) => {
                      const label =
                        "roomNumber" in item
                          ? "Phòng " + item.roomNumber
                          : item.name;
                      const Icon = structureIcons[singular[collection]];
                      return (
                        <li key={item.id}>
                          <button
                            className="hotel-structure-node"
                            aria-label={"Xem " + label}
                            onClick={() =>
                              select(singular[collection], item.id, label)
                            }
                          >
                            <span className="hotel-structure-icon">
                              <Icon size={20} aria-hidden="true" />
                            </span>
                            <span className="hotel-structure-node-name">
                              <strong>{label}</strong>
                              {"code" in item && <small>{item.code}</small>}
                            </span>
                            <StructureStatus active={item.isActive} />
                            <ChevronRight
                              className="hotel-structure-node-arrow"
                              size={18}
                              aria-hidden="true"
                            />
                          </button>
                        </li>
                      );
                    })}
                  </ul>
                )}
                <nav
                  aria-label="Phân trang cấu trúc"
                  className="hotel-structure-pagination"
                >
                  <AppButton
                    icon={ChevronLeft}
                    disabled={page.page <= 1}
                    onClick={() =>
                      setRequest({
                        ...selected,
                        query: { ...selected.query, page: page.page - 1 },
                      })
                    }
                  >
                    Trang trước
                  </AppButton>
                  <span>
                    Trang {page.page} /{" "}
                    {Math.max(1, Math.ceil(page.totalCount / page.pageSize))}
                  </span>
                  <AppButton
                    icon={ChevronRight}
                    disabled={page.page * page.pageSize >= page.totalCount}
                    onClick={() =>
                      setRequest({
                        ...selected,
                        query: { ...selected.query, page: page.page + 1 },
                      })
                    }
                  >
                    Trang sau
                  </AppButton>
                </nav>
              </section>
            )}
            <StructureDetails data={data} />
          </div>
        </section>
      )}
    </main>
  );
}
