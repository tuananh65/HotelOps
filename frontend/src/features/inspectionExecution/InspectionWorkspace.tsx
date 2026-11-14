import { useEffect, useRef, useState } from "react";
import { createInspectionApi, InspectionApiError } from "./api";
import {
  encodeEvidence,
  optionalNote,
  reason,
  type CriterionResult,
  type LoadedInspection,
  type SeverityOption,
} from "./model";
/** Direct-ID UI only; mounted by an explicit trusted DEV3 host only after separate approval. */
export function InspectionWorkspace({
  branch,
  id,
  onDenied,
  onSessionInvalid,
  severities,
}: {
  branch: number;
  id: number;
  onDenied: (s: 401 | 403) => void;
  onSessionInvalid?: () => void;
  severities?: SeverityOption[];
}) {
  const lifetime = useRef<AbortController | null>(null);
  const running = useRef<AbortController | null>(null);
  const [current, setCurrent] = useState<LoadedInspection | null>(null);
  const [busy, setBusy] = useState(false);
  const [reload, setReload] = useState(0);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [why, setWhy] = useState("");
  const [outcomes, setOutcomes] = useState<
    Record<number, "PASS" | "FAIL" | "">
  >({});
  const [notes, setNotes] = useState<Record<number, string>>({});
  const [selection, setSelection] = useState("");
  const [severity, setSeverity] = useState("");
  const [description, setDescription] = useState("");
  const [location, setLocation] = useState("");
  const [uploads, setUploads] = useState<File[]>([]);
  const [blocked, setBlocked] = useState(false);
  useEffect(() => {
    const scope = new AbortController();
    lifetime.current = scope;
    return () => {
      scope.abort();
      running.current?.abort();
      lifetime.current = null;
    };
  }, []);
  useEffect(() => {
    const controller = new AbortController();
    const scope = lifetime.current;
    if (!scope) return;
    const api = createInspectionApi({ signal: scope.signal, onSessionInvalid });
    setCurrent(null);
    setError("");
    setNotice("");
    setBlocked(false);
    setBusy(true);
    void api
      .read(branch, id, controller.signal)
      .then((result) => {
        if (scope.signal.aborted || controller.signal.aborted) return;
        setCurrent(result);
        setOutcomes(
          Object.fromEntries(
            result.value.criteria.map((c) => [
              c.itemId,
              c.outcome === "PASS" || c.outcome === "FAIL" ? c.outcome : "",
            ]),
          ),
        );
        setNotes(
          Object.fromEntries(
            result.value.criteria.map((c) => [c.itemId, c.note ?? ""]),
          ),
        );
      })
      .catch((e) => {
        if (!controller.signal.aborted && !scope.signal.aborted) handle(e);
      })
      .finally(() => {
        if (!scope.signal.aborted && !controller.signal.aborted) setBusy(false);
      });
    return () => controller.abort();
    // Admission identity is controlled by parent key; onSessionInvalid changes do not trigger a replay.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [branch, id, reload]);
  function handle(e: unknown) {
    if (e instanceof InspectionApiError) {
      if (e.status === 401 || e.status === 403) {
        onDenied(e.status);
        setCurrent(null);
        setBlocked(true);
      }
      if (e.uncertain || [409, 412, 428].includes(e.status)) {
        setBlocked(true);
        setCurrent(null);
      }
      setError(
        e.uncertain
          ? "Write outcome unknown. Verify server state in a fresh authorized read; do not automatically retry."
          : e.message,
      );
    } else if (e instanceof Error && e.name !== "AbortError")
      setError(e.message);
  }
  async function action(kind: "start" | "save" | "finish" | "defect") {
    if (
      !current ||
      busy ||
      running.current !== null ||
      blocked ||
      !lifetime.current ||
      lifetime.current.signal.aborted
    )
      return;
    const scope = lifetime.current,
      task = new AbortController();
    running.current = task;
    const api = createInspectionApi({ signal: scope.signal, onSessionInvalid });
    setBusy(true);
    setError("");
    setNotice("");
    try {
      let result: LoadedInspection;
      if (kind === "start")
        result = await api.start(
          branch,
          id,
          current.etag,
          reason(why),
          task.signal,
        );
      else if (kind === "finish")
        result = await api.finish(
          branch,
          id,
          current.etag,
          reason(why),
          task.signal,
        );
      else if (kind === "save") {
        const results: CriterionResult[] = current.value.criteria
          .filter(
            (c) =>
              outcomes[c.itemId] === "PASS" || outcomes[c.itemId] === "FAIL",
          )
          .map((c) => ({
            itemId: c.itemId,
            outcome: outcomes[c.itemId] as "PASS" | "FAIL",
            note: optionalNote(notes[c.itemId] ?? ""),
          }));
        result = await api.save(
          branch,
          id,
          current,
          results,
          reason(why),
          task.signal,
        );
      } else {
        const choice = Number(severity),
          itemId = Number(selection);
        if (!severities?.length)
          throw new InspectionApiError(
            403,
            "Authoritative severity catalog unavailable; defect submission disabled.",
          );
        const criterion = current.value.criteria.find(
          (c) => c.itemId === itemId,
        );
        if (!criterion || criterion.outcome !== "FAIL")
          throw new Error(
            "Record a FAILED criterion result before submitting its defect.",
          );
        const files = [];
        for (const file of uploads)
          files.push(await encodeEvidence(file, task.signal));
        result = await api.defect(
          branch,
          id,
          current.etag,
          severities,
          { itemId, severityId: choice, description, location, photos: files },
          task.signal,
        );
      }
      if (task.signal.aborted || scope.signal.aborted) return;
      setCurrent(result);
      setBlocked(false);
      setUploads([]);
      setNotice(
        kind === "finish"
          ? "Inspection sent to PENDING_CONCLUSION; not approved, PASSED or READY."
          : "Operation acknowledged by server.",
      );
    } catch (e) {
      if (!task.signal.aborted && !scope.signal.aborted) handle(e);
    } finally {
      if (running.current === task) running.current = null;
      if (!task.signal.aborted && !scope.signal.aborted) setBusy(false);
    }
  }
  return (
    <section aria-label="Inspection execution">
      <h2>Inspection #{id}</h2>
      <p>
        Branch #{branch}. Direct Inspection ID only; no directory or approval
        workflow.
      </p>
      {error && <p role="alert">{error}</p>}
      {notice && <p role="status">{notice}</p>}
      {blocked && (
        <p role="alert">
          This operation is blocked. Recheck the server before taking another
          action.
        </p>
      )}
      <button
        type="button"
        disabled={busy}
        onClick={() => {
          running.current?.abort();
          setReload((n) => n + 1);
        }}
      >
        Verify current state (GET)
      </button>
      {busy && <p role="status">Processing…</p>}
      {current && (
        <>
          <p>
            State: <strong>{current.value.state}</strong>. Assigned version #
            {current.value.versionId}; Room #{current.value.roomId}; Task #
            {current.value.cleaningTaskId}.
          </p>
          <label>
            Reason{" "}
            <input
              value={why}
              maxLength={500}
              onChange={(e) => setWhy(e.target.value)}
            />
          </label>
          <button
            type="button"
            disabled={busy || blocked || current.value.state !== "ASSIGNED"}
            onClick={() => void action("start")}
          >
            Start Inspection
          </button>
          <h3>Assigned checklist criteria</h3>
          {current.value.criteria.map((c) => (
            <fieldset
              key={c.itemId}
              disabled={busy || blocked || current.value.state !== "INSPECTING"}
            >
              <legend>
                #{c.itemId} {c.criterion} {c.mandatory ? "(required)" : ""}
              </legend>
              {c.description && <p>{c.description}</p>}
              <label>
                Result{" "}
                <select
                  value={outcomes[c.itemId] ?? ""}
                  onChange={(e) =>
                    setOutcomes((v) => ({
                      ...v,
                      [c.itemId]: e.target.value as "PASS" | "FAIL" | "",
                    }))
                  }
                >
                  <option value="">Not recorded</option>
                  <option value="PASS">PASS</option>
                  <option value="FAIL">FAIL</option>
                </select>
              </label>
              <label>
                Note{" "}
                <textarea
                  value={notes[c.itemId] ?? ""}
                  maxLength={4000}
                  onChange={(e) =>
                    setNotes((v) => ({ ...v, [c.itemId]: e.target.value }))
                  }
                />
              </label>
            </fieldset>
          ))}
          <button
            type="button"
            disabled={busy || blocked || current.value.state !== "INSPECTING"}
            onClick={() => void action("save")}
          >
            Save results
          </button>
          <button
            type="button"
            disabled={busy || blocked || current.value.state !== "INSPECTING"}
            onClick={() => void action("finish")}
          >
            Finish → PENDING_CONCLUSION
          </button>
          <h3>Defects</h3>
          {current.value.defects.map((d) => (
            <p key={d.id}>
              Defect #{d.id}: {d.description} — {d.status}
            </p>
          ))}
          {!severities?.length && (
            <p role="status">
              Authoritative severity IDs not available; defect submission is
              disabled. INSPECTION_PERFORM does not grant evidence read.
            </p>
          )}
          <fieldset
            disabled={
              busy ||
              blocked ||
              !severities?.length ||
              !(
                current.value.state === "INSPECTING" ||
                current.value.state === "PENDING_CONCLUSION"
              )
            }
          >
            <legend>
              Record defect (only for previously saved FAILED criterion)
            </legend>
            <label>
              Failed criterion{" "}
              <select
                value={selection}
                onChange={(e) => setSelection(e.target.value)}
              >
                <option value="">Select</option>
                {current.value.criteria
                  .filter((c) => c.outcome === "FAIL")
                  .map((c) => (
                    <option key={c.itemId} value={c.itemId}>
                      {c.criterion}
                    </option>
                  ))}
              </select>
            </label>
            <label>
              Severity{" "}
              <select
                value={severity}
                onChange={(e) => setSeverity(e.target.value)}
              >
                <option value="">Select</option>
                {severities
                  ?.filter(
                    (s) =>
                      s.id > 0 && (s.code === "MINOR" || s.code === "MAJOR"),
                  )
                  .map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name}
                    </option>
                  ))}
              </select>
            </label>
            <label>
              Description{" "}
              <textarea
                value={description}
                maxLength={4000}
                onChange={(e) => setDescription(e.target.value)}
              />
            </label>
            <label>
              Location{" "}
              <input
                value={location}
                maxLength={250}
                onChange={(e) => setLocation(e.target.value)}
              />
            </label>
            <label>
              Evidence (JPG, PNG or PDF, max 5 files / 10 MiB each){" "}
              <input
                type="file"
                multiple
                accept="image/png,image/jpeg,application/pdf"
                onChange={(e) => setUploads(Array.from(e.target.files ?? []))}
              />
            </label>
            <button
              type="button"
              disabled={busy || blocked || !severities?.length}
              onClick={() => void action("defect")}
            >
              Record defect
            </button>
          </fieldset>
          <p>
            Evidence read/download is not available in this source increment
            without separately verified INSPECTION_VIEW.
          </p>
        </>
      )}
    </section>
  );
}
