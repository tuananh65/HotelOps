import { useEffect, useRef, useState } from "react";
import { useOutletContext } from "react-router-dom";
import {
  createInspectionApi,
  InspectionError,
  type Criterion,
  type Defect,
  type Evidence,
} from "./api";
export const api = createInspectionApi();
export function useOperation() {
  const { sessionFailure } = useOutletContext<{ sessionFailure: () => void }>();
  const [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  const active = useRef<AbortController | null>(null);
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      active.current?.abort();
    };
  }, []);
  async function run<T>(
    work: (signal: AbortSignal) => Promise<T>,
    done: (v: T) => void,
    failed?: (e: unknown) => void,
  ) {
    if (active.current) return;
    const c = new AbortController();
    active.current = c;
    setBusy(true);
    setError("");
    try {
      const value = await work(c.signal);
      if (mounted.current && !c.signal.aborted) done(value);
    } catch (e) {
      if (mounted.current && !c.signal.aborted) {
        setError(
          e instanceof InspectionError
            ? e.message + " [" + e.code + "]"
            : "Operation failed. Reload to confirm the persisted result.",
        );
        failed?.(e);
        if (
          e instanceof InspectionError &&
          (e.status === 401 ||
            (e.status === 403 && e.code === "PASSWORD_CHANGE_REQUIRED"))
        )
          sessionFailure();
      }
    } finally {
      if (active.current === c) active.current = null;
      if (mounted.current) setBusy(false);
    }
  }
  return { run, busy, error, setError };
}
export function Results({
  criteria,
  defects,
}: {
  criteria: Criterion[];
  defects: Defect[];
}) {
  return (
    <>
      <h2>Results</h2>
      {criteria.map((c) => (
        <article key={c.itemId}>
          <strong>{c.criterion}</strong> {c.mandatory ? "(Mandatory)" : ""}
          <p>
            {c.outcome ?? "Not recorded"} — {c.note}
          </p>
          {c.evidenceIds?.length ? (
            <p>Evidence IDs: {c.evidenceIds.join(", ")}</p>
          ) : null}
        </article>
      ))}
      <h2>Defects</h2>
      {!defects.length && <p>No defects recorded.</p>}
      {defects.map((d) => (
        <article key={d.id}>
          <strong>
            #{d.id} {d.description}
          </strong>
          <p>
            Criterion #{d.itemId} · Severity reference #{d.severityId} ·{" "}
            {d.status} · {d.location}
          </p>
          {d.evidenceIds?.length ? (
            <p>Evidence IDs: {d.evidenceIds.join(", ")}</p>
          ) : null}
        </article>
      ))}
    </>
  );
}
export function EvidenceList({
  items,
  branch,
  id,
}: {
  items: Evidence[];
  branch: number;
  id: number;
}) {
  const { run, busy, error } = useOperation();
  return (
    <section>
      <h2>Evidence</h2>
      {error && <p role="alert">{error}</p>}
      {!items.length && <p>No evidence.</p>}
      <ul>
        {items.map((e) => (
          <li key={e.id}>
            #{e.id} {e.name} ({e.mimeType}, {e.bytes ?? "?"} bytes){" "}
            <button
              disabled={busy}
              onClick={() =>
                void run(
                  (s) => api.download(branch, id, e.id, s),
                  (blob) => {
                    const url = URL.createObjectURL(blob);
                    const a = document.createElement("a");
                    a.href = url;
                    a.download = e.name.replace(/[/\\\x00-\x1f]/g, "_");
                    a.click();
                    setTimeout(() => URL.revokeObjectURL(url), 1000);
                  },
                )
              }
            >
              Download {e.name}
            </button>
          </li>
        ))}
      </ul>
    </section>
  );
}
