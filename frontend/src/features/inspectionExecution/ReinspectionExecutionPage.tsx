import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { ApiError } from "../../api/dev1/client";
import {
  readReinspection,
  concludeReinspection,
  reinspectionEvidenceUrl,
  type ReinspectionExecution,
  type ReinspectionConclusion,
} from "../../api/dev1/reinspectionExecution";
import { encodeEvidence } from "./model";
import { RequestReworkForm } from "./RequestReworkForm";

export function ReinspectionExecutionPage() {
  const { branch = "", id = "" } = useParams();
  const { state } = useAuth();
  const identity = useRef(state.authorityGeneration),
    revision = useRef(0);
  if (identity.current !== state.authorityGeneration) {
    identity.current = state.authorityGeneration;
    ++revision.current;
  }
  return (
    <Execution
      key={`${branch}:${id}:${revision.current}`}
      branch={branch}
      id={id}
    />
  );
}
function Execution({ branch, id }: { branch: string; id: string }) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    state.session &&
    !state.session.mustChangePassword
      ? state.authorityGeneration
      : null;
  const current = useRef(owner);
  current.current = owner;
  const lifetime = useRef<AbortController | null>(null),
    writing = useRef(false);
  const [loaded, setLoaded] = useState<{
    owner: typeof owner;
    data: ReinspectionExecution;
  } | null>(null);
  const [decisions, setDecisions] = useState<
    Record<number, { fixed: string; note: string; files: File[] }>
  >({});
  const [reason, setReason] = useState(""),
    [message, setMessage] = useState("");
  const [review, setReview] = useState(false),
    [busy, setBusy] = useState(false),
    [locked, setLocked] = useState(false);
  const [saved, setSaved] = useState<{
    owner: typeof owner;
    data: ReinspectionConclusion;
  } | null>(null);
  useEffect(() => {
    if (!owner) return;
    const abort = new AbortController();
    lifetime.current = abort;
    setLoaded(null);
    setDecisions({});
    setReview(false);
    void readReinspection(branch, id, abort.signal)
      .then((data) => {
        if (!abort.signal.aborted && current.current === owner) {
          setLoaded({ owner, data });
          setDecisions(
            Object.fromEntries(
              data.defects.map((d) => [
                d.id,
                { fixed: "", note: "", files: [] },
              ]),
            ),
          );
        }
      })
      .catch((error) => {
        if (!abort.signal.aborted && current.current === owner) {
          setMessage(
            "This reinspection is unavailable or you no longer have access.",
          );
          if (error instanceof ApiError && error.status === 401)
            actions.invalidateSession(owner);
        }
      });
    return () => {
      abort.abort();
      if (lifetime.current === abort) lifetime.current = null;
    };
  }, [owner, branch, id, actions]);
  const data = owner && loaded?.owner === owner ? loaded.data : null;
  const result =
    owner && saved?.owner === owner ? saved.data : (data?.conclusion ?? null);
  const terminalRead = data && data.state !== "ASSIGNED" ? data : null;
  const change = (
    defect: number,
    value: Partial<{ fixed: string; note: string; files: File[] }>,
  ) =>
    setDecisions((old) => ({ ...old, [defect]: { ...old[defect], ...value } }));
  async function submit() {
    const abort = lifetime.current;
    if (
      !owner ||
      !data ||
      !abort ||
      abort.signal.aborted ||
      !review ||
      writing.current ||
      locked ||
      result
    )
      return;
    const selected = data.defects.map((d) => ({
      id: d.id,
      ...decisions[d.id],
    }));
    if (
      selected.some((d) => !["FIXED", "NOT_FIXED"].includes(d.fixed)) ||
      !reason.trim() ||
      selected.reduce((n, d) => n + d.files.length, 0) > 5
    ) {
      setMessage(
        "Decide every defect, enter a reason and select at most five photos.",
      );
      return;
    }
    writing.current = true;
    setBusy(true);
    setMessage("");
    const isCurrent = () =>
      current.current === owner && lifetime.current === abort;
    let sending = false;
    try {
      const defects = await Promise.all(
        selected.map(async (d) => ({
          defectId: d.id,
          fixed: d.fixed === "FIXED",
          note: d.note.trim(),
          photos: await Promise.all(
            d.files.map((file) => encodeEvidence(file, abort.signal)),
          ),
        })),
      );
      abort.signal.throwIfAborted();
      if (!isCurrent()) return;
      sending = true;
      const concluded = await concludeReinspection(
        branch,
        id,
        data,
        crypto.randomUUID(),
        reason.trim(),
        defects,
        { signal: abort.signal, isCurrent },
      );
      if (isCurrent() && !abort.signal.aborted) {
        setSaved({ owner, data: concluded });
        setLocked(true);
        setReview(false);
      }
    } catch (error) {
      if (isCurrent() && !abort.signal.aborted) {
        setReview(false);
        setLocked(sending);
        setMessage(
          !sending
            ? "A selected photo is invalid. Use a PNG or JPEG image and review again."
            : error instanceof ApiError &&
                [400, 403, 409, 412, 428].includes(error.status)
              ? "Conclusion was rejected. Reload to review current state; no automatic retry was sent."
              : "Conclusion outcome is unconfirmed. Do not submit another conclusion; check the current record.",
        );
        if (error instanceof ApiError && error.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      writing.current = false;
      if (isCurrent() && !abort.signal.aborted) setBusy(false);
    }
  }
  if (!owner) return <p role="status">Checking session…</p>;
  return (
    <main>
      <h1>Reinspect room</h1>
      <Link to="/notifications">Notifications</Link>
      {data && (
        <p>
          <Link to={`/reinspections/${branch}/${id}/original`}>
            Original inspection #{data.originalInspectionId}
          </Link>
        </p>
      )}
      {message && <p role="alert">{message}</p>}
      {!data && !message && <p role="status">Loading reinspection…</p>}
      {result && (
        <section aria-label="Reinspection conclusion">
          <p role="status">
            Reinspection {result.state === "PASSED" ? "Passed" : "Failed"}.
            Rework {result.reworkState === "COMPLETED" ? "Completed" : "Failed"}
            .
          </p>
          <p>
            {terminalRead
              ? `Current room readiness: ${terminalRead.effectiveReadiness}.`
              : result.effectiveReadiness === "BLOCKED"
                ? "The room remains blocked (MSG17)."
                : result.state === "PASSED"
                  ? "The room was ready at conclusion."
                  : "The cleaning cycle requires another rework."}
          </p>
          {terminalRead && (
            <>
              <p>
                Recorded outcome: {result.state}; readiness at conclusion:{" "}
                {result.effectiveReadiness}.
              </p>
              {terminalRead.defects.map((d) => (
                <section key={d.id} aria-label={`Recorded defect ${d.id}`}>
                  <h2>{d.description}</h2>
                  <p>Remediation: {d.remediation}</p>
                  <p>Result: {d.fixed ? "Fixed" : "Not Fixed"}</p>
                  <p>{d.note}</p>
                  {(
                    [
                      ["Original", d.originalPhotos],
                      ["Remediation", d.remediationPhotos],
                      ["Reinspection", d.reinspectionPhotos],
                    ] as const
                  ).map(([label, files]) => (
                    <p key={label}>
                      {files.map((file) => (
                        <a
                          key={file}
                          href={reinspectionEvidenceUrl(branch, id, file)}
                          target="_blank"
                          rel="noreferrer"
                        >
                          {label} photo #{file}{" "}
                        </a>
                      ))}
                    </p>
                  ))}
                </section>
              ))}
            </>
          )}
          <p>Notification pending delivery.</p>
          {result.state === "FAILED" && (
            <RequestReworkForm
              branch={Number(branch)}
              id={Number(id)}
              onDenied={(status) => {
                setLoaded(null);
                setSaved(null);
                setMessage(
                  "The next rework is unavailable with your current access.",
                );
                if (status === 401) actions.invalidateSession(owner);
              }}
              onSessionInvalid={() => actions.invalidateSession(owner)}
            />
          )}
        </section>
      )}
      {data && !result && (
        <>
          <p>
            Room #{data.roomId} · Rework #{data.reworkId} · Cleaning cycle #
            {data.cleaningTaskId}
          </p>
          {data.effectiveReadiness === "BLOCKED" && (
            <p role="status">The room is blocked (MSG17).</p>
          )}
          <form
            onSubmit={(e) => {
              e.preventDefault();
              setMessage("");
              setReview(true);
            }}
          >
            <fieldset disabled={busy || locked || review}>
              <legend>Defect decisions</legend>
              {data.defects.map((d) => (
                <section key={d.id} aria-label={`Defect ${d.id}`}>
                  <h2>{d.description}</h2>
                  <p>Remediation: {d.remediation}</p>
                  {(
                    [
                      ["Original", d.originalPhotos],
                      ["Remediation", d.remediationPhotos],
                    ] as const
                  ).map(([label, photos]) => (
                    <p key={label}>
                      {photos.map((file) => (
                        <a
                          key={file}
                          href={reinspectionEvidenceUrl(branch, id, file)}
                          target="_blank"
                          rel="noreferrer"
                        >
                          {label} photo #{file}{" "}
                        </a>
                      ))}
                    </p>
                  ))}
                  <label htmlFor={`decision-${d.id}`}>
                    Result for defect #{d.id}
                  </label>
                  <select
                    id={`decision-${d.id}`}
                    required
                    value={decisions[d.id]?.fixed ?? ""}
                    onChange={(e) => change(d.id, { fixed: e.target.value })}
                  >
                    <option value="">Select result</option>
                    <option value="FIXED">Fixed</option>
                    <option value="NOT_FIXED">Not Fixed</option>
                  </select>
                  <label htmlFor={`note-${d.id}`}>
                    Note for defect #{d.id}
                  </label>
                  <textarea
                    id={`note-${d.id}`}
                    maxLength={500}
                    value={decisions[d.id]?.note ?? ""}
                    onChange={(e) => change(d.id, { note: e.target.value })}
                  />
                  <label htmlFor={`photos-${d.id}`}>
                    Photos for defect #{d.id}
                  </label>
                  <input
                    id={`photos-${d.id}`}
                    type="file"
                    accept="image/png,image/jpeg"
                    multiple
                    onChange={(e) =>
                      change(d.id, { files: Array.from(e.target.files ?? []) })
                    }
                  />
                </section>
              ))}
              <label htmlFor="conclusion-reason">Conclusion reason</label>
              <textarea
                id="conclusion-reason"
                required
                maxLength={500}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
              <button type="submit">Review conclusion</button>
            </fieldset>
            {review && !locked && (
              <section aria-label="Review conclusion">
                <p>
                  {data.defects.every((d) => decisions[d.id]?.fixed === "FIXED")
                    ? "All defects are Fixed. Conclude Passed."
                    : "A defect is Not Fixed. Conclude Failed and request another rework."}
                </p>
                <p>{reason}</p>
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => void submit()}
                >
                  Confirm conclusion
                </button>
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => setReview(false)}
                >
                  Edit decisions
                </button>
              </section>
            )}
          </form>
        </>
      )}
      {busy && <p role="status">Conclusion pending…</p>}
    </main>
  );
}
