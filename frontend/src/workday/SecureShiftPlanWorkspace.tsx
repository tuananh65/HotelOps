import { useMemo } from "react";
import { ShiftPlanPreviewPanel } from "./ShiftPlanPreviewPanel";
import { createShiftPlanApi } from "./shiftPlanApi";
/** Standalone source integration. Production router remains unchanged. */
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
