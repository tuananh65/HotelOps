import { users, filteredUsers, userInspector, userList, userRows, userOverview, userDetail, userForm } from './users.js';
const app = document.querySelector('#app');
const description = document.querySelector('#description');
const params = new URLSearchParams(location.search);
const initialVariant = ['a','b','c','d'].includes(params.get('variant')) ? params.get('variant') : 'd';
const initialPalette = ['1','2','3','4','5','6','7','8','9'].includes(params.get('palette')) ? params.get('palette') : initialVariant === 'c' ? '2' : '1';
const state = { variant: initialVariant, palette: initialPalette, split: 360, view: 'list', selected: '101', branch: '', department: '', role: '', editing: null, more: false, phone: false, query: '', status: '', menu: false };
const paths = {
  home: '<path d="m3 10 9-7 9 7v10H3Z"/><path d="M9 20v-7h6v7"/>',
  rooms: '<rect x="4" y="3" width="16" height="18" rx="2"/><path d="M8 7h2m4 0h2M8 11h2m4 0h2M10 21v-6h4v6"/>',
  task: '<rect x="5" y="4" width="14" height="17" rx="2"/><path d="M9 3h6v4H9zM9 12l2 2 4-4M9 18h6"/>',
  box: '<path d="m3 7 9-4 9 4v10l-9 4-9-4Zm0 0 9 4 9-4M12 11v10"/>',
  people: '<circle cx="9" cy="8" r="3"/><path d="M3 21v-3a6 6 0 0 1 12 0v3M16 5a3 3 0 0 1 0 6m3 10v-3a6 6 0 0 0-2-4"/>',
  chart: '<path d="M4 3v18h17M8 16v-4m5 4V7m5 9V4"/>',
  settings: '<path d="M4 6h16M4 12h16M4 18h16"/><circle cx="9" cy="6" r="2"/><circle cx="16" cy="12" r="2"/><circle cx="9" cy="18" r="2"/>',
  arrow: '<path d="M5 12h14m-5-5 5 5-5 5"/>',
  menu: '<path d="M4 6h16M4 12h16M4 18h16"/>'
};
const icon = name => `<svg viewBox="0 0 24 24" aria-hidden="true">${paths[name] || paths.task}</svg>`;
const brand = '<div class="brand">Hotel<span>Ops</span></div>';
function navigation(){return '<button data-view="list" class="'+(state.view==='list'?'on':'')+'">Người dùng</button><button data-view="overview" class="'+(state.view==='overview'?'on':'')+'">Theo chi nhánh</button><button data-create>Tạo tài khoản</button>';}
function context(){return '<div class="context"><div><strong>Quản trị hệ thống</strong><span class="secondary">/ Người dùng</span></div><div><span class="secondary">Tài khoản và phạm vi truy cập</span><button class="mobile-menu" data-menu aria-label="Mở điều hướng">'+icon('menu')+'</button></div></div>';}
function renderRows(){const rows=document.querySelector('#rows');if(!rows)return;rows.innerHTML=userRows(state,app.classList.contains('phone'));const count=filteredUsers(state).length;document.querySelector('#empty').hidden=count!==0;document.querySelector('#result-count').textContent=count+' / '+users.length+' tài khoản';}
function render() {
  app.dataset.variant = state.variant;
  app.dataset.palette = state.palette;
  app.style.setProperty('--split-width', state.split+'px');
  const share = new URL(location.href); share.searchParams.set('variant', state.variant); share.searchParams.set('palette',state.palette); history.replaceState(null,'',share);
  app.classList.toggle('phone', state.phone || innerWidth <= 700);
  app.classList.toggle('menu-open', state.menu);
  const layouts = { a: 'Sidebar đơn; danh sách và chi tiết chia vùng, điều chỉnh được độ rộng.', b: 'Menu ngang và vùng nội dung toàn chiều rộng; chi tiết mở thành trang riêng.', c: 'Thanh nhóm nghiệp vụ + menu phụ; danh sách có vùng xem nhanh.', d: 'Tổng quan tài khoản, danh sách theo chi nhánh và hồ sơ ở các không gian riêng.' };
  const palettes = {1:'Navy – xanh lam',2:'Xanh rừng – ngọc',3:'Mận – trắng đá',4:'Than chì – xanh băng',5:'Teal biển sâu',6:'Electric Fuchsia',7:'Tím tươi',8:'Cacao – kem',9:'Trắng mây'};
  description.textContent = state.variant.toUpperCase()+' + '+state.palette+' · '+layouts[state.variant]+' '+palettes[state.palette]+'.';
  const content = state.view === 'overview' ? userOverview(state) : state.view === 'list' ? userList(state) : state.view === 'form' ? userForm(state) : userDetail(state);
  const note = '<p class="note">Bản xem trước tương tác. Tất cả tài khoản, đơn vị và vai trò ở đây là minh họa; không phải dữ liệu nhân sự thật.</p>';
  if (['b','d'].includes(state.variant)) {
    app.innerHTML = `<header class="global">${brand}<nav aria-label="Điều hướng bản mẫu">${navigation()}</nav><div class="identity"><span class="avatar">NV</span>Nhân viên mẫu</div><button class="mobile-menu" data-menu aria-label="Mở điều hướng">${icon('menu')}</button></header>${context()}<main class="main">${content}${note}</main>`;
  } else if (state.variant === 'a') {
    app.innerHTML = `<div class="single-sidebar"><aside class="single-nav">${brand}<p>Không gian làm việc</p><nav aria-label="Điều hướng bản mẫu">${navigation()}</nav><div class="single-bottom">Ca sáng · Minh họa<br><small>Danh sách và chi tiết cùng màn hình</small></div></aside><div class="workspace">${context()}<main class="main">${content}${note}</main></div></div>`;
  } else {
    app.innerHTML = `<div class="two-tier"><nav class="rail" aria-label="Nhóm nghiệp vụ"><span class="mark">H</span><button data-view="overview" title="Tổng quan">${icon('home')}Tổng quan</button><button class="on" data-view="list" title="Người dùng">${icon('people')}Người dùng</button><button data-view="overview" title="Chi nhánh">${icon('rooms')}Chi nhánh</button><button disabled title="Ngoài phạm vi bản xem trước">${icon('box')}Nguồn lực</button><button disabled title="Ngoài phạm vi bản xem trước">${icon('chart')}Báo cáo</button><button disabled title="Ngoài phạm vi bản xem trước">${icon('settings')}Cài đặt</button></nav><aside class="subnav">${brand}<h2>Quản lý truy cập</h2>${navigation()}<hr><p class="filter-label">Trong bản xem trước</p><p>Danh sách người dùng<br>Hồ sơ tài khoản<br>Tạo / sửa bản mẫu</p><p>Mỗi chức năng vẫn giữ trang riêng và đường quay về rõ ràng.</p></aside><div class="workspace">${context()}<main class="main">${content}${note}</main></div></div>`;
  }
  if (state.view === 'list') { document.querySelector('#search').value = state.query; document.querySelector('#status').value = state.status; renderRows(); }
  document.querySelectorAll('button[data-palette]').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.palette === state.palette)));
  document.querySelectorAll('[data-variant]').forEach(button => { if (button.tagName === 'BUTTON') button.setAttribute('aria-pressed', String(button.dataset.variant === state.variant)); });
}
document.addEventListener('click', event => {
  const button = event.target.closest('button'); if (!button || button.disabled) return;
  if (button.dataset.variant) { state.variant = button.dataset.variant; state.view = 'list'; state.menu = false; render(); }
  else if (button.dataset.palette) { state.palette=button.dataset.palette; render(); }
  else if (button.id === 'device') { state.phone = !state.phone; button.setAttribute('aria-pressed', String(state.phone)); button.textContent = state.phone ? 'Xem máy tính' : 'Xem điện thoại'; render(); }
  else if (button.dataset.view) { state.view = button.dataset.view; state.menu = false; render(); }
  else if (button.dataset.detail) { state.selected = button.dataset.detail; state.view = 'detail'; render(); }
  else if (button.dataset.select) { state.selected = button.dataset.select; render(); }
  else if (button.hasAttribute('data-reset')) { state.query = ''; state.status = ''; state.branch='';state.department='';state.role='';render(); }
  else if(button.hasAttribute('data-create')){state.editing=null;state.view='form';state.menu=false;render();}
  else if(button.dataset.edit){state.editing=button.dataset.edit;state.view='form';render();}
  else if(button.hasAttribute('data-more')){state.more=!state.more;render();}
  else if(button.dataset.branch){state.branch=button.dataset.branch;state.more=true;state.view='list';render();}
  else if(button.dataset.accountAction){document.querySelector('#demo-result').textContent=button.dataset.accountAction+': đây là vị trí luồng nghiệp vụ. Bản xem trước không thực thi thao tác hay thay đổi quyền.';}
  else if (button.hasAttribute('data-menu')) { state.menu = !state.menu; render(); }
  else if (button.hasAttribute('data-demo-action')) document.querySelector('#demo-result').textContent = 'Đây là vị trí thao tác nghiệp vụ. Bản xem trước không gửi yêu cầu API hay thay đổi trạng thái thực.';
});
document.addEventListener('input', event => { if (event.target.id === 'split') { state.split=Number(event.target.value); app.style.setProperty('--split-width',state.split+'px'); return; } if (event.target.id === 'search') { state.query = event.target.value; renderRows(); } });
document.addEventListener('change', event => { if (['status','branch','department','role'].includes(event.target.id)) { state[event.target.id] = event.target.value; renderRows(); } });
document.addEventListener('submit',event=>{if(event.target.id!=='user-form')return;event.preventDefault();const values=Object.fromEntries(new FormData(event.target));for(const key of ['fullName','username','employeeCode','email'])values[key]=String(values[key]).trim();if(['fullName','username','employeeCode','email'].some(key=>!values[key])){document.querySelector('#form-error').textContent='Vui lòng nhập đủ các trường bắt buộc.';return;}if(users.some(u=>u.id!==state.editing&&(u.username.toLowerCase()===values.username.toLowerCase()||u.employeeCode.toLowerCase()===values.employeeCode.toLowerCase()))){document.querySelector('#form-error').textContent='Tên đăng nhập hoặc mã nhân viên đã có trong dữ liệu mẫu.';return;}if(state.editing){const user=users.find(u=>u.id===state.editing);Object.assign(user,values);state.selected=user.id;}else{const user={...values,id:String(Math.max(...users.map(u=>Number(u.id)))+1),role:'Chưa gán vai trò',status:'ACTIVE'};users.push(user);state.selected=user.id;}state.view='detail';render();});
window.addEventListener('resize',()=>{if(state.view!=='form')render();});
render();
