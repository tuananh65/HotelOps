import { useEffect, useState } from "react";
import { useAuth } from "./auth";
import { api } from "./api";

const commands = /^(Bắt đầu|Tiếp nhận|Nhận việc|Nhận công việc|Nhận bàn giao|Tiếp tục|Tạm dừng|Gửi |Xác nhận|Hoàn thành|Hoàn tất|Lưu |Báo sự cố|Phân công|Tạo |Thêm vật tư|Ghi sổ|Cấp phát|Duyệt|Từ chối|Luân chuyển|Đã sửa|Không thể sửa|Hủy công việc)/i;

export default function ShiftCommandGuard({ page }: {page:string}) {
  const { branch, profile, can } = useAuth();
  const exempt=profile.roles.some(r=>(r.branchId===null||r.branchId===branch)&&["OWNER","SYS_ADMIN","OPERATION_MANAGER"].includes(r.code));
  const applicable=!!branch&&!exempt&&can("ATTENDANCE_RECORD")&&!['workday','notifications','configuration','internal-settings','templates','settings','users'].includes(page);
  const [allowed,setAllowed]=useState(false);
  useEffect(()=>{
    if(!applicable)return;
    let disposed=false;
    const update=()=>api<{canOperate:boolean}>(`workday/command-access?branchId=${branch}`).then(result=>{if(!disposed)setAllowed(result.canOperate);}).catch(()=>{if(!disposed)setAllowed(false);});
    setAllowed(false);void update();
    const timer=setInterval(update,15000);
    window.addEventListener('focus',update);
    window.addEventListener('compact:notification-change',update);
    window.addEventListener('compact:shift-required',update);
    return()=>{disposed=true;clearInterval(timer);window.removeEventListener('focus',update);window.removeEventListener('compact:notification-change',update);window.removeEventListener('compact:shift-required',update);};
  },[branch,applicable]);
  useEffect(()=>{
    if(!applicable||allowed)return;
    const blocked=new Set<HTMLButtonElement>();
    const apply=()=>document.querySelectorAll<HTMLButtonElement>('#main-content button').forEach(button=>{
      if(button.closest('[data-cleaning-continuation="true"]')) {
        if(button.dataset.shiftBlocked){button.disabled=false;delete button.dataset.shiftBlocked;button.removeAttribute('title');blocked.delete(button);}
        return;
      }
      const text=button.textContent?.trim()||'';
      const passive=/^(Hủy$|Đóng|×$|Tìm|Lọc|Xóa bộ lọc|Xem|Mở lịch|Tuần|Hôm nay|Trước$|Tiếp$)/.test(text);
      const action=!passive&&(button.matches('.confirm-accept')||commands.test(text)||button.type==='submit'&&!!button.closest('form'));
      if(action&&!button.disabled){button.disabled=true;button.dataset.shiftBlocked='true';button.title='Bạn cần được phân ca và đang trong giờ ca để thực hiện thao tác này';blocked.add(button);}
    });
    apply();const observer=new MutationObserver(apply);observer.observe(document.body,{childList:true,subtree:true,attributes:true,attributeFilter:['disabled','data-cleaning-continuation']});
    return()=>{observer.disconnect();blocked.forEach(button=>{if(button.dataset.shiftBlocked){button.disabled=false;delete button.dataset.shiftBlocked;button.removeAttribute('title');}});};
  },[applicable,allowed,page]);
  return applicable&&!allowed?<div className="panel shift-command-notice" role="status">Bạn cần được phân ca và đang trong giờ ca để thực hiện thao tác này. Liên hệ quản lý vận hành nếu cần kiểm tra phân ca.</div>:null;
}
