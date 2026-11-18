import { useLayoutEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createCreateUserApi } from "./createUserApi";
import type {
  CreateUserFailure,
  CreateUserRequest,
  CreateUserResponse,
} from "./createUserContracts";

export const unknownCreationMessage =
  "Chưa xác nhận tạo tài khoản; tài khoản có thể đã tồn tại. Không gửi lại. Nhờ quản trị viên kiểm tra và đặt lại mật khẩu nếu cần.";

type Progress =
  | { phase: "idle" | "pending" | "created" | "unknown" }
  | {
      phase: "not-dispatched" | "rejected";
      error: CreateUserFailure;
    };

/** Only nonsecret progress survives provider fallback. Successful secrets go directly to the mounted handoff. */
export function useCreateUser() {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" ? state.session : null;
  const currentOwner = useRef(owner);
  currentOwner.current = owner; // Mask stale callbacks before effect cleanup.
  const [api] = useState(() => createCreateUserApi());
  const [progress, setProgress] = useState<Progress>({ phase: "idle" });
  const phase = useRef<Progress["phase"]>("idle");
  const busy = useRef(false);
  const mounted = useRef(false);
  const generation = useRef(0);
  const controller = useRef<AbortController | null>(null);
  const attemptOwner = useRef(owner);

  function publish(next: Progress) {
    phase.current = next.phase;
    setProgress(next);
  }

  useLayoutEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      ++generation.current;
      controller.current?.abort();
    };
  }, []);

  useLayoutEffect(() => {
    if (controller.current && attemptOwner.current !== owner) {
      ++generation.current;
      controller.current.abort();
      controller.current = null;
      busy.current = false;
      // The API may already have sent POST. Aborting does not prove rollback.
      if (phase.current === "pending") publish({ phase: "unknown" });
    }
  }, [owner]);

  async function submit(
    input: CreateUserRequest,
    receive: (data: CreateUserResponse) => void,
  ) {
    if (
      !mounted.current ||
      !owner ||
      currentOwner.current !== owner ||
      busy.current ||
      ["unknown", "created"].includes(phase.current)
    )
      return;
    busy.current = true; // Synchronous, including two events before React renders.
    const attempt = ++generation.current;
    const abort = new AbortController();
    controller.current = abort;
    attemptOwner.current = owner;
    publish({ phase: "pending" });
    try {
      const result = await api.createUser(input, abort.signal);
      if (
        !mounted.current ||
        attempt !== generation.current ||
        currentOwner.current !== owner ||
        abort.signal.aborted
      )
        return;
      switch (result.outcome) {
        case "CONFIRMED_CREATED":
          receive(result.data);
          publish({ phase: "created" });
          break;
        case "OUTCOME_UNKNOWN":
          publish({ phase: "unknown" });
          // A received401 invalidates session authority even if its body was unreadable.
          // Keep the ambiguous creation locked; session recovery never replays POST.
          if (result.error.status === 401) void actions.revalidate();
          break;
        case "NOT_DISPATCHED":
          publish({ phase: "not-dispatched", error: result.error });
          if (result.error.status === 401) void actions.revalidate();
          break;
        case "REJECTED":
          publish({ phase: "rejected", error: result.error });
          if (
            result.error.status === 401 ||
            result.error.reason === "PASSWORD_CHANGE_REQUIRED"
          )
            void actions.revalidate();
          break;
      }
    } catch {
      if (
        mounted.current &&
        attempt === generation.current &&
        currentOwner.current === owner
      )
        publish({ phase: "unknown" });
    } finally {
      if (attempt === generation.current) {
        busy.current = false;
        controller.current = null;
      }
    }
  }
  return { progress, submit };
}

export function createUserFailureMessage(error: CreateUserFailure): string {
  switch (error.reason) {
    case "INVALID_REQUEST":
    case "VALIDATION_FAILED":
      return "Kiểm tra thông tin tài khoản và quan hệ giữa chi nhánh, bộ phận, vai trò.";
    case "CSRF_VALIDATION_FAILED":
      return "Kiểm tra bảo mật bị từ chối. Không tự gửi lại yêu cầu.";
    case "SESSION_INVALID":
      return "Cần xác minh phiên trước khi tạo tài khoản.";
    case "FORBIDDEN":
      return "Không có quyền truy cập. Máy chủ phải cho phép tạo tài khoản.";
    case "PASSWORD_CHANGE_REQUIRED":
      return "Đổi mật khẩu trước khi tạo tài khoản.";
    case "DUPLICATE":
      return error.duplicateFields?.length
        ? `Đã được sử dụng: ${error.duplicateFields.join(", ")}.`
        : "Mã nhân viên, tên đăng nhập hoặc email đã được sử dụng.";
    case "LAST_DURABLE_ADMIN":
      return "Tài khoản này ảnh hưởng an toàn truy cập quản trị. Liên hệ quản trị viên có quyền.";
    case "UNSUPPORTED_MEDIA_TYPE":
      return "Máy chủ từ chối định dạng yêu cầu.";
    case "CANCELLED":
      return "Đã hủy trước khi gửi yêu cầu tạo tài khoản.";
    default:
      return "Không bắt đầu được tạo tài khoản. Chưa gửi yêu cầu tạo.";
  }
}
