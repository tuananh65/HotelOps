import { Link } from "react-router-dom";
import type { ReadTypes } from "./contracts";

/** Read navigation only; the target API independently checks access. */
export function RoomDirectoryResults({ data }: { data: ReadTypes["rooms"] }) {
  return (
    <section aria-label="Room directory results">
      <p>
        Page {data.page}. {data.totalCount} rooms reported by the server.
      </p>
      {data.items.length === 0 ? (
        <p>No rooms on this page.</p>
      ) : (
        <table>
          <caption>Rooms returned by the server</caption>
          <thead>
            <tr>
              <th scope="col">Room</th>
              <th scope="col">Branch</th>
              <th scope="col">Floor</th>
              <th scope="col">Area</th>
              <th scope="col">Room type</th>
              <th scope="col">Stored status</th>
              <th scope="col">Active</th>
              <th scope="col">Read actions</th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((room) => (
              <tr key={`${room.branchId}:${room.id}`}>
                <th scope="row">{room.roomNumber}</th>
                <td>{room.branchId}</td>
                <td>{room.floor.name}</td>
                <td>{room.area?.name ?? "No area"}</td>
                <td>{room.roomType.name}</td>
                <td>{room.operationalStatusCode}</td>
                <td>{room.isActive ? "Yes" : "No"}</td>
                <td>
                  <Link
                    to={`/rooms/${room.id}?branchId=${room.branchId}`}
                    aria-label={`Read room ${room.roomNumber} in branch ${room.branchId}`}
                  >
                    Read details
                  </Link>{" "}
                  <Link
                    to={`/rooms/${room.id}?branchId=${room.branchId}&view=history`}
                    aria-label={`Read history for room ${room.roomNumber} in branch ${room.branchId}`}
                  >
                    Recorded status history
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <p>
        Read access does not establish permission to update a room. Status
        history contains recorded events only.
      </p>
    </section>
  );
}
