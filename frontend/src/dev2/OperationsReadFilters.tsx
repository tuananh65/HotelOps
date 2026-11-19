import { SearchableSelect } from "../components/SearchableSelect";
import { useContext, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { OperationBranchesContext } from "./AuthorizedOperations";
import type { Operation, Values } from "./contracts";
import { Fields } from "./Fields";
import { SearchInput, searchSuggestions } from "../components/SearchInput";

export function OperationsReadFilters({
  operation,
  values,
  change,
  busy,
  run,
  records,
}: {
  operation: Operation;
  values: Values;
  change: (values: Values) => void;
  busy: boolean;
  run: () => void;
  records?: unknown;
}) {
  const branches = useContext(OperationBranchesContext);
  const [, setQuery] = useSearchParams();
  const [branchDraft, setBranchDraft] = useState(String(values.branchId ?? ""));
  return (
    <form
      className="ops-read-card ops-read-filters"
      onSubmit={(event) => {
        event.preventDefault();
        if (branches.global && branchDraft !== String(values.branchId ?? "")) {
          if (
            /^[1-9]\d*$/.test(branchDraft) &&
            Number.isSafeInteger(Number(branchDraft))
          )
            setQuery({ branchId: branchDraft });
        } else run();
      }}
    >
      <div className="ops-filter-primary">
        <label>
          Chi nhánh
          {branches.global ? (
            <input
              type="number"
              min="1"
              required
              value={branchDraft}
              onChange={(e) => setBranchDraft(e.target.value)}
            />
          ) : (
            <SearchableSelect
              aria-label="Chi nhánh"
              required
              value={String(values.branchId ?? "")}
              onChange={(e) => setQuery({ branchId: e.target.value })}
            >
              <option value="">Chọn chi nhánh</option>
              {branches.ids.map((id) => (
                <option key={id} value={id}>
                  {branches.names?.[id] ?? "Chưa có tên chi nhánh"}
                </option>
              ))}
            </SearchableSelect>
          )}
        </label>
        <label>
          Tìm kiếm
          <SearchInput suggestions={searchSuggestions(records)}
            maxLength={200}
            placeholder={
              operation.module === "maintenance"
                ? "Mã hoặc tiêu đề công việc"
                : "Mã yêu cầu hoặc mục đích sử dụng"
            }
            value={String(values.keyword ?? "")}
            onChange={(e) =>
              change({ ...values, keyword: e.target.value, page: 1 })
            }
          />
        </label>
        <button disabled={busy} type="submit">
          {busy ? "Đang tải…" : "Áp dụng"}
        </button>
      </div>
      <details>
        <summary>Bộ lọc nâng cao</summary>
        <Fields
          fields={operation.fields.filter(
            (f) =>
              !["branchId", "keyword", "page", "pageSize"].includes(f.name),
          )}
          values={values}
          onChange={(v) => change({ ...v, page: 1 })}
          actor=""
        />
      </details>
    </form>
  );
}
