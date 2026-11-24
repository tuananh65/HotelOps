export type Field = {
  key: string;
  label: string;
  required?: boolean;
  type?: "number" | "date" | "checkbox";
  choices?: [string, string][];
  lookup?: string;
  max?: number;
  default?: string | number;
  filter?: string;
};
export type Catalog = {
  id: string;
  name: string;
  key: string;
  read: string;
  write: string;
  scoped: boolean;
  fields: Field[];
};
const code: Field = { key: "code", label: "Mã", required: true, max: 30 };
const name: Field = { key: "name", label: "Tên", required: true, max: 150 };
const active: Field = {
  key: "isActive",
  label: "Đang hoạt động",
  type: "checkbox",
};
const department: Field = {
  key: "departmentId",
  label: "Bộ phận",
  lookup: "departments",
};
const location: Field = {
  key: "locationId",
  label: "Tầng / khu vực",
  lookup: "locations",
};
export const catalogs: Catalog[] = [
  {
    id: "references",
    name: "Danh mục dùng chung",
    key: "referenceId",
    read: "MASTER_DATA_MANAGE",
    write: "MASTER_DATA_MANAGE",
    scoped: false,
    fields: [
      {
        key: "kind",
        label: "Loại danh mục",
        required: true,
        choices: [
          ["WORK_CATEGORY", "Loại công việc"],
          ["ASSET_TYPE", "Loại tài sản"],
          ["ITEM_CATEGORY", "Nhóm vật tư"],
          ["UOM", "Đơn vị tính"],
        ],
      },
      code,
      name,
      { key: "sortOrder", label: "Thứ tự", type: "number", default: 0 },
      active,
    ],
  },
  {
    id: "branches",
    name: "Chi nhánh",
    key: "branchId",
    read: "MASTER_DATA_MANAGE",
    write: "MASTER_DATA_MANAGE",
    scoped: false,
    fields: [
      code,
      name,
      { key: "address", label: "Địa chỉ", max: 500 },
      { key: "phone", label: "Điện thoại", max: 30 },
      { key: "representativeName", label: "Người đại diện", max: 150 },
      {
        key: "timezone",
        label: "Múi giờ",
        required: true,
        default: "Asia/Ho_Chi_Minh",
        max: 80,
      },
      active,
    ],
  },
  {
    id: "departments",
    name: "Bộ phận",
    key: "departmentId",
    read: "ROOM_VIEW",
    write: "MASTER_DATA_MANAGE",
    scoped: true,
    fields: [code, name, { key: "description", label: "Mô tả", max: 2000 }, active],
  },
  {
    id: "locations",
    name: "Tầng / khu vực",
    key: "locationId",
    read: "ROOM_VIEW",
    write: "MASTER_DATA_MANAGE",
    scoped: true,
    fields: [
      code,
      name,
      {
        key: "kind",
        label: "Loại vị trí",
        required: true,
        choices: [
          ["FLOOR", "Tầng"],
          ["AREA", "Khu vực"],
        ],
      },
      { key: "parentLocationId", label: "Vị trí cha", lookup: "locations" },
      { key: "floorNumber", label: "Số tầng", type: "number" },
      active,
    ],
  },
  {
    id: "room-types",
    name: "Loại phòng",
    key: "roomTypeId",
    read: "ROOM_VIEW",
    write: "MASTER_DATA_MANAGE",
    scoped: true,
    fields: [
      code,
      name,
      {
        key: "capacity",
        label: "Sức chứa",
        type: "number",
        required: true,
        default: 2,
      },
      { key: "description", label: "Mô tả", max: 2000 },
      active,
    ],
  },
  {
    id: "rooms",
    name: "Danh mục phòng",
    key: "roomId",
    read: "ROOM_VIEW",
    write: "MASTER_DATA_MANAGE",
    scoped: true,
    fields: [
      code,
      {
        key: "roomTypeId",
        label: "Loại phòng",
        required: true,
        lookup: "room-types",
      },
      location,
      department,
      active,
    ],
  },
  {
    id: "warehouses",
    name: "Kho",
    key: "warehouseId",
    read: "INVENTORY_VIEW",
    write: "ITEM_MASTER_MANAGE",
    scoped: true,
    fields: [code, name, department, active],
  },
  {
    id: "items",
    name: "Vật tư",
    key: "itemId",
    read: "INVENTORY_VIEW",
    write: "ITEM_MASTER_MANAGE",
    scoped: false,
    fields: [
      code,
      name,
      {
        key: "categoryId",
        label: "Nhóm vật tư",
        lookup: "references",
        filter: "ITEM_CATEGORY",
      },
      {
        key: "baseUnitId",
        label: "Đơn vị cơ sở",
        required: true,
        lookup: "references",
        filter: "UOM",
      },
      { key: "description", label: "Mô tả", max: 2000 },
      active,
    ],
  },
  {
    id: "stock-policies",
    name: "Định mức kho",
    key: "inventoryPolicyId",
    read: "INVENTORY_VIEW",
    write: "ITEM_MASTER_MANAGE",
    scoped: true,
    fields: [
      {
        key: "warehouseId",
        label: "Kho",
        required: true,
        lookup: "warehouses",
      },
      { key: "itemId", label: "Vật tư", required: true, lookup: "items" },
      {
        key: "minimumQty",
        label: "Tồn tối thiểu",
        required: true,
        type: "number",
        default: 0,
      },
      {
        key: "targetQty",
        label: "Tồn mục tiêu",
        required: true,
        type: "number",
        default: 0,
      },
    ],
  },
  {
    id: "assets",
    name: "Danh mục tài sản",
    key: "assetId",
    read: "ASSET_VIEW",
    write: "ASSET_MANAGE",
    scoped: true,
    fields: [
      { key: "assetCode", label: "Mã tài sản", required: true, max: 60 },
      {
        key: "conditionCode",
        label: "Tình trạng ban đầu",
        required: true,
        default: "GOOD",
        choices: [
          ["GOOD", "Tốt"],
          ["FAIR", "Bình thường"],
          ["POOR", "Kém"],
          ["FAULTY", "Hỏng"],
          ["UNDER_REPAIR", "Đang sửa chữa"],
          ["LOST", "Thất lạc"],
        ],
      },
      name,
      {
        key: "assetTypeId",
        label: "Loại tài sản",
        required: true,
        lookup: "references",
        filter: "ASSET_TYPE",
      },
      { key: "roomId", label: "Phòng", lookup: "rooms" },
      department,
      { key: "serialNumber", label: "Số sê-ri", max: 100 },
      { key: "purchasedOn", label: "Ngày mua", type: "date" },
      { key: "warrantyUntil", label: "Bảo hành đến", type: "date" },
    ],
  },
  {
    id: "roles",
    name: "Vai trò",
    key: "roleId",
    read: "ROLE_MANAGE",
    write: "ROLE_MANAGE",
    scoped: false,
    fields: [code, name, { key: "description", label: "Mô tả", max: 2000 }],
  },
  {
    id: "permissions",
    name: "Quyền hệ thống",
    key: "permissionId",
    read: "ROLE_MANAGE",
    write: "ROLE_MANAGE",
    scoped: false,
    fields: [
      code,
      { key: "moduleCode", label: "Nhóm" },
      { key: "description", label: "Mô tả" },
      active,
    ],
  },
];
