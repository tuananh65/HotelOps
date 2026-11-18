import { useEffect, useRef, useState } from "react";
import type { PublicSession } from "../auth/authStore";
import type { UsersApiError, UsersApiResult } from "./contracts";
import { useUsersContext } from "./UsersRouteBoundary";

interface CompletedRead<T> {
  readonly owner: PublicSession;
  readonly key: string;
  readonly attempt: number;
  readonly result: UsersApiResult<T>;
}

export function useUserRead<T>(
  key: string,
  load: (signal: AbortSignal) => Promise<UsersApiResult<T>>,
) {
  const { session, sessionFailure } = useUsersContext();
  const generation = useRef(0);
  const [attempt, setAttempt] = useState(0);
  const [completed, setCompleted] = useState<CompletedRead<T> | null>(null);
  useEffect(() => {
    const current = ++generation.current;
    const controller = new AbortController();
    setCompleted(null);
    // StrictMode's discarded setup is cancelled before it can issue a GET.
    void Promise.resolve().then(async () => {
      if (controller.signal.aborted || current !== generation.current) return;
      let result: UsersApiResult<T>;
      try {
        result = await load(controller.signal);
      } catch {
        result = { ok: false, error: { kind: "network" } };
      }
      if (controller.signal.aborted || current !== generation.current) return;
      if (!result.ok) sessionFailure(result.error);
      setCompleted({ owner: session, key, attempt, result });
    });
    return () => {
      ++generation.current;
      controller.abort();
    };
  }, [session, key, attempt, load, sessionFailure]);

  // Mask old data during render, before effect cleanup, including same-user identity replacement.
  const result =
    completed?.owner === session &&
    completed.key === key &&
    completed.attempt === attempt
      ? completed.result
      : null;
  return { result, reload: () => setAttempt((value) => value + 1) };
}

export function usersReadMessage(error: UsersApiError): string {
  if (error.kind === "invalid-request")
    return "Kiểm tra bộ lọc và các mã dạng số.";
  if (error.kind === "cancelled")
    return "Đã hủy đọc dữ liệu. Bạn có thể thử lại.";
  if (error.kind === "invalid-response")
    return "Không xác thực được phản hồi máy chủ.";
  if (error.kind === "http") {
    if (error.status === 400) return "Kiểm tra bộ lọc yêu cầu.";
    if (error.status === 401)
      return "Cần kiểm tra phiên trước khi tải dữ liệu tài khoản.";
    if (error.status === 403)
      return "Không có quyền truy cập. Phản hồi này không xác nhận tài khoản có tồn tại hay không.";
    if (error.status === 404) return "Không tìm thấy tài khoản yêu cầu.";
  }
  return "Thông tin tài khoản hiện chưa khả dụng. Bạn có thể thử lại.";
}
