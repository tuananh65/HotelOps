import { DeferredFilterPanel } from "../components/workspace-interactions";
import { useEffect, useState } from "react";
import type { AuthorityGeneration } from "../auth/authStore";
import { AppButton } from "../components/design-system";
import { LoadingIndicator } from "../components/LoadingIndicator";
import {
  DashboardFailure,
  readDashboardOptions,
  type DashboardOptions,
} from "./dashboardApi";
const empty = { branchId: "", departmentId: "", from: "", to: "" };
export function DashboardFilters({
  owner,
  roomsOnly = false,
  invalidate,
  apply,
}: {
  owner: AuthorityGeneration;
  roomsOnly?: boolean;
  invalidate: (owner: AuthorityGeneration) => void;
  apply: (query: string) => void;
}) {
  const [retry, setRetry] = useState(0);
  const [result, setResult] = useState<{
    owner: AuthorityGeneration;
    options?: DashboardOptions;
    error?: string;
  } | null>(null);
  const [draft, setDraft] = useState(empty);
  const [appliedDraft, setAppliedDraft] = useState(empty);
  useEffect(() => {
    const controller = new AbortController();
    setResult(null);
    setDraft(empty);
    setAppliedDraft(empty);
    void readDashboardOptions(controller.signal, roomsOnly)
      .then((options) => {
        if (!controller.signal.aborted) setResult({ owner, options });
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        setResult({
          owner,
          error:
            "Bộ lọc tổng quan chưa khả dụng. Thử tải lại lựa chọn được phép.",
        });
        if (error instanceof DashboardFailure && error.status === 401)
          invalidate(owner);
      });
    return () => controller.abort();
  }, [owner, retry, invalidate, roomsOnly]);
  const options = result?.owner === owner ? result.options : undefined;
  const error = result?.owner === owner ? result.error : undefined;
  const departments =
    options?.departments.filter(
      (item) => !draft.branchId || String(item.branchId) === draft.branchId,
    ) ?? [];
  function commit(value: typeof empty) {
    if (
      !options ||
      (value.branchId &&
        !options.branches.some((item) => String(item.id) === value.branchId)) ||
      (value.departmentId &&
        !options.departments.some(
          (item) =>
            String(item.id) === value.departmentId &&
            (!value.branchId || String(item.branchId) === value.branchId),
        ))
    )
      return;
    const query = new URLSearchParams();
    for (const [key, item] of Object.entries(value))
      if (item) query.set(key, item);
    setAppliedDraft({ ...value });
    setDraft({ ...value });
    apply(query.toString());
  }
  return (
    <section
      className="ds-dashboard-filter-toolbar"
      aria-label="Phạm vi tổng quan"
    >
      <div>
        <strong>Phạm vi dữ liệu</strong>
        <p>Chọn chi nhánh, bộ phận và khoảng thời gian cần theo dõi.</p>
      </div>
      <DeferredFilterPanel
        activeCount={Object.values(appliedDraft).filter(Boolean).length}
        disabled={!options}
        onOpen={() => setDraft({ ...appliedDraft })}
        onReset={() => setDraft(empty)}
        onApply={() => commit(draft)}
      >
        {!options && !error && (
          <LoadingIndicator compact label="Đang tải bộ lọc được phép sử dụng" />
        )}
        {error && (
          <>
            <p role="alert">{error}</p>
            <AppButton onClick={() => setRetry((value) => value + 1)}>
              Tải lại bộ lọc
            </AppButton>
          </>
        )}
        <fieldset disabled={!options}>
          <legend>Điều kiện lọc</legend>
          <label>
            Chi nhánh
            <select
              aria-label="Chi nhánh"
              value={options ? draft.branchId : ""}
              onChange={(event) =>
                setDraft((value) => ({
                  ...value,
                  branchId: event.target.value,
                  departmentId: "",
                }))
              }
            >
              <option value="">Tất cả chi nhánh được phép</option>
              {options?.branches.map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {branch.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Bộ phận
            <select
              aria-label="Bộ phận"
              value={options ? draft.departmentId : ""}
              onChange={(event) =>
                setDraft((value) => ({
                  ...value,
                  departmentId: event.target.value,
                }))
              }
            >
              <option value="">Tất cả bộ phận được phép</option>
              {departments.map((department) => (
                <option key={department.id} value={department.id}>
                  {department.name} —{" "}
                  {
                    options?.branches.find(
                      (branch) => branch.id === department.branchId,
                    )?.name
                  }
                </option>
              ))}
            </select>
          </label>
          <label>
            Từ ngày (UTC)
            <input
              type="date"
              value={draft.from}
              max={draft.to || undefined}
              onChange={(event) =>
                setDraft((value) => ({ ...value, from: event.target.value }))
              }
            />
          </label>
          <label>
            Đến ngày (UTC)
            <input
              type="date"
              value={draft.to}
              min={draft.from || undefined}
              onChange={(event) =>
                setDraft((value) => ({ ...value, to: event.target.value }))
              }
            />
          </label>
        </fieldset>
      </DeferredFilterPanel>
      {Object.values(appliedDraft).some(Boolean) && (
        <div className="ds-filter-chips" aria-label="Bộ lọc đã áp dụng">
          {Object.entries(appliedDraft)
            .filter(([, value]) => value)
            .map(([field, value]) => (
              <button
                type="button"
                key={field}
                aria-label={`Xóa bộ lọc ${field === "branchId" ? "chi nhánh" : field === "departmentId" ? "bộ phận" : field === "from" ? "từ ngày" : "đến ngày"}`}
                onClick={() =>
                  commit({
                    ...appliedDraft,
                    [field]: "",
                    ...(field === "branchId" ? { departmentId: "" } : {}),
                  })
                }
              >
                {field === "branchId"
                  ? (options?.branches.find(
                      (branch) => String(branch.id) === value,
                    )?.name ?? "Chi nhánh")
                  : field === "departmentId"
                    ? (options?.departments.find(
                        (department) => String(department.id) === value,
                      )?.name ?? "Bộ phận")
                    : `${field === "from" ? "Từ" : "Đến"}: ${value}`}{" "}
                ×
              </button>
            ))}
          <AppButton variant="ghost" onClick={() => commit(empty)}>
            Xóa tất cả
          </AppButton>
        </div>
      )}
    </section>
  );
}
