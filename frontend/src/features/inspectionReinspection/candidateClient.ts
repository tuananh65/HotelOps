import { ApiError, getJson, send } from "../../api/dev1/client";

export interface CandidateItem {
  inspectionId: number;
  reworkId: number;
  originalInspectionId: number;
  roomId: number;
  assignedChecklistVersionId: number;
}
export interface CandidateDetails {
  inspectionId: number;
  reworkId: number;
  originalInspectionId: number;
  originalChecklistVersionId: number;
  assignedChecklistVersionId: number;
  inspectionRevision: string;
  reworkRevision: string;
  bindingRevision: string;
  defects: {
    defectId: number;
    description: string;
    originalCriterion: string | null;
    remediationNote: string | null;
    fixed: boolean | null;
    recordedBy: number | null;
    recordedAt: string | null;
  }[];
  criteria: {
    itemId: number;
    criterion: string;
    passCondition: string | null;
    isRequired: boolean;
    isActive: boolean;
  }[];
  candidateStorageAvailable: true;
  lifecycleConclusionAvailable: false;
}
function id(value: unknown): value is number {
  return Number.isSafeInteger(value) && (value as number) > 0;
}
export function strongTag(value: unknown): value is string {
  return typeof value === "string" && /^"[0-9A-F]{16}"$/.test(value);
}
function record(value: unknown): Record<string, unknown> {
  if (!value || typeof value !== "object" || Array.isArray(value))
    throw new ApiError(502, "INVALID_CANDIDATE_RESPONSE");
  return value as Record<string, unknown>;
}
export function decodeDetails(value: unknown): CandidateDetails {
  const x = record(value);
  if (
    ![
      x.inspectionId,
      x.reworkId,
      x.originalInspectionId,
      x.originalChecklistVersionId,
      x.assignedChecklistVersionId,
    ].every(id) ||
    ![x.inspectionRevision, x.reworkRevision, x.bindingRevision].every(
      strongTag,
    ) ||
    x.candidateStorageAvailable !== true ||
    x.lifecycleConclusionAvailable !== false ||
    !Array.isArray(x.defects) ||
    x.defects.length === 0 ||
    !Array.isArray(x.criteria)
  )
    throw new ApiError(502, "INVALID_CANDIDATE_RESPONSE");
  const criteria = new Set<number>();
  for (const v of x.criteria) {
    const c = record(v);
    if (
      !id(c.itemId) ||
      criteria.has(c.itemId) ||
      typeof c.criterion !== "string" ||
      !(c.passCondition === null || typeof c.passCondition === "string") ||
      typeof c.isRequired !== "boolean" ||
      typeof c.isActive !== "boolean"
    )
      throw new ApiError(502, "INVALID_CANDIDATE_RESPONSE");
    criteria.add(c.itemId);
  }
  const ids = new Set<number>();
  for (const item of x.defects) {
    const d = record(item);
    if (
      !id(d.defectId) ||
      ids.has(d.defectId) ||
      !(d.fixed === null || typeof d.fixed === "boolean") ||
      typeof d.description !== "string" ||
      !(
        d.originalCriterion === null || typeof d.originalCriterion === "string"
      ) ||
      !(d.remediationNote === null || typeof d.remediationNote === "string") ||
      !(d.recordedBy === null || id(d.recordedBy)) ||
      !(d.recordedAt === null || typeof d.recordedAt === "string")
    )
      throw new ApiError(502, "INVALID_CANDIDATE_RESPONSE");
    ids.add(d.defectId);
  }
  return x as unknown as CandidateDetails;
}
export class ReinspectionCandidateClient {
  private path(branch: number) {
    if (!id(branch)) throw new ApiError(400, "BRANCH_REQUIRED");
    return `/branches/${branch}/reinspection-workflow-candidate`;
  }
  async list(branch: number, signal?: AbortSignal): Promise<CandidateItem[]> {
    const body = await getJson(this.path(branch), signal);
    if (
      !Array.isArray(body) ||
      body.some((v) => {
        const x = record(v);
        return ![
          x.inspectionId,
          x.reworkId,
          x.originalInspectionId,
          x.roomId,
          x.assignedChecklistVersionId,
        ].every(id);
      })
    )
      throw new ApiError(502, "INVALID_CANDIDATE_RESPONSE");
    return body as CandidateItem[];
  }
  async read(
    branch: number,
    inspection: number,
    rework: number,
    signal?: AbortSignal,
  ): Promise<CandidateDetails> {
    if (!id(inspection) || !id(rework))
      throw new ApiError(400, "SOURCE_REQUIRED");
    const x = decodeDetails(
      await getJson(
        `${this.path(branch)}/${inspection}?reworkId=${rework}`,
        signal,
      ),
    );
    if (x.inspectionId !== inspection || x.reworkId !== rework)
      throw new ApiError(502, "SOURCE_MISMATCH");
    return x;
  }
  private async mutation(
    path: string,
    method: string,
    body: unknown,
    tag: string,
    signal?: AbortSignal,
  ): Promise<CandidateDetails> {
    const csrf = record(await getJson("/auth/csrf", signal));
    if (
      csrf.headerName !== "X-CSRF-TOKEN" ||
      typeof csrf.csrfToken !== "string" ||
      !csrf.csrfToken.trim() ||
      /[\r\n]/.test(csrf.csrfToken)
    )
      throw new ApiError(502, "INVALID_CSRF_RESPONSE");
    const response = await send(path, signal, {
      method,
      headers: {
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": csrf.csrfToken,
        "If-Match": tag,
      },
      body: JSON.stringify(body),
    });
    const value = decodeDetails(response.body);
    if (response.etag && response.etag !== value.inspectionRevision)
      throw new ApiError(502, "REVISION_RESPONSE_MISMATCH");
    return value;
  }
  async bind(
    branch: number,
    inspection: number,
    rework: number,
    inspectionRevision: string,
    reworkRevision: string,
    signal?: AbortSignal,
  ) {
    if (
      !id(inspection) ||
      !id(rework) ||
      !strongTag(inspectionRevision) ||
      !strongTag(reworkRevision)
    )
      throw new ApiError(400, "SOURCE_REQUIRED");
    const bound = await this.mutation(
      `${this.path(branch)}/${inspection}/bind`,
      "POST",
      { reworkId: rework, inspectionRevision, reworkRevision },
      inspectionRevision,
      signal,
    );
    if (bound.inspectionId !== inspection || bound.reworkId !== rework)
      throw new ApiError(502, "SOURCE_MISMATCH");
    return bound;
  }
  async save(
    branch: number,
    source: CandidateDetails,
    decisions: { defectId: number; fixed: boolean | null }[],
    signal?: AbortSignal,
  ) {
    decodeDetails(source);
    if (
      decisions.length !== source.defects.length ||
      new Set(decisions.map((d) => d.defectId)).size !== decisions.length ||
      decisions.some(
        (d) =>
          typeof d.fixed !== "boolean" ||
          !source.defects.some((x) => x.defectId === d.defectId),
      )
    )
      throw new ApiError(400, "EVERY_DEFECT_DECISION_REQUIRED");
    const saved = await this.mutation(
      `${this.path(branch)}/${source.inspectionId}/results`,
      "PUT",
      {
        reworkId: source.reworkId,
        reworkRevision: source.reworkRevision,
        bindingRevision: source.bindingRevision,
        decisions,
      },
      source.inspectionRevision,
      signal,
    );
    if (
      saved.inspectionId !== source.inspectionId ||
      saved.reworkId !== source.reworkId
    )
      throw new ApiError(502, "SOURCE_MISMATCH");
    return saved;
  }
}
export function candidateError(error: unknown) {
  if (error instanceof ApiError) {
    if (error.status === 401) return "Your session expired. Sign in again.";
    if (error.status === 403)
      return "You cannot perform reinspection in this branch.";
    if (error.status === 412)
      return "The source changed. Reload before saving.";
    if (error.status === 0 || error.status >= 500)
      return "The result could not be confirmed. Reload before another save.";
    if (error.status === 400)
      return "Review every selected defect before saving.";
  }
  return "Could not load the reinspection. Please reload.";
}
