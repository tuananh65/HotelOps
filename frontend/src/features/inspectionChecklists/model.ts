export interface CriterionInput {
  text: string;
  group: string | null;
  mandatory: boolean;
  passCondition: string | null;
  description: string | null;
}
export interface Criterion extends CriterionInput {
  id: number;
  order: number;
}
export interface Version {
  id: number;
  number: number;
  status: string;
  effectiveAt: string | null;
  publishedBy: number | null;
  usedByInspection: boolean;
  criteria: Criterion[];
}
export interface Checklist {
  id: number;
  branchId: number;
  code: string;
  name: string;
  description: string | null;
  active: boolean;
  roomTypeIds: number[];
  versions: Version[];
}
export interface CreateInput {
  code: string;
  name: string;
  description: string | null;
  roomTypeIds: number[];
  criteria: CriterionInput[];
}
export interface RoomType {
  id: number;
  branchId: number | null;
  code: string;
  name: string;
  isActive: boolean;
}
export const emptyCriterion = (): CriterionInput => ({
  text: "",
  group: null,
  mandatory: true,
  passCondition: null,
  description: null,
});
export function positiveId(value: string | number): number {
  if (
    !/^\d+$/.test(String(value)) ||
    !Number.isSafeInteger(Number(value)) ||
    Number(value) <= 0
  )
    throw new Error("Mã phải là số nguyên dương hợp lệ.");
  return Number(value);
}
export function roomTypeIds(value: string): number[] {
  const ids = value.split(",").map((v) => positiveId(v.trim()));
  if (!ids.length || ids.length > 100 || new Set(ids).size !== ids.length)
    throw new Error("Chọn 1–100 loại phòng khác nhau.");
  return ids;
}
function text(
  value: string | null,
  max: number,
  required = false,
): string | null {
  const clean = value?.trim() || "";
  if (
    (required && !clean) ||
    clean.length > max ||
    /[\x00-\x1f\x7f-\x9f]/.test(clean)
  )
    throw new Error(
      `Nội dung ${required ? "bắt buộc, " : ""}tối đa ${max} ký tự, không có ký tự điều khiển.`,
    );
  return clean || null;
}
export function criteriaInput(items: CriterionInput[]): CriterionInput[] {
  if (items.length < 1 || items.length > 200)
    throw new Error("Checklist cần 1–200 tiêu chí.");
  if (items.some((c) => typeof c.mandatory !== "boolean"))
    throw new Error("Mỗi tiêu chí cần lựa chọn bắt buộc rõ ràng.");
  return items.map((c) => ({
    text: text(c.text, 250, true)!,
    group: text(c.group, 100),
    mandatory: c.mandatory === true,
    passCondition: text(c.passCondition, 4000),
    description: text(c.description, 4000),
  }));
}
export function createInput(
  code: string,
  name: string,
  description: string,
  ids: string,
  criteria: CriterionInput[],
): CreateInput {
  const normalized = text(code, 40, true)!.toUpperCase();
  if (!/^[A-Z0-9][A-Z0-9_-]*$/.test(normalized))
    throw new Error("Mã checklist chỉ gồm chữ, số, dấu _ hoặc -.");
  return {
    code: normalized,
    name: text(name, 200, true)!,
    description: text(description, 4000),
    roomTypeIds: roomTypeIds(ids),
    criteria: criteriaInput(criteria),
  };
}
export const reasonInput = (reason: string) => text(reason, 500, true)!;
export function lifecycle(checklist: Checklist) {
  const draft = checklist.versions.find((v) => v.status === "DRAFT");
  return {
    draft,
    canEdit: checklist.active && (!draft || !draft.usedByInspection),
    canPublish:
      checklist.active &&
      !!draft &&
      !draft.usedByInspection &&
      draft.criteria.length > 0,
    canRetire:
      checklist.active &&
      checklist.versions.some((v) => v.status === "PUBLISHED"),
  };
}
export function editableCriteria(checklist: Checklist): CriterionInput[] {
  const source =
    lifecycle(checklist).draft ||
    [...checklist.versions].sort((a, b) => b.number - a.number)[0];
  return (
    source?.criteria.map((c) => ({
      text: c.text,
      group: c.group,
      mandatory: c.mandatory,
      passCondition: c.passCondition,
      description: c.description,
    })) || [emptyCriterion()]
  );
}
