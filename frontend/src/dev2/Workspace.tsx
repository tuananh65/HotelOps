import {
  OperationSurface,
  announceWorkflowSuccess,
} from "../components/workspace-interactions";
import { AuthorizedOperations } from "./AuthorizedOperations";
import { IncidentReportFields } from "./IncidentReportFields";
import { requestCreationDefaults } from "./requestCreationContext";
import { cleaningBlockerContext } from "./cleaningBlockerContext";
import { IncidentEscalationPanel } from "./IncidentEscalationPanel";
import { IncidentTriagePanel } from "./IncidentTriagePanel";
import { IncidentCancellationPanel } from "./IncidentCancellationPanel";
import { IncidentReopenPanel } from "./IncidentReopenPanel";
import { IncidentClassificationPanel } from "./IncidentClassificationPanel";
import { PreventiveSchedulesPanel } from "./PreventiveSchedulesPanel";
import { PreventiveOccurrencesPanel } from "./PreventiveOccurrencesPanel";
import { IncidentRoomBlocksPanel } from "./IncidentRoomBlocksPanel";
import { MaintenanceAssignmentPanel } from "./MaintenanceAssignmentPanel";
import { useEffect, useRef, useState } from "react";
import {
  Link,
  Navigate,
  Route,
  Routes,
  useParams,
  useSearchParams,
  useLocation,
} from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import { operations } from "./catalog.generated";
import { createDev2Api, operationPath, payload } from "./api";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { modules, label, type Operation, type Values } from "./contracts";
import { Fields, defaults } from "./Fields";
import { DataView } from "./DataView";
import { IncidentEvidencePanel } from "./IncidentEvidencePanel";
import {
  IncidentDuplicateReview,
  incidentLocationKey,
  type IncidentReview,
} from "./IncidentDuplicateReview";
import { MaterialApproverField } from "./MaterialApproverField";
import { MaterialCatalogFields } from "./MaterialCatalogFields";
import { MaterialBranchField } from "./MaterialBranchField";
import {
  MaterialApprovalFields,
  materialApprovalActions,
  materialApprovalReady,
} from "./MaterialApprovalFields";
import {
  MaterialCancellationFields,
  materialRequesterActions,
  materialRequesterReady,
} from "./MaterialRequesterFields";
import { MaterialIssueFields, materialIssueReady } from "./MaterialIssueFields";
import { MaterialAvailabilityView } from "./MaterialAvailabilityView";
import { MaterialHistoryView } from "./MaterialHistoryView";
import {
  ReplenishmentCloseFields,
  replenishmentCanClose,
} from "./ReplenishmentCloseFields";
import { ReplenishmentReceiptFields } from "./ReplenishmentReceiptFields";
import { ReplenishmentListView } from "./ReplenishmentListView";
import { ReplenishmentDetailView } from "./ReplenishmentDetailView";
import { MaterialListView } from "./MaterialListView";
import { MaterialReviewHistory } from "./MaterialReviewHistory";
import { IncidentMaintenanceLinks } from "./IncidentMaintenanceLinks";
import { IncidentListView } from "./IncidentListView";
import { MaintenanceReadView } from "./MaintenanceReadView";
import { MaterialTaskLinks } from "./MaterialTaskLinks";
import { ReplenishmentWaitingRequests } from "./ReplenishmentWaitingRequests";
import {
  ReplenishmentPostFields,
  replenishmentPostReady,
} from "./ReplenishmentPostFields";
import {
  ReplenishmentCreateFields,
  replenishmentCreateReady,
} from "./ReplenishmentCreateFields";

