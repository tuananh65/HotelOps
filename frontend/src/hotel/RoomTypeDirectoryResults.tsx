import { Link } from "react-router-dom";
import type { ReadTypes } from "./contracts";

export function RoomTypeDirectoryResults({ data }: { data: ReadTypes["roomTypes"] }) {
  return <section aria-label="Room type directory results">
    <p aria-label="Applied room type filters">
      Branch: {data.branchId ?? "All authorized branches"}; Status: {data.isActive === null ? "All" : data.isActive ? "Active" : "Inactive"};
      Search: {data.search || "None"}; Sort: {data.sort} ({data.descending ? "descending" : "ascending"}).
    </p>
    <p>Page {data.page}; {data.pageSize} per page; {data.total} matching room types.</p>
    {data.items.length === 0 ? <p>MSG10: No room types match the applied filters on this page.</p> :
      <table>
        <caption>Room types returned by the server</caption>
        <thead><tr>{["Code", "Name", "Branch", "Active rooms", "Status", "Details"].map(label => <th scope="col" key={label}>{label}</th>)}</tr></thead>
        <tbody>{data.items.map(({ details: t, activeRooms }) => <tr key={`${t.branchId}:${t.id}`}>
          <th scope="row">{t.code}</th><td>{t.name}</td><td>{t.branchId ?? "Global"}</td>
          <td>{activeRooms}</td><td>{t.isActive ? "Active" : "Inactive"}</td>
          <td>{t.branchId !== null ? <Link to={`/structure/room-types/${t.id}?branchId=${t.branchId}`}>Open {t.name} ({t.code})</Link> :
            <details><summary>View global room type</summary>
              <p>{t.description ?? "No description"}</p>
              <p>Standard cleaning minutes: {t.standardCleaningMinutes ?? "Unavailable"}</p>
              <p>Standard capacity: {t.standardCapacity ?? "Unavailable"}</p>
              <p>Default checklist: {t.defaultChecklistId ?? "None"}</p>
            </details>}</td>
        </tr>)}</tbody>
      </table>}
  </section>;
}
