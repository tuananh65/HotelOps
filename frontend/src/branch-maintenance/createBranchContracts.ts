import {
  decodeBranch,
  safeId,
  strongBranchTag,
  type EditableBranch,
  type BranchError,
} from "./contracts";
export interface CreateBranchRequest {
  code: string;
  name: string;
  address: string;
  phone: string | null;
  email: string | null;
}
export interface CreatedBranch {
  branch: EditableBranch;
  etag: string | null;
}
export type CreateBranchResult =
  | { outcome: "CONFIRMED"; data: CreatedBranch }
  | {
      outcome: "NOT_DISPATCHED" | "REJECTED" | "OUTCOME_UNKNOWN";
      error: BranchError;
    };
const field = (v: unknown, max: number): v is string =>
  typeof v === "string" &&
  v.trim().length > 0 &&
  v.trim().length <= max &&
  !/[\u0000-\u001f\u007f]/.test(v);
const optional = (v: unknown, max: number) =>
  v === null ||
  (typeof v === "string" &&
    v.trim().length <= max &&
    !/[\u0000-\u001f\u007f]/.test(v));
/** Code normalization matches BranchAdministrationService.CreateAsync; all other values remain server-owned. */
export function createRequest(
  v: CreateBranchRequest,
): CreateBranchRequest | undefined {
  if (
    typeof v.code !== "string" ||
    !field(v.name, 200) ||
    !field(v.address, 4000) ||
    !optional(v.phone, 20) ||
    !optional(v.email, 150)
  )
    return;
  const code = v.code.trim().toUpperCase();
  if (
    !/^[A-Z0-9][A-Z0-9_-]{0,29}$/.test(code) ||
    !/^[\x00-\x7f]*$/.test(v.code)
  )
    return;
  if (v.phone !== null && !/^[0-9+() .-]*$/.test(v.phone)) return;
  if (
    v.email !== null &&
    v.email.trim() !== "" &&
    !/^[\x21-\x7e]+@[^\s@]+$/.test(v.email.trim())
  )
    return;
  return {
    code,
    name: v.name,
    address: v.address,
    phone: v.phone === null || v.phone.trim() === "" ? null : v.phone,
    email: v.email === null || v.email.trim() === "" ? null : v.email,
  };
}
export function createdBranch(
  v: unknown,
  code: string,
  etag: string | null,
): CreatedBranch | undefined {
  if (
    typeof v !== "object" ||
    v === null ||
    !("id" in v) ||
    !safeId(v.id) ||
    (etag !== null && !strongBranchTag(etag))
  )
    return;
  const branch = decodeBranch(v, v.id);
  if (
    !branch ||
    branch.code !== code ||
    !branch.isActive ||
    branch.timezone !== "Asia/Ho_Chi_Minh" ||
    !branch.address?.trim()
  )
    return;
  return { branch, etag };
}
