import { displayCode } from "../i18n/vi";
// Unmounted historical business harness; canonical shared read-only route is unchanged.
import { useEffect, useRef, useState } from "react";
import { Link, Navigate, useLocation, useParams } from "react-router-dom";
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
export function StandaloneBranchStructureTree() {
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
        <p role="status">
          Chưa xác minh được phiên. Dữ liệu cấu trúc đã ẩn.
        </p>
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
  const details =
    data?.nodeType === "branch"
      ? data.branch
      : data?.nodeType === "floor"
        ? data.selectedFloor
        : data?.nodeType === "area"
          ? data.selectedArea
          : data?.nodeType === "room"
            ? data.selectedRoom
            : data?.selectedDepartment;
  return (
    <main className="hotel-workspace">
      <h1>Cấu trúc khách sạn — Cấu trúc chi nhánh</h1>
      <nav aria-label="Điều hướng cấu trúc khách sạn">
        <Link to="/profile">Hồ sơ cá nhân</Link>
        <Link to="/branches/new">Tạo chi nhánh</Link>
        {decimalId(branch) && (
          <Link to={`/branches/${decimalId(branch)}/edit`}>
            Cập nhật thông tin chi nhánh
          </Link>
        )}
        <Link to="/floors">Tầng</Link>
        <Link to="/areas">Khu vực</Link>
        <Link to="/rooms">Phòng</Link>
        <Link to="/room-types">Loại phòng</Link>
      </nav>
      <p>
        Xem từng trang dữ liệu được máy chủ cho phép. Số đếm không có nghĩa đã tải toàn bộ mục con.
      </p>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          root();
        }}
      >
        <label>
          Mã chi nhánh
          <input
            name="branchId"
            inputMode="numeric"
            value={branch}
            onChange={(event) => {
              setBranch(event.target.value);
              setRequest(null);
            }}
          />
        </label>
        <button disabled={!decimalId(branch) || flow.phase === "reading"}>
          Tải dữ liệu
        </button>
      </form>
      <label>
        Bộ lọc hoạt động
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
      {flow.phase === "reading" && <p role="status">Đang tải dữ liệu từ máy chủ...</p>}
      {flow.message && <p role="alert">{flow.message}</p>}
      {raw && !data && (
        <p role="alert">
          Phản hồi cấu trúc không khớp ngữ cảnh đã chọn. Không hiển thị dữ liệu.
        </p>
      )}
      {data && selected && (
        <section aria-label="Cấu trúc chi nhánh theo từng cấp">
          <nav aria-label="Đường dẫn cấu trúc">
            <button onClick={() => ancestor(-1)}>
              Chi nhánh: {data.branch.name}
            </button>
            {selected.trail.map((node, index) => (
              <button key={node.type + node.id} onClick={() => ancestor(index)}>
                {displayCode(node.type)}: {node.label}
              </button>
            ))}
          </nav>
          <section aria-label="Chi tiết mục đã chọn">
            <h2>Đã chọn {data.nodeType}</h2>
            <dl>
              {Object.entries(details ?? {}).map(([field, value]) => (
                <div key={field}>
                  <dt>{field}</dt>
                  <dd>{value === null ? "Chưa cung cấp" : String(value)}</dd>
                </div>
              ))}
            </dl>
          </section>
          <nav aria-label="Bộ sưu tập khả dụng">
            {collectionsFor(data.nodeType as StructureNode)
              .filter((collection) =>
                data.availableCollections.includes(collection),
              )
              .map((collection) => (
                <button
                  key={collection}
                  aria-pressed={data.collection === collection}
                  onClick={() =>
                    setRequest({
                      ...selected,
                      query: { ...selected.query, collection, page: 1 },
                    })
                  }
                >
                  {heading[collection]}
                </button>
              ))}
          </nav>
          {page && data.collection && (
            <section
              aria-label={`${heading[data.collection as StructureCollection]} Trang`}
            >
              <h2>{heading[data.collection as StructureCollection]}</h2>
              <p>
                {page.totalCount} hồ sơ; trang {page.page} trên{" "}
                {Math.max(1, Math.ceil(page.totalCount / page.pageSize))}; Số dòng mỗi trang {page.pageSize}.
              </p>
              {page.items.length === 0 ? (
                <p>Không có hồ sơ trên trang này.</p>
              ) : (
                <ul aria-label="Các mục cấu trúc">
                  {page.items.map((item) => {
                    const label =
                      "roomNumber" in item ? item.roomNumber : item.name;
                    return (
                      <li key={item.id}>
                        <button
                          onClick={() =>
                            select(
                              singular[data.collection as StructureCollection],
                              item.id,
                              label,
                            )
                          }
                        >
                          {singular[data.collection as StructureCollection]}:{" "}
                          {label}
                        </button>
                        <span> — {item.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}</span>
                      </li>
                    );
                  })}
                </ul>
              )}
              <button
                disabled={page.page <= 1}
                onClick={() =>
                  setRequest({
                    ...selected,
                    query: { ...selected.query, page: page.page - 1 },
                  })
                }
              >
                Trang trước
              </button>
              <button
                disabled={page.page * page.pageSize >= page.totalCount}
                onClick={() =>
                  setRequest({
                    ...selected,
                    query: { ...selected.query, page: page.page + 1 },
                  })
                }
              >
                Trang sau
              </button>
            </section>
          )}
        </section>
      )}
    </main>
  );
}
