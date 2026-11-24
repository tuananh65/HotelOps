import { useState } from "react";
import { Building2 } from "lucide-react";
import type { Row } from "./api";
import { useCatalogRows } from "./use-catalog-rows";
import { State } from "./ui";
import StructureTree from "./structure-tree";

function BranchNode({ hotel }: { hotel: Row }) {
  const [open, setOpen] = useState(false);
  const locations = useCatalogRows(
    open && hotel.isActive ? "locations" : null,
    hotel.branchId,
  );
  const rooms = useCatalogRows(
    open && hotel.isActive ? "rooms" : null,
    hotel.branchId,
  );
  return (
    <li>
      <details onToggle={(e) => setOpen(e.currentTarget.open)}>
        <summary>
          <Building2 size={18} /> {hotel.name}{" "}
          {!hotel.isActive && <span className="badge neutral">Đã ngừng</span>}
        </summary>
        <div className="branch-tree-content">
          <p>
            {hotel.address || "Chưa cập nhật địa chỉ"} ·{" "}
            {hotel.phone || "Chưa cập nhật số điện thoại"}
          </p>
          <a className="button-link" href={`#catalog?hotel=${hotel.branchId}`}>
            Thông tin & chỉnh sửa chi nhánh
          </a>
          {open && hotel.isActive && (
            <State
              loading={locations.loading || rooms.loading}
              error={locations.error || rooms.error}
            >
              <StructureTree
                locations={locations.data || []}
                rooms={rooms.data || []}
                onRooms={(id) => {
                  location.hash = `catalog?hotel=${hotel.branchId}&resource=rooms&location=${id}`;
                }}
              />
            </State>
          )}
        </div>
      </details>
    </li>
  );
}

export default function BranchTree({ hotels }: { hotels: Row[] }) {
  return (
    <ul className="branch-tree" aria-label="Cấu trúc khách sạn">
      {hotels.map((hotel) => (
        <BranchNode key={hotel.branchId} hotel={hotel} />
      ))}
    </ul>
  );
}
