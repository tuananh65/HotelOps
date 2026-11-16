import { ShiftDefinitionsPanel } from "./ShiftDefinitionsPanel";
import { ShiftRosterPanel } from "./ShiftRosterPanel";
import { useMemo, useState } from "react";
import { ShiftPlanPreviewPanel } from "./ShiftPlanPreviewPanel";
import { createShiftPlanApi } from "./shiftPlanApi";
/** Server authorizes every draft and repeats eligibility within the save transaction. */
export function SecureShiftPlanWorkspace({
  saveEnabled = false,
}: {
  saveEnabled?: boolean;
}) {
  const [rosterRevision, setRosterRevision] = useState(0);
  const api = useMemo(
    () => createShiftPlanApi(globalThis.fetch.bind(globalThis), saveEnabled),
    [saveEnabled],
  );
  return (
    <><ShiftDefinitionsPanel /><ShiftRosterPanel revision={rosterRevision} /><ShiftPlanPreviewPanel
      adapter={api.preview}
      saveAdapter={saveEnabled ? api.save : undefined}
      onSaved={() => setRosterRevision(value => value + 1)}
    /></>
  );
}