const identities = new WeakMap<object, number>();
let identity = 0;
function key(session: object) {
  if (!identities.has(session)) identities.set(session, ++identity);
  return identities.get(session);
}
function operationDefaults(operation: Operation, source: Values = {}): Values {
  if (operation.id === "A004A-056")
    return {
      ...defaults(operation.fields, source),
      branchId: source.branchId,
      sourceRequestId: source.sourceRequestId,
      sourceItemId: source.itemId,
      shortageSource: source.shortageSource,
    };
  if (operation.id === "A004A-051")
    return defaults(operation.fields, {
      ...source,
      mode: "FullRemaining",
      lines: [],
    });
  if (operation.id === "A004A-044" && Array.isArray(source.lines))
    return defaults(operation.fields, {
      ...source,
      lines: source.lines.filter(isObject).filter((l) => l.isActive === true),
    });
  if (operation.id === "A004A-047" && Array.isArray(source.lines))
    return defaults(operation.fields, {
      ...source,
      lines: source.lines
        .filter(isObject)
        .filter((l) => l.isActive === true)
        .map((l) => ({
          requestLineId: l.requestLineId,
          approvedQty: l.requestedQty,
        })),
    });
  return defaults(operation.fields, source);
}
function readAction(operation: Operation, fallback: string): string {
  if (["A004A-003", "A004A-004", "A004A-005"].includes(operation.id))
    return "A004A-003";
  if (operation.module === "inventory") {
    if (/^\/api\/inventory\/items/.test(operation.route))
      return operation.route.includes("conversions/")
        ? "inventory:Conversion"
        : "inventory:Item";
    if (/^\/api\/inventory\/units/.test(operation.route))
      return "inventory:Unit";
    if (/^\/api\/inventory\/warehouses/.test(operation.route))
      return operation.route.includes("threshold")
        ? "inventory:Thresholds"
        : "inventory:Warehouse";
    if (/^\/api\/inventory\/stocktakes/.test(operation.route))
      return "inventory:Stocktake";
  }
  return fallback;
}
export function Dev2Workspace({
  readOnly = false,
  enabledModules,
  enabledActions,
}: {
  readOnly?: boolean;
  enabledModules?: string[];
  enabledActions?: string[];
} = {}) {
  const { state, actions } = useAuth();
  if (state.status === "RESTRICTED" || state.session?.mustChangePassword)
    return <Navigate to="/change-password" replace />;
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate to="/login" replace />;
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.pending !== null
  )
    return (
      <main>
        <p role="status">Xác minh phiên đăng nhập để tải chức năng vận hành.</p>
        <button onClick={() => void actions.revalidate()}>
          Kiểm tra phiên đăng nhập
        </button>
      </main>
    );
  return (
    <Routes>
      <Route
        path=":module/:id?"
        element={
          <AuthorizedOperations>
            {(access) => (
              <Workspace
                key={key(state.session!)}
                readOnly={readOnly}
                enabledModules={access.modules.filter(
                  (m) => !enabledModules || enabledModules.includes(m),
                )}
                enabledActions={[
                  ...access.actions.filter(
                    (a) => !enabledActions || enabledActions.includes(a),
                  ),
                  ...access.extras,
                ]}
              />
            )}
          </AuthorizedOperations>
        }
      />
      <Route
        path="*"
        element={<Navigate replace to="/operations/incidents" />}
      />
    </Routes>
  );
}
function Workspace({
  readOnly,
  enabledModules,
  enabledActions,
}: {
  readOnly: boolean;
  enabledModules?: string[];
  enabledActions?: string[];
}) {
  const { module = "incidents", id = "" } = useParams();
  const { state, actions } = useAuth();
  const [query, setQuery] = useSearchParams();
  const location = useLocation();
  const blocker = cleaningBlockerContext(
    location.state,
    String(state.session?.user.id ?? ""),
  );
  const config = modules.find((m) => m.id === module);
  if (!config || (enabledModules && !enabledModules.includes(module)))
    return <p role="alert">Chức năng đích chưa được kích hoạt.</p>;
  const context: Values = {};
  if (
    blocker &&
    ["A004A-021", "A004A-043"].includes(query.get("action") ?? "")
  ) {
    context.cleaningBlocker = blocker;
    context.roomId = blocker.room;
    context.receivingRoomId = blocker.room;
    context.description = blocker.note;
    context.purpose = blocker.note;
  }
  for (const name of [
    "branchId",
    "departmentId",
    "incidentId",
    "sourceWarehouseId",
    "warehouseId",
    "assetId",
    "roomId",
    "areaId",
    "occurrenceId",
    "runId",
    "occurrenceVersion",
    "occurrenceCheckKey",
    "handoverId",
    "fulfillmentId",
    "conversionId",
    "itemId",
    "requesterId",
    "internalRequestId",
    "sourceVersion",
    "sourceRequestId",
    "shortageSource",
  ])
    if (query.has(name)) context[name] = query.get(name)!;
  return (
    <Screen
      readOnly={
        readOnly ||
        (enabledActions !== undefined &&
          !operations.some(
            (op) =>
              op.module === module &&
              op.method !== "GET" &&
              enabledActions.includes(op.id),
          ) &&
          !(
            module === "incidents" && enabledActions.includes("INCIDENT_MANAGE")
          ))
      }
      enabledModules={enabledModules}
      enabledActions={enabledActions}
      key={`${module}:${id}:${query.toString()}`}
      module={module}
      recordId={id}
      actor={state.session!.user.id}
      config={config}
      context={context}
      actionId={query.get("action") ?? undefined}
      select={(a) => {
        const next = new URLSearchParams(query);
        next.set("action", a);
        setQuery(next);
      }}
      expired={() => void actions.revalidate()}
    />
  );
}
function Screen({
  readOnly,
  enabledModules,
  enabledActions,
  module,
  recordId,
  actor,
  config,
  context,
  actionId,
  select,
  expired,
}: {
  readOnly: boolean;
  enabledModules?: string[];
  enabledActions?: string[];
  module: string;
  recordId: string;
  actor: string;
  config: (typeof modules)[number];
  context: Values;
  actionId?: string;
  select: (v: string) => void;
  expired: () => void;
}) {
  // Hold/Resume use versioned detail, never the unversioned generic form.
  const available = operations.filter(
    (x) =>
      ![
        "A004A-020",
        "A004A-026",
        "A004A-027",
        "A004A-028",
        "A004A-029",
        "A004A-030",
        "A004A-031",
        "A004A-032",
      ].includes(x.id) &&
      x.module === module &&
      (!readOnly || x.method === "GET") &&
      (!enabledActions || enabledActions.includes(x.id)),
  );
  const initial = available.find(
    (x) => x.id === (actionId ?? (recordId ? config.detail : config.list)),
  )!;
  const operation = initial ?? available.find((x) => x.id === config.list)!;
  const [parameters, setParameters] = useState<Values>(() => ({
    id: recordId,
    assetId: recordId,
    incidentId: recordId,
    handoverId: recordId,
    fulfillmentId: context.fulfillmentId ?? "",
    conversionId: context.conversionId ?? "",
    warehouseId: context.warehouseId ?? recordId,
    itemId: context.itemId ?? "",
  }));
  const [values, setValues] = useState<Values>(() =>
    operationDefaults(operation, context),
  );
  const sourceRequest =
    ["A004A-021", "A004A-043"].includes(operation.id) &&
    context.internalRequestId != null
      ? String(context.internalRequestId)
      : "";
  const sourceBranch = String(context.branchId ?? ""),
    sourceVersion = String(context.sourceVersion ?? "");
  const [sourceReady, setSourceReady] = useState(false);
  const [sourceError, setSourceError] = useState("");
  useEffect(() => {
    setSourceReady(false);
    setSourceError("");
    if (!sourceRequest) return;
    const abort = new AbortController();
    void requestCreationDefaults(
      sourceRequest,
      sourceBranch,
      sourceVersion,
      operation.id === "A004A-021",
      abort.signal,
    )
      .then((defaults) => {
        if (!abort.signal.aborted) {
          setValues((v) => ({ ...v, ...defaults }));
          setSourceReady(true);
        }
      })
      .catch((e) => {
        if (!abort.signal.aborted)
          setSourceError(
            e instanceof Error ? e.message : "Yêu cầu nguồn chưa khả dụng.",
          );
      });
    return () => abort.abort();
  }, [sourceRequest, sourceBranch, sourceVersion, operation.id]);
  const [record, setRecord] = useState<Values>({});
  const blockerOrigin = cleaningBlockerContext(
    { cleaningBlocker: context.cleaningBlocker },
    actor,
  );
  const createdSourceId =
    operation.id === "A004A-021"
      ? record.incidentId
      : operation.id === "A004A-043"
        ? record.requestId
        : null;
  const validCreatedSource =
    Number.isSafeInteger(createdSourceId) &&
    Number(createdSourceId) > 0 &&
    Number(record.branchId) === Number(blockerOrigin?.branch);
  const [reviewedIncidentLocation, setReviewedIncidentLocation] =
    useState<IncidentReview | null>(null);
  const [incidentFiles, setIncidentFiles] = useState<File[]>([]);
  const [data, setData] = useState<unknown>();
  const [message, setMessage] = useState("");
  const [maintenanceCommitNotice, setMaintenanceCommitNotice] = useState("");
  const [incidentCommitNotice, setIncidentCommitNotice] = useState("");
  const [busy, setBusy] = useState(false);
  const [commandOpen, setCommandOpen] = useState(true);

  const [denied, setDenied] = useState(false);
  const [sessionLost, setSessionLost] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [revision, setRevision] = useState(0);
  const mounted = useRef(true),
    controller = useRef<AbortController | null>(null),
    submitting = useRef(false),
    generation = useRef(0);
  const api = useRef(createDev2Api());
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      generation.current++;
      submitting.current = false;
      controller.current?.abort();
    };
  }, []);
  async function run(target: Operation, input = values, args = parameters) {
    if (submitting.current) return;
    let commandInput = input;
    try {
      if (target.id === "A004A-059") {
        if (
          !replenishmentPostReady(record, input) ||
          String(args.id) !== String(record.replenishmentId)
        )
          throw Error(
            "Tải phiếu nhập nháp hợp lệ, kiểm tra từng dòng và xác nhận ghi sổ.",
          );
        commandInput = { ...input, expectedRowVersion: record.rowVersion };
      }
      if (target.id === "A004A-056" && !replenishmentCreateReady(input))
        throw Error(
          "Chọn thiếu hụt được phép và nhập số lượng cơ sở dương trước khi tạo bổ sung.",
        );
      if (target.id === "A004A-058") {
        if (
          !replenishmentCanClose(record) ||
          String(args.id) !== String(record.replenishmentId) ||
          input.receiptSelectionVersion !== record.rowVersion ||
          !Array.isArray(input.lines) ||
          !input.lines.length
        )
          throw Error(
            "Tải yêu cầu bổ sung đang mở và chọn phiếu nhập đã ghi sổ hợp lệ.",
          );
        commandInput = { ...input, expectedRowVersion: record.rowVersion };
      }
      if (target.id === "A004A-057") {
        if (
          !replenishmentCanClose(record) ||
          String(args.id) !== String(record.replenishmentId)
        )
          throw Error(
            "Tải yêu cầu bổ sung đang mở trước khi đóng phần còn lại.",
          );
        if (
          input.confirmRemainderClosure !== true ||
          String(input.reason ?? "").trim().length < 10
        )
          throw Error(
            "Xác nhận đóng phần còn lại và nhập lý do ít nhất 10 ký tự, không tính khoảng trắng đầu và cuối.",
          );
        commandInput = {
          ...input,
          reason: String(input.reason).trim(),
          expectedRowVersion: record.rowVersion,
        };
      }
      if (target.id === "A004A-051") {
        if (
          !materialIssueReady(record, actor) ||
          String(args.id) !== String(record.requestId)
        )
          throw Error("Tải yêu cầu đã duyệt hiện tại trước khi cấp vật tư.");
        const enteredLines = Array.isArray(input.lines)
          ? input.lines.filter(isObject)
          : [];
        if (
          input.mode === "Partial" &&
          enteredLines.some(
            (l) =>
              !Number.isFinite(Number(l.quantity)) || Number(l.quantity) < 0,
          )
        )
          throw Error("Số lượng cấp phải hợp lệ và không âm.");
        const issueLines = enteredLines.filter((l) => Number(l.quantity) > 0);
        if (input.mode === "Partial" && issueLines.length === 0)
          throw Error("Nhập ít nhất một số lượng cấp lớn hơn 0.");
        const receiverName = String(input.receiverName ?? "").trim();
        if (
          !receiverName ||
          receiverName.length > 200 ||
          /[\u0000-\u001f\u007f-\u009f]/.test(receiverName)
        )
          throw Error(
            "MSG03: Nhập tên người nhận (1–200 ký tự, không có ký tự điều khiển).",
          );
        if (String(input.warehouseId) !== String(record.sourceWarehouseId))
          throw Error("Chọn kho của yêu cầu đã duyệt.");
        commandInput = {
          ...input,
          receiverName,
          warehouseId: Number(input.warehouseId),
          lines: input.mode === "FullRemaining" ? [] : issueLines,
        };
      }
      if (
        ["A004A-021", "A004A-043"].includes(target.id) &&
        (context.internalRequestId != null || context.sourceVersion != null)
      ) {
        if (
          !sourceReady ||
          !Number.isSafeInteger(Number(context.internalRequestId)) ||
          Number(context.internalRequestId) <= 0 ||
          !Number.isSafeInteger(Number(context.branchId)) ||
          Number(context.branchId) <= 0 ||
          !["required", "optional"].includes(
            String(input.materialRequirement),
          ) ||
          typeof context.sourceVersion !== "string" ||
          !/^[A-Za-z0-9+/]{11}=$/.test(context.sourceVersion)
        )
          throw Error(
            "Tải lại yêu cầu nội bộ và chọn công việc này có bắt buộc để hoàn tất hay không.",
          );
        commandInput = {
          ...input,
          branchId: context.branchId,
          internalRequestId: context.internalRequestId,
          requestLink: {
            version: context.sourceVersion,
            mandatory: input.materialRequirement === "required",
          },
        };
      }
      if (target.id === "A004A-021") {
        if (
          !String(input.incidentTitle ?? "").trim() ||
          !String(input.description ?? "").trim() ||
          !input.discoveredAt
        )
          throw Error("MSG03: Cần tiêu đề, mô tả và thời điểm phát hiện.");
        if (context.occurrenceId != null) {
          if (
            !Number.isSafeInteger(Number(context.occurrenceId)) ||
            Number(context.occurrenceId) <= 0 ||
            typeof context.occurrenceVersion !== "string" ||
            !/^"[0-9A-F]{16}"$/.test(context.occurrenceVersion) ||
            typeof context.occurrenceCheckKey !== "string" ||
            !context.occurrenceCheckKey ||
            context.occurrenceCheckKey.length > 64
          )
            throw Error(
              "Tải lại lần thực hiện nguồn trước khi báo vấn đề này.",
            );
          commandInput = {
            ...commandInput,
            occurrenceLink: {
              runId: Number(context.occurrenceId),
              version: context.occurrenceVersion,
              failedCheckKey: context.occurrenceCheckKey,
            },
          };
        }
        if (
          !reviewedIncidentLocation ||
          reviewedIncidentLocation.key !== incidentLocationKey(commandInput)
        )
          throw Error(
            "Xem sự cố tương tự ở vị trí hiện tại trước khi tạo mới.",
          );
        commandInput = {
          ...commandInput,
          similarReviewToken: reviewedIncidentLocation.token,
        };
      }
      payload(target.fields, commandInput, actor);
      operationPath(target, args);
    } catch (e) {
      setMessage(
        e instanceof Error ? e.message : "Vui lòng kiểm tra biểu mẫu.",
      );
      return;
    }
    submitting.current = true;
    const runId = ++generation.current;
    setBusy(true);
    setMessage("");
    setData(undefined);
    const abort = new AbortController();
    controller.current = abort;
    try {
      if (target.id === "A004A-021") {
        try {
          if (
            incidentFiles.length > 5 ||
            incidentFiles.some(
              (f) =>
                f.size < 1 ||
                f.size > 10 * 1024 * 1024 ||
                !["image/jpeg", "image/png", "application/pdf"].includes(
                  f.type,
                ),
            )
          )
            throw Error(
              "MSG23: Chọn tối đa 5 tệp JPG, PNG hoặc PDF, mỗi tệp tối đa 10 MiB.",
            );
          const files = await Promise.all(
            incidentFiles.map(async (file) => {
              const bytes = new Uint8Array(await file.arrayBuffer());
              let binary = "";
              for (let offset = 0; offset < bytes.length; offset += 8192)
                binary += String.fromCharCode(
                  ...bytes.subarray(offset, offset + 8192),
                );
              return {
                name: file.name,
                mimeType: file.type,
                base64: btoa(binary),
              };
            }),
          );
          if (!mounted.current || abort.signal.aborted) return;
          commandInput = { ...commandInput, files };
        } catch {
          if (mounted.current && !abort.signal.aborted)
            setMessage(
              "MSG23: Không chuẩn bị được tệp. Chọn tối đa 5 tệp JPG, PNG hoặc PDF có thể đọc, mỗi tệp tối đa 10 MiB rồi gửi lại.",
            );
          return; // No HTTP mutation was sent; keep the form and review available for correction.
        }
      }
      const response = await api.current(
        target,
        args,
        commandInput,
        actor,
        abort.signal,
      );
      if (!mounted.current || abort.signal.aborted) return;
      if (!response.ok) {
        const error = response.error;
        if (error.kind === "http") {
          if (target.id === "A004A-021" && error.status >= 500)
            setUncertain(true);
          if (
            error.status === 401 ||
            error.problem?.code === "PASSWORD_CHANGE_REQUIRED"
          ) {
            setSessionLost(true);
            setRecord({});
            setValues({});
            expired();
            return;
          }
          setDenied(error.status === 403);
          if (error.status === 403) {
            setRecord({});
            setValues({});
          }
          setMessage(
            error.status === 403
              ? sourceRequest
                ? "MSG02: Cần quyền công việc đích. Quay lại yêu cầu để liên kết công việc sẵn có hoặc nhờ người có trách nhiệm."
                : "Bạn không có quyền thực hiện thao tác trên đối tượng này. Tên vai trò không tự xác lập quyền."
              : error.status === 404
                ? "Không tìm thấy hồ sơ hoặc hồ sơ ngoài phạm vi được phép."
                : error.status === 400 || error.status === 409
                  ? (error.problem?.detail ??
                    error.problem?.title ??
                    "Lỗi kiểm tra hoặc xung đột phiên bản. Tải lại hồ sơ trước khi thử lại.")
                  : "Yêu cầu thất bại. Xác minh hồ sơ trước khi thay đổi tiếp.",
          );
        } else if (error.kind === "stale") {
          setSessionLost(true);
          setRecord({});
          setValues({});
          expired();
        } else {
          if (target.method !== "GET") setUncertain(true);
          setMessage(
            error.kind === "network"
              ? "Kết nối bị gián đoạn. Chưa xác định kết quả. Kiểm tra hồ sơ trước khi thử lại; hệ thống chưa tự gửi lại."
              : "Chưa xác minh phản hồi. Kiểm tra phiên và tải lại hồ sơ.",
          );
        }
        return;
      }
      setDenied(false);
      setData(response.data);
      const root = isObject(response.data) ? response.data : {};
      setRecord(root);
      if (target.method !== "GET") {
        setCommandOpen(false);
        announceWorkflowSuccess("Đã lưu thay đổi.", "dev2/Workspace.tsx");
        if (sourceRequest && ["A004A-021", "A004A-043"].includes(target.id)) {
          setSourceReady(false);
          setSourceError(
            "MSG08: Đã tạo và liên kết công việc. Tải lại yêu cầu nguồn trước khi tạo thêm.",
          );
        }
        if (target.id === "A004A-021") {
          setIncidentFiles([]);
          setReviewedIncidentLocation(null);
        }
        setMessage(
          sourceRequest && ["A004A-021", "A004A-043"].includes(target.id)
            ? "MSG08: Đã tạo công việc và liên kết với yêu cầu nội bộ. Tải lại yêu cầu nguồn để xem liên kết."
            : target.id === "A004A-021"
              ? "MSG09: Đã báo sự cố. Đang chờ phân loại và tiếp nhận."
              : "Máy chủ đã lưu. Màn hình liên quan sẽ tải dữ liệu hiện tại khi mở.",
        );
        // Read the current record after mutation so subsequent commands use its actual RowVersion.
        setValues(operationDefaults(operation, root));
        const detail = operations.find(
          (x) => x.id === readAction(operation, config.detail),
        );
        const rootKey = (
          {
            assets: "assetId",
            incidents: "incidentId",
            maintenance: "maintenanceWorkId",
            "material-requests": "requestId",
            replenishments: "replenishmentId",
            inventory: "documentId",
          } as Record<string, string>
        )[module];
        const inventoryRoot =
          detail?.id === "inventory:Item"
            ? root.itemId
            : detail?.id === "inventory:Unit"
              ? root.uomId
              : detail?.id === "inventory:Warehouse"
                ? root.warehouseId
                : detail?.id === "inventory:Stocktake"
                  ? root.stocktakeId
                  : undefined;
        const rootId = inventoryRoot ?? root[rootKey] ?? args.id;
        if (detail && rootId) {
          const refreshed = await api.current(
            detail,
            { ...args, id: rootId },
            defaults(detail.fields),
            actor,
            abort.signal,
          );
          if (!mounted.current || abort.signal.aborted) return;
          if (refreshed.ok && isObject(refreshed.data)) {
            setRecord(refreshed.data);
            setData(refreshed.data);
            setValues(operationDefaults(operation, refreshed.data));
          } else if (
            !refreshed.ok &&
            refreshed.error.kind === "http" &&
            refreshed.error.status === 401
          ) {
            setSessionLost(true);
            expired();
            return;
          } else
            setMessage(
              "Đã lưu. Chưa đọc lại được; tải lại trước thay đổi tiếp theo có kiểm tra phiên bản.",
            );
        }
      }
    } finally {
      if (generation.current === runId) {
        submitting.current = false;
        if (mounted.current) setBusy(false);
      }
    }
  }
  useEffect(() => {
    if (
      operation.method === "GET" &&
      operation.route
        .match(/\{\w+:long\}/g)
        ?.every((p) => Boolean(parameters[p.slice(1).split(":")[0]])) !== false
    )
      void run(operation);
    else if (
      recordId &&
      operation.method !== "GET" &&
      operation.id !== "A004A-024"
    ) {
      const detail = operations.find(
        (x) => x.id === readAction(operation, config.detail),
      )!;
      const abort = new AbortController();
      controller.current = abort;
      setBusy(true);
      void api
        .current(
          detail,
          { ...parameters, id: recordId },
          defaults(detail.fields),
          actor,
          abort.signal,
        )
        .then((r) => {
          if (!mounted.current || abort.signal.aborted) return;
          if (r.ok && isObject(r.data)) {
            setRecord(r.data);
            setData(r.data);
            setValues(operationDefaults(operation, { ...context, ...r.data }));
            setUncertain(false);
          } else if (
            !r.ok &&
            r.error.kind === "http" &&
            r.error.status === 401
          ) {
            setSessionLost(true);
            expired();
          }
          setBusy(false);
        });
    }
    // A route/operation selection remounts this screen. Revalidation never retries writes.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [revision]);
  if (sessionLost)
    return (
      <p role="alert">
        Phiên đăng nhập đã thay đổi. Dữ liệu nghiệp vụ đã được ẩn. Xác minh
        phiên trước khi tiếp tục.
      </p>
    );
  const routeFields = [...operation.route.matchAll(/\{(\w+):long\}/g)].map(
    (m) => ({ name: m[1], type: "long", optional: false }),
  );
  const branch = values.branchId ?? record.branchId ?? context.branchId;
  const materialWrite = ["A004A-043", "A004A-044"].includes(operation.id);
  const materialSubmit = operation.id === "A004A-045";
  const needsApproval =
    materialSubmit ||
    values.submit === true ||
    (operation.id === "A004A-044" &&
      ["PENDING_APPROVAL", "APPROVED"].includes(String(record.rawStatus)));
  const historicalLineMax = Array.isArray(record.lines)
    ? Math.max(0, ...record.lines.filter(isObject).map((l) => Number(l.lineNo)))
    : 0;
  const incidentCommitted = (notice: string) => {
    setData(undefined);
    setRecord({});
    setDenied(false);
    setIncidentCommitNotice(notice);
    setRevision((v) => v + 1);
  };
  const change = (v: Values) => {
    setValues(v);
    setDenied(false);
  };
  return (
    <main className="dev2-workspace">
      <header>
        <h1>{config.name}</h1>
        <Link to="/profile">Hồ sơ cá nhân</Link>
        <Link to="/home">Trang chủ</Link>
      </header>
      {maintenanceCommitNotice && (
        <p role="status">{maintenanceCommitNotice}</p>
      )}
      {incidentCommitNotice && <p role="status">{incidentCommitNotice}</p>}
      {context.occurrenceId != null && (
        <p role="status">
          Báo hạng mục không đạt từ lần thực hiện #
          {String(context.occurrenceId)}. Keep the original target. After
          saving, reload the occurrence before continuing.
        </p>
      )}
      {Number.isSafeInteger(Number(context.occurrenceId)) &&
        Number(context.occurrenceId) > 0 &&
        Number.isSafeInteger(Number(branch)) &&
        Number(branch) > 0 && (
          <Link
            to={`/operations/maintenance?branchId=${Number(branch)}&runId=${Number(context.occurrenceId)}`}
          >
            Quay lại lần bảo trì
          </Link>
        )}
      {context.internalRequestId != null &&
        actionId === config.create &&
        !available.some((op) => op.id === actionId) && (
          <p role="status">
            Quyền hiện tại không cho phép tạo công việc liên quan. Chưa tạo công
            việc nào.{" "}
            <Link
              to={`/internal-requests?requestId=${encodeURIComponent(String(context.internalRequestId))}`}
            >
              Quay lại nguồn
            </Link>
          </p>
        )}
      <nav aria-label="Vận hành">
        {modules
          .filter((m) => !enabledModules || enabledModules.includes(m.id))
          .map((m) => (
            <Link
              key={m.id}
              aria-current={m.id === module ? "page" : undefined}
              to={`/operations/${m.id}${branch && /^[1-9]\d*$/.test(String(branch)) ? "?branchId=" + branch : ""}`}
            >
              {m.name}
            </Link>
          ))}
      </nav>
      {module === "incidents" &&
        operation.id !== config.create &&
        !readOnly &&
        available.some((op) => op.id === config.create) && (
          <div className="dev2-toolbar">
            <Link
              to={`/operations/incidents?action=${config.create}${branch ? "&branchId=" + encodeURIComponent(String(branch)) : ""}`}
            >
              Báo sự cố
            </Link>
          </div>
        )}
      {!readOnly && module !== "incidents" && (
        <p>
          Sự cố → Công việc bảo trì → Yêu cầu vật tư → Cấp phát → Vật tư khả
          dụng
        </p>
      )}
      {module !== "incidents" && (
        <div className="dev2-toolbar">
          {module === "maintenance" &&
            Number.isSafeInteger(Number(branch)) &&
            Number(branch) > 0 && (
              <PreventiveOccurrencesPanel
                key={
                  "occurrences:" + String(branch) + ":" + String(context.runId)
                }
                branchId={Number(branch)}
                runId={
                  context.runId == null ? undefined : Number(context.runId)
                }
              />
            )}
          {module === "maintenance" &&
            !readOnly &&
            enabledActions?.includes("PM_MANAGE") &&
            Number.isSafeInteger(Number(branch)) &&
            Number(branch) > 0 && (
              <PreventiveSchedulesPanel
                key={String(branch)}
                branchId={Number(branch)}
              />
            )}
          {available.some((op) => op.id === config.list) && (
            <Link to={`/operations/${module}`}>Danh sách</Link>
          )}
          {!readOnly && available.some((op) => op.id === config.create) && (
            <Link
              to={`/operations/${module}${recordId ? "/" + recordId : ""}?action=${config.create}`}
            >
              Tạo mới
            </Link>
          )}
          <label>
            Thao tác
            <select
              aria-label="Thao tác"
              value={operation.id}
              disabled={busy}
              onChange={(e) => select(e.target.value)}
            >
              {["GET", "WRITE"].map((group) => (
                <optgroup
                  key={group}
                  label={
                    group === "GET"
                      ? "Tra cứu và lịch sử"
                      : "Thao tác nghiệp vụ"
                  }
                >
                  {available
                    .filter((o) => (o.method === "GET") === (group === "GET"))
                    .map((o) => (
                      <option key={o.id} value={o.id}>
                        {o.name}
                      </option>
                    ))}
                </optgroup>
              ))}
            </select>
          </label>
          <button
            disabled={busy}
            onClick={() => {
              setDenied(false);
              setRevision((v) => v + 1);
            }}
          >
            Tải lại hồ sơ
          </button>
        </div>
      )}
      {module !== "incidents" && (
        <p className="dev2-permission">
          Quyền cần có:{" "}
          {materialApprovalActions.includes(operation.id)
            ? "Người duyệt được chỉ định, có thẩm quyền tương ứng và MR_VIEW"
            : materialRequesterActions.includes(operation.id)
              ? "Người yêu cầu hiện tại có MR_CREATE và quyền MR_VIEW riêng"
              : operation.permissions.join(" + ") || operation.permissionHint}
          . The server checks your branch, resource, ownership and workflow for
          every request.
        </p>
      )}
      {sourceRequest && !sourceReady && (
        <p role="status">
          {sourceError || "Đang tải thông tin mặc định của yêu cầu nguồn..."}
          {sourceError && (
            <Link
              to={
                "/internal-requests?requestId=" +
                encodeURIComponent(sourceRequest)
              }
            >
              Quay lại nguồn và tải lại
            </Link>
          )}
        </p>
      )}
      {blockerOrigin && (
        <section aria-label="Nguồn gây trở ngại vệ sinh">
          {validCreatedSource && operation.id === "A004A-043" && (
            <MaterialTaskLinks record={record} kind="cleaning" />
          )}
          <p>
            Kiểm tra ghi chú điền sẵn và phòng trước khi lưu. Yêu cầu vật tư đã
            lưu phải liên kết công việc vệ sinh #{blockerOrigin.task} trước khi
            gửi báo cáo trở ngại.
          </p>
          <Link
            to={`/cleaning/tasks/${blockerOrigin.task}?branchId=${blockerOrigin.branch}&execution=1`}
            state={{
              cleaningBlocker: {
                ...blockerOrigin,
                ...(validCreatedSource
                  ? operation.id === "A004A-021"
                    ? { incident: String(createdSourceId) }
                    : { material: String(createdSourceId) }
                  : {}),
              },
            }}
          >
            Quay lại trở ngại vệ sinh
            {validCreatedSource ? "với nguồn đã lưu" : ""}
          </Link>
        </section>
      )}
      {operation.id !== "A004A-023" && (
        <OperationSurface
          activeCount={
            data === undefined
              ? 0
              : operation.fields.filter((field) => {
                  const value = values[field.name];
                  return (
                    field.optional &&
                    !["page", "pageSize"].includes(field.name) &&
                    value !== undefined &&
                    value !== null &&
                    value !== "" &&
                    value !== field.default &&
                    (!Array.isArray(value) || value.length > 0)
                  );
                }).length
          }
          long={
            operation.fields.length > 6 ||
            operation.fields.some((field) => !!field.fields?.length)
          }
          read={operation.method === "GET"}
          quick={operation.id === "A004A-022"}
          title={operation.name}
          open={commandOpen}
          busy={busy}
          error={message}
          onClose={() => setCommandOpen(false)}
          onReopen={() => setCommandOpen(true)}
        >
          <form
            data-operation={operation.id}
            onSubmit={(e) => {
              e.preventDefault();
              void run(operation);
            }}
          >
            <fieldset disabled={busy || (!!sourceRequest && !sourceReady)}>
              <legend>
                {operation.id === "A004A-022"
                  ? "Tìm sự cố"
                  : operation.id === "A004A-021"
                    ? "Báo cáo sự cố"
                    : operation.name}
              </legend>
              {operation.id === "A004A-022" && (
                <label>
                  Tìm theo mã hoặc tiêu đề
                  <input
                    name="keyword"
                    value={String(values.keyword ?? "")}
                    onChange={(e) =>
                      change({ ...values, keyword: e.target.value, page: 1 })
                    }
                  />
                </label>
              )}
              <Fields
                fields={
                  materialRequesterActions.includes(operation.id) ||
                  ["A004A-051", "A004A-057", "A004A-058", "A004A-059"].includes(
                    operation.id,
                  )
                    ? []
                    : routeFields
                }
                values={parameters}
                actor={actor}
                onChange={(v) => {
                  setParameters(v);
                  setDenied(false);
                }}
              />
              {["A004A-021", "A004A-043"].includes(operation.id) &&
                context.internalRequestId != null && (
                  <section aria-label="Yêu cầu nội bộ nguồn">
                    <p>
                      Mục này{" "}
                      {operation.id === "A004A-021"
                        ? "incident"
                        : "Yêu cầu vật tư"}{" "}
                      sẽ được liên kết với yêu cầu nội bộ nguồn khi lưu.
                    </p>
                    <Link
                      to={`/internal-requests?requestId=${encodeURIComponent(String(context.internalRequestId))}`}
                    >
                      Quay lại nguồn và tải lại
                    </Link>
                    <label>
                      Điều kiện hoàn tất
                      <select
                        required
                        value={String(values.materialRequirement ?? "")}
                        onChange={(event) =>
                          change({
                            ...values,
                            materialRequirement: event.target.value,
                          })
                        }
                      >
                        <option value="">Chọn điều kiện</option>
                        <option value="required">Bắt buộc để hoàn tất</option>
                        <option value="optional">Không bắt buộc</option>
                      </select>
                    </label>
                  </section>
                )}
              {operation.id === "A004A-043" && (
                <MaterialBranchField
                  value={values.branchId}
                  fixed={context.internalRequestId != null}
                  onChange={(branchId) =>
                    change({
                      ...values,
                      branchId,
                      assignedApproverUserId: "",
                      departmentId: "",
                      sourceWarehouseId: "",
                      receivingRoomId: "",
                      receivingAreaId: "",
                      incidentId: "",
                      priorityId: "",
                      lines: [],
                    })
                  }
                />
              )}
              <Fields
                fields={
                  ["A004A-021", "A004A-022"].includes(operation.id) ||
                  materialApprovalActions.includes(operation.id) ||
                  materialSubmit ||
                  [
                    "A004A-049",
                    "A004A-056",
                    "A004A-057",
                    "A004A-058",
                    "A004A-059",
                  ].includes(operation.id)
                    ? []
                    : operation.id === "A004A-051"
                      ? operation.fields.filter((f) =>
                          ["mode", "documentDate"].includes(f.name),
                        )
                      : materialWrite
                        ? operation.fields
                            .filter((f) =>
                              ["purpose", "neededAt", "submit"].includes(
                                f.name,
                              ),
                            )
                            .map((f) =>
                              f.name === "neededAt" && needsApproval
                                ? { ...f, optional: false }
                                : f,
                            )
                        : ["A004A-021", "A004A-022"].includes(operation.id)
                          ? operation.fields.filter(
                              (f) =>
                                f.name !== "categoryId" &&
                                !(
                                  operation.id === "A004A-021" &&
                                  context.internalRequestId != null &&
                                  f.name === "branchId"
                                ),
                            )
                          : operation.fields
                }
                values={values}
                actor={actor}
                onChange={(v) =>
                  change(
                    operation.id === "A004A-043" &&
                      v.branchId !== values.branchId
                      ? {
                          ...v,
                          assignedApproverUserId: "",
                          departmentId: "",
                          sourceWarehouseId: "",
                          receivingRoomId: "",
                          receivingAreaId: "",
                          incidentId: "",
                          priorityId: "",
                          lines: [],
                        }
                      : ["A004A-021", "A004A-022"].includes(operation.id) &&
                          v.branchId !== values.branchId
                        ? { ...v, categoryId: "" }
                        : v,
                  )
                }
              />
              {operation.id === "A004A-021" && (
                <IncidentReportFields
                  values={values}
                  onChange={change}
                  fixed={context.branchId != null}
                />
              )}

              {operation.id === "A004A-021" && (
                <IncidentDuplicateReview
                  values={values}
                  onReviewed={setReviewedIncidentLocation}
                />
              )}
              {operation.id === "A004A-021" && (
                <label>
                  Ảnh / tài liệu minh chứng (không bắt buộc, tối đa 5 tệp JPG,
                  PNG hoặc PDF, 10 MiB mỗi tệp)
                  <input
                    key={String(record.incidentId ?? "new")}
                    type="file"
                    multiple
                    accept=".jpg,.jpeg,.png,.pdf"
                    disabled={busy}
                    onChange={(e) =>
                      setIncidentFiles(Array.from(e.target.files ?? []))
                    }
                  />
                </label>
              )}
              {materialWrite && (
                <MaterialCatalogFields
                  values={values}
                  onChange={change}
                  historicalLineMax={
                    operation.id === "A004A-044" ? historicalLineMax : undefined
                  }
                />
              )}
              {materialRequesterActions.includes(operation.id) &&
                !materialRequesterReady(record, actor, operation.id) && (
                  <p role="status">
                    Mở yêu cầu hiện tại ở trạng thái hợp lệ trước khi lưu. Không
                    được sửa sau lần cấp đầu tiên.
                  </p>
                )}
              {operation.id === "A004A-049" && (
                <MaterialCancellationFields values={values} onChange={change} />
              )}
              {materialApprovalActions.includes(operation.id) && (
                <>
                  {!materialApprovalReady(record, actor, operation.id) && (
                    <p role="status">
                      Tải yêu cầu đang chờ bạn quyết định trước khi lưu.
                    </p>
                  )}
                  <MaterialApprovalFields
                    record={record}
                    values={values}
                    action={operation.id}
                    onChange={change}
                  />
                </>
              )}
              {(materialWrite || materialSubmit) && (
                <MaterialApproverField
                  branch={materialSubmit ? record.branchId : values.branchId}
                  warehouse={
                    materialSubmit
                      ? record.sourceWarehouseId
                      : values.sourceWarehouseId
                  }
                  value={values.assignedApproverUserId}
                  required={needsApproval}
                  onChange={(assignedApproverUserId) =>
                    change({ ...values, assignedApproverUserId })
                  }
                />
              )}
              {operation.id === "A004A-051" && (
                <>
                  <p>
                    Cần quyền thủ kho, MR_FULFILL, STOCK_ISSUE_POST,
                    INVENTORY_VIEW và MR_VIEW. Người yêu cầu và người duyệt
                    không được cấp phát phiếu này.
                  </p>
                  {!materialIssueReady(record, actor) && (
                    <p role="status">
                      Mở yêu cầu đã duyệt đủ điều kiện trước khi cấp vật tư.
                    </p>
                  )}
                  <MaterialIssueFields
                    record={record}
                    values={values}
                    onChange={change}
                  />
                </>
              )}
              {operation.id === "A004A-057" && (
                <ReplenishmentCloseFields
                  record={record}
                  values={values}
                  onChange={change}
                />
              )}
              {operation.id === "A004A-056" && (
                <ReplenishmentCreateFields values={values} onChange={change} />
              )}
              {operation.id === "A004A-058" && (
                <ReplenishmentReceiptFields
                  record={record}
                  values={values}
                  onChange={change}
                />
              )}
              {operation.id === "A004A-059" && (
                <ReplenishmentPostFields
                  record={record}
                  values={values}
                  onChange={change}
                />
              )}
              <button
                data-dialog-action
                type="submit"
                disabled={
                  busy ||
                  denied ||
                  uncertain ||
                  (operation.id === "A004A-059" &&
                    (!replenishmentPostReady(record, values) ||
                      String(parameters.id) !==
                        String(record.replenishmentId))) ||
                  (operation.id === "A004A-056" &&
                    !replenishmentCreateReady(values)) ||
                  (operation.id === "A004A-058" &&
                    (!replenishmentCanClose(record) ||
                      !Array.isArray(values.lines) ||
                      !values.lines.length ||
                      values.receiptSelectionVersion !== record.rowVersion)) ||
                  (operation.id === "A004A-057" &&
                    (!replenishmentCanClose(record) ||
                      values.confirmRemainderClosure !== true ||
                      String(parameters.id) !==
                        String(record.replenishmentId))) ||
                  (operation.id === "A004A-051" &&
                    !materialIssueReady(record, actor)) ||
                  (materialApprovalActions.includes(operation.id) &&
                    !materialApprovalReady(record, actor, operation.id)) ||
                  (materialRequesterActions.includes(operation.id) &&
                    (!materialRequesterReady(record, actor, operation.id) ||
                      String(parameters.id) !== String(record.requestId))) ||
                  (materialWrite &&
                    (!Array.isArray(values.lines) || values.lines.length === 0))
                }
              >
                {busy
                  ? "Đang xử lý…"
                  : operation.method === "GET"
                    ? operation.id === "A004A-022"
                      ? "Tìm kiếm / tải lại"
                      : "Tải hồ sơ"
                    : "Lưu" + operation.name.toLowerCase()}
              </button>
            </fieldset>
          </form>
        </OperationSurface>
      )}
      {operation.id === "A004A-023" && (
        <Link
          to={`/operations/incidents${branch ? `?branchId=${branch}` : ""}`}
        >
          Quay lại danh sách sự cố
        </Link>
      )}

      {busy && <LoadingIndicator label="Đang tải dữ liệu" />}
      {message && (
        <p
          role={
            denied || !message.startsWith("Máy chủ đã lưu") ? "alert" : "status"
          }
        >
          {message}
        </p>
      )}
      {data !== undefined &&
        (["A004A-035", "A004A-036"].includes(operation.id) && isObject(data) ? (
          <MaintenanceReadView
            onCommitted={(notice) => {
              setData(undefined);
              setRecord({});
              setDenied(false);
              setMaintenanceCommitNotice(notice);
              setRevision((v) => v + 1);
            }}
            data={data}
            detail={operation.id === "A004A-036"}
            canDetail={available.some((op) => op.id === "A004A-036")}
            canVerify={
              !readOnly &&
              (!enabledActions || enabledActions.includes("A004A-031"))
            }
            canReturn={
              !readOnly &&
              (!enabledActions || enabledActions.includes("A004A-032"))
            }
            canLog={
              !readOnly &&
              (!enabledActions || enabledActions.includes("A004A-029"))
            }
            canMaterials={
              !enabledActions || enabledActions.includes("A004A-034")
            }
            canSubmit={
              !readOnly &&
              (!enabledActions || enabledActions.includes("A004A-030"))
            }
            canStart={
              !readOnly &&
              (!enabledActions || enabledActions.includes("A004A-026"))
            }
            canResume={
              !readOnly &&
              (!enabledActions || enabledActions.includes("A004A-028"))
            }
            canHold={
              !readOnly &&
              (!enabledActions || enabledActions.includes("A004A-027"))
            }
          />
        ) : operation.id === "A004A-022" && isObject(data) ? (
          <IncidentListView
            data={data}
            canDetail={available.some((op) => op.id === "A004A-023")}
          />
        ) : operation.id === "A004A-040" && isObject(data) ? (
          <MaterialAvailabilityView data={data} />
        ) : operation.id === "A004A-052" && isObject(data) ? (
          <ReplenishmentListView data={data} />
        ) : operation.id === "A004A-053" && isObject(data) ? (
          <ReplenishmentDetailView record={data} />
        ) : operation.id === "A004A-037" && isObject(data) ? (
          <MaterialListView data={data} />
        ) : (
          <DataView data={data} title={config.name} />
        ))}
      {operation.id === "A004A-023" &&
        isObject(data) &&
        !denied &&
        !sessionLost &&
        available.some((op) => op.id === "A004A-023") &&
        (!enabledActions || enabledActions.includes("A004A-036")) && (
          <IncidentMaintenanceLinks data={data} />
        )}
      {operation.id === "A004A-023" &&
        !readOnly &&
        !denied &&
        !sessionLost &&
        isObject(data) &&
        data.status !== "CLOSED" &&
        data.status !== "CANCELLED" &&
        enabledActions?.includes("INCIDENT_MANAGE") && (
          <IncidentClassificationPanel
            onCommitted={incidentCommitted}
            key={
              "IncidentClassificationPanel:" +
              String(data.incidentId) +
              ":" +
              String(data.triageETag)
            }
            data={data}
          />
        )}
      {operation.id === "A004A-023" &&
        !readOnly &&
        (!enabledActions || enabledActions.includes("A004A-020")) &&
        data !== undefined &&
        !denied &&
        !sessionLost &&
        isObject(data) && (
          <IncidentTriagePanel
            onCommitted={incidentCommitted}
            onReload={() => incidentCommitted("")}
            key={
              "IncidentTriagePanel:" +
              String(data.incidentId) +
              ":" +
              String(data.triageETag)
            }
            data={data}
          />
        )}
      {operation.id === "A004A-023" &&
        !readOnly &&
        !denied &&
        !sessionLost &&
        isObject(data) && (
          <IncidentCancellationPanel
            onCommitted={incidentCommitted}
            onReload={() => incidentCommitted("")}
            key={
              "IncidentCancellationPanel:" +
              String(data.incidentId) +
              ":" +
              String(data.triageETag)
            }
            data={data}
          />
        )}
      {operation.id === "A004A-023" &&
        !readOnly &&
        !denied &&
        !sessionLost &&
        isObject(data) &&
        data.status === "CLOSED" &&
        enabledActions?.includes("INCIDENT_MANAGE") && (
          <IncidentReopenPanel
            onCommitted={incidentCommitted}
            key={
              "IncidentReopenPanel:" +
              String(data.incidentId) +
              ":" +
              String(data.triageETag)
            }
            data={data}
          />
        )}
      {operation.id === "A004A-023" &&
        !denied &&
        !sessionLost &&
        isObject(data) &&
        typeof data.incidentId === "number" && (
          <IncidentEscalationPanel
            onCommitted={incidentCommitted}
            key={"IncidentEscalationPanel:" + String(data.incidentId)}
            id={data.incidentId}
          />
        )}
      {operation.id === "A004A-023" &&
        !readOnly &&
        !denied &&
        !sessionLost &&
        isObject(data) &&
        data.canManageRoomBlocks === true &&
        typeof data.incidentId === "number" && (
          <IncidentRoomBlocksPanel
            onCommitted={incidentCommitted}
            key={
              "IncidentRoomBlocksPanel:" +
              String(data.incidentId) +
              ":" +
              String(data.triageETag)
            }
            incidentId={data.incidentId}
          />
        )}
      {operation.id === "A004A-023" &&
        !readOnly &&
        !denied &&
        !sessionLost &&
        isObject(data) &&
        data.canAssign === true &&
        typeof data.incidentId === "number" && (
          <MaintenanceAssignmentPanel
            onCommitted={incidentCommitted}
            key={
              "MaintenanceAssignmentPanel:" +
              String(data.incidentId) +
              ":" +
              String(data.triageETag)
            }
            incidentId={data.incidentId}
          />
        )}
      {module === "material-requests" &&
        record.requestId != null &&
        Array.isArray(record.cycles) && <MaterialHistoryView record={record} />}
      {operation.id === "A004A-023" &&
        data !== undefined &&
        !denied &&
        !sessionLost &&
        typeof record.incidentId === "number" &&
        record.incidentId > 0 && (
          <IncidentEvidencePanel
            key={"IncidentEvidencePanel:" + String(record.incidentId)}
            incidentId={record.incidentId}
          />
        )}
      {module === "material-requests" &&
        operation.id === "A004A-038" &&
        record.requestId != null && (
          <>
            <MaterialTaskLinks record={record} />
            <MaterialTaskLinks record={record} kind="cleaning" />
            <MaterialTaskLinks record={record} kind="maintenance" />
            <MaterialTaskLinks record={record} kind="incident" />
          </>
        )}
      {module === "material-requests" &&
        materialApprovalReady(record, actor, "A004A-047") && (
          <MaterialReviewHistory record={record} />
        )}
      {module === "material-requests" &&
        record.rawStatus === "PARTIALLY_ISSUED" &&
        record.branchId != null &&
        record.sourceWarehouseId != null &&
        (!enabledModules || enabledModules.includes("replenishments")) &&
        (!enabledActions || enabledActions.includes("A004A-056")) && (
          <Link
            to={`/operations/replenishments?action=A004A-056&branchId=${encodeURIComponent(String(record.branchId))}&warehouseId=${encodeURIComponent(String(record.sourceWarehouseId))}&shortageSource=material-requests&sourceRequestId=${encodeURIComponent(String(record.requestId))}`}
          >
            Kiểm tra thiếu hụt và yêu cầu bổ sung phần còn lại
          </Link>
        )}
      {module === "replenishments" &&
        operation.id === "A004A-053" &&
        record.replenishmentId != null && (
          <ReplenishmentWaitingRequests record={record} />
        )}
      {module === "replenishments" &&
        record.replenishmentId != null &&
        available.some((op) => op.id === "A004A-057") &&
        replenishmentCanClose(record) && (
          <Link
            to={`/operations/replenishments/${encodeURIComponent(String(record.replenishmentId))}?action=A004A-057`}
          >
            Đóng phần còn lại chưa nhận
          </Link>
        )}
      {module === "replenishments" &&
        record.replenishmentId != null &&
        available.some((op) => op.id === "A004A-058") &&
        replenishmentCanClose(record) && (
          <Link
            to={`/operations/replenishments/${encodeURIComponent(String(record.replenishmentId))}?action=A004A-058`}
          >
            Liên kết phiếu nhập đã ghi sổ
          </Link>
        )}
      {module === "replenishments" &&
        record.replenishmentId != null &&
        available.some((op) => op.id === "A004A-059") &&
        replenishmentCanClose(record) && (
          <Link
            to={`/operations/replenishments/${encodeURIComponent(String(record.replenishmentId))}?action=A004A-059`}
          >
            Xác minh và ghi sổ phiếu nhập nháp
          </Link>
        )}
      {["A004A-040", "A004A-041", "A004A-042"].includes(operation.id) &&
        recordId && (
          <Link
            to={`/operations/material-requests/${encodeURIComponent(recordId)}`}
          >
            Quay lại yêu cầu vật tư
          </Link>
        )}
      {module === "material-requests" &&
        record.requestId != null &&
        Array.isArray(record.fulfillments) && (
          <section aria-label="Chứng từ cấp phát vật tư">
            <p>Chứng từ cấp phát</p>
            <ul>
              {record.fulfillments.filter(isObject).map((f) => (
                <li key={String(f.fulfillmentId)}>
                  <Link
                    to={`/operations/material-requests/${encodeURIComponent(String(record.requestId))}?action=A004A-042&fulfillmentId=${encodeURIComponent(String(f.fulfillmentId))}`}
                  >
                    {String(f.fulfillmentNo)}
                  </Link>
                </li>
              ))}
            </ul>
          </section>
        )}
      {module === "material-requests" &&
        record.requestId != null &&
        record.requesterId != null &&
        Array.isArray(record.lines) && (
          <section aria-label="Thông tin xét duyệt yêu cầu vật tư">
            {(record.rawStatus === "APPROVED" ||
              record.rawStatus === "PARTIALLY_ISSUED" ||
              materialApprovalReady(record, actor, "A004A-047")) && (
              <Link
                to={`/operations/material-requests/${record.requestId}?action=A004A-040`}
              >
                Xem tồn kho khả dụng
              </Link>
            )}
            <p>
              Tồn kho chỉ là xem trước theo đơn vị cơ sở; xét duyệt yêu cầu
              không giữ chỗ vật tư.
            </p>
            <p>
              Yêu cầu gần đây của người yêu cầu này trong phạm vi bạn được xem:
            </p>
            <ul>
              {record.lines
                .filter(isObject)
                .filter(
                  (line, index, lines) =>
                    line.isActive === true &&
                    lines.findIndex(
                      (other) =>
                        other.isActive === true && other.itemId === line.itemId,
                    ) === index,
                )
                .map((line) => (
                  <li key={String(line.itemId)}>
                    <Link
                      to={`/operations/material-requests?branchId=${encodeURIComponent(String(record.branchId))}&requesterId=${encodeURIComponent(String(record.requesterId))}&itemId=${encodeURIComponent(String(line.itemId))}`}
                    >
                      {String(line.itemName ?? `Vật tư ${line.itemId}`)}
                    </Link>
                  </li>
                ))}
            </ul>
          </section>
        )}
      {operation.method === "GET" &&
        isObject(data) &&
        typeof data.page === "number" &&
        typeof data.pageSize === "number" && (
          <div className="dev2-toolbar" aria-label="Phân trang">
            <button
              disabled={busy || data.page <= 1}
              onClick={() => {
                const next = { ...values, page: Number(data.page) - 1 };
                setValues(next);
                void run(operation, next);
              }}
            >
              Trang trước
            </button>
            <span>Trang {data.page}</span>
            <button
              disabled={
                busy ||
                typeof (data.total ?? data.totalCount) !== "number" ||
                data.page * data.pageSize >=
                  Number(data.total ?? data.totalCount)
              }
              onClick={() => {
                const next = { ...values, page: Number(data.page) + 1 };
                setValues(next);
                void run(operation, next);
              }}
            >
              Trang sau
            </button>
          </div>
        )}
      {!readOnly && record.incidentId != null && (
        <p>
          <Link
            to={`/operations/maintenance?action=A004A-035&incidentId=${record.incidentId}${record.branchId ? "&branchId=" + record.branchId : ""}`}
          >
            Xem công việc bảo trì liên quan
          </Link>{" "}
          ·{" "}
          <Link
            to={`/operations/incidents/${record.incidentId}?action=A004A-023`}
          >
            Mở phân công xử lý sự cố {String(record.incidentId)}
          </Link>{" "}
          ·{" "}
          <Link
            to={`/operations/material-requests?action=A004A-043&incidentId=${record.incidentId}${record.branchId ? "&branchId=" + record.branchId : ""}${record.departmentId ? "&departmentId=" + record.departmentId : ""}`}
          >
            Yêu cầu vật tư cho sự cố này
          </Link>
        </p>
      )}
      {!readOnly &&
        available.some((op) => op.id === "A004A-051") &&
        module === "material-requests" &&
        record.requestId != null && (
          <p>
            <Link
              to={`/operations/material-requests/${record.requestId}?action=A004A-051`}
            >
              Cấp phát các dòng đã duyệt
            </Link>
          </p>
        )}
      {module === "maintenance" && record.maintenanceWorkId != null && (
        <p>
          <Link
            to={`/operations/maintenance/${record.maintenanceWorkId}?action=A004A-034`}
          >
            Vật tư đã cấp còn khả dụng
          </Link>
        </p>
      )}
      {!readOnly && !enabledActions && (
        <footer>
          <Link to="/inventory/items">Danh mục vật tư kho</Link> ·{" "}
          <Link to="/operations/inventory?action=inventory:Movements">
            Biến động kho
          </Link>{" "}
          ·{" "}
          <Link to="/operations/inventory?action=inventory:Documents">
            Chứng từ kho
          </Link>
        </footer>
      )}
    </main>
  );
}
