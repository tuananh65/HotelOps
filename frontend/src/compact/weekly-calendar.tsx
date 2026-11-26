import { useState } from "react";
import { useAuth } from "./auth";
import { dateKey, type Row } from "./api";
import { Badge, Dialog, State, useQuery } from "./ui";

const time=(minutes:number)=>`${String(Math.floor(minutes/60)).padStart(2,"0")}:${String(minutes%60).padStart(2,"0")}`;
export default function WeeklyCalendar({close}:{close:()=>void}) {
  const {branch,profile}=useAuth();
  const [date,setDate]=useState(dateKey());
  const query=useQuery(`workday?branchId=${branch}&date=${date}`);
  const move=(days:number)=>{const next=new Date(date+"T12:00:00");next.setDate(next.getDate()+days);setDate(dateKey(next));};
  const rows=(query.data?.assignments??[]).filter((row:Row)=>row.userId===profile.user.fields.userId);
  return <Dialog title="Lịch làm việc của tôi" close={close}>
    <div className="toolbar"><button onClick={()=>move(-7)}>Tuần trước</button><button onClick={()=>setDate(dateKey())}>Tuần hiện tại</button><button onClick={()=>move(7)}>Tuần sau</button></div>
    <State {...query}>{query.data&&<div className="personal-week-calendar">
      {Array.from({length:7},(_,index)=>{
        const day=new Date(query.data.weekStart+"T12:00:00");day.setDate(day.getDate()+index);const key=dateKey(day);
        const assignments=rows.filter((row:Row)=>row.workDate===key);
        return <section key={key}><h3>{index===6?"Chủ nhật":`Thứ ${index+2}`} · {day.toLocaleDateString("vi-VN",{day:"2-digit",month:"2-digit"})}</h3>
          {assignments.length?assignments.map((row:Row)=>{const shift=query.data.shifts.find((s:Row)=>s.shiftId===row.shiftId);return <article key={row.assignmentId}><strong>{shift?.name??"Ca làm việc"}</strong><p>{shift?`${time(shift.startMinute)}–${time(shift.endMinute)}`:""}</p><Badge value={row.status}/></article>;}):<p>Không có ca</p>}
        </section>;
      })}
    </div>}</State>
  </Dialog>;
}
