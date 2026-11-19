import { userMessage } from "../i18n/userMessage";
import {
  createContext,
  useCallback,
  useContext,
  useState,
  type ReactNode,
} from "react";

const schemaMessages: Record<string, string> = {
  IR_COMPLETION_SCHEMA_REQUIRED: "Hoàn tất yêu cầu",
  IR_WITHDRAWAL_SCHEMA_REQUIRED: "Rút yêu cầu",
  IR_PROGRESS_SCHEMA_REQUIRED: "Cập nhật tiến độ",
  IR_ESCALATION_SCHEMA_REQUIRED: "Chuyển cấp xử lý",
  IR_HANDOVER_SCHEMA_REQUIRED: "Bàn giao",
  IR_REOPEN_SCHEMA_REQUIRED: "Mở lại yêu cầu",
};
const Context = createContext<(error: unknown) => boolean>(() => false);
export const useWorkflowAvailability = () => useContext(Context);

/** Recognize only the API's explicit installation failure, never a permission denial. */
export async function workflowProblemCode(
  response: Response,
): Promise<string | undefined> {
  const problem: unknown = await response.json().catch(() => null);
  return problem &&
    typeof problem === "object" &&
    "code" in problem &&
    typeof problem.code === "string"
    ? problem.code
    : undefined;
}

export function WorkflowAvailability({ children }: { children: ReactNode }) {
  const [missing, setMissing] = useState<string[]>([]);
  const [revision, setRevision] = useState(0);
  const report = useCallback((error: unknown) => {
    if (
      !error ||
      typeof error !== "object" ||
      !("status" in error) ||
      error.status !== 503 ||
      !("code" in error) ||
      typeof error.code !== "string" ||
      !schemaMessages[error.code]
    )
      return false;
    const code = error.code;
    setMissing((previous) =>
      previous.includes(code) ? previous : [...previous, code],
    );
    return true;
  }, []);
  return (
    <Context.Provider value={report}>
      {missing.length > 0 && (
        <section className="ir-unavailable" role="status">
          <strong>Một số thao tác chưa sẵn sàng</strong>
          <p>
            Hệ thống cần được cập nhật trước khi sử dụng:{" "}
            {userMessage(missing.map((code) => schemaMessages[code]).join(", "))}.
          </p>
          <p>
            Bạn vẫn có thể xem thông tin và lịch sử yêu cầu. Vui lòng liên hệ
            quản trị viên để hoàn tất cập nhật.
          </p>
          <button
            onClick={() => {
              setMissing([]);
              setRevision((v) => v + 1);
            }}
          >
            Kiểm tra lại
          </button>
        </section>
      )}
      <div className="ir-actions" key={revision}>
        {children}
      </div>
    </Context.Provider>
  );
}
