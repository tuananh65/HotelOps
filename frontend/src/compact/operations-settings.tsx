import { useState } from "react";
import Catalog from "./catalog";
import PageTitle from "./page-title";
import { useAuth } from "./auth";
import { Empty } from "./ui";
import "./operations.css";

// Catalog keeps the existing read/write grants and branch checks.
const sections = [
  {
    id: "locations",
    name: "Tầng / khu vực",
    mode: "hotels",
    permission: "ROOM_VIEW",
  },
  { id: "rooms", name: "Phòng", mode: "hotels", permission: "ROOM_VIEW" },
  {
    id: "room-types",
    name: "Loại phòng & bộ phận",
    mode: "settings",
    permission: "ROOM_VIEW",
  },
  {
    id: "warehouses",
    name: "Kho",
    mode: "supplies",
    permission: "INVENTORY_VIEW",
  },
  {
    id: "stock-policies",
    name: "Định mức vật tư",
    mode: "supplies",
    permission: "INVENTORY_VIEW",
  },
] as const;

export default function OperationsSettings() {
  const { branch, can } = useAuth();
  const available = sections.filter((s) => can(s.permission, false));
  const requested = new URLSearchParams(location.hash.split("?")[1]).get(
    "resource",
  );
  const [selected, select] = useState(
    requested === "departments" ? "room-types" : requested || "locations",
  );
  const section = available.find((s) => s.id === selected) || available[0];
  return (
    <>
      <PageTitle>
        <h1>Cấu hình vận hành</h1>
        <p>Danh mục trong chi nhánh hiện tại, theo quyền được cấp</p>
      </PageTitle>
      <nav className="operations-settings-tabs" aria-label="Danh mục vận hành">
        {available.map((s) => (
          <button
            key={s.id}
            aria-pressed={s.id === section?.id}
            onClick={() => select(s.id)}
          >
            {s.name}
          </button>
        ))}
      </nav>
      <p className="hint">
        Quyền xem danh mục không bao gồm quyền chỉnh sửa. Các nút thay đổi chỉ
        xuất hiện khi có quyền quản lý tương ứng.
      </p>
      {section ? (
        <Catalog
          key={`${branch}:${section.id}:${requested}`}
          mode={section.mode}
          embedded
          resourceOverride={
            section.id === "room-types" && requested === "departments"
              ? "departments"
              : section.id
          }
        />
      ) : (
        <Empty>Chưa được cấp quyền xem danh mục vận hành.</Empty>
      )}
    </>
  );
}
