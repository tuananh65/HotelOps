import { useEffect, useState } from "react";
import { ApiError } from "../../api/dev1/client";
import {
  ReinspectionCandidateClient,
  type CandidateDetails,
  type ConclusionProposal,
  candidateError,
} from "./candidateClient";
const api = new ReinspectionCandidateClient();
/** Uses the existing actual typed transport. Proposals never activate operational conclusion. */
export function ReinspectionConclusionReview({
  branchId,
  source,
  disabled,
  onPersisted,
  onBlocked,
  onReviewed,
}: {
  branchId: number;
  source: CandidateDetails;
  disabled: boolean;
  onPersisted: () => Promise<void>;
  onBlocked: () => void;
  onReviewed: () => void;
}) {
  const [proposal, setProposal] = useState<ConclusionProposal | null>(null),
    [available, setAvailable] = useState(false),
    [review, setReview] = useState(false),
    [confirmed, setConfirmed] = useState(false),
    [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  const [operation] = useState(() => crypto.randomUUID());
  useEffect(() => {
    const c = new AbortController();
    setAvailable(false);
    api
      .readConclusion(branchId, source, c.signal)
      .then((p) => {
        if (!c.signal.aborted) {
          setProposal(p);
          setAvailable(true);
          if (p) onReviewed();
        }
      })
      .catch((e) => {
        if (!c.signal.aborted) {
          setError(candidateError(e));
          if (
            e instanceof ApiError &&
            (e.status === 401 || e.status === 403 || e.status === 412)
          )
            onBlocked();
        }
      });
    return () => c.abort();
  }, [branchId, source, onBlocked, onReviewed]);
  const complete = source.defects.every(
    (d) =>
      typeof d.fixed === "boolean" &&
      d.recordedBy !== null &&
      d.recordedAt !== null,
  );
  async function confirm() {
    if (!confirmed || disabled || busy || !complete || proposal || !available)
      return;
    setBusy(true);
    setError("");
    try {
      const saved = await api.prepareConclusion(
        branchId,
        source,
        operation,
        true,
      );
      setProposal(saved);
      onReviewed();
      setReview(false);
      await onPersisted();
    } catch (e) {
      setError(candidateError(e));
      if (
        e instanceof ApiError &&
        (e.status === 0 ||
          e.status === 401 ||
          e.status === 403 ||
          e.status === 409 ||
          e.status === 412 ||
          e.status >= 500)
      )
        onBlocked();
    } finally {
      setBusy(false);
    }
  }
  return (
    <section aria-label="Reinspection conclusion review">
      {error && <p role="alert">{error}</p>}
      {proposal ? (
        <p role="status">
          Saved proposal: Rework {proposal.proposedReworkState}. Operational
          lifecycle remains unchanged.
        </p>
      ) : (
        <>
          <button
            disabled={disabled || busy || !complete || !available}
            onClick={() => {
              setReview(true);
              setConfirmed(false);
            }}
          >
            Review conclusion proposal
          </button>
          {review && (
            <div>
              <p>
                {source.defects.every((d) => d.fixed === true)
                  ? "All selected defects are Fixed. Proposed Rework Completed."
                  : "Some selected defects are Not Fixed. Proposed Rework Failed; no new Rework will be created."}
              </p>
              <label>
                <input
                  type="checkbox"
                  checked={confirmed}
                  disabled={disabled || busy}
                  onChange={(e) => setConfirmed(e.target.checked)}
                />
                I confirm the saved defect decisions
              </label>
              <button
                disabled={!confirmed || disabled || busy}
                onClick={confirm}
              >
                Save conclusion proposal
              </button>
            </div>
          )}
        </>
      )}
      <p>Operational conclusion requires accepted lifecycle and schema.</p>
    </section>
  );
}
