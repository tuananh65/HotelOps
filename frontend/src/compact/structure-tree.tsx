import { useState } from "react";
import { ChevronDown, ChevronRight, BedDouble, Layers } from "lucide-react";
import type { Row } from "./api";

export default function StructureTree({ locations, rooms, onEdit, onRooms }: {
  locations: Row[]; rooms: Row[]; onEdit?: (row: Row) => void; onRooms: (id?: number) => void;
}) {
  const [expanded, setExpanded] = useState<Set<number>>(new Set());
  function node(location: Row, ancestors: Set<number>) {
    if (ancestors.has(location.locationId)) return null;
    const seen = new Set(ancestors).add(location.locationId);
    const children = locations.filter(l => l.parentLocationId === location.locationId);
    const ownRooms = rooms.filter(r => r.locationId === location.locationId);
    const open = expanded.has(location.locationId);
    return <li key={location.locationId}>
      <div className="structure-node">
        <button aria-expanded={open} onClick={() => setExpanded(previous => {
          const next = new Set(previous); next.has(location.locationId) ? next.delete(location.locationId) : next.add(location.locationId); return next;
        })}>{open ? <ChevronDown /> : <ChevronRight />}<Layers /><strong>{location.name}</strong><span>{ownRooms.length} phòng</span></button>
        <button onClick={() => onRooms(location.locationId)}>Xem phòng</button>
        {onEdit && <button onClick={() => onEdit(location)}>Chỉnh sửa</button>}
      </div>
      {open && <ul>{children.map(child => node(child, seen))}{ownRooms.map(room => <li key={`room-${room.roomId}`}><button className="structure-room" onClick={() => onRooms(location.locationId)}><BedDouble /> Phòng {room.code}</button></li>)}</ul>}
    </li>;
  }
  const roots = locations.filter(l => !l.parentLocationId || !locations.some(parent => parent.locationId === l.parentLocationId));
  return <div className="structure-tree"><ul aria-label="Sơ đồ tầng và phòng">{roots.map(location => node(location, new Set()))}</ul>
    {rooms.some(r => !locations.some(l => l.locationId === r.locationId)) && <button onClick={() => onRooms()}>Xem phòng chưa có tầng</button>}
  </div>;
}
