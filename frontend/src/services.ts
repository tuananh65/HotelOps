/** API facade retained for the existing 80 screens. No business persistence or state transitions run here. */
import {
  actions,
  domainFor,
  domains,
  screenById,
  type Action,
  type Domain,
  type RecordData,
} from "./domain";
import {
  actionContracts,
  endpoints,
  resourcePaths,
  type ResourceKey,
} from "./api/endpoints";
import {
  entityFromDto,
  incidentFromDto,
  entityToDto,
  commandLines,
  localDateTimeToUtc,
} from "./api/adapters";
import { apiConfig, requireAuthConfiguration } from "./api/config";
import {
  ApiError,
  configureSession,
  invalidateSessionRequests,
  request,
  setUnauthorizedHandler,
} from "./api/http";
import {
  authService,
  contextService,
  incidentReads,
  moduleServices,
  readAll,
} from "./api/modules";
import { isInventoryScreen, loadInventoryScreen } from "./controllers/inventoryController";
import type {
  BalanceDto,
  CommandRequest,
  EntityDto,
  Query,
  SessionDto,
  UserProvisioningDto,
} from "./api/types";
export type Database = { [key: string]: RecordData[] };
export type Session = { userId: string; expires: number; restricted: boolean };
const emptyDatabase = (): Database =>
  Object.fromEntries(
    [
      ...Object.keys(resourcePaths),
      "mockMetadata",
      "alertEpisodes",
      "reports",
    ].map((key) => [key, []]),
  );
let database = emptyDatabase();
let inventoryReadRecords = new WeakSet<RecordData>();
let session: SessionDto | null = null;
let authState: "initial" | "loading" | "authenticated" | "anonymous" | "error" =
  "initial";
let authError: Error | null = null;
let balances: BalanceDto[] = [];
let metrics: Record<string, number | null> = {};
let snapshotTime = "";
let missingSources: string[] = [];
let activeScreen: { id: number; recordId?: string; search: string } | undefined;
let generation = 0;
let identityEpoch = 0;
const listeners = new Set<() => void>();
export const subscribe = (listener: () => void) => {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
};
export const snapshot = () => database;
const publish = () => {
  database = { ...database };
  listeners.forEach((listener) => listener());
};
export const authentication = () => ({ state: authState, error: authError });
export const serverSnapshot = () => snapshotTime;
export const serverMetric = (key: string) => metrics[key] ?? "N/A";
export const missingDataSources = () => missingSources;
export const resolveDemoRecordId = (_key: string, requested: string) =>
  requested;
