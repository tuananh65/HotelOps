import { useState } from "react";
import CleaningCompletionReport from "./cleaning-completion-report";
import type { Row } from "./api";

export default function MaintenanceResultReport({ work, pending, busy, saved, cancel, canReplace }: {
  work: Row; pending: boolean; busy: (value: boolean) => void; saved: () => void; cancel: () => void; canReplace: boolean;
}) {
  const [outcome, setOutcome] = useState<"complete" | "unrepairable">("complete");
  return <div className="maintenance-result-report">
    <fieldset disabled={pending} className="maintenance-result-options">
      <legend>Kết quả sửa chữa</legend>
      <label><input type="radio" name="maintenance-outcome" value="complete" checked={outcome === "complete"} onChange={() => setOutcome("complete")}/>Sửa thành công</label>
      <label><input type="radio" name="maintenance-outcome" value="unrepairable" disabled={!canReplace} checked={outcome === "unrepairable"} onChange={() => setOutcome("unrepairable")}/>Không thể sửa chữa — Cần mua mới</label>
      {!canReplace && <p>Công việc chưa gắn tài sản cần thay mới. Liên hệ trưởng đội bảo trì để kiểm tra thông tin tài sản.</p>}
    </fieldset>
    <CleaningCompletionReport key={outcome} work={work} maintenanceOutcome={outcome} pending={pending} busy={busy} saved={saved} cancel={cancel}/>
  </div>;
}
