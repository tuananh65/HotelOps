import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import {
  decodePreview,
  draftSnapshot,
  issueLabels,
  type Preview,
  type PreviewAdapter,
  type ShiftDraft,
} from "./shiftPlanPreview";
/** Unmounted internal-preview presentation. An adapter must authorize all server resources; a logged-in UI is never that authority. */
export function ShiftPlanPreviewPanel({
  adapter,
}: {
  adapter?: PreviewAdapter;
}) {
  const { state } = useAuth();
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.pending !== null ||
    state.session.mustChangePassword
  )
    return <p role="alert">Confirmed unrestricted session required.</p>;
  return (
    <Bound
      key={`${identityKey(state.session)}:${adapter ? identityKey(adapter) : 0}`}
      adapter={adapter}
    />
  );
}
const keys = new WeakMap<object, number>();
let serial = 0;
function identityKey(o: object) {
  if (!keys.has(o)) keys.set(o, ++serial);
  return keys.get(o)!;
}
function Bound({ adapter }: { adapter?: PreviewAdapter }) {
  const [drafts, setDrafts] = useState<readonly ShiftDraft[]>([]),
    [fields, setFields] = useState({
      employeeId: "",
      branchId: "",
      departmentId: "",
      templateId: "",
      shiftDate: "",
    });
  const [preview, setPreview] = useState<Preview | null>(null),
    [message, setMessage] = useState(""),
    [pending, setPending] = useState(false);
  const epoch = useRef(0),
    alive = useRef(true),
    busy = useRef(false),
    abort = useRef<AbortController | null>(null),
    latest = useRef(adapter);
  latest.current = adapter;
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
      epoch.current++;
      abort.current?.abort();
    };
  }, []);
  useEffect(() => {
    epoch.current++;
    abort.current?.abort();
    busy.current = false;
    setPending(false);
    setPreview(null);
  }, [adapter]);
  function invalidate() {
    epoch.current++;
    abort.current?.abort();
    busy.current = false;
    setPending(false);
    setPreview(null);
    setMessage("");
  }
  async function run() {
    if (!adapter || busy.current) return;
    const snapshot = draftSnapshot(drafts);
    if (!snapshot) {
      setMessage("Add valid drafts before preview.");
      return;
    }
    busy.current = true;
    setPending(true);
    setPreview(null);
    setMessage("");
    const generation = ++epoch.current;
    const controller = new AbortController();
    abort.current = controller;
    const current = () =>
      alive.current &&
      epoch.current === generation &&
      latest.current === adapter &&
      !controller.signal.aborted;
    try {
      const raw = await adapter(snapshot, controller.signal);
      if (!current()) return;
      const result = decodePreview(raw, snapshot.length);
      if (result) setPreview(result);
      else
        setMessage(
          "Preview unavailable: invalid server result. No permission to save.",
        );
    } catch {
      if (current()) setMessage("Preview unavailable. No automatic retry.");
    } finally {
      if (current()) {
        busy.current = false;
        setPending(false);
      }
    }
  }
  return (
    <section aria-label="Shift plan preview">
      <h1>Plan shift assignments</h1>
      <p>
        Diagnostic preview only. Every resource requires server authorization.
        No roster has been saved.
      </p>
      {!adapter && (
        <p role="alert">
          Authorized preview adapter unavailable. Public preview contract
          pending.
        </p>
      )}
      <form
        onSubmit={(e) => {
          e.preventDefault();
          const draft = {
            employeeId: Number(fields.employeeId),
            branchId: Number(fields.branchId),
            departmentId: Number(fields.departmentId),
            templateId: Number(fields.templateId),
            shiftDate: fields.shiftDate,
          };
          if (!draftSnapshot([draft]) || drafts.length >= 500) {
            setMessage(
              "Use positive safe IDs and a real calendar date; maximum 500 drafts.",
            );
            return;
          }
          invalidate();
          setDrafts((v) => [...v, draft]);
        }}
      >
        {(
          [
            "employeeId",
            "branchId",
            "departmentId",
            "templateId",
            "shiftDate",
          ] as const
        ).map((name) => (
          <label key={name}>
            {name}
            <input
              aria-label={name}
              value={fields[name]}
              onChange={(e) => {
                invalidate();
                setFields((v) => ({ ...v, [name]: e.target.value }));
              }}
            />
          </label>
        ))}
        <button>Add draft</button>
      </form>
      <ol>
        {drafts.map((d, i) => (
          <li key={i}>
            Employee {d.employeeId}; Branch {d.branchId}; Department{" "}
            {d.departmentId}; Template {d.templateId}; Date {d.shiftDate}
            <button
              onClick={() => {
                invalidate();
                setDrafts((v) => v.filter((_, n) => n !== i));
              }}
            >
              Remove draft {i + 1}
            </button>
          </li>
        ))}
      </ol>
      <button
        disabled={!adapter || !drafts.length || pending}
        onClick={() => void run()}
      >
        Preview assignments
      </button>
      {pending && <p role="status">Loading preview...</p>}
      {message && <p role="alert">{message}</p>}
      {preview && (
        <section aria-label="Preview results">
          <p role="status">
            Server revalidation required. This preview is not an atomic
            snapshot, version or permission to save.
          </p>
          <ol>
            {preview.rows.map((r) => (
              <li key={r.index}>
                Draft {r.index + 1}: {r.startUtc ?? "No confirmed interval"}{" "}
                {r.endUtc && <>to {r.endUtc}</>}
                {r.issues.length ? (
                  <ul>
                    {r.issues.map((issue) => (
                      <li key={issue}>{issueLabels[issue]}</li>
                    ))}
                  </ul>
                ) : (
                  <p>
                    No diagnostic issues reported. Revalidation still required.
                  </p>
                )}
              </li>
            ))}
          </ol>
        </section>
      )}
    </section>
  );
}
