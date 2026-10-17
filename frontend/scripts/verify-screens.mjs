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
assert.equal(ucs.size, 144);
assert.equal(Object.keys(catalog.ucs).length, 144);
const apiInventory = JSON.parse(
  await readFile("docs/api-integration/screen-inventory.json", "utf8"),
);
assert.equal(apiInventory.length, 80);
for (const screen of manifest) {
  assert.deepEqual(
    screen.ucs,
    catalog.screens.find((row) => row.id === screen.id).ucs,
  );
  assert.ok(
    apiInventory.some(
      (row) => row.id === screen.id && row.route === screen.route,
    ),
  );
}
await mkdir(".test-output", { recursive: true });
await build({
  entryPoints: ["src/api/endpoints.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/verified-endpoints.mjs",
});
const { actionContracts } =
  await import("../.test-output/verified-endpoints.mjs");
assert.equal(Object.keys(actionContracts).length, 103);
assert.equal(
  new Set(Object.values(actionContracts).map((action) => action.id)).size,
  103,
);
await writeFile(
  "docs/screen-routes.md",
  "# HotelOps screen routes\n\nRoutes require an API-backed session except Login/Forgot Password. For detail routes, use identifiers returned by the API; no prototype record is generated.\n\n" +
    manifest
      .map(
        (screen) =>
          `- ${screen.id}. ${screen.name}: \`${screen.route}\` (${screen.ucs.join(", ")})`,
      )
      .join("\n") +
    "\n",
);
await writeFile(
  "docs/screen-matrix.md",
  "# Required screen implementation matrix\n\n80 required screens and 144 mapped UCs. This verifies source traceability; it does not independently accept every alternative UC flow or prove a real server integration. Utility routes are outside the 80-screen count.\n\n| ID | Screen | File | Route | FE | UC | Actors from SRS | Implementation |\n|---|---|---|---|---|---|---|---|\n" +
    manifest
      .map(
        (screen) =>
          `|${screen.id}|${screen.name}|${screen.file}|${screen.route}|${screen.feature}|${screen.ucs.join(", ")}|${screen.roles.join(", ")}|API contract wired; real server pending|`,
      )
      .join("\n") +
    "\n",
);
await writeFile(
  "docs/verification/screen-files.json",
  JSON.stringify(screenFiles.sort(), null, 2) + "\n",
);
console.log(
  `80 screen files and route imports verified; ${ucs.size} UCs mapped; 103 unique business command contracts. No prototype database was loaded.`,
);
