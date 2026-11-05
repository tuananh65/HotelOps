import { domainFor, screenById } from "../domain";
import { currentUser, snapshot, visible } from "../services";

export function demoPath(id: number): string {
  const screen = screenById(id);
  if (!screen.route.includes(":id")) return screen.route;
  const key =
    id === 50 ? "assets" : id === 36 ? "inspections" : domainFor(id).key;
  const scoped = currentUser() ? visible(key) : [];
  const rows = scoped.length
    ? scoped
    : snapshot()[id === 44 ? "occurrences" : key] || [];
  const row =
    id === 36
      ? rows.find((r) => r.status === "Failed")
      : id === 37
        ? rows.find((r) => r.rework)
        : rows[0];
  return screen.route.replace(":id", row?.id || "select");
}
