import { useState } from "react";

/** Review-only DTO matching ReinspectionReviewDto. Not a mutation authority or mock source. */
export interface ReinspectionReviewSource {
  inspectionId: number;
  reworkId: number;
  originalInspectionId: number;
  assignedChecklistVersionId: number;
  originalChecklistVersionId: number;
  inspectionRevision: string;
  reworkRevision: string;
  defects: {
    defectId: number;
    remediationNote: string | null;
    remediatedBy: number | null;
    remediatedAt: string | null;
  }[];
  sourceBindingAccepted: false;
  mutationAvailable: false;
  dependencies: string[];
}

/** Unmounted candidate: real routing/GET binding needs accepted source/schema first.
 * No fetch, fake permission, evidence URL or mutation dispatch is introduced. */
export function ReinspectionReviewCandidate({ source }: { source: ReinspectionReviewSource }) {
  // Remount the editor on any identity/revision change; never carry decisions across sources.
  return <Review key={`${source.inspectionId}:${source.reworkId}:${source.inspectionRevision}:${source.reworkRevision}`} source={source} />;
}
function Review({ source }: { source: ReinspectionReviewSource }) {
  const [decisions, setDecisions] = useState<Record<number, string>>({});
  const answered = source.defects.filter(d => decisions[d.defectId] === "fixed" || decisions[d.defectId] === "not-fixed").length;
  return <section aria-label="Reinspection candidate">
    <h2>Reinspection #{source.inspectionId} · Rework #{source.reworkId}</h2>
    <p>Original inspection #{source.originalInspectionId}. Original checklist version {source.originalChecklistVersionId}; assigned version {source.assignedChecklistVersionId}.</p>
    <p role="status">Review candidate — saving and conclusion are unavailable pending source binding and lifecycle approval.</p>
    {source.defects.map(defect => <fieldset key={defect.defectId}>
      <legend>Defect #{defect.defectId}</legend>
      <p>{defect.remediationNote ?? "No remediation note"}</p>
      <label>Decision <select value={decisions[defect.defectId] ?? ""} onChange={event => setDecisions(old => ({ ...old, [defect.defectId]: event.target.value }))}>
        <option value="">Not reviewed</option><option value="fixed">Fixed</option><option value="not-fixed">Not Fixed</option>
      </select></label>
    </fieldset>)}
    <p>{answered}/{source.defects.length} defects reviewed</p>
    <button type="button" disabled>Conclude unavailable</button>
  </section>;
}
