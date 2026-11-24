import { AlertPopup } from "./alert-popup";
import { useEffect, useState } from "react";
import { action, api, type Row } from "./api";
import { useAuth } from "./auth";
import PageTitle from "./page-title";
import { ConfirmationActions, Dialog, Empty, State, useQuery } from "./ui";
import { shiftTime, workdayMessage } from "./workday-scheduling";
import "./workday.css";
import ShiftTimePicker from "./shift-time-picker";

export default function WorkdaySettings({ close }: { close?: () => void }) {
  const { branch, can } = useAuth();
  const [revision, refresh] = useState(0);
  const [editor, setEditor] = useState<Row | null>(null);
  const [removing, setRemoving] = useState<Row | null>(null);
  const [showInactive, setShowInactive] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const query = useQuery(
    can("SHIFT_MANAGE") ? `workday/configuration?branchId=${branch}` : null,
    revision,
  );
  useEffect(() => {
    setEditor(null);
    setRemoving(null);
    setShowInactive(false);
    setError("");
    setNotice("");
  }, [branch]);
  const roleOptions = (query.data?.roles ?? []).filter((r: Row) =>
    editor?.departmentId && query.data?.departmentRoles?.some((link: Row) =>
      link.departmentId === editor.departmentId && link.roleId === r.roleId));
  const shifts = (query.data?.shifts ?? []).filter((s: Row) => showInactive || s.isActive);
  if (!can("SHIFT_MANAGE"))
    return <Empty>Bạn chưa có quyền cấu hình ca tại chi nhánh này.</Empty>;
  return (
    <section className="workday-settings">
      <PageTitle>
        <h1>Cấu hình ca làm việc</h1>
        <p>Bộ phận, đối tượng và giờ ca trong chi nhánh hiện tại</p>
      </PageTitle>
      <div className="toolbar">
        {close && <button onClick={close}>Quay lại lịch</button>}
        <button
          className="primary"
          disabled={!query.data || query.loading}
          onClick={() => {
            setError("");
            setEditor({
              name: "",
              startMinute: 480,
              endMinute: 960,
              capacity: 1,
              isActive: true,
            });
          }}
        >
          Tạo ca
        </button>
      </div>
      <label className="workday-check">
        <input type="checkbox" checked={showInactive} onChange={e => setShowInactive(e.target.checked)} />
        Hiện ca đã xóa / ngừng sử dụng
      </label>
      <p>
        Ca đã có lịch sử giữ nguyên bộ phận, đối tượng và giờ. Muốn đổi các
        thông tin này, hãy tạo ca mới. Chỉ ngừng ca sau khi xử lý các phân công
        chưa kết thúc.
      </p>
      {notice && (
        <AlertPopup tone="success">
          {notice}
        </AlertPopup>
      )}
      <State {...query} retry={() => refresh((x) => x + 1)}>
        {!shifts.length ? (
          <Empty>{showInactive ? "Chưa có ca làm việc." : "Chưa có ca đang sử dụng."}</Empty>
        ) : (
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th>Ca</th>
                  <th>Bộ phận / đối tượng</th>
                  <th>Giờ</th>
                  <th>Số người</th>
                  <th>Trạng thái</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {shifts.map((s: Row) => (
                  <tr key={s.shiftId}>
                    <td data-label="Ca">{s.name}</td>
                    <td data-label="Bộ phận / đối tượng">
                      {s.department} · {s.role}
                    </td>
                    <td data-label="Giờ">
                      {shiftTime(s.startMinute)}–{shiftTime(s.endMinute)}
                    </td>
                    <td data-label="Số người">{s.capacity}</td>
                    <td data-label="Trạng thái">{s.isActive ? "Đang dùng" : "Đã ngừng"}</td>
                    <td data-label="Thao tác">
                      <button
                        onClick={() => {
                          setError("");
                          setEditor(s);
                        }}
                      >
                        Sửa {s.name}
                      </button>
                      {s.isActive && <button
                        className="shift-delete"
                        aria-label={`Xóa ca ${s.name} ${shiftTime(s.startMinute)}–${shiftTime(s.endMinute)}`}
                        onClick={() => { setError(""); setNotice(""); setRemoving(s); }}
                      >Xóa ca</button>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </State>
      {removing && <Dialog className="confirmation-popup" title="Xóa ca làm việc" close={() => { if (!pending) setRemoving(null); }}>
        <p><strong>{removing.name}</strong> · {shiftTime(removing.startMinute)}–{shiftTime(removing.endMinute)}</p>
        <p>{removing.department} · {removing.role}</p>
        <p>Ca sẽ ngừng sử dụng và không thể phân công mới. Lịch sử ca đã làm được giữ nguyên. Chỉ xóa được khi không còn phân công chưa kết thúc.</p>
        <form onSubmit={async e => {
          e.preventDefault(); if (pending) return;
          setPending(true); setError("");
          try {
            await api(`workday/shifts/${removing.shiftId}`, action(removing.rowVersion, {
              branchId: branch, departmentId: removing.departmentId, roleId: removing.roleId,
              name: removing.name, startMinute: removing.startMinute, endMinute: removing.endMinute,
              capacity: removing.capacity, isActive: false,
            }), "PUT");
            setRemoving(null); setNotice("Đã xóa ca khỏi danh sách đang sử dụng. Lịch sử được giữ nguyên.");
            refresh(x => x + 1);
          } catch(err) { setError(workdayMessage(err)); refresh(x => x + 1); }
          finally { setPending(false); }
        }}>
          {error && <AlertPopup>{error}</AlertPopup>}
          <ConfirmationActions pending={pending} cancel={()=>setRemoving(null)}/>
        </form>
      </Dialog>}
      {editor && (
        <Dialog
          title={editor.shiftId ? "Sửa ca làm việc" : "Tạo ca làm việc"}
          close={() => {
            if (!pending) setEditor(null);
          }}
        >
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              if (pending) return;
              const form = new FormData(e.currentTarget);
              const minutes = (name: string) => {
                const [h, m] = String(form.get(name)).split(":").map(Number);
                return h * 60 + m;
              };
              setPending(true);
              setError("");
              try {
                await api(
                  editor.shiftId
                    ? `workday/shifts/${editor.shiftId}`
                    : "workday/shifts",
                  action(editor.rowVersion ?? "", {
                    branchId: branch,
                    name: String(form.get("name")).trim(),
                    departmentId: editor.hasHistory
                      ? editor.departmentId
                      : Number(form.get("departmentId")),
                    roleId: editor.hasHistory
                      ? editor.roleId
                      : Number(form.get("roleId")),
                    startMinute: editor.hasHistory
                      ? editor.startMinute
                      : minutes("start"),
                    endMinute: editor.hasHistory
                      ? editor.endMinute
                      : minutes("end"),
                    capacity: Number(form.get("capacity")),
                    isActive: form.has("active"),
                  }),
                  editor.shiftId ? "PUT" : "POST",
                );
                setEditor(null);
                setNotice("Đã lưu cấu hình ca.");
                refresh((x) => x + 1);
              } catch (err) {
                setError(workdayMessage(err));
                refresh((x) => x + 1);
              } finally {
                setPending(false);
              }
            }}
          >
            <label>
              Tên ca
              <input
                name="name"
                required
                maxLength={150}
                defaultValue={editor.name}
                disabled={pending}
              />
            </label>
            <label>
              Bộ phận
              <select
                name="departmentId"
                required
                value={editor.departmentId ?? ""}
                onChange={(e) => setEditor({ ...editor, departmentId: Number(e.target.value), roleId: undefined })}
                disabled={pending || editor.hasHistory}
              >
                <option value="" disabled>
                  Chọn bộ phận
                </option>
                {editor.departmentId &&
                  !query.data?.departments.some(
                    (d: Row) => d.departmentId === editor.departmentId,
                  ) && (
                    <option value={editor.departmentId}>
                      {editor.department} (đã ngừng)
                    </option>
                  )}
                {query.data?.departments.map((d: Row) => (
                  <option key={d.departmentId} value={d.departmentId}>
                    {d.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Đối tượng
              <select
                name="roleId"
                required
                value={editor.roleId ?? ""}
                onChange={(e) => setEditor({ ...editor, roleId: Number(e.target.value) })}
                disabled={pending || editor.hasHistory || !editor.departmentId}
              >
                <option value="" disabled>
                  Chọn đối tượng
                </option>
                {editor.roleId &&
                  !roleOptions.some(
                    (r: Row) => r.roleId === editor.roleId,
                  ) && (
                    <option value={editor.roleId}>
                      {editor.role} (không còn đủ điều kiện)
                    </option>
                  )}
                {roleOptions.map((r: Row) => (
                  <option key={r.roleId} value={r.roleId}>
                    {r.name}
                  </option>
                ))}
              </select>
              {!editor.departmentId && <small>Chọn bộ phận trước khi chọn đối tượng.</small>}
              {editor.departmentId && !roleOptions.length && !editor.hasHistory && <small>Bộ phận chưa có đối tượng đang hoạt động đủ điều kiện nhận ca.</small>}
            </label>
            <ShiftTimePicker name="start" label="Bắt đầu" minutes={editor.startMinute} disabled={pending || editor.hasHistory}/>
            <ShiftTimePicker name="end" label="Kết thúc" minutes={editor.endMinute} allowMidnight disabled={pending || editor.hasHistory}/>
            <label>
              Sức chứa
              <input
                type="number"
                name="capacity"
                min={1}
                max={100}
                required
                defaultValue={editor.capacity}
                disabled={pending}
              />
            </label>
            <label className="workday-check">
              <input
                type="checkbox"
                name="active"
                defaultChecked={editor.isActive}
                disabled={pending}
              />
              Đang dùng
            </label>
            {editor.hasHistory && (
              <p>
                Đã có phân công; bộ phận, đối tượng và giờ được khóa để giữ lịch
                sử.
              </p>
            )}
            <button className="primary" disabled={pending}>
              Lưu ca
            </button>
            {error && (
              <AlertPopup>
                {error}
              </AlertPopup>
            )}
          </form>
        </Dialog>
      )}
    </section>
  );
}
