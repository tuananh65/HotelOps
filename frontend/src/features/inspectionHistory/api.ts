import {
  decodeDetail,
  decodePage,
  evidencePath,
  filters,
  historyQuery,
  MAX_EVIDENCE_BYTES,
  positiveId,
  type Evidence,
  type Filters,
} from "./model";
export class HistoryError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}
const supported = new Map([
  ["image/png", ".png"],
  ["image/jpeg", ".jpg"],
  ["application/pdf", ".pdf"],
]);
export function createHistoryApi(
  branchId: number,
  lifetime: AbortSignal,
  send: typeof fetch = (...args) => fetch(...args),
) {
  const branch = positiveId(branchId);
  async function request(path: string, signal?: AbortSignal) {
    const active = signal ? AbortSignal.any([signal, lifetime]) : lifetime;
    active.throwIfAborted();
    const response = await send(path, {
      method: "GET",
      credentials: "include",
      cache: "no-store",
      redirect: "error",
      signal: active,
      headers: {
        Accept: "application/json, image/png, image/jpeg, application/pdf",
      },
    });
    active.throwIfAborted();
    if (!response.ok)
      throw new HistoryError(
        response.status,
        response.status === 401
          ? "Phiên không hợp lệ. Cần xác thực lại."
          : response.status === 403
            ? "Không có quyền xem tài nguyên này."
            : `Không đọc được lịch sử (${response.status}).`,
      );
    return { response, active };
  }
  async function json(path: string, signal?: AbortSignal) {
    const { response, active } = await request(path, signal);
    if (
      response.headers
        .get("Content-Type")
        ?.split(";")[0]
        .trim()
        .toLowerCase() !== "application/json"
    )
      throw new HistoryError(502, "Phản hồi JSON không hợp lệ.");
    const value: unknown = await response.json();
    active.throwIfAborted();
    return value;
  }
  return {
    async list(input: Filters, signal?: AbortSignal) {
      const q = filters(input);
      return decodePage(
        await json(
          "/api/inspection-history?" + historyQuery(branch, q),
          signal,
        ),
        branch,
        q,
      );
    },
    async detail(inspectionId: number, signal?: AbortSignal) {
      const id = positiveId(inspectionId);
      return decodeDetail(
        await json(`/api/inspection-history/${id}?branchId=${branch}`, signal),
        branch,
        id,
      );
    },
    async download(
      inspectionId: number,
      evidence: Evidence,
      signal?: AbortSignal,
    ) {
      // Metadata must come from the decoded detail, never from a URL parameter.
      const mime = evidence.mimeType?.toLowerCase();
      if (
        !mime ||
        !supported.has(mime) ||
        evidence.bytes === null ||
        !Number.isSafeInteger(evidence.bytes) ||
        evidence.bytes <= 0 ||
        evidence.bytes > MAX_EVIDENCE_BYTES
      )
        throw new HistoryError(
          400,
          "Chưa đủ metadata hợp lệ để tải evidence an toàn.",
        );
      const { response, active } = await request(
        evidencePath(branch, inspectionId, evidence.id),
        signal,
      );
      if (
        response.headers
          .get("Content-Type")
          ?.split(";")[0]
          .trim()
          .toLowerCase() !== mime
      )
        throw new HistoryError(502, "Định dạng evidence không khớp.");
      const length = response.headers.get("Content-Length");
      if (
        length !== null &&
        (!/^\d+$/.test(length) || Number(length) !== evidence.bytes)
      )
        throw new HistoryError(502, "Kích thước evidence không khớp.");
      const reader = response.body?.getReader();
      if (!reader) throw new HistoryError(502, "Evidence trống.");
      const cancel = () => {
        void reader.cancel().catch(() => {});
      };
      active.addEventListener("abort", cancel, { once: true });
      const chunks: Uint8Array[] = [];
      let size = 0;
      try {
        while (true) {
          active.throwIfAborted();
          const part = await reader.read();
          active.throwIfAborted();
          if (part.done) break;
          size += part.value.byteLength;
          if (size > MAX_EVIDENCE_BYTES || size > evidence.bytes)
            throw new HistoryError(502, "Evidence vượt kích thước cho phép.");
          chunks.push(part.value);
        }
      } finally {
        active.removeEventListener("abort", cancel);
        await reader.cancel().catch(() => {});
        reader.releaseLock();
      }
      if (size !== evidence.bytes)
        throw new HistoryError(502, "Evidence không đủ dữ liệu.");
      const bytes = new Uint8Array(size);
      let offset = 0;
      for (const chunk of chunks) {
        bytes.set(chunk, offset);
        offset += chunk.byteLength;
      }
      const signature =
        mime === "image/png"
          ? [137, 80, 78, 71, 13, 10, 26, 10]
          : mime === "image/jpeg"
            ? [255, 216, 255]
            : [37, 80, 68, 70, 45];
      if (!signature.every((value, index) => bytes[index] === value))
        throw new HistoryError(502, "Nội dung evidence không đúng định dạng.");
      active.throwIfAborted();
      // Generated local filename avoids untrusted Content-Disposition/path names.
      return {
        blob: new Blob([bytes], { type: mime }),
        name: `inspection-${positiveId(inspectionId)}-evidence-${positiveId(evidence.id)}${supported.get(mime)}`,
      };
    },
  };
}
