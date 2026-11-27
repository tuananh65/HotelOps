import { useState } from "react";
import { type Row } from "./api";
import { Empty, Pager, State, useQuery } from "./ui";
import PageToolbar from "./page-toolbar";
import PageTitle from "./page-title";

export default function CrossBranchStock({ branch }: { branch: number }) {
  const query = useQuery(`documents/transfer-catalog?branchId=${branch}`);
  const [warehouse,setWarehouse]=useState(0),[search,setSearch]=useState(""),[page,setPage]=useState(1);
  const [filters,setFilters]=useState(false),[low,setLow]=useState(false),[sort,setSort]=useState("name");
  const warehouses:Row[]=query.data?.warehouses.filter((w:Row)=>w.branchId!==branch)||[];
  const selected=warehouse||warehouses[0]?.warehouseId;
  const rows:Row[]=(query.data?.balances||[]).filter((b:Row)=>b.warehouseId===selected).map((b:Row)=>({...query.data.items.find((i:Row)=>i.itemId===b.itemId),...b}))
    .filter((r:Row)=>`${r.name} ${r.code}`.toLocaleLowerCase("vi").includes(search.toLocaleLowerCase("vi"))&&(!low||(r.minimumQty!=null&&r.availableBaseQty<r.minimumQty)))
    .sort((a:Row,b:Row)=>sort==="quantity"?a.availableBaseQty-b.availableBaseQty:String(a.name).localeCompare(String(b.name),"vi"));
  return <><PageTitle><h1>Tồn kho vật tư</h1><p>Xem kho chi nhánh khác và đề nghị điều chuyển về chi nhánh quản lý.</p></PageTitle><State {...query}>
    <div className="stock-context"><label>Kho đang xem<select value={selected||""} onChange={e=>{setWarehouse(Number(e.target.value));setPage(1);}}>{warehouses.map(w=><option key={w.warehouseId} value={w.warehouseId}>{w.branchName} · {w.name}</option>)}</select></label>
      {selected&&<a className="button primary" href={`#documents?transfer=1&sourceWarehouseId=${selected}`}>Yêu cầu điều chuyển</a>}</div>
    <PageToolbar><label className="search">Tìm vật tư<input value={search} onChange={e=>{setSearch(e.target.value);setPage(1);}}/></label><button aria-expanded={filters} onClick={()=>setFilters(!filters)}>Bộ lọc</button></PageToolbar>
    {filters&&<PageToolbar><label>Sắp xếp<select value={sort} onChange={e=>{setSort(e.target.value);setPage(1);}}><option value="name">Tên vật tư</option><option value="quantity">Tồn tăng dần</option></select></label><label><input type="checkbox" checked={low} onChange={e=>{setLow(e.target.checked);setPage(1);}}/>Dưới tối thiểu</label></PageToolbar>}
    <div className="table-scroll"><table className="mobile-records"><thead><tr><th>STT</th><th>Vật tư</th><th className="numeric">Tồn cơ sở</th><th>Đơn vị</th><th>Tối thiểu</th></tr></thead><tbody>{rows.slice((page-1)*25,page*25).map((r,index)=><tr key={r.itemId} className={r.minimumQty!=null&&r.availableBaseQty<r.minimumQty?"stock-below-minimum":undefined}>
      <td data-label="STT">{(page-1)*25+index+1}</td><td data-label="Vật tư">{r.name}</td><td className="numeric" data-label="Tồn cơ sở">{Number(r.baseQty).toLocaleString("vi-VN")}<small>Có thể cấp: {Number(r.availableBaseQty).toLocaleString("vi-VN")}</small>{r.minimumQty!=null&&r.availableBaseQty<r.minimumQty&&<small>Tồn khả dụng dưới tối thiểu</small>}</td><td data-label="Đơn vị">{query.data.units.find((u:Row)=>u.referenceId===r.baseUnitId)?.name||"—"}</td><td data-label="Tối thiểu">{r.minimumQty??"—"}</td>
    </tr>)}</tbody></table></div>{!rows.length&&<Empty>Chưa có tồn kho phù hợp với bộ lọc.</Empty>}<Pager page={page} total={rows.length} pageSize={25} onPage={setPage}/>
  </State></>;
}