export function currentSession(): Session | null {
  return session && new Date(session.expiresAt).getTime() > Date.now()
    ? {
        userId: session.principal.id,
        expires: new Date(session.expiresAt).getTime(),
        restricted: session.mustChangePassword,
      }
    : null;
}
export function currentUser(): RecordData | null {
  const p = session?.principal;
  return p
    ? {
        id: p.id,
        name: p.name,
        username: p.username,
        email: p.email,
        roles: p.roleNames,
        branch: p.primaryBranch.name,
        branchId: p.primaryBranch.id,
        branches: p.branches.map((b) => b.name),
        department: p.primaryDepartment.name,
        departmentId: p.primaryDepartment.id,
        status: "Active",
        version: 0,
        createdAt: "",
      }
    : null;
}
function clearSession() {
  invalidateSessionRequests();
  generation++;
  identityEpoch++;
  session = null;
  authState = "anonymous";
  authError = null;
  database = emptyDatabase();
  inventoryReadRecords = new WeakSet<RecordData>();
  balances = [];
  metrics = {};
  snapshotTime = "";
  missingSources = [];
  activeScreen = undefined;
  configureSession();
  publish();
}
setUnauthorizedHandler(clearSession);
function applySession(result: SessionDto, preserveData = false) {
  if (!result?.principal?.grants || !result.expiresAt)
    throw new ApiError(
      "The session response does not match SessionDto.",
      0,
      "server",
    );
  if (apiConfig.authMode === "bearer" && !result.accessToken)
    throw new ApiError(
      "Bearer mode requires an accessToken from the agreed login response.",
      0,
      "configuration",
    );
  session = result;
  authState = "authenticated";
  authError = null;
  if (!preserveData) {
    identityEpoch++;
    invalidateSessionRequests();
  }
  const previousBranch = branchContext();
  if (
    previousBranch !== "All branches" &&
    !result.principal.branches.some((branch) => branch.name === previousBranch)
  )
    sessionStorage.removeItem("hotelops-branch");
  configureSession(result.accessToken, result.csrfToken);
  const user = currentUser()!;
  database = preserveData
    ? {
        ...database,
        users: [...database.users.filter((row) => row.id !== user.id), user],
      }
    : { ...emptyDatabase(), users: [user] };
  publish();
}
let restoring: Promise<void> | undefined;
export async function restoreSession() {
  if (authState === "authenticated" || authState === "anonymous") return;
  if (restoring) return restoring;
  authState = "loading";
  authError = null;
  publish();
  restoring = (async () => {
    try {
      requireAuthConfiguration();
      // Memory-only bearer tokens cannot be restored after a browser refresh.
      if (apiConfig.authMode === "bearer") {
        clearSession();
        return;
      }
      applySession(await authService.session());
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) clearSession();
      else {
        authState = "error";
        authError = error as Error;
        publish();
      }
    } finally {
      restoring = undefined;
    }
  })();
  return restoring;
}
export async function login(username: string, password: string) {
  requireAuthConfiguration();
  applySession(
    await authService.login({ username: username.trim(), password }),
  );
  return currentUser()!;
}
export async function logout() {
  await authService.logout();
  clearSession();
}
let lastSessionCheck = 0;
let checkingSession = false;
export function touch() {
  if (!session) return;
  if (apiConfig.authMode !== "cookie") {
    if (!currentSession()) clearSession();
    return;
  }
  // Refresh a sliding cookie session on real user activity, never on an idle timer.
  if (checkingSession || Date.now() - lastSessionCheck < 60000) return;
  checkingSession = true;
  lastSessionCheck = Date.now();
  const epoch = identityEpoch;
  void authService
    .session()
    .then((result) => {
      if (session && epoch === identityEpoch) applySession(result, true);
    })
    .catch(() => {
      /* HTTP 401 already expires the session; other failures surface on the next operation. */
    })
    .finally(() => {
      checkingSession = false;
    });
}
export async function changePassword(
  current: string,
  next: string,
  confirm: string,
) {
  if (next !== confirm)
    throw new ApiError("Passwords must match.", 400, "validation", {
      errors: { confirm: ["Passwords must match."] },
    });
  const updated = await authService.changePassword({
    currentPassword: current,
    newPassword: next,
    confirmNewPassword: confirm,
  });
  // Password change rotates the backend session and returns the new cookie session.
  applySession(updated);
}
function grantMatches(uc: string, record?: RecordData) {
  return (
    session?.principal.grants.some(
      (grant) =>
        grant.useCase === uc &&
        (!record ||
          ((record.branchId == null ||
            grant.branchIds === null ||
            grant.branchIds.includes(String(record.branchId))) &&
            (record.departmentId == null ||
              grant.departmentIds === null ||
              grant.departmentIds.includes(String(record.departmentId))) &&
            (!grant.ownOnly ||
              [record.assignee, record.creator, record.recipient].includes(
                session?.principal.id,
              )))),
    ) ?? false
  );
}
export function can(uc: string, record?: RecordData): boolean {
  return !!currentSession() && grantMatches(uc, record);
}
/** Navigation only: inventory reads still require a successful authorized API response.
 * This does not grant any use case, mutation, role or branch permission. */
