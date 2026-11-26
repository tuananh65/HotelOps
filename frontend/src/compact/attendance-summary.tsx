import { useEffect, useState } from "react";
import { useAuth } from "./auth";
import { timestamp } from "./api";
import { State, useQuery } from "./ui";
import StatChart from "./stat-chart";

type Summary = { timezone:string; updatedAt:string; periods:{period:string;from:string;to:string;completedShifts:number;workedHours:number;workingShifts:number}[] };
export default function AttendanceSummary() {
  const { can, profile, branch } = useAuth();
  const excluded=profile.roles.some(r=>(r.branchId===null||r.branchId===branch)&&["OPERATION_MANAGER","OWNER","SYS_ADMIN"].includes(r.code));
  return can("ATTENDANCE_RECORD")&&!excluded ? <AttendanceSummaryContent/> : null;
}
function AttendanceSummaryContent() {
  const { branch }=useAuth();
  const [revision,refresh]=useState(0), [period,setPeriod]=useState("WEEK");
  const query=useQuery<Summary>(`workday/personal-summary?branchId=${branch}`,revision);
  useEffect(()=>{const timer=setInterval(()=>{if(document.visibilityState==="visible")refresh(n=>n+1);},30000);return()=>clearInterval(timer);},[]);
  const selected=query.data?.periods.find(p=>p.period===period);
  return <section className="panel attendance-summary" aria-label="Thống kê ca làm của tôi">
    <div className="section-title"><h2>Ca làm của tôi</h2><div className="tabs" aria-label="Kỳ thống kê">
      {[["DAY","Ngày"],["WEEK","Tuần"],["MONTH","Tháng"]].map(([key,title])=><button key={key} type="button" aria-pressed={period===key} className={period===key?"primary":""} onClick={()=>setPeriod(key)}>{title}</button>)}
    </div></div>
    <State {...query} retry={()=>refresh(n=>n+1)}>{selected&&<>
      <small>{new Date(selected.from+"T12:00:00").toLocaleDateString("vi-VN")} – {new Date(selected.to+"T12:00:00").toLocaleDateString("vi-VN")}</small>
      <div className="attendance-chart-grid"><StatChart title="Số ca làm việc" rows={[{name:"Đã kết thúc",value:selected.completedShifts,color:"#21665e"},{name:"Đang trong ca",value:selected.workingShifts,color:"#a56926"}]}/>
      <div className="attendance-hours"><strong>{selected.workedHours.toLocaleString("vi-VN",{maximumFractionDigits:2})}</strong><span>giờ đã làm</span><small>Tính theo giờ vào–ra thực tế của ca đã kết thúc. Ca đang làm được thống kê riêng.</small></div></div>
      <small>Cập nhật {timestamp(query.data!.updatedAt)} · {query.data!.timezone}</small>
    </>}</State>
  </section>;
}
