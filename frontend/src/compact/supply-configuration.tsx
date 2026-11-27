import { useState } from "react";
import { useAuth } from "./auth";
import { timestamp, type Row } from "./api";
import { Dialog, Empty, State, useQuery } from "./ui";
import { AlertPopup } from "./alert-popup";
import PageTitle from "./page-title";
import InventoryLifecycle from "./inventory-lifecycle";
import PageToolbar from "./page-toolbar";
import StatusSwitch from "./status-switch";
import { saveSupplyConfiguration, supplyGroups, supplyMessage, supplyQuantity, type SaveSupplyConfiguration, type SupplyConfiguration, type SupplyGroup } from "./supply-api";
import "./supplies.css";

const groupDescriptions: Record<SupplyGroup, string> = {
  A: "Cấp vật tư khi nhân viên nhận công việc. Định mức phòng bằng số lượng mỗi khách nhân sức chứa khách tối đa của phòng.",
  B: "Cấu hình đồ dùng bổ sung cho phòng và mức tồn tối thiểu tại kho chi nhánh.",
  C: "Quản lý thiết bị cần sửa chữa; thiết bị gắn với phòng không cấp phát theo ca.",
};

export default function SupplyConfigurationPage() {
  const { branch } = useAuth();
  return <ConfigurationBranch key={branch} />;
}
function ConfigurationBranch() {
  const { branch, can, profile } = useAuth();
  const write = can("ROOM_SUPPLY_CONFIG", false) && profile.roles.some(r => r.code === "OPERATION_MANAGER" && (r.branchId == null || r.branchId === branch));
  const read = can("ROOM_SUPPLY_CONFIG", false) || can("STAFF_SUPPLY_VIEW", true);
  const [revision, bump] = useState(0), [search, setSearch] = useState(""), [group, setGroup] = useState<SupplyGroup>("A");
  const [status, setStatus] = useState("");
  const [filters, setFilters] = useState(false);
  const [edit, setEdit] = useState<SupplyConfiguration | "new" | null>(null), [pending, setPending] = useState(false), [error, setError] = useState("");
  const query = useQuery<SupplyConfiguration[]>(read ? `supplies/configuration?branchId=${branch}` : null, revision);
  const options = useQuery<{ items: Row[]; warehouses: Row[]; references: Row[] }>(write ? `workspace/options?branchId=${branch}` : null, revision);
  const assets = useQuery<Row[]>(write && can("ASSET_VIEW", false) ? `assets?branchId=${branch}` : null);
  async function save(values: SaveSupplyConfiguration) {
    if (pending || !write) return;
    setPending(true); setError("");
    try { await saveSupplyConfiguration(values); setGroup(values.supplyGroup); setEdit(null); bump(n => n + 1); }
    catch (e) { setError(supplyMessage(e)); bump(n => n + 1); }
    finally { setPending(false); }
  }
  // workspace/options already scopes active warehouses to the requested branch.
  const warehouses = options.data?.warehouses || [];
  const rows = (query.data || []).filter(r => r.supplyGroup === group && (!status || String(r.isActive) === status) && `${r.name} ${r.code}`.toLocaleLowerCase("vi").includes(search.toLocaleLowerCase("vi")));
  return <div className="supply-page">
    <PageTitle><h1>Cấu hình vật tư phòng</h1><p>Định mức cấp theo công việc và sức chứa tối đa của phòng</p></PageTitle>
    {!read ? <Empty>Bạn chưa được cấp quyền xem cấu hình vật tư.</Empty> : <>
      {write && <details><summary>Phân loại vật tư và chính sách hạn sử dụng</summary><InventoryLifecycle branch={branch} configuration /></details>}
      <nav className="supply-group-tabs" aria-label="Nhóm cấu hình vật tư">{(Object.keys(supplyGroups) as SupplyGroup[]).map(key => <button key={key} className={group === key ? "active" : ""} aria-pressed={group === key} onClick={() => { setGroup(key); setSearch(""); }}><strong>{supplyGroups[key]}</strong><span>{(query.data || []).filter(r => r.supplyGroup === key).length} cấu hình</span></button>)}</nav>
      <section className="supply-group-summary"><p>{groupDescriptions[group]}</p>{write && <State {...options}><p>Kho chi nhánh: <strong>{warehouses.length === 1 ? warehouses[0].name : "Chưa xác định được kho duy nhất"}</strong>. Áp dụng chung cho mọi vật tư lưu kho.</p></State>}</section>
      <PageToolbar actions={write && <button className="primary" onClick={() => { setError(""); setEdit("new"); }}>Thêm cấu hình</button>}>
        <label>Tìm vật tư<input value={search} onChange={e => setSearch(e.target.value)} /></label>
        <button aria-expanded={filters} onClick={() => setFilters(!filters)}>Bộ lọc</button>
        <button onClick={() => bump(n => n + 1)}>Tải lại</button>
      </PageToolbar>
      {filters && <PageToolbar><label>Trạng thái<select value={status} onChange={e => setStatus(e.target.value)}><option value="">Tất cả trạng thái</option><option value="true">Đang áp dụng</option><option value="false">Ngừng áp dụng</option></select></label><button onClick={() => { setSearch(""); setStatus(""); }}>Đặt lại</button></PageToolbar>}
      {error && !edit && <AlertPopup>{error}</AlertPopup>}
      <State {...query} retry={() => bump(n => n + 1)}>
        <table className="supply-table"><thead><tr><th>Vật tư / thiết bị</th><th>{group === "A" ? "Định mức cấp phát" : "Quản lý tồn kho"}</th><th>Cập nhật</th><th>Trạng thái</th>{write && <th>Thao tác</th>}</tr></thead><tbody>
          {rows.map(row => <tr key={row.configurationId}>
            <td data-label="Vật tư / thiết bị"><strong>{row.name}</strong><small>{row.code} · {row.unit || "Thiết bị"}</small></td>
            <td data-label={group === "A" ? "Định mức cấp phát" : "Quản lý tồn kho"}>{row.supplyGroup === "A" && <><div>Mỗi khách: {supplyQuantity(row.perGuestQuantity)}</div></>}{row.supplyGroup === "B" && row.initialQuantity > 0 && <div>Định mức đồ dùng: {supplyQuantity(row.initialQuantity)}</div>}{row.itemId != null ? <div>Tồn tối thiểu: {supplyQuantity(row.minimumStock)}</div> : <span>Thiết bị gắn với phòng</span>}</td>
            <td data-label="Phiên bản">{row.revision}<small>{timestamp(row.updatedAt)}</small><small>Người cập nhật: {row.updatedBy === profile.user.fields.userId ? profile.user.fields.fullName || "Bạn" : `Nhân viên ${row.updatedBy}`}</small></td>
            <td data-label="Trạng thái">{write ? <StatusSwitch active={row.isActive} label={row.name} disabled={pending || query.loading} onChange={() => save({ ...row, isActive: !row.isActive })} /> : <span className={`badge ${row.isActive ? "good" : "bad"}`}>{row.isActive ? "Đang áp dụng" : "Ngừng áp dụng"}</span>}</td>
            {write && <td data-label="Thao tác"><button disabled={pending} onClick={() => { setError(""); setEdit(row); }}>Chỉnh sửa</button></td>}
          </tr>)}
        </tbody></table>{!rows.length && <Empty>Chưa có cấu hình phù hợp. Không có định mức nào được tự áp dụng.</Empty>}
      </State>
    </>}
    {edit && write && <Dialog title={edit === "new" ? "Thêm cấu hình vật tư" : "Chỉnh sửa định mức"} close={() => !pending && setEdit(null)}>
      <State {...options}><State {...assets}>
        <ConfigurationForm key={edit === "new" ? `new-${group}` : edit.configurationId} branch={branch} initialGroup={group} row={edit === "new" ? undefined : edit} options={options.data!} assets={assets.data || []} pending={pending} save={save} />
      </State></State>{error && <AlertPopup>{error}</AlertPopup>}
    </Dialog>}
  </div>;
}
function ConfigurationForm({ branch, initialGroup, row, options, assets, pending, save }: { branch: number; initialGroup: SupplyGroup; row?: SupplyConfiguration; options: { items: Row[]; warehouses: Row[]; references: Row[] }; assets: Row[]; pending: boolean; save: (values: SaveSupplyConfiguration) => void }) {
  const [subject, setSubject] = useState(row ? `${row.itemId ? "item" : "asset"}:${row.itemId || row.assetId}` : "");
  const [group, setGroup] = useState<SupplyGroup>(row?.supplyGroup || initialGroup);
  const warehouses = options.warehouses;
  const isAsset = subject.startsWith("asset:"), id = Number(subject.split(":")[1]);
  const item = options.items.find(i => i.itemId === id);
  const lifecycle = useQuery<Row[]>(`inventory-lifecycle/profiles?branchId=${branch}`);
  const reusable = !isAsset && lifecycle.data?.some(p => p.itemId === id && p.classification === "REUSABLE");
  const reusableAllocation = !isAsset && group === "B" && reusable;
  const unitId = row?.unitId || item?.baseUnitId || null;
  const unit = options.references.find(r => r.referenceId === unitId)?.name || row?.unit || "—";
  return <form className="supply-form" onSubmit={e => {
    e.preventDefault(); const form = new FormData(e.currentTarget);
    if (!isAsset && warehouses.length !== 1) return;
    save({ branchId: branch, itemId: isAsset ? null : id, assetId: isAsset ? id : null, supplyGroup: isAsset ? "C" : group, warehouseId: null, unitId: isAsset ? null : unitId, initialQuantity: !isAsset && group === "A" ? (row?.initialQuantity || Number(form.get("perGuest"))) : reusableAllocation ? Number(form.get("initial")) : 0, minimumStock: isAsset ? 0 : Number(form.get("minimum")), perGuestQuantity: !isAsset && group === "A" ? Number(form.get("perGuest")) : 0, isActive: row?.isActive ?? true, rowVersion: row?.rowVersion || null });
  }}>
    <label>Vật tư / thiết bị<select required value={subject} disabled={!!row || pending} onChange={e => { setSubject(e.target.value); if (e.target.value.startsWith("asset:")) setGroup("C"); }}>
      <option value="">Chọn vật tư hoặc thiết bị</option>
      {row && <option value={subject}>{row.name}</option>}
      {!row && <><optgroup label="Vật tư">{options.items.map(i => <option key={i.itemId} value={`item:${i.itemId}`}>{i.name} · {i.code}</option>)}</optgroup>{group === "C" && <optgroup label="Thiết bị thuộc chi nhánh">{assets.filter(a => a.isActive).map(a => <option key={a.assetId} value={`asset:${a.assetId}`}>{a.name} · {a.assetCode}</option>)}</optgroup>}</>}
    </select></label>
    {row ? <label>Nhóm<select value={group} disabled={isAsset || pending} onChange={e => setGroup(e.target.value as SupplyGroup)}>{Object.entries(supplyGroups).map(([key, name]) => <option key={key} value={key}>{name}</option>)}</select></label> : <p>Nhóm: <strong>{supplyGroups[group]}</strong></p>}
    {!isAsset && <><p>Đơn vị cơ sở: <strong>{unit}</strong></p><p>Kho chi nhánh: <strong>{warehouses.length === 1 ? warehouses[0].name : "Chưa xác định được kho duy nhất"}</strong></p>{warehouses.length !== 1 && <AlertPopup>{supplyMessage("BRANCH_REQUIRES_ONE_ACTIVE_WAREHOUSE")}</AlertPopup>}
      <div className="supply-form-grid">{reusableAllocation && <label>Định mức đồ dùng<input name="initial" type="number" min="0" step="0.0001" required defaultValue={row?.initialQuantity ?? 0} disabled={pending} /></label>}{group === "A" && <label>Cho mỗi khách<input name="perGuest" type="number" min="0.0001" step="0.0001" required defaultValue={row?.perGuestQuantity || ""} disabled={pending} /></label>}<label>Tồn tối thiểu<input name="minimum" type="number" min="0" step="0.0001" required defaultValue={row?.minimumStock ?? ""} disabled={pending} /></label></div>
    </>}
    <p className="muted">Thay đổi tạo phiên bản mới; các định mức đã lưu và giao dịch đã ghi nhận được giữ nguyên. Ca tự có hiệu lực không cấp vật tư. Chỉ ghi nhận tiêu hao theo số thực tế bổ sung vào phòng.</p>
    {!!lifecycle.error && <AlertPopup>Không tải được phân loại vật tư. Vui lòng mở lại biểu mẫu.</AlertPopup>}
    <button className="primary" disabled={pending || lifecycle.loading || !!lifecycle.error || !subject || (!isAsset && warehouses.length !== 1)}>{pending ? "Đang lưu…" : "Lưu cấu hình"}</button>
  </form>;
}
