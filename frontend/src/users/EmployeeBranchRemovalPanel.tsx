import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import { planningRequest } from "../operationalTasks/planning";
import { TaskProgressError } from "../operationalTasks/progress";

type Branch = { id: number; name: string; isPrimary: boolean };
export function EmployeeBranchRemovalPanel({
  user,
  branch,
  version,
  branches,
  onChanged,
}: {
  user: number;
  branch: number;
  version: string;
  branches: Branch[];
  onChanged: () => void;
}) {
  const { state, actions } = useAuth();
  const owner = state.authorityGeneration;
  const [replacement, setReplacement] = useState(""),
    [department, setDepartment] = useState(""),
    [reason, setReason] = useState("");
  const [options, setOptions] = useState<{ id: number; name: string }[]>([]),
    [confirmed, setConfirmed] = useState(false),
    [busy, setBusy] = useState(false),
    [message, setMessage] = useState("");
  const active = useRef<AbortController | null>(null),
    saving = useRef(false),
    generation = useRef(owner);
  generation.current = owner;
  const primary = branches.find((b) => b.id === branch)?.isPrimary === true;
  useEffect(
    () => () => active.current?.abort(),
    [owner, user, branch, version],
  );
  useEffect(() => {
    const abort = new AbortController();
    setOptions([]);
    setDepartment("");
    if (!replacement) return () => abort.abort();
    void planningRequest(
      `/api/users/${user}/branches/${replacement}/departments`,
      abort.signal,
    )
      .then((r) => r.json())
      .then((body: unknown) => {
        if (
          !isObject(body) ||
          body.userId !== user ||
          body.branchId !== Number(replacement) ||
          !Array.isArray(body.replacementDepartments)
        )
          throw Error("Phân công thành viên thay thế không hợp lệ");
        const options = body.replacementDepartments.flatMap((d) =>
          isObject(d) &&
          typeof d.id === "number" &&
          Number.isSafeInteger(d.id) &&
          d.id > 0 &&
          typeof d.name === "string"
            ? [{ id: d.id, name: d.name }]
            : [],
        );
        if (!abort.signal.aborted) setOptions(options);
      })
      .catch((e) => {
        if (!abort.signal.aborted) {
          setMessage(
            "Phân công thay thế chưa khả dụng. Tải lại trước khi thử lại.",
          );
          if (e instanceof TaskProgressError && e.status === 401)
            actions.invalidateSession(owner);
        }
      });
    return () => abort.abort();
  }, [owner, user, replacement, actions]);
  async function remove() {
    if (
      saving.current ||
      !confirmed ||
      !reason.trim() ||
      (primary &&
        (!replacement || !options.some((d) => d.id === Number(department))))
    )
      return;
    saving.current = true;
    setBusy(true);
    setMessage("");
    const abort = new AbortController();
    active.current = abort;
    try {
      const csrf: unknown = await (
        await planningRequest("/api/auth/csrf", abort.signal)
      ).json();
      if (
        !isObject(csrf) ||
        csrf.headerName !== "X-CSRF-TOKEN" ||
        typeof csrf.csrfToken !== "string" ||
        !/^[\x21-\x7e]+$/.test(csrf.csrfToken) ||
        generation.current !== owner
      )
        throw Error("Phiên đăng nhập đã thay đổi; tải lại trước khi lưu.");
      const response = await fetch(
        `/api/users/${user}/branches/${branch}/remove`,
        {
          method: "POST",
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: abort.signal,
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            "If-Match": version,
          },
          body: JSON.stringify({
            reason,
            ...(primary
              ? {
                  replacementBranchId: Number(replacement),
                  replacementDepartmentId: Number(department),
                }
              : {}),
          }),
        },
      );
      if (!response.ok) {
        if (response.status === 401) throw new TaskProgressError(401);
        const problem: unknown = await response.json();
        const details =
          isObject(problem) && Array.isArray(problem.dependencies)
            ? problem.dependencies
                .flatMap((d) =>
                  isObject(d) &&
                  typeof d.kind === "string" &&
                  typeof d.count === "number"
                    ? [`${d.kind}: ${d.count}`]
                    : [],
                )
                .join("; ")
            : "";
        throw Error(
          (isObject(problem) && typeof problem.title === "string"
            ? problem.title
            : "Yêu cầu xóa bị từ chối") + (details ? ": " + details : ""),
        );
      }
      if (response.status !== 204)
        throw Error("Xác nhận xóa phân quyền chi nhánh không hợp lệ");
      if (!abort.signal.aborted && generation.current === owner) {
        (announceWorkflowSuccess(
          "MSG08: Đã xóa quyền chi nhánh và phân công thành viên; giữ lịch sử và thu hồi phiên nhân viên.",
          "users/EmployeeBranchRemovalPanel.tsx",
        ),
          setMessage(
            "MSG08: Đã xóa quyền chi nhánh và phân công thành viên; giữ lịch sử và thu hồi phiên nhân viên.",
          ));
        onChanged();
      }
    } catch (e) {
      if (!abort.signal.aborted && generation.current === owner) {
        if (e instanceof TaskProgressError && e.status === 401)
          actions.invalidateSession(owner);
        setMessage(
          `${e instanceof Error ? e.message : "Chưa xác nhận kết quả"}. Tải lại phân công thành viên trước khi thử lại.`,
        );
        // A stale version or ambiguous response must not be silently replayed.
        setConfirmed(false);
      }
    } finally {
      saving.current = false;
      if (!abort.signal.aborted && generation.current === owner) setBusy(false);
    }
  }
  return (
    <ActionDialog
      scope="users/EmployeeBranchRemovalPanel.tsx"
      title="Xóa quyền chi nhánh"
      busy={busy}
    >
      <section>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void remove();
          }}
        >
          <fieldset disabled={busy}>
            <p>
              Mọi phân công bộ phận trong chi nhánh này sẽ kết thúc. Cần xử lý
              vai trò, quyền, phân công trưởng bộ phận và trách nhiệm còn mở
              trước; không tự điều chuyển.
            </p>
            {primary && (
              <>
                <label>
                  Chi nhánh chính thay thế
                  <select
                    required
                    value={replacement}
                    onChange={(e) => setReplacement(e.target.value)}
                  >
                    <option value="">Chọn chi nhánh đang hoạt động</option>
                    {branches
                      .filter((b) => b.id !== branch)
                      .map((b) => (
                        <option key={b.id} value={b.id}>
                          {b.name}
                        </option>
                      ))}
                  </select>
                </label>
                <label>
                  Bộ phận chính thay thế
                  <select
                    required
                    value={department}
                    onChange={(e) => setDepartment(e.target.value)}
                  >
                    <option value="">
                      Chọn phân công thành viên đang hiệu lực
                    </option>
                    {options.map((d) => (
                      <option key={d.id} value={d.id}>
                        {d.name}
                      </option>
                    ))}
                  </select>
                </label>
              </>
            )}
            <label>
              Lý do
              <input
                required
                minLength={10}
                maxLength={250}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
            </label>
            <label>
              <input
                required
                type="checkbox"
                checked={confirmed}
                onChange={(e) => setConfirmed(e.target.checked)}
              />
              Xác nhận xóa phân quyền chi nhánh và thu hồi phiên nhân viên.
            </label>
            <button data-dialog-action disabled={!confirmed}>
              Xóa quyền truy cập chi nhánh
            </button>
          </fieldset>
        </form>
        {message && <p role="status">{message}</p>}
      </section>
    </ActionDialog>
  );
}
