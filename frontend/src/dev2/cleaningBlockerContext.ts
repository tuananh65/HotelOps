export type CleaningBlockerContext = {
  actor: string; branch: string; task: string; room: number; note: string; reason: string;
  incident?: string; material?: string;
};
export function cleaningBlockerContext(value: unknown, actor: string): CleaningBlockerContext | null {
  if (!value || typeof value !== "object") return null;
  const c = (value as { cleaningBlocker?: CleaningBlockerContext }).cleaningBlocker;
  const id = (v: unknown) => typeof v === "string" && /^[1-9]\d*$/.test(v) && Number.isSafeInteger(Number(v));
  return c && c.actor === actor && id(c.branch) && id(c.task) && Number.isSafeInteger(c.room) && c.room > 0
    && typeof c.note === "string" && c.note.length <= 500 && typeof c.reason === "string"
    && (c.reason === "" || id(c.reason)) && (c.incident == null || c.incident === "" || id(c.incident))
    && (c.material == null || c.material === "" || id(c.material)) ? c : null;
}
