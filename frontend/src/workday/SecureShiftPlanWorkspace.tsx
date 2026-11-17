import { ShiftDefinitionsPanel } from "./ShiftDefinitionsPanel";
import { ShiftRosterPanel } from "./ShiftRosterPanel";
import { Fragment, useMemo, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { ShiftPlanPreviewPanel } from "./ShiftPlanPreviewPanel";
import { createShiftPlanApi } from "./shiftPlanApi";
/** Server authorizes every draft and repeats eligibility within the save transaction. */
export function SecureShiftPlanWorkspace({
  saveEnabled = false,
}: {
  saveEnabled?: boolean;
}) {
  const { state } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const [binding, setBinding] = useState(owner);
  const [generation, setGeneration] = useState(0);
  if (binding !== owner) {
    setBinding(owner);
    setGeneration(value => value + 1);
  }
  const [rosterRevision, setRosterRevision] = useState(0);
  const api = useMemo(
    () => createShiftPlanApi(globalThis.fetch.bind(globalThis), saveEnabled),
    [saveEnabled],
  );
  if (owner === null) return null;
  return (
    <Fragment key={generation}><ShiftDefinitionsPanel /><ShiftRosterPanel revision={rosterRevision} /><ShiftPlanPreviewPanel
      adapter={api.preview}
      saveAdapter={saveEnabled ? api.save : undefined}
      onSaved={() => setRosterRevision(value => value + 1)}
    /></Fragment>
  );
}
