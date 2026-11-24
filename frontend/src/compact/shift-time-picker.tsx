import { useState } from "react";

export default function ShiftTimePicker({name,label,minutes,allowMidnight=false,disabled=false}:{name:string;label:string;minutes:number;allowMidnight?:boolean;disabled?:boolean}) {
  const [value,setValue]=useState(minutes);
  const hour=Math.floor(value/60),minute=value%60;
  const two=(n:number)=>String(n).padStart(2,"0");
  return <fieldset className="shift-time-picker" disabled={disabled}>
    <legend>{label}</legend>
    <input type="hidden" name={name} value={`${two(hour)}:${two(minute)}`}/>
    <div className="actions">
      <label>Giờ<select aria-label={`${label} — giờ`} value={hour} onChange={e=>{const h=Number(e.target.value);setValue(h*60+(h===24?0:minute));}}>
        {Array.from({length:allowMidnight?25:24},(_,h)=><option key={h} value={h}>{two(h)}</option>)}
      </select></label>
      <label>Phút<select aria-label={`${label} — phút`} value={minute} disabled={disabled||hour===24} onChange={e=>setValue(hour*60+Number(e.target.value))}>
        {Array.from({length:60},(_,m)=><option key={m} value={m}>{two(m)}</option>)}
      </select></label>
    </div>
  </fieldset>;
}
