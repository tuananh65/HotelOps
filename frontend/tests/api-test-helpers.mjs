import { build } from "esbuild";
import { mkdir } from "node:fs/promises";
export async function bundle(entry, tag, authMode = "cookie") {
  await mkdir(".test-output", { recursive: true });
  const outfile = `.test-output/${tag}.mjs`;
  await build({
    entryPoints: [entry],
    bundle: true,
    platform: "node",
    format: "esm",
    outfile,
    define: {
      "import.meta.env": JSON.stringify({
        VITE_AUTH_MODE: authMode,
        VITE_API_BASE_URL: "https://api.test",
      }),
    },
  });
  return import(`../${outfile}?${Math.random()}`);
}
export class Storage {
  data = new Map();
  getItem(k) {
    return this.data.get(k) ?? null;
  }
  setItem(k, v) {
    this.data.set(k, String(v));
  }
  removeItem(k) {
    this.data.delete(k);
  }
}
export function browserGlobals() {
  globalThis.localStorage = new Storage();
  globalThis.sessionStorage = new Storage();
  globalThis.window = { dispatchEvent: () => true };
}
export const json = (value, status = 200) =>
  new Response(JSON.stringify(value), {
    status,
    headers: { "Content-Type": "application/json" },
  });
export const entity = (id = "room-1", extra = {}) => ({
  id,
  code: "R-101",
  name: "101",
  statusCode: "PENDING_INSPECTION",
  branchId: "1",
  branchName: "Bui Vien",
  departmentId: "2",
  departmentName: "Housekeeping",
  rowVersion: "AAAAAQ==",
  createdAt: "2026-10-16T08:00:00Z",
  allowedActions: [],
  canEdit: true,
  attributes: {},
  ...extra,
});
export const session = (grants = []) => ({
  principal: {
    id: "7",
    name: "Tester",
    username: "tester",
    email: "tester@example.test",
    roleNames: ["Housekeeping Staff"],
    primaryBranch: { id: "1", name: "Bui Vien" },
    primaryDepartment: { id: "2", name: "Housekeeping" },
    branches: [{ id: "1", name: "Bui Vien" }],
    departments: [{ id: "2", name: "Housekeeping" }],
    grants,
  },
  expiresAt: "2099-01-01T00:00:00Z",
  mustChangePassword: false,
  csrfToken: "test-csrf",
});
