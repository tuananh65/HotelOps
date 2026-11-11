// Offline guard verification only. No SQL identity, credential or approved lease is supplied.
import { spawnSync } from "node:child_process";
import { readFileSync, writeFileSync, mkdirSync, mkdtempSync } from "node:fs";
import { createHash } from "node:crypto";
import assert from "node:assert/strict";
import { resolve } from "node:path";
const dir = resolve("frontend/.test-output/uc139-guard");
mkdirSync(dir, { recursive: true });
const hash = (b) => createHash("sha256").update(b).digest("hex").toUpperCase();
const artifact = resolve(dir, "offline-artifact.txt");
writeFileSync(artifact, "SYNTHETIC OFFLINE ONLY; NOT AN APPROVAL");
const packageDirectory = mkdtempSync(resolve(dir, "package-")) + "-new";
const emitted = spawnSync(
  "dotnet",
  [
    "scripts/verification/ScheduledWorkdayTrialHost/bin/Debug/net10.0/ScheduledWorkdayTrialHost.dll",
    "--emit-package",
    packageDirectory,
  ],
  { encoding: "utf8" },
);
assert.equal(emitted.status, 0, emitted.stdout + emitted.stderr);
const receipt = {
  ...JSON.parse(
    readFileSync(
      "scripts/verification/ScheduledWorkdayAcceptance/receipt.template.json",
    ),
  ),
  database: "HotelOps_UC139_Trial_OFFLINE",
  databaseGuid: "00000000-0000-0000-0000-000000000001",
  owner: "Offline owner",
  leaseId: "Offline lease",
  trialPackageManifest: resolve(packageDirectory, "package.json"),
  trialPackageSha256: hash(
    readFileSync(resolve(packageDirectory, "package.json")),
  ),
  shiftId: 20,
  raceShiftId: 21,
  rollbackShiftId: 22,
  actorId: 1,
  branchId: 1,
  deniedBranchId: 2,
  assignmentId: 10,
  raceAssignmentId: 11,
  rollbackAssignmentId: 12,
  schemaArtifact: resolve(packageDirectory, "01-install-candidate.sql"),
  schemaSha256: hash(
    readFileSync(resolve(packageDirectory, "01-install-candidate.sql")),
  ),
  fixtureArtifact: resolve(packageDirectory, "04-fixtures-candidate.sql"),
  fixtureSha256: hash(
    readFileSync(resolve(packageDirectory, "04-fixtures-candidate.sql")),
  ),
  origin: "https://localhost:7019/",
};
const file = resolve(dir, "receipt.json");
const save = (r) => {
  writeFileSync(file, JSON.stringify(r));
  return hash(readFileSync(file));
};
const assembly =
  "scripts/verification/ScheduledWorkdayAcceptance/bin/Debug/net10.0/ScheduledWorkdayAcceptance.dll";
function check(args, status, text) {
  const r = spawnSync("dotnet", [assembly, ...args], { encoding: "utf8" });
  assert.equal(r.status, status, r.stdout + r.stderr);
  assert.ok(r.stdout.includes(text), r.stdout);
  console.log("PASS " + text);
}
check([], 2, "default is no execution");
let sha = save(receipt);
check(["--prepare", file, sha], 0, "PREPARED_ONLY; SQL/HTTPS/WRITES NOT_RUN");
check(["--prepare", file, "0".repeat(64)], 1, "FAIL receipt hash");
check(["--execute", file, sha], 1, "FAIL explicit active exclusive lease");
sha = save({ ...receipt, database: "HotelOpsDB" });
check(["--prepare", file, sha], 1, "FAIL disposable database name allowlist");
sha = save({ ...receipt, trialPackageSha256: "0".repeat(64) });
check(["--prepare", file, sha], 1, "FAIL trial execution");
sha = save({ ...receipt, origin: "https://example.com/" });
check(["--prepare", file, sha], 1, "FAIL trial execution");
sha = save({ ...receipt, shiftId: 21 });
check(["--prepare", file, sha], 1, "FAIL trial execution");
sha = save({
  ...receipt,
  databaseGuid: "00000000-0000-0000-0000-000000000000",
});
check(["--prepare", file, sha], 1, "FAIL named owner");
console.log("GUARDS: 9 PASS; SQL/HTTPS NOT_RUN");
