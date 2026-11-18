import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { PublicSession } from "../auth/authStore";
import { createUserProfileApi } from "./userProfileApi";
import { createUserBranchMembershipApi } from "./userBranchMembershipApi";
import type { UserProfileDto } from "./userProfileContracts";
import type { UserBranchMembershipDto } from "./userBranchMembershipContracts";
import { createHttpClient, isObject } from "../auth/http";
export type BranchAssignmentOption={id:number;name:string;departments:{id:number;name:string}[]};
const positiveId=(v:unknown):v is number=>typeof v==="number"&&Number.isSafeInteger(v)&&v>0;
function options(v:unknown):BranchAssignmentOption[]|undefined{
  if(!Array.isArray(v)||!v.every(b=>isObject(b)&&positiveId(b.id)&&typeof b.name==="string"&&Array.isArray(b.departments)
    &&b.departments.every(d=>isObject(d)&&positiveId(d.id)&&typeof d.name==="string")))return;
  return v as BranchAssignmentOption[];
}

export const unknownMembershipMessage =
  "Chưa xác nhận kết quả thêm phân quyền chi nhánh; phân quyền có thể đã được tạo. Không tự gửi lại. Nhờ quản trị viên kiểm tra trước khi thay đổi tiếp.";
export function destinationBranchId(value: string): number | null {
  const number =
    value.length > 0 && !/[^0-9]/.test(value) ? Number(value) : NaN;
  return Number.isSafeInteger(number) && number > 0 ? number : null;
}
type Snapshot = { profile: UserProfileDto; etag: string; revision: number; options:BranchAssignmentOption[] };
type Read = { owner: PublicSession; target: number; generation: number } & (
  | { kind: "loading" }
  | { kind: "error"; message: string }
  | { kind: "ready"; snapshot: Snapshot }
);
type Progress = {
  phase:
    | "idle"
    | "pending"
    | "confirmed"
    | "unknown"
    | "stale"
    | "rejected"
    | "not-dispatched";
  message?: string;
  membership?: UserBranchMembershipDto;
};

export function useAddUserBranch(target: number | null) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" ? state.session : null;
  const current = useRef({ owner, target });
  current.current = { owner, target };
  const previous = useRef({ owner, target });
  const [profileApi] = useState(() => createUserProfileApi());
  const [membershipApi] = useState(() => createUserBranchMembershipApi());
  const [attempt, setAttempt] = useState(0);
  const [read, setRead] = useState<Read | null>(null);
  const [progress, setProgress] = useState<Progress>({ phase: "idle" });
  const phase = useRef<Progress["phase"]>("idle");
  const mounted = useRef(false),
    busy = useRef(false),
    paused = useRef(false),
    recovered = useRef(false);
  const readGeneration = useRef(0),
    mutationGeneration = useRef(0),
    revision = useRef(0);
  const controller = useRef<AbortController | null>(null);
  const usable = useRef<Snapshot | null>(null);
  function publish(next: Progress) {
    phase.current = next.phase;
    setProgress(next);
  }
  function recover(status?: number, reason?: string) {
    if (
      (status === 401 || reason === "PASSWORD_CHANGE_REQUIRED") &&
      !recovered.current
    ) {
      recovered.current = true;
      paused.current = true;
      void actions.revalidate(); // Bounded GET recovery; never replay a mutation.
    }
  }
  useLayoutEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      ++mutationGeneration.current;
      controller.current?.abort();
    };
  }, []);
  useLayoutEffect(() => {
    if (previous.current.owner === owner && previous.current.target === target)
      return;
    const changedTarget = previous.current.target !== target;
    const pending = phase.current === "pending";
    ++mutationGeneration.current;
    controller.current?.abort();
    controller.current = null;
    busy.current = false;
    usable.current = null;
    setRead(null);
    if (changedTarget) {
      paused.current = false;
      recovered.current = false;
      publish({ phase: "idle" });
    } else if (pending) {
      paused.current = true;
      publish({ phase: "unknown" });
    } else if (phase.current === "confirmed") {
      paused.current = true;
      publish({ phase: "idle" });
    } else if (!["unknown", "rejected", "stale"].includes(phase.current))
      publish({ phase: "idle" });
    previous.current = { owner, target };
  }, [owner, target]);
  useEffect(() => {
    const generation = ++readGeneration.current,
      abort = new AbortController();
    if (!owner || target === null || paused.current) return () => abort.abort();
    setRead({ owner, target, generation, kind: "loading" });
    void Promise.resolve().then(async () => {
      if (abort.signal.aborted || generation !== readGeneration.current) return;
      const [result,destinations] = await Promise.all([profileApi.getProfile(target, abort.signal),
        createHttpClient()({method:"GET",url:`/api/users/${target}/branch-assignment-options`,expectedStatus:200,signal:abort.signal,decode:options})]);
      if (
        !mounted.current ||
        abort.signal.aborted ||
        generation !== readGeneration.current ||
        current.current.owner !== owner ||
        current.current.target !== target
      )
        return;
      if (result.ok&&!destinations.ok){
        setRead({owner,target,generation,kind:"error",message:"Không tải được phân công chi nhánh / bộ phận. Tải lại để cập nhật quyền truy cập."});
        if(destinations.error.kind==="http")recover(destinations.error.status);
        return;
      }
      if (result.ok&&destinations.ok)
        setRead({
          owner,
          target,
          generation,
          kind: "ready",
          snapshot: {
            profile: result.data,
            etag: result.etag,
            revision: ++revision.current,
            options:destinations.data,
          },
        });
      else if(!result.ok) {
        setRead({
          owner,
          target,
          generation,
          kind: "error",
          message: failureMessage(result.error.status, result.error.reason),
        });
        recover(result.error.status, result.error.reason);
      }
    });
    return () => {
      ++readGeneration.current;
      abort.abort();
    };
  }, [owner, target, attempt, profileApi]);
  const visible =
    owner &&
    read?.owner === owner &&
    read.target === target &&
    read.generation === readGeneration.current
      ? read
      : null;
  const snapshot = visible?.kind === "ready" ? visible.snapshot : null;
  usable.current = snapshot;
  function reload() {
    if (!owner || target === null || busy.current) return;
    ++readGeneration.current;
    usable.current = null;
    setRead(null);
    paused.current = false;
    recovered.current = false;
    if (!["unknown", "confirmed"].includes(phase.current))
      publish({ phase: "idle" });
    setAttempt((n) => n + 1);
  }
  async function add(branchId: number, departmentId: number) {
    if (
      !mounted.current ||
      !owner ||
      target === null ||
      !snapshot ||
      usable.current !== snapshot ||
      current.current.owner !== owner ||
      current.current.target !== target ||
      busy.current ||
      paused.current ||
      ["unknown", "stale", "confirmed"].includes(phase.current) ||
      !Number.isSafeInteger(branchId) ||
      branchId <= 0 || !Number.isSafeInteger(departmentId) || departmentId <= 0 ||
      !snapshot.options.some(b=>b.id===branchId&&b.departments.some(d=>d.id===departmentId))
    )
      return;
    busy.current = true;
    const generation = ++mutationGeneration.current,
      abort = new AbortController();
    controller.current = abort;
    publish({ phase: "pending" });
    try {
      const result = await membershipApi.addBranchMembership(
        target,
        snapshot.etag,
        branchId,
        abort.signal,
        departmentId,
      );
      if (
        !mounted.current ||
        generation !== mutationGeneration.current ||
        current.current.owner !== owner ||
        current.current.target !== target
      )
        return;
      if (result.outcome === "CONFIRMED_ADDED") {
        usable.current = null;
        setRead(null);
        paused.current = true;
        publish({ phase: "confirmed", membership: result.data });
      } else if (result.outcome === "OUTCOME_UNKNOWN") {
        usable.current = null;
        setRead(null);
        paused.current = true;
        publish({ phase: "unknown" });
        // Receiving401 invalidates session authority even when its body is unreadable.
        // Preserve the unknown mutation outcome and never replay membership creation.
        recover(result.error.status, result.error.reason);
      } else {
        const e = result.error;
        if ([401, 403, 404, 412, 428].includes(e.status ?? 0)) {
          usable.current = null;
          setRead(null);
          paused.current = true;
        }
        publish({
          phase:
            e.status === 412 || e.status === 428
              ? "stale"
              : result.outcome === "NOT_DISPATCHED"
                ? "not-dispatched"
                : "rejected",
          message: failureMessage(e.status, e.reason),
        });
        recover(e.status, e.reason);
      }
    } catch {
      if (
        mounted.current &&
        generation === mutationGeneration.current &&
        current.current.owner === owner &&
        current.current.target === target
      ) {
        usable.current = null;
        setRead(null);
        paused.current = true;
        publish({ phase: "unknown" });
      }
    } finally {
      if (generation === mutationGeneration.current) {
        busy.current = false;
        controller.current = null;
      }
    }
  }
  return {
    snapshot,
    progress,
    loading: visible?.kind === "loading",
    error: visible?.kind === "error" ? visible.message : null,
    reload,
    add,
  };
}

