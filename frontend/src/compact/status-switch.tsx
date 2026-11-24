import "./status-switch.css";

export default function StatusSwitch({active,label,disabled,onChange}:{active:boolean;label:string;disabled?:boolean;onChange:()=>void}) {
  return <button type="button" role="switch" aria-checked={active} aria-label={`${label}: ${active?"Đang hoạt động":"Ngừng sử dụng"}`} title={active?"Ngừng sử dụng":"Kích hoạt"} className="status-switch" disabled={disabled} onClick={onChange}><span aria-hidden="true" /></button>;
}
