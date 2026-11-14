export interface EditableBranch {
  id: number;
  code: string;
  name: string;
  address: string | null;
  phone: string | null;
  email: string | null;
  timezone: string | null;
  isActive: boolean;
}
export interface BranchUpdate {
  name: string;
  address: string;
  phone: string | null;
  email: string | null;
  reason: string;
}
export interface BranchSnapshot {
  branch: EditableBranch;
  etag: string;
}
export type BranchError = {
  kind:
    | "invalid-request"
    | "invalid-response"
    | "network"
    | "cancelled"
    | "http"
    | "stale";
  status?: number;
};
export type BranchReadResult =
  { ok: true; data: BranchSnapshot } | { ok: false; error: BranchError };
export type BranchWriteResult =
  | { outcome: "CONFIRMED"; data: BranchSnapshot }
  | {
      outcome: "NOT_DISPATCHED" | "REJECTED" | "OUTCOME_UNKNOWN";
      error: BranchError;
    };
export const safeId = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
export const strongBranchTag = (v: unknown): v is string =>
  typeof v === "string" && /^"[A-Za-z0-9+/]{10}[AEIMQUYcgkosw048]="$/.test(v);
const text = (v: unknown, max: number): v is string =>
  typeof v === "string" && v.length <= max && !/[\u0000-\u001f\u007f]/.test(v);
const nullable = (v: unknown, max: number): v is string | null =>
  v === null || text(v, max);
export function decodeBranch(
  v: unknown,
  id: number,
): EditableBranch | undefined {
  if (typeof v !== "object" || v === null || Array.isArray(v)) return;
  const x = v as Record<string, unknown>;
  if (
    x.id !== id ||
    !safeId(x.id) ||
    !text(x.code, 30) ||
    !x.code.length ||
    !text(x.name, 200) ||
    !x.name.length ||
    !nullable(x.address, 4000) ||
    !nullable(x.phone, 20) ||
    !nullable(x.email, 150) ||
    !nullable(x.timezone, 100) ||
    typeof x.isActive !== "boolean"
  )
    return;
  return {
    id: x.id,
    code: x.code,
    name: x.name,
    address: x.address,
    phone: x.phone,
    email: x.email,
    timezone: x.timezone,
    isActive: x.isActive,
  };
}
/** UI mirrors structural limits only; backend remains final validation authority. */
export function validUpdate(x: BranchUpdate): boolean {
  return (
    text(x.name, 200) &&
    x.name.trim().length > 0 &&
    text(x.address, 4000) &&
    x.address.trim().length > 0 &&
    nullable(x.phone, 20) &&
    (x.phone === null || /^[0-9+() .-]*$/.test(x.phone)) &&
    nullable(x.email, 150) &&
    (x.email === null || /^[\x21-\x7e]+@[^\s@]+$/.test(x.email)) &&
    text(x.reason, 500) &&
    x.reason.trim().length >= 10
  );
}
