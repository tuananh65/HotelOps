import { useState } from "react";
import { Link } from "react-router-dom";
import { parseId } from "./api";
import {
  reads,
  type Assigned,
  type Task,
  type Page,
  type Planning,
} from "./reads";
import { useOperation } from "./shared";
export function InspectionDirectory() {
  const { run, busy, error, setError } = useOperation();
  const [branch, setBranch] = useState(""),
    [search, setSearch] = useState(""),
    [kind, setKind] = useState<"assigned" | "tasks">("assigned");
  const [snapshot, setSnapshot] = useState<{
    branch: number;
    search: string;
    kind: "assigned" | "tasks";
    page: Page<Assigned | Task>;
  } | null>(null);
  const [planning, setPlanning] = useState<Planning | null>(null);
  const [selected, setSelected] = useState("");
  function load(page = 1) {
    setPlanning(null);
    setSelected("");
    setSnapshot(null);
    try {
      const id = parseId(branch);
      void run(
        (s) => reads.list(id, kind, search, page, s),
        (p) => setSnapshot({ branch: id, kind, search, page: p }),
      );
    } catch {
      setError("Enter a valid branch ID.");
    }
  }
  return (
    <section>
      <h2>Inspection work lists</h2>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          load();
        }}
      >
        <label>
          List branch ID
          <input
            required
            disabled={busy}
            value={branch}
            onChange={(e) => {
              setBranch(e.target.value);
              setSnapshot(null);
              setPlanning(null);
            }}
          />
        </label>
        <label>
          Inspection list
          <select
            disabled={busy}
            aria-label="Inspection list"
            value={kind}
            onChange={(e) => {
              setKind(e.target.value as "assigned" | "tasks");
              setSnapshot(null);
              setPlanning(null);
            }}
          >
            <option value="assigned">Assigned to me</option>
            <option value="tasks">Awaiting assignment — read only</option>
          </select>
        </label>
        <label>
          List search
          <input
            maxLength={150}
            disabled={busy}
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setSnapshot(null);
              setPlanning(null);
            }}
          />
        </label>
        <button disabled={busy}>Load inspections</button>
      </form>
      {busy && <p role="status">Loading inspection work list…</p>}
      {error && <p role="alert">{error}</p>}
      {snapshot && (
        <>
          <p>
            {snapshot.page.total} records · Page {snapshot.page.page}
          </p>
          {!snapshot.page.items.length && (
            <p>No inspections or tasks match this list.</p>
          )}
          <ul>
            {snapshot.page.items.map((i) => (
              <li key={i.id}>
                {i.number} · Room {i.roomNumber} · {i.state}{" "}
                {"inspectorId" in i ? (
                  <>
                    <span>
                      Inspector {i.inspectorName} · Checklist version #
                      {i.versionId} · Due {i.dueAt ?? "Not set"}
                    </span>{" "}
                    <Link to={`/inspections/${i.branchId}/${i.id}/execution`}>
                      Open {i.number}
                    </Link>
                  </>
                ) : (
                  <>
                    <span>
                      {i.pendingReinspection
                        ? "Pending reinspection cycle"
                        : ""}
                    </span>
                    <button
                      disabled={busy}
                      onClick={() => {
                        setPlanning(null);
                        setSelected("");
                        void run(
                          (s) => reads.planning(snapshot.branch, i.id, s),
                          setPlanning,
                        );
                      }}
                    >
                      Plan {i.number}
                    </button>
                  </>
                )}
              </li>
            ))}
          </ul>
          <button
            disabled={busy || snapshot.page.page <= 1}
            onClick={() => load(snapshot.page.page - 1)}
          >
            Previous list page
          </button>
          <button
            disabled={
              busy ||
              snapshot.page.page * snapshot.page.pageSize >= snapshot.page.total
            }
            onClick={() => load(snapshot.page.page + 1)}
          >
            Next list page
          </button>
          <button disabled={busy} onClick={() => load(snapshot.page.page)}>
            Refresh list
          </button>
        </>
      )}
      {planning && (
        <section>
          <h3>Assignment planning — task #{planning.task.id}</h3>
          <p>
            Source captured at {planning.capturedAt}. Workflow verified:{" "}
            {String(planning.workflowStateVerified)}. This read is not
            assignment authority.
          </p>
          <p>
            {planning.checklist
              ? `${planning.checklist.name} · version ${planning.checklist.versionNumber} (#${planning.checklist.versionId})`
              : "No eligible checklist returned."}
          </p>
          <label>
            Inspector candidate
            <select
              value={selected}
              onChange={(e) => setSelected(e.target.value)}
            >
              <option value="">Choose candidate for preview</option>
              {planning.candidates.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name} · open tasks {c.openTasks}
                </option>
              ))}
            </select>
          </label>
          <p>
            {planning.total} candidates; showing page {planning.filters.page}.
            Raw scheduled shift records are not verified work-time attendance.
          </p>
          {selected &&
            planning.recordedShifts
              .filter((s) => s.userId === Number(selected))
              .map((s) => (
                <p key={`${s.id}-${s.userId}`}>
                  {s.from} – {s.to} · {s.recordedShiftStatus} /{" "}
                  {s.recordedAssignmentStatus}
                </p>
              ))}
          <p role="status">
            Assignment submission unavailable: mandatory notification
            integration is NOT_INTEGRATED in the existing writer. No assignment
            request is sent.
          </p>
        </section>
      )}
    </section>
  );
}