export function canOpenScreen(id: number): boolean {
  const current = currentSession();
  return !!current && (!current.restricted || id === 3) &&
    ([3, 4].includes(id) || isInventoryScreen(id) || screenUCs(id).some(uc => can(uc)));
}
export function inScope(record: RecordData): boolean {
  if (!currentSession()) return false;
  if (!currentSession()!.restricted && inventoryReadRecords.has(record)) return true;
  if (
    record.branchId &&
    !session!.principal.branches.some((b) => b.id === String(record.branchId))
  )
    return false;
  return record.canView !== false;
}
export function visible(key: string): RecordData[] {
  const branch = branchContext();
  return (database[key] ?? []).filter(
    (record) =>
      inScope(record) &&
      (branch === "All branches" || !record.branch || record.branch === branch),
  );
}
export function branchContext() {
  return sessionStorage.getItem("hotelops-branch") || "All branches";
}
export function setBranchContext(branch: string) {
  if (
    branch !== "All branches" &&
    !session?.principal.branches.some((b) => b.name === branch)
  )
    return;
  sessionStorage.setItem("hotelops-branch", branch);
  generation++;
  publish();
}
export function resolveReference(
  resource: string,
  name: string,
  branch?: string,
) {
  const list =
    resource === "branches"
      ? session?.principal.branches
      : resource === "departments"
        ? session?.principal.departments
        : undefined;
  const byId = (list ?? database[resource] ?? []).find((r) => r.id === name);
  if (byId) return byId.id;
  const branchId = branch
    ? session?.principal.branches.find(
        (b) => b.id === branch || b.name === branch,
      )?.id
    : undefined;
  const scoped = (database[resource] ?? []).filter(
    (r) => r.name === name && (!branchId || r.branchId === branchId),
  );
  const matches = scoped.length
    ? scoped
    : (list?.filter((r) => r.name === name) ?? []);
  if (matches.length !== 1)
    throw new ApiError(
      `Select an unambiguous ${resource} reference.`,
      400,
      "validation",
      {
        errors: {
          [resource === "branches" ? "branch" : "department"]: [
            "Select an active record in your scope.",
          ],
        },
      },
    );
  return matches[0].id;
}
function scopeQuery(): Query {
  const branch = branchContext();
  return {
    branchId:
      branch === "All branches"
        ? undefined
        : resolveReference("branches", branch),
  };
}
function primaryResource(id: number): ResourceKey | undefined {
  if ([3, 4, 5, 12, 23, 30, 37, 38, 50, 53, 61, 69, 70, 73, 78].includes(id))
    return undefined;
  if (id === 36) return "inspections";
  return domainFor(id).key as ResourceKey;
}
function detailResource(id: number): ResourceKey | undefined {
  if (id === 50) return "assets";
  if ([36, 37].includes(id)) return "inspections";
  return primaryResource(id);
}
export async function loadScreen(
  id: number,
  recordId?: string,
  signal?: AbortSignal,
  search = "",
) {
  const ticket = ++generation;
  if (!currentSession())
    throw new ApiError(
      "Your session has expired. Please log in again.",
      401,
      "unauthenticated",
    );
  const next = emptyDatabase();
  next.users = [currentUser()!];
  if (isInventoryScreen(id)) {
    if (currentSession()!.restricted)
      throw new ApiError("Change your password before accessing inventory.", 403, "forbidden");
    // Clear stale inventory before a new permission check or a failed reload.
    inventoryReadRecords = new WeakSet<RecordData>();
    database = next;
    balances = [];
    metrics = {};
    snapshotTime = "";
    missingSources = [];
    activeScreen = undefined;
    publish();
    const result = await loadInventoryScreen(id, recordId, signal);
    if (ticket !== generation || signal?.aborted) return;
    for (const [key, rows] of Object.entries(result.collections)) {
      next[key] = rows;
      rows.forEach(row => inventoryReadRecords.add(row));
    }
    database = next;
    balances = result.balances;
    activeScreen = { id, recordId, search };
    publish();
    return;
  }
  inventoryReadRecords = new WeakSet<RecordData>();
  if ([3, 4].includes(id)) {
    if (ticket === generation) {
      database = next;
      activeScreen = { id, recordId, search };
      publish();
    }
    return;
  }
  const key =
    recordId && recordId !== "select"
      ? detailResource(id)
      : primaryResource(id);
  const isDetail = !!recordId && recordId !== "select";
  const url = new URLSearchParams(search);
  const query = {
    ...scopeQuery(),
    recordId: isDetail ? recordId : undefined,
    roomId: url.get("room") || undefined,
    sourceRequestId: url.get("source") || undefined,
  };
  const [context, data] = await Promise.all([
    contextService.get(id, query, signal),
    key
      ? key === "incidents"
        ? isDetail
          ? incidentReads.get(recordId!, signal).then(incidentFromDto)
          : incidentReads.list(signal).then((rows) => {
              if (!Array.isArray(rows))
                throw new ApiError(
                  "The existing incidents API must return an array.",
                  0,
                  "server",
                );
              return rows.map(incidentFromDto);
            })
        : isDetail
          ? moduleServices[key].get(recordId!, signal).then(entityFromDto)
          : readAll(key, scopeQuery(), signal).then((rows) =>
              rows.map(entityFromDto),
            )
      : Promise.resolve(undefined),
  ]);
  if (ticket !== generation || signal?.aborted) return;
  if (!context?.snapshot || !context.collections)
    throw new ApiError(
      "Screen context does not match ScreenContextDto.",
      0,
      "server",
    );
  for (const [resource, rows] of Object.entries(context.collections)) {
    if (!(resource in next) || !Array.isArray(rows))
      throw new ApiError(
        "Invalid screen context collection: " + resource,
        0,
        "server",
      );
    next[resource] = rows.map(entityFromDto);
  }
  if (key && data) {
    const rows = Array.isArray(data) ? data : [data];
    next[key] = rows.map((row) => {
      const contextRecord = next[key].find((r) => r.id === row.id);
      return key === "incidents"
        ? {
            ...contextRecord,
            ...row,
            allowedActions: contextRecord?.allowedActions ?? [],
            rowVersion: contextRecord?.rowVersion,
            canEdit: contextRecord?.canEdit === true,
          }
        : row;
    });
  }
  if (id === 44 && recordId) {
    const work = next.maintenance.find((row) => row.id === recordId);
    if (
      work?.incident &&
      !next.incidents.some((row) => row.id === work.incident)
    )
      throw new ApiError(
        "The maintenance context must include its source incident and capabilities.",
        0,
        "server",
      );
  }
  if (!next.users.some((u) => u.id === session!.principal.id))
    next.users.push(currentUser()!);
  for (const token of uncertainWrites) {
    const separator = token.indexOf(":");
    const resource = token.slice(0, separator);
    const record = token.slice(separator + 1);
    if (
      (record === "new" && resource === key) ||
      (record && next[resource]?.some((row) => row.id === record))
    )
      uncertainWrites.delete(token);
  }
  database = next;
  balances = context.balances ?? [];
  metrics = context.metrics ?? {};
  snapshotTime = context.snapshot;
  missingSources = context.missingSources ?? [];
  activeScreen = { id, recordId, search };
  publish();
}
const pending = new Set<string>();
const uncertainWrites = new Set<string>();
export function isPending(key: string) {
  return pending.has(key) || uncertainWrites.has(key);
}
export async function runMutation<T>(
  key: string,
  operation: () => Promise<T>,
): Promise<T> {
  if (uncertainWrites.has(key))
    throw new ApiError(
      "Reload the record before submitting again.",
      409,
      "conflict",
      {},
      true,
    );
  if (pending.has(key))
    throw new ApiError(
      "This operation is already being submitted.",
      409,
      "conflict",
    );
  pending.add(key);
  publish();
  try {
    return await operation();
  } catch (error) {
    if (error instanceof ApiError && error.outcomeUnknown)
      uncertainWrites.add(key);
    throw error;
  } finally {
    pending.delete(key);
    publish();
  }
}
function assertPermission(uc: string, record?: RecordData) {
  if (!can(uc, record))
    throw new ApiError(
      "You do not have permission to perform this action.",
      403,
      "forbidden",
    );
}
async function refreshAfterWrite(
  key: ResourceKey,
  id: string,
  dto?: EntityDto,
) {
  let value: RecordData;
  try {
    value = dto
      ? entityFromDto(dto)
      : key === "incidents"
        ? incidentFromDto(await incidentReads.get(id))
        : entityFromDto(await moduleServices[key].get(id));
  } catch (error) {
    throw new ApiError(
      "The action was accepted but its result could not be loaded. Reload before submitting again.",
      error instanceof ApiError ? error.status : 0,
      "network",
      {},
      true,
    );
  }
  database = {
    ...database,
    [key]: [...(database[key] ?? []).filter((r) => r.id !== value.id), value],
  };
  publish();
  if (activeScreen) {
    try {
      await loadScreen(
        activeScreen.id,
        activeScreen.recordId,
        undefined,
        activeScreen.search,
      );
    } catch (error) {
      // The write succeeded. Keep its confirmed result; never invite a second write on reload failure.
      if (error instanceof ApiError && error.status === 401) throw error;
      window.dispatchEvent(
        new CustomEvent("hotelops-toast", {
          detail:
            "The action was saved. Related data could not refresh; reload this screen.",
        }),
      );
    }
  }
  return value;
}
export async function save(
  d: Domain,
  input: Record<string, unknown>,
  existing?: RecordData,
): Promise<RecordData> {
  const uc = existing
    ? d.updateUC
    : d.key === "transactions" && input.type === "Transfer"
      ? "UC-083"
      : d.key === "transactions" && input.type === "Return"
        ? "UC-090"
        : d.createUC;
  if (!uc)
    throw new ApiError("This operation is not available.", 403, "forbidden");
  assertPermission(uc, existing);
  if (existing && !existing.canEdit)
    throw new ApiError(
      "This record is read-only in its current state.",
      409,
      "conflict",
    );
  const key = d.key as ResourceKey;
  const writable =
    d.key === "users" && existing
      ? ["name", "email", "phone"]
      : d.fields.map((f) => f.key);
  const dto = entityToDto(
    { ...input, rowVersion: existing?.rowVersion },
    writable,
    resolveReference,
  );
  if (existing && !existing.rowVersion)
    throw new ApiError(
      "The API has not supplied the concurrency token for this record.",
      409,
      "conflict",
    );
  return runMutation(key + ":" + (existing?.id ?? "new"), async () => {
    if (key === "users" && !existing) {
      const created = await request<UserProvisioningDto>(resourcePaths.users, {
        method: "POST",
        body: dto,
      });
      if (!created?.record?.id || !created.temporaryPassword)
        throw new ApiError(
          "The user provisioning result is incomplete. Reload before submitting again.",
          0,
          "server",
          {},
          true,
        );
      const record = await refreshAfterWrite(
        "users",
        created.record.id,
        created.record,
      );
      return {
        ...record,
        temporaryPassword: created.temporaryPassword,
        validForHours: created.validForHours,
      };
    }
    const result = existing
      ? await moduleServices[key].update(existing.id, dto)
      : await moduleServices[key].create(dto);
    if (!result && !existing)
      throw new ApiError(
        "Create succeeded without an identifier. Reload before submitting again.",
        0,
        "server",
        {},
        true,
      );
    return refreshAfterWrite(key, result?.id ?? existing!.id, result);
  });
}
export function availableActions(d: Domain, r: RecordData) {
  return (actions[d.key] ?? []).filter((a) => {
    const contract = actionContracts[d.key + ":" + a.label];
    return (
      contract &&
      can(a.uc, r) &&
      r.allowedActions?.includes(contract.id) &&
      (!a.from || a.from.includes(r.status)) &&
      (!a.own ||
        [r.assignee, r.creator, r.recipient].includes(currentUser()?.id)) &&
      (d.key !== "inspections" ||
        (r.rework
          ? a.label === "Conclude reinspection"
          : a.label !== "Conclude reinspection")) &&
      (d.key !== "transactions" ||
        (a.label === "Post receipt"
          ? r.type === "Receipt"
          : a.label === "Post return"
            ? r.type === "Return"
            : ["Dispatch transfer", "Confirm receipt"].includes(a.label)
              ? r.type === "Transfer"
              : true))
    );
  });
}
function commandBody(
  input: Record<string, unknown>,
  record: RecordData,
  allowed: string[],
  operation?: string,
): CommandRequest {
  const converted = entityToDto(input, allowed, resolveReference).attributes;
  if (input.lines && operation)
    converted.lines = commandLines(
      operation,
      input.lines as Record<string, unknown>[],
    ) as unknown as EntityDto["attributes"]["lines"];
  return {
    ...converted,
    rowVersion: record.rowVersion,
    reason: input.reason as string | undefined,
    note: input.note as string | undefined,
  };
}
async function command(
  key: ResourceKey,
  record: RecordData,
  operation: string,
  body: CommandRequest,
) {
  if (!record.rowVersion)
    throw new ApiError(
      "The API must supply a rowVersion before this action can be submitted.",
      409,
      "conflict",
    );
  return runMutation(key + ":" + record.id, async () =>
    refreshAfterWrite(
      key,
      record.id,
      await moduleServices[key].command(record.id, operation, body),
    ),
  );
}
export async function perform(
  d: Domain,
  r: RecordData,
  a: Action,
  input: Record<string, unknown> = {},
) {
  assertPermission(a.uc, r);
  const contract = actionContracts[d.key + ":" + a.label];
  if (!contract || !availableActions(d, r).some((v) => v.label === a.label))
    throw new ApiError(
      "This action is not available in the record's current state.",
      409,
      "conflict",
    );
  if (a.reason && String(input.reason ?? "").trim().length < 10)
    throw new ApiError(
      "Please give a reason of at least 10 characters.",
      400,
      "validation",
      { errors: { reason: ["At least 10 characters are required."] } },
    );
  if (d.key === "users" && a.label === "Reset password") {
    if (!r.rowVersion)
      throw new ApiError(
        "The API must supply a rowVersion before resetting a password.",
        409,
        "conflict",
      );
    return runMutation("users:" + r.id, async () => {
      const result = await request<UserProvisioningDto>(
        `${resourcePaths.users}/${encodeURIComponent(r.id)}/reset-password`,
        { method: "POST", body: { rowVersion: r.rowVersion } },
      );
      if (!result?.record?.id || !result.temporaryPassword)
        throw new ApiError(
          "The password reset result is incomplete. Reload the user record.",
          0,
          "server",
          {},
          true,
        );
      const user = await refreshAfterWrite("users", r.id, result.record);
      return {
        ...user,
        temporaryPassword: result.temporaryPassword,
        validForHours: result.validForHours,
      };
    });
  }
  return command(
    d.key as ResourceKey,
    r,
    contract.path,
    commandBody(input, r, [...contract.fields, "reason", "note"], contract.id),
  );
}
export function canAssign(d: Domain, r: RecordData) {
  const uc =
    d.key === "cleaning"
      ? r.assignee
        ? "UC-030"
        : "UC-029"
      : d.key === "incidents"
        ? "UC-053"
        : "UC-104";
  const op = r.assignee ? "reassign" : "assign";
  return (
    can(uc, r) &&
    r.allowedActions?.includes(d.key + "." + op) &&
    [
      "Pending Assignment",
      "Assigned",
      "Accepted",
      "In Progress",
      "Paused",
      "On Hold",
    ].includes(r.status)
  );
}
export async function assign(
  d: Domain,
  r: RecordData,
  assignee: string,
  reason: string,
) {
  if (!canAssign(d, r))
    throw new ApiError(
      "Assignment is not available for this record.",
      403,
      "forbidden",
    );
  return command(d.key as ResourceKey, r, r.assignee ? "reassign" : "assign", {
    rowVersion: r.rowVersion,
    assigneeId: assignee,
    reason,
  });
}
export async function updateCriteria(
  d: Domain,
  r: RecordData,
  items: string[],
) {
  assertPermission(d.key === "cleaning" ? "UC-032" : "UC-109", r);
  if (
    r.assignee !== currentUser()?.id ||
    r.status !== "In Progress" ||
    !r.allowedActions?.includes(d.key + ".criteria")
  )
    throw new ApiError(
      "You cannot change these completion criteria.",
      403,
      "forbidden",
    );
  return command(d.key as ResourceKey, r, "criteria", {
    rowVersion: r.rowVersion,
    completedItems: items,
  });
}
export async function workday(start: boolean, reason: string) {
  assertPermission("UC-139");
  return runMutation("workday", async () => {
    const result = await request<EntityDto>(
      `/api/workdays/${start ? "start" : "end"}`,
      { method: "POST", body: { reason } },
    );
    return refreshAfterWrite("workdays", result.id, result);
  });
}
export async function requestCorrection(
  interval: RecordData,
  start: string,
  end: string,
  reason: string,
) {
  assertPermission("UC-111", interval);
  return runMutation("correction:" + interval.id, async () => {
    const result = await request<EntityDto>("/api/time-corrections", {
      method: "POST",
      body: {
        intervalId: interval.id,
        proposedStartAt: localDateTimeToUtc(start),
        proposedEndAt: localDateTimeToUtc(end),
        reason,
      },
    });
    return refreshAfterWrite("corrections", result.id, result);
  });
}
export async function applyCorrection(r: RecordData, accept: boolean) {
  assertPermission("work_interval.correct", r);
  if (
    !r.allowedActions?.includes(
      accept ? "corrections.apply" : "corrections.reject",
    )
  )
    throw new ApiError(
      "This correction decision is not available.",
      403,
      "forbidden",
    );
  return command("corrections", r, accept ? "apply" : "reject", {
    rowVersion: r.rowVersion,
  });
}
export async function createReinspection(
  rework: RecordData,
  inspector: string,
) {
  assertPermission("UC-047", rework);
  return runMutation("reinspection:" + rework.id, async () => {
    const result = await request<EntityDto>(
      `/api/cleaning-reworks/${encodeURIComponent(rework.id)}/reinspections`,
      {
        method: "POST",
        body: { rowVersion: rework.rowVersion, inspectorId: inspector },
      },
    );
    return refreshAfterWrite("inspections", result.id, result);
  });
}
export async function linkWork(
  requestId: string,
  key: string,
  recordId: string,
) {
  const source = database.requests.find((r) => r.id === requestId);
  assertPermission("UC-118", source);
  if (!source)
    throw new ApiError(
      "Reload the source request before linking work.",
      404,
      "notFound",
    );
  return command("requests", source, "linked-work", {
    rowVersion: source.rowVersion,
    resource: key,
    recordId,
    mandatory: true,
  });
}
export function balance(
  _db: Database,
  warehouse: string,
  item: string,
  at?: string,
): number {
  // Balance is a server projection; never reconstruct a stock balance from a paged ledger.
  const row = balances.find(
    (b) =>
      b.warehouseId === warehouse &&
      b.itemId === item &&
      (at ? b.at === at : !b.at),
  );
  return row?.quantity ?? Number.NaN;
}
export function minimumStock(
  item: RecordData,
  warehouse: string,
): number | undefined {
  return (
    balances.find(
      (b) => b.warehouseId === warehouse && b.itemId === item.id && !b.at,
    )?.minimum ?? undefined
  );
}
async function generate(path: string, uc: string) {
  assertPermission(uc);
  return runMutation(path, async () => {
    const result = await request<{ generated: number }>(path, {
      method: "POST",
    });
    if (activeScreen)
      await loadScreen(
        activeScreen.id,
        activeScreen.recordId,
        undefined,
        activeScreen.search,
      );
    return result.generated;
  });
}
export const generateDueMaintenance = () =>
  generate("/api/maintenance-schedules/generate-due", "UC-061");
export const generateRecurringTasks = () =>
  generate("/api/task-series/generate-due", "UC-102");
export function screenUCs(id: number) {
  const extras: Record<number, string[]> = {
    8: ["UC-141"],
    17: ["UC-142"],
    39: ["UC-143"],
    43: ["UC-144"],
  };
  return [...screenById(id).ucs, ...(extras[id] ?? [])];
}
export const allowedBranches = () => session?.principal.branches ?? [];
export const allowedDepartments = () => session?.principal.departments ?? [];
