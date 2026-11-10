import { useState } from "react";
export interface ApproveReviewSource {
  inspectionId: number;
  inspectionRevision: string;
  mandatoryCriteriaPassed: boolean;
  assignedChecklistVersionId: number;
}
/** Review-only command preparation. No approved Passed endpoint/event or mutation transport. */
export function ApproveInspectionReviewCandidate({
  source,
}: {
  source: ApproveReviewSource;
}) {
  return (
    <Review
      key={`${source.inspectionId}:${source.inspectionRevision}`}
      source={source}
    />
  );
}
function Review({ source }: { source: ApproveReviewSource }) {
  const [confirmed, setConfirmed] = useState(false);
  const [operation] = useState(() => crypto.randomUUID());
  return (
    <section aria-label="Approve inspection review">
      <h2>Approve inspection #{source.inspectionId}</h2>
      <p>Assigned checklist version {source.assignedChecklistVersionId}</p>
      <p>
        {source.mandatoryCriteriaPassed
          ? "Mandatory criteria have passed. Review the current source before approval."
          : "All mandatory criteria must pass before approval."}
      </p>
      <label>
        <input
          type="checkbox"
          checked={confirmed}
          onChange={(e) => setConfirmed(e.target.checked)}
        />
        I have reviewed the inspection
      </label>
      <p>Approval is currently unavailable.</p>
      <button
        disabled
        data-operation-id={operation}
        data-inspection-revision={source.inspectionRevision}
      >
        Approve unavailable
      </button>
    </section>
  );
}