function failureMessage(status?: number, reason?: string): string {
  if (status === 403)
    return "Không có quyền truy cập. Phản hồi này không xác nhận tài khoản có tồn tại hay không.";
  if (status === 401)
    return "Cần kiểm tra phiên. Tải lại hồ sơ sau khi khôi phục phiên. Không gửi lại yêu cầu phân công thành viên.";
  if (status === 404) return "Máy chủ không tìm thấy tài khoản này.";
  if (status === 412 || status === 428)
    return "Cần phiên bản mới. Tải lại hồ sơ, kiểm tra tài khoản và nơi nhận rồi xác nhận lần thử mới.";
  switch (reason) {
    case "TARGET_NOT_ACTIVE":
      return "Máy chủ từ chối vì tài khoản đích không hoạt động.";
    case "MEMBERSHIP_EXISTS":
      return "Phân công thành viên đã tồn tại, có thể đã bị thu hồi trước đó. Không thể cấp lại tại đây.";
    case "EXISTING_ROLE_PATH_CONFLICT":
      return "Vai trò hiện có ở chi nhánh đích đang chặn thêm phân công. Không thể sửa vai trò trong quy trình này.";
    case "OPERATIONS_MANAGER_SCOPE_CONFLICT":
      return "Giới hạn phạm vi quản lý vận hành đang chặn thêm phân công thành viên.";
    case "BRANCH_UNAVAILABLE":
      return "Chi nhánh đích chưa khả dụng. Kiểm tra mã chi nhánh.";
    case "DEPARTMENT_UNAVAILABLE":
      return "Chọn bộ phận đang hoạt động thuộc chi nhánh đích.";
    case "CSRF_VALIDATION_FAILED":
      return "Kiểm tra bảo mật bị từ chối. Không gửi lại phân công thành viên.";
    case "HTTPS_REQUIRED":
      return "Cần kết nối HTTPS an toàn.";
    case "REQUEST_TOO_LARGE":
      return "Yêu cầu vượt dung lượng cho phép.";
  }
  if (status === 415)
    return "Định dạng yêu cầu bị từ chối. Không gửi lại phân công thành viên.";
  if (status === 400)
    return "Kiểm tra nơi tiếp nhận và phiên bản yêu cầu. Không gửi lại yêu cầu phân công thành viên.";
  return "Không hoàn tất an toàn yêu cầu. Không gửi lại phân công thành viên.";
}
