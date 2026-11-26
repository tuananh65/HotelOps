import { AlertPopup } from "./alert-popup";
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import { useState } from "react";
import { api, fields, message, timestamp, type Row } from "./api";
import { useAuth } from "./auth";
import { Dialog, Empty, State, useQuery } from "./ui";
export default function Templates({ embedded = false }: { embedded?: boolean } = {}) {
  const { branch, can } = useAuth();
  const [revision, bump] = useState(0),
    [form, setForm] = useState(""),
    [pending, setPending] = useState(false),
    [error, setError] = useState("");
  const query = useQuery(`templates?branchId=${branch}`, revision);
  async function mutate(path: string, body: Row) {
    if (pending) return;
    setPending(true);
    setError("");
    try {
      await api(path, body);
      setForm("");
      bump((n) => n + 1);
    } catch (e) {
      setError(message(e));
    } finally {
      setPending(false);
    }
  }
  return (
    <>
      {!embedded && <div className="page-heading">
        <PageTitle>
          <h1>Mẫu & lịch bảo trì</h1>
          <p>
            Mẫu đã công bố được lưu theo phiên bản; công việc đang chạy giữ
            nguyên checklist.
          </p>
        </PageTitle>
      </div>}
      <PageToolbar
        actions={
          <>
            {can("CHECKLIST_MANAGE", false) && (query.data?.checklistKinds || []).includes("MAINTENANCE") && (
              <button
                onClick={() => {
                  setForm("checklist");
                  setError("");
                }}
              >
                + Tạo checklist
              </button>
            )}
            {can("PM_MANAGE", false) && (
              <button
                onClick={() => {
                  setForm("schedule");
                  setError("");
                }}
              >
                + Tạo lịch định kỳ
              </button>
            )}
          </>
        }
      />
      {!form && error && (
        <AlertPopup>
          {error}
        </AlertPopup>
      )}
      <State {...query} retry={() => bump((n) => n + 1)}>
        {can("CHECKLIST_MANAGE", false) && (
          <>
            <h2>Mẫu checklist</h2>
            <div className="table-scroll">
              <table>
                <thead>
                  <tr>
                    <th>Mã</th>
                    <th>Tên</th>
                    <th>Loại</th>
                    <th>Phiên bản</th>
                    <th>Công bố</th>
                  </tr>
                </thead>
                <tbody>
                  {fields(query.data?.checklists).filter(r => r.kind === "MAINTENANCE").map((r) => (
                    <tr key={r.checklistTemplateId}>
                      <td>{r.code}</td>
                      <td>{r.name}</td>
                      <td>{r.kind}</td>
                      <td>{r.versionNo}</td>
                      <td>{timestamp(r.publishedAt)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {!fields(query.data?.checklists).some(r => r.kind === "MAINTENANCE") && <Empty />}
          </>
        )}
        <h2>Lịch công việc định kỳ</h2>
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Công việc</th>
                <th>Chu kỳ</th>
                <th>Lần đến hạn</th>
                <th>Trạng thái</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {fields(query.data?.schedules).filter(r => r.kind === "MAINTENANCE").map((r) => (
                <tr key={r.workTemplateId}>
                  <td>{r.title}</td>
                  <td>
                    Mỗi {r.frequencyInterval}{" "}
                    {
                      (
                        {
                          DAILY: "ngày",
                          WEEKLY: "tuần",
                          MONTHLY: "tháng",
                        } as Record<string, string>
                      )[r.frequency]
                    }
                  </td>
                  <td>{timestamp(r.nextDueAt)}</td>
                  <td>{r.isActive ? "Đang áp dụng" : "Đã dừng"}</td>
                  <td>
                    {r.isActive &&
                      can(
                        r.kind === "MAINTENANCE" ? "PM_MANAGE" : "TASK_MANAGE",
                        false,
                      ) && (
                        <button
                          disabled={pending}
                          onClick={() =>
                            mutate(
                              `templates/schedules/${r.workTemplateId}/disable`,
                              { rowVersion: r.rowVersion },
                            )
                          }
                        >
                          Dừng lịch
                        </button>
                      )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!fields(query.data?.schedules).some(r => r.kind === "MAINTENANCE") && <Empty />}
      </State>
      {form && (
        <Dialog
          title={form === "checklist" ? "Tạo checklist" : "Tạo lịch định kỳ"}
          close={() => {
            if (!pending) setForm("");
          }}
        >
          <form
            onSubmit={(e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              if (form === "checklist")
                mutate("templates/checklists", {
                  branchId: branch,
                  code: String(f.get("code") || ""),
                  name: f.get("title"),
                  kind: f.get("kind"),
                  items: String(f.get("items"))
                    .split("\n")
                    .map((x) => x.trim())
                    .filter(Boolean)
                    .map((description) => ({
                      description,
                      isRequired: true,
                      weight: 1,
                    })),
                });
              else
                mutate("templates/schedules", {
                  branchId: branch,
                  code: String(f.get("code") || ""),
                  title: f.get("title"),
                  kind: f.get("kind"),
                  frequency: f.get("frequency"),
                  frequencyInterval: Number(f.get("frequencyInterval")),
                  nextDueAt: new Date(String(f.get("nextDueAt"))).toISOString(),
                });
            }}
          >
            <label>
              Mã mẫu
              <input name="code" required maxLength={60} />
            </label>
            <label>
              Tên mẫu
              <input name="title" required maxLength={200} />
            </label>
            <label>
              Loại công việc
              <select name="kind">
                {form === "checklist" ? (
                  <>
                    {(query.data?.checklistKinds || []).filter((kind: string) => kind === "MAINTENANCE").map((kind: string) => <option key={kind} value={kind}>Bảo trì</option>)}
                  </>
                ) : (
                  <>
                    {can("PM_MANAGE", false) && (
                      <option value="MAINTENANCE">Bảo trì</option>
                    )}
                  </>
                )}
              </select>
            </label>
            {form === "checklist" ? (
              <>
                <label>
                  Các mục kiểm tra (mỗi dòng một mục bắt buộc)
                  <textarea name="items" required rows={6} />
                </label>
                <p className="hint">
                  Lưu sẽ công bố một phiên bản mới. Mẫu đã công bố không sửa
                  trực tiếp.
                </p>
              </>
            ) : (
              <>
                <label>
                  Chu kỳ
                  <select name="frequency">
                    <option value="DAILY">Ngày</option>
                    <option value="WEEKLY">Tuần</option>
                    <option value="MONTHLY">Tháng</option>
                  </select>
                </label>
                <label>
                  Khoảng cách chu kỳ
                  <input
                    name="frequencyInterval"
                    type="number"
                    required
                    min={1}
                    max={365}
                    defaultValue={1}
                  />
                </label>
                <label>
                  Lần đến hạn đầu tiên
                  <input name="nextDueAt" type="datetime-local" required />
                </label>
              </>
            )}
            {error && (
              <AlertPopup>
                {error}
              </AlertPopup>
            )}
            <button className="primary" disabled={pending}>
              {form === "checklist" ? "Công bố checklist" : "Lưu lịch"}
            </button>
          </form>
        </Dialog>
      )}
    </>
  );
}
