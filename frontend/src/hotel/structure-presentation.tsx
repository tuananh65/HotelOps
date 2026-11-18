import { Link } from "react-router-dom";
import {
  Building2,
  Layers3,
  MapPin,
  BedDouble,
  Users,
  Settings2,
  Plus,
  ArrowUpRight,
} from "lucide-react";
import type { StructureDto } from "./contracts";
import type { StructureNode } from "./structureView";

export const structureNames = {
  branch: "Chi nhánh",
  floor: "Tầng",
  area: "Khu vực",
  room: "Phòng",
  department: "Bộ phận",
};
export const structureIcons = {
  branch: Building2,
  floor: Layers3,
  area: MapPin,
  room: BedDouble,
  department: Users,
};

export function StructureStatus({ active }: { active: boolean }) {
  return (
    <span className={`hotel-structure-status${active ? " is-active" : ""}`}>
      <span aria-hidden="true" />
      {active ? "Đang hoạt động" : "Ngừng hoạt động"}
    </span>
  );
}

export function StructureDetails({ data }: { data: StructureDto }) {
  const type = data.nodeType as StructureNode;
  const item =
    type === "branch"
      ? data.branch
      : type === "floor"
        ? data.selectedFloor
        : type === "area"
          ? data.selectedArea
          : type === "room"
            ? data.selectedRoom
            : data.selectedDepartment;
  if (!item) return null;
  const Icon = structureIcons[type] ?? Building2;
  return (
    <aside
      className="hotel-structure-card hotel-structure-details"
      aria-label="Chi tiết mục đã chọn"
    >
      <div className="hotel-structure-detail-title">
        <span className="hotel-structure-icon">
          <Icon size={23} aria-hidden="true" />
        </span>
        <div>
          <p>Đang xem</p>
          <h2>{structureNames[type]}</h2>
        </div>
      </div>
      <dl>
        {"code" in item && (
          <div>
            <dt>Mã {structureNames[type].toLocaleLowerCase("vi")}</dt>
            <dd>{item.code}</dd>
          </div>
        )}
        <div>
          <dt>
            {type === "room"
              ? "Số phòng"
              : "Tên " + structureNames[type].toLocaleLowerCase("vi")}
          </dt>
          <dd>{"roomNumber" in item ? item.roomNumber : item.name}</dd>
        </div>
        {"floorNumber" in item && typeof item.floorNumber === "number" && (
          <div>
            <dt>Số tầng</dt>
            <dd>{item.floorNumber}</dd>
          </div>
        )}
        {"description" in item &&
          typeof item.description === "string" &&
          item.description !== "" && (
            <div>
              <dt>Mô tả</dt>
              <dd>{item.description}</dd>
            </div>
          )}
        <div>
          <dt>Trạng thái</dt>
          <dd>
            <StructureStatus active={item.isActive} />
          </dd>
        </div>
      </dl>
      {data.visibility === "management" &&
        (type === "branch" || type === "floor" || type === "area") && (
          <Link
            className="ds-button ds-button--secondary hotel-structure-detail-link"
            to={
              type === "branch"
                ? `/structure/branches/${data.branch.id}/edit`
                : `/structure/${type}s/${item.id}?branchId=${data.branch.id}`
            }
          >
            <Settings2 size={16} aria-hidden="true" />
            {type === "branch"
              ? "Cập nhật thông tin chi nhánh"
              : `Quản lý ${structureNames[type].toLocaleLowerCase("vi")}`}
          </Link>
        )}
    </aside>
  );
}

export function StructureManagementLinks({ branchId }: { branchId: number }) {
  const groups = [
    { path: "floors", label: "Tầng", create: "Thêm tầng", Icon: Layers3 },
    { path: "areas", label: "Khu vực", create: "Thêm khu vực", Icon: MapPin },
    {
      path: "departments",
      label: "Bộ phận",
      create: "Thêm bộ phận",
      Icon: Users,
    },
    { path: "rooms", label: "Phòng", create: "Thêm phòng", Icon: BedDouble },
    { path: "room-types", label: "Loại phòng", create: null, Icon: Building2 },
  ];
  return (
    <details className="hotel-structure-management">
      <summary>
        <Settings2 size={17} aria-hidden="true" />
        Quản lý cấu trúc
      </summary>
      <nav aria-label="Điều hướng cấu trúc khách sạn">
        {groups.map(({ path, label, create, Icon }) => (
          <div key={path}>
            <h3>
              <Icon size={18} aria-hidden="true" />
              {label}
            </h3>
            <Link to={`/structure/${path}?branchId=${branchId}`}>
              Quản lý {label.toLocaleLowerCase("vi")}
              <ArrowUpRight size={15} aria-hidden="true" />
            </Link>
            {create && (
              <Link
                to={`/structure/${path}?branchId=${branchId}&action=create`}
              >
                <Plus size={15} aria-hidden="true" />
                {create}
              </Link>
            )}
          </div>
        ))}
      </nav>
    </details>
  );
}
