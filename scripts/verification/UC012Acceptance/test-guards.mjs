import { test } from "node:test";
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { createHash } from "node:crypto";
const dll = resolve(
  process.env.UC012_RUNNER_DLL ??
    "scripts/verification/UC012Acceptance/bin/Debug/net10.0/UC012Acceptance.dll",
);
const folder = mkdtempSync(join(tmpdir(), "hotelops-uc012-guards-"));
const hash = (path) =>
  createHash("sha256").update(readFileSync(path)).digest("hex");
function run(...args) {
  const env = { ...process.env };
  for (const key of [
    "UC012_TRIAL_SQL",
    "UC012_EXECUTE",
    "UC012_ACTOR_PASSWORD",
    "UC012_DENIED_PASSWORD",
  ])
    delete env[key];
  const p = spawnSync("dotnet", [dll, ...args], { env, encoding: "utf8" });
  assert.equal(p.error, undefined);
  assert.ok(!p.stdout.includes("Password="));
  return p;
}
test("default disabled", () => assert.equal(run().status, 2));
test("offline behavioral guards", () => {
  const p = run("--self-test");
  assert.equal(p.status, 0);
  assert.match(p.stdout, /44\/44/);
});
const prepared = join(folder, "prepared");
test("prepare emits unapproved template and exact 12-case manifest", () => {
  assert.equal(run("--prepare", prepared).status, 0);
  const r = JSON.parse(readFileSync(join(prepared, "receipt.template.json"))),
    m = JSON.parse(readFileSync(join(prepared, "manifest.json")));
  assert.equal(r.ExecutionApproved, false);
  assert.equal(m.CaseIds.length, 12);
  assert.equal(new Set(m.CaseIds).size, 12);
  assert.equal(m.Sql, "NOT_RUN");
  assert.equal(m.Https, "NOT_RUN");
});
test("prepare refuses overwrite", () =>
  assert.equal(run("--prepare", prepared).status, 2));
for (const mode of ["--preflight", "--execute"])
  test(mode + " unapproved template fails before connection/listener", () => {
    const input = join(prepared, "receipt.template.json"),
      output = join(folder, mode.slice(2) + ".json");
    assert.equal(run(mode, input, hash(input), output).status, 1);
    const r = JSON.parse(readFileSync(output));
    assert.equal(r.FailureCode, "OWNER_APPROVAL_REQUIRED");
    assert.equal(r.RuntimeAcceptance, false);
    assert.deepEqual(r.Cases, []);
  });
test("bad hash fails without credentials or raw exception", () => {
  const input = join(prepared, "receipt.template.json"),
    output = join(folder, "bad-hash.json");
  assert.equal(run("--execute", input, "0".repeat(64), output).status, 1);
  assert.equal(
    JSON.parse(readFileSync(output)).FailureCode,
    "ARTIFACT_IDENTITY_REQUIRED",
  );
});
test("unknown receipt fields fail closed", () => {
  const input = join(folder, "unknown.json"),
    output = join(folder, "unknown-result.json");
  writeFileSync(input, JSON.stringify({ adminBypass: true }));
  assert.equal(run("--execute", input, hash(input), output).status, 1);
  assert.equal(JSON.parse(readFileSync(output)).RuntimeAcceptance, false);
});
test("report requires exact artifact hash", () => {
  const p = join(folder, "preflight.json");
  assert.equal(run("--report", p, hash(p)).status, 0);
  assert.equal(run("--report", p, "0".repeat(64)).status, 1);
});
test("existing reports cannot be overwritten", () => {
  const input = join(prepared, "receipt.template.json"),
    output = join(folder, "preflight.json");
  const before = hash(output);
  assert.equal(run("--preflight", input, hash(input), output).status, 2);
  assert.equal(hash(output), before);
});
test("incomplete report cannot claim live acceptance", () => {
  const p = join(folder, "incomplete-report.json");
  writeFileSync(
    p,
    JSON.stringify({
      Status: "SQL_HTTPS_ACCEPTANCE_PASS",
      RuntimeAcceptance: true,
      Mode: "--execute",
      PreflightPassed: true,
      FailureCode: "",
      Cases: [],
    }),
  );
  assert.equal(run("--report", p, hash(p)).status, 1);
});
