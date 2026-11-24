import { useState } from "react";
import { useAuth } from "./auth";
import { timestamp, type Row } from "./api";
import { Dialog, Empty, State, useQuery } from "./ui";
import { AlertPopup } from "./alert-popup";
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import StatusSwitch from "./status-switch";
import { saveSupplyConfiguration, supplyGroups, supplyMessage, supplyQuantity, type SaveSupplyConfiguration, type SupplyConfiguration, type SupplyGroup } from "./supply-api";
import "./supplies.css";

export default function SupplyConfigurationPage() {
  const { branch } = useAuth();
  return <ConfigurationBranch key={branch} />;
}
function ConfigurationBranch() {
  const { branch, can, profile } = useAuth();
  const write = can("ROOM_SUPPLY_CONFIG", false) && profile.roles.some(r => r.code === "OPERATION_MANAGER" && (r.branchId == null || r.branchId === branch));
  const read = can("ROOM_SUPPLY_CONFIG", false) || can("STAFF_SUPPLY_VIEW", true);
  const [revision, bump] = useState(0), [search, setSearch] = useState(""), [group, setGroup] = useState("");
  const [filters, setFilters] = useState(false);
  const [edit, setEdit] = useState<SupplyConfiguration | "new" | null>(null), [pending, setPending] = useState(false), [error, setError] = useState("");
  const query = useQuery<SupplyConfiguration[]>(read ? `supplies/configuration?branchId=${branch}` : null, revision);
  const options = useQuery<{ items: Row[]; warehouses: Row[]; references: Row[] }>(write ? `workspace/options?branchId=${branch}` : null);
  const assets = useQuery<Row[]>(write && can("ASSET_VIEW", false) ? `assets?branchId=${branch}` : null);
  async function save(values: SaveSupplyConfiguration) {
    if (pending || !write) return;
    setPending(true); setError("");
    try { await saveSupplyConfiguration(values); setEdit(null); bump(n => n + 1); }
    catch (e) { setError(supplyMessage(e)); bump(n => n + 1); }
    finally { setPending(false); }
  }
  const rows = (query.data || []).filter(r => (!group || r.supplyGroup === group) && `${r.name} ${r.code}`.toLocaleLowerCase("vi").includes(search.toLocaleLowerCase("vi")));
  return <div className="supply-page">
    <PageTitle><h1>Cấu hình vật tư phòng</h1><p>Định mức theo chi nhánh; ca đã mở giữ phiên bản đầu ca</p></PageTitle>
    {!read ? <Empty>Bạn chưa được cấp quyền xem cấu hình vật tư.</Empty> : <>
      <PageToolbar actions={write && <button className="primary" onClick={() => { setError(""); setEdit("new"); }}>Thêm cấu hình</button>}>
        <label>Tìm vật tư<input value={search} onChange={e => setSearch(e.target.value)} /></label>
        <button aria-expanded={filters} onClick={() => setFilters(!filters)}>Bộ lọc</button>
        <button onClick={() => bump(n => n + 1)}>Tải lại</button>
      </PageToolbar>
      {filters && <PageToolbar><label>Nhóm<select value={group} onChange={e => setGroup(e.target.value)}><option value="">Tất cả nhóm</option>{Object.entries(supplyGroups).map(([key, name]) => <option key={key} value={key}>{name}</option>)}</select></label><button onClick={() => { setSearch(""); setGroup(""); }}>Đặt lại</button></PageToolbar>}
      {error && !edit && <AlertPopup>{error}</AlertPopup>}
      <State {...query} retry={() => bump(n => n + 1)}>
        <table className="supply-table"><thead><tr><th>Vật tư / thiết bị</th><th>Nhóm</th><th>Định mức</th><th>Phiên bản</th><th>Trạng thái</th>{write && <th>Thao tác</th>}</tr></thead><tbody>
          {rows.map(row => <tr key={row.configurationId}>
            <td data-label="Vật tư / thiết bị"><strong>{row.name}</strong><small>{row.code} · {row.unit || "Thiết bị"}</small></td>
            <td data-label="Nhóm">{supplyGroups[row.supplyGroup]}</td>
            <td data-label="Định mức">{row.supplyGroup === "A" && <><div>Đầu ca: {supplyQuantity(row.initialQuantity)}</div><div>Mỗi khách: {supplyQuantity(row.perGuestQuantity)}</div></>}{row.itemId != null && <div>Tồn tối thiểu: {supplyQuantity(row.minimumStock)}</div>}{row.warehouseId && <small>{options.data?.warehouses.find(w => w.warehouseId === row.warehouseId)?.name || `Kho ${row.warehouseId}`}</small>}</td>
            <td data-label="Phiên bản">{row.revision}<small>{timestamp(row.updatedAt)}</small><small>Người cập nhật: {row.updatedBy === profile.user.fields.userId ? profile.user.fields.fullName || "Bạn" : `Nhân viên ${row.updatedBy}`}</small></td>
            <td data-label="Trạng thái">{write ? <StatusSwitch active={row.isActive} label={row.name} disabled={pending || query.loading} onChange={() => save({ ...row, isActive: !row.isActive })} /> : <span className={`badge ${row.isActive ? "good" : "bad"}`}>{row.isActive ? "Đang áp dụng" : "Ngừng áp dụng"}</span>}</td>
            {write && <td data-label="Thao tác"><button disabled={pending} onClick={() => { setError(""); setEdit(row); }}>Chỉnh sửa</button></td>}
          </tr>)}
        </tbody></table>{!rows.length && <Empty>Chưa có cấu hình phù hợp. Không có định mức nào được tự áp dụng.</Empty>}
      </State>
    </>}
    {edit && write && <Dialog title={edit === "new" ? "Thêm cấu hình vật tư" : "Chỉnh sửa định mức"} close={() => !pending && setEdit(null)}>
      <State {...options}><State {...assets}>
        <ConfigurationForm key={edit === "new" ? "new" : edit.configurationId} branch={branch} row={edit === "new" ? undefined : edit} options={options.data!} assets={assets.data || []} pending={pending} save={save} />
      </State></State>{error && <AlertPopup>{error}</AlertPopup>}
    </Dialog>}
  </div>;
}
function ConfigurationForm({ branch, row, options, assets, pending, save }: { branch: number; row?: SupplyConfiguration; options: { items: Row[]; warehouses: Row[]; references: Row[] }; assets: Row[]; pending: boolean; save: (values: SaveSupplyConfiguration) => void }) {
  const [subject, setSubject] = useState(row ? `${row.itemId ? "item" : "asset"}:${row.itemId || row.assetId}` : "");
  const [group, setGroup] = useState<SupplyGroup>(row?.supplyGroup || "A");
  const isAsset = subject.startsWith("asset:"), id = Number(subject.split(":")[1]);
  const item = options.items.find(i => i.itemId === id);
  const unitId = row?.unitId || item?.baseUnitId || null;
  const unit = options.references.find(r => r.referenceId === unitId)?.name || row?.unit || "—";
  return <form className="supply-form" onSubmit={e => {
    e.preventDefault(); const form = new FormData(e.currentTarget);
    save({ branchId: branch, itemId: isAsset ? null : id, assetId: isAsset ? id : null, supplyGroup: isAsset ? "C" : group, warehouseId: isAsset ? null : Number(form.get("warehouse")) || null, unitId: isAsset ? null : unitId, initialQuantity: !isAsset && group === "A" ? Number(form.get("initial")) : 0, minimumStock: isAsset ? 0 : Number(form.get("minimum")), perGuestQuantity: !isAsset && group === "A" ? Number(form.get("perGuest")) : 0, isActive: row?.isActive ?? true, rowVersion: row?.rowVersion || null });
  }}>
    <label>Vật tư / thiết bị<select required value={subject} disabled={!!row || pending} onChange={e => { setSubject(e.target.value); if (e.target.value.startsWith("asset:")) setGroup("C"); }}>
      <option value="">Chọn vật tư hoặc thiết bị</option>
      {row && <option value={subject}>{row.name}</option>}
      {!row && <><optgroup label="Vật tư">{options.items.map(i => <option key={i.itemId} value={`item:${i.itemId}`}>{i.name} · {i.code}</option>)}</optgroup><optgroup label="Thiết bị thuộc chi nhánh">{assets.filter(a => a.isActive).map(a => <option key={a.assetId} value={`asset:${a.assetId}`}>{a.name} · {a.assetCode}</option>)}</optgroup></>}
    </select></label>
    <label>Nhóm<select value={group} disabled={isAsset || pending} onChange={e => setGroup(e.target.value as SupplyGroup)}>{Object.entries(supplyGroups).map(([key, name]) => <option key={key} value={key}>{name}</option>)}</select></label>
    {!isAsset && <><p>Đơn vị cơ sở: <strong>{unit}</strong></p><label>Kho cấp vật tư<select name="warehouse" required={group === "A"} defaultValue={row?.warehouseId || ""} disabled={pending}><option value="">Chọn kho</option>{options.warehouses.map(w => <option key={w.warehouseId} value={w.warehouseId}>{w.name}</option>)}</select></label>
      <div className="supply-form-grid">{group === "A" && <><label>Cấp đầu ca<input name="initial" type="number" min="0.0001" step="0.0001" required defaultValue={row?.initialQuantity || ""} disabled={pending} /></label><label>Cho mỗi khách<input name="perGuest" type="number" min="0.0001" step="0.0001" required defaultValue={row?.perGuestQuantity || ""} disabled={pending} /></label></>}<label>Tồn tối thiểu<input name="minimum" type="number" min="0" step="0.0001" required defaultValue={row?.minimumStock ?? ""} disabled={pending} /></label></div>
    </>}
    <p className="muted">Thay đổi tạo phiên bản mới. Định mức ca đang mở và giao dịch đã ghi nhận được giữ nguyên.</p>
    <button className="primary" disabled={pending || !subject}>{pending ? "Đang lưu…" : "Lưu cấu hình"}</button>
  </form>;
}
