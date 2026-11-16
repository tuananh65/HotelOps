import { useMemo } from "react";
import { ShiftPlanPreviewPanel } from "./ShiftPlanPreviewPanel";
import { createShiftPlanApi } from "./shiftPlanApi";
/** Server authorizes every draft and repeats eligibility within the save transaction. */
export function SecureShiftPlanWorkspace({
  saveEnabled = false,
}: {
  saveEnabled?: boolean;
}) {
  const api = useMemo(
    () => createShiftPlanApi(globalThis.fetch.bind(globalThis), saveEnabled),
    [saveEnabled],
  );
  return (
    <ShiftPlanPreviewPanel
      adapter={api.preview}
      saveAdapter={saveEnabled ? api.save : undefined}
    />
  );
}
