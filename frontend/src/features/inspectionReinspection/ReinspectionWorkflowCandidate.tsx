import { ReinspectionConclusionReview } from "./ReinspectionConclusionReview";
import { useCallback, useEffect, useState } from "react";
import { ApiError } from "../../api/dev1/client";
import {
  ReinspectionCandidateClient,
  candidateError,
  type CandidateDetails,
  type CandidateItem,
} from "./candidateClient";
const api = new ReinspectionCandidateClient();

/** Unmounted until accepted lifecycle/routing. Uses typed real API transport, no mock store. */
export function ReinspectionWorkflowCandidate({
  branchId,
}: {
  branchId: number;
}) {
  return <Workflow key={branchId} branchId={branchId} />;
}
function Workflow({ branchId }: { branchId: number }) {
  const [items, setItems] = useState<CandidateItem[]>([]),
    [source, setSource] = useState<CandidateDetails | null>(null);
  const [answers, setAnswers] = useState<Record<number, boolean | null>>({}),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false),
    [reloadRequired, setReloadRequired] = useState(false),
    [notice, setNotice] = useState(""),
    [reviewed, setReviewed] = useState(false);
  useEffect(() => {
    const c = new AbortController();
    setBusy(true);
    api
      .list(branchId, c.signal)
      .then(setItems)
      .catch((e) => {
        if (!c.signal.aborted) setError(candidateError(e));
      })
      .finally(() => {
        if (!c.signal.aborted) setBusy(false);
      });
    return () => c.abort();
  }, [branchId]);
  function apply(value: CandidateDetails) {
    setSource(value);
    setReviewed(false);
    setAnswers(
      Object.fromEntries(value.defects.map((d) => [d.defectId, d.fixed])),
    );
    setReloadRequired(false);
  }
  async function open(item: { inspectionId: number; reworkId: number }) {
    setBusy(true);
    setError("");
    setNotice("");
    try {
      apply(await api.read(branchId, item.inspectionId, item.reworkId));
    } catch (e) {
      setError(candidateError(e));
      setReloadRequired(true);
    } finally {
      setBusy(false);
    }
  }
  async function save() {
    if (!source || busy || reloadRequired || reviewed) return;
    setBusy(true);
    setError("");
    setNotice("");
    try {
      apply(
        await api.save(
          branchId,
          source,
          source.defects.map((d) => ({
            defectId: d.defectId,
            fixed: answers[d.defectId] ?? null,
          })),
        ),
      );
      setNotice("Results saved. You can reload to review persisted decisions.");
    } catch (e) {
      setError(candidateError(e));
      if (
        e instanceof ApiError &&
        (e.status === 401 ||
          e.status === 403 ||
          e.status === 409 ||
          e.status === 412 ||
          e.status === 0 ||
          e.status >= 500)
      )
        setReloadRequired(true);
    } finally {
      setBusy(false);
    }
  }
  const markReviewed = useCallback(() => setReviewed(true), []);
  const blockReview = useCallback(() => setReloadRequired(true), []);
  const dirty =
    !!source && source.defects.some((d) => answers[d.defectId] !== d.fixed);
  const complete =
    source &&
    source.defects.every((d) => typeof answers[d.defectId] === "boolean");
  return (
    <section aria-label="Reinspection workflow">
      <h2>Assigned reinspections</h2>
      {busy && <p role="status">Loading…</p>}
      {error && <p role="alert">{error}</p>}
      {notice && <p role="status">{notice}</p>}
      {!busy && items.length === 0 && (
        <p>No assigned reinspections available.</p>
      )}
      <ul>
        {items.map((i) => (
          <li key={i.inspectionId}>
            <button disabled={busy} onClick={() => open(i)}>
              Inspection #{i.inspectionId} · Rework #{i.reworkId}
            </button>
          </li>
        ))}
      </ul>
      {source && (
        <div>
          <h3>Inspection #{source.inspectionId}</h3>
          <p>
            Original inspection #{source.originalInspectionId}. Checklist
            versions: original {source.originalChecklistVersionId}, assigned{" "}
            {source.assignedChecklistVersionId}.
          </p>
          <details>
            <summary>Assigned checklist</summary>
            <ul>
              {source.criteria.map((c) => (
                <li key={c.itemId}>
                  {c.criterion}
                  {c.isRequired ? " (mandatory)" : ""}
                  {!c.isActive ? " (inactive in assigned version)" : ""}
                  {c.passCondition && <p>{c.passCondition}</p>}
                </li>
              ))}
            </ul>
          </details>
          {source.defects.map((d) => (
            <fieldset
              key={d.defectId}
              disabled={busy || reloadRequired || reviewed}
            >
              <legend>Defect #{d.defectId}</legend>
              <p>{d.description}</p>
              {d.originalCriterion && (
                <p>Original criterion: {d.originalCriterion}</p>
              )}
              <p>{d.remediationNote ?? "No remediation note"}</p>
              <label>
                Decision{" "}
                <select
                  value={
                    answers[d.defectId] === true
                      ? "fixed"
                      : answers[d.defectId] === false
                        ? "not-fixed"
                        : ""
                  }
                  onChange={(e) =>
                    setAnswers((old) => ({
                      ...old,
                      [d.defectId]:
                        e.target.value === "fixed"
                          ? true
                          : e.target.value === "not-fixed"
                            ? false
                            : null,
                    }))
                  }
                >
                  <option value="">Not reviewed</option>
                  <option value="fixed">Fixed</option>
                  <option value="not-fixed">Not Fixed</option>
                </select>
              </label>
              {d.recordedAt && (
                <p>
                  Saved by #{d.recordedBy} at {d.recordedAt}
                </p>
              )}
            </fieldset>
          ))}
          <button
            disabled={busy || !complete || reloadRequired || reviewed}
            onClick={save}
          >
            Save results
          </button>
          <button disabled={busy} onClick={() => open(source)}>
            Reload persisted results
          </button>
          <p>
            Review all saved decisions before conclusion. Concluding is
            currently unavailable.
          </p>
          <ReinspectionConclusionReview
            key={`${source.inspectionId}:${source.inspectionRevision}:${source.bindingRevision}`}
            branchId={branchId}
            source={source}
            disabled={busy || reloadRequired || dirty}
            onPersisted={() => open(source)}
            onBlocked={blockReview}
            onReviewed={markReviewed}
          />
          <button disabled>Conclude unavailable</button>
        </div>
      )}
    </section>
  );
}
