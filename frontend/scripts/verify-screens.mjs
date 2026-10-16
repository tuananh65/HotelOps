import { readdir, readFile, writeFile, mkdir } from "node:fs/promises";
import { build } from "esbuild";
import assert from "node:assert/strict";
import ts from "typescript";
const manifest = JSON.parse(
  await readFile("docs/screen-manifest.json", "utf8"),
);
const catalog = JSON.parse(await readFile("src/catalog.json", "utf8"));
async function files(dir) {
  const entries = await readdir(dir, { withFileTypes: true });
  return (
    await Promise.all(
      entries.map((e) =>
        e.isDirectory() ? files(`${dir}/${e.name}`) : `${dir}/${e.name}`,
      ),
    )
  ).flat();
}
const screenFiles = (await files("src/screens")).filter((f) =>
  f.endsWith(".tsx"),
);
assert.equal(
  screenFiles.length,
  80,
  "Exactly 80 screen-level TSX files are required",
);
assert.equal(manifest.length, 80);
assert.equal(new Set(manifest.map((s) => s.file)).size, 80);
assert.equal(new Set(manifest.map((s) => s.route)).size, 80);
const routeConfig = await readFile("src/routes/routeConfig.ts", "utf8");
const routeAST = ts.createSourceFile(
  "routeConfig.ts",
  routeConfig,
  ts.ScriptTarget.Latest,
  true,
);
const screenImports = routeAST.statements
  .filter(ts.isImportDeclaration)
  .filter((n) => n.moduleSpecifier.text.startsWith("../screens/"));
assert.equal(screenImports.length, 80);
const componentRegistry = routeAST.statements
  .filter(ts.isVariableStatement)
  .flatMap((statement) => statement.declarationList.declarations)
  .find((declaration) => declaration.name.getText(routeAST) === "components");
assert.ok(
  componentRegistry &&
    ts.isArrayLiteralExpression(componentRegistry.initializer),
);
assert.deepEqual(
  componentRegistry.initializer.elements.map((element) =>
    element.getText(routeAST),
  ),
  manifest.map((screen) => screen.component),
);
const features = [
  ...new Set(
    manifest.flatMap((screen) =>
      [...screen.feature.matchAll(/FE-\d+/g)].map((match) => match[0]),
    ),
  ),
].sort();
assert.deepEqual(
  features,
  Array.from(
    { length: 15 },
    (_, index) => `FE-${String(index + 1).padStart(2, "0")}`,
  ),
);
for (const s of manifest) {
  assert.ok(screenFiles.includes(s.file), s.file);
  assert.ok(
    screenImports.some(
      (n) =>
        n.importClause.name.text === s.component &&
        n.moduleSpecifier.text === `../${s.file.slice(4, -4)}`,
    ),
  );
  const source = await readFile(s.file, "utf8");
  assert.match(source, /export default function/);
  assert.doesNotMatch(source, /GenericPage|GenericListPage/);
  assert.equal(s.route, catalog.screens.find((r) => r.id === s.id).route);
}
const ucs = new Set(manifest.flatMap((s) => s.ucs));
assert.equal(ucs.size, 140);
await mkdir(".test-output", { recursive: true });
await build({
  entryPoints: ["src/services.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/demo-data.mjs",
});
class Storage {
  map = new Map();
  getItem(k) {
    return this.map.get(k) || null;
  }
  setItem(k, v) {
    this.map.set(k, String(v));
  }
  removeItem(k) {
    this.map.delete(k);
  }
}
globalThis.localStorage = new Storage();
globalThis.sessionStorage = new Storage();
const service = await import("../.test-output/demo-data.mjs");
const db = service.snapshot();
const mappings = {
  7: "notifications",
  9: "users",
  11: "roles",
  14: "branches",
  16: "locations",
  18: "roomTypes",
  20: "rooms",
  22: "departments",
  26: "cleaning",
  29: "rework",
  32: "checklists",
  34: "inspections",
  35: "inspections",
  36: "inspections",
  37: "inspections",
  41: "incidents",
  44: "occurrences",
  46: "schedules",
  49: "assets",
  50: "assets",
  52: "warehouses",
  55: "items",
  58: "transactions",
  60: "stocktakes",
  63: "materials",
  65: "replenishments",
  67: "tasks",
  72: "shifts",
  75: "handovers",
  77: "requests",
  80: "audit",
};
const rows = manifest.map((s) => {
  const key = mappings[s.id];
  const candidates = key ? db[key].filter((r) => r.branch === "Bui Vien") : [];
  let row =
    s.id === 36
      ? candidates.find((r) => r.status === "Failed")
      : s.id === 37
        ? candidates.find(r => r.rework)
      : s.id === 7
        ? candidates.find(
            (r) =>
              r.recipient === db.users.find((u) => u.username === "admin").id,
          )
        : candidates.find((r) => /-BV-|^USR-DEMO/.test(r.id)) || candidates[0];
  // Reinspection is a selector screen until a submitted rework is assigned an inspector.
  const demo = s.route.replace(":id", row?.id || "select");
  if (s.route.includes(":id") && s.id !== 37)
    assert.ok(row, `No demo record for screen ${s.id}`);
  return { ...s, demo };
});
await writeFile(
  "docs/screen-routes.md",
  "# HotelOps demo URLs\n\nOpen these relative routes on the current deployment origin. All screens are public; listed roles describe source requirements only. Synthetic data uses deterministic IDs and persists locally. /dev/screens resolves working detail links.\n\n" +
    rows
      .map(
        (s) =>
          `## ${String(s.id).padStart(2, "0")} ${s.name}\n\n- Route: \`${s.route}\`\n- Demo: [${s.demo}](${s.demo})\n- Roles: ${s.roles.join(", ")}\n`,
      )
      .join("\n"),
);
await writeFile(
  "docs/screen-matrix.md",
  "# Required screen implementation matrix\n\nExactly 80 required screens; /dev/screens and /dev/design-system are public review utilities and are not counted. All 140 UC IDs are mapped; this is traceability, not independent acceptance of every alternative UC flow.\n\n| # | Screen | File | Route | Demo URL | FE | UC | Primary Role(s) | Status |\n|---|---|---|---|---|---|---|---|---|\n" +
    rows
      .map(
        (s) =>
          `| ${s.id} | ${s.name} | ${s.file} | ${s.route} | ${s.demo} | ${s.feature} | ${s.ucs.join(", ")} | ${s.roles.join(", ")} | Implemented |`,
      )
      .join("\n") +
    "\n",
);
await writeFile(
  "docs/verification/mock-counts.json",
  JSON.stringify(
    Object.fromEntries(Object.entries(db).map(([k, v]) => [k, v.length])),
    null,
    2,
  ),
);
await writeFile(
  "docs/verification/screen-files.json",
  JSON.stringify(screenFiles.sort(), null, 2),
);
console.log(
  `80 required screen files found; 80 route imports verified; ${ucs.size} UC IDs mapped. Demo URLs and matrix updated.`,
);
console.log(
  JSON.stringify(
    Object.fromEntries(
      Object.entries(db)
        .filter(([k]) => !k.startsWith("mock"))
        .map(([k, v]) => [k, v.length]),
    ),
  ),
);
