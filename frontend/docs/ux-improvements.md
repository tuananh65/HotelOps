# HotelOps — content-first operations refactor

Ngày: 2026-10-06. Thực hiện yêu cầu trong attachment 651cea4c-e53f-403d-9983-4caaf00427f1/Pasted text.txt. Bản này thay thế hướng giao diện gold/brown/cream ở lượt chỉnh UX trước.

## Kết quả thiết kế

- Navy sidebar, white/cool-gray surfaces, charcoal text và blue interaction accents. Palette và semantic colors tập trung trong tokens.css; legacy tokens chỉ còn là alias tương thích.
- Topbar 60px, breadcrumb gọn không lặp tên màn hình, EN/VI gọn, vẫn đủ Screens, chi nhánh, thông báo và hồ sơ.
- Header ngắn, tiêu đề sans-serif 24px; description nghiệp vụ một dòng khi hữu ích. FE nhỏ, UC giữ trong directory và source mapping.
- KPI strip khoảng 76–90px, nền trung tính và có collapse/expand. Bỏ KPI không cần thiết ở Role List, Room Type List, Unit of Measurement và Reference Data.
- Toolbar danh sách một hàng ở desktop: tìm kiếm, các bộ lọc phù hợp, date popup, domain filter popup và Reset. Đã tích hợp domain filters của 28 màn danh sách vào toolbar.
- Table header 44px, data rows 48px; sticky header trong vùng cuộn records. Không giảm mock data, không bỏ phân trang hoặc sắp xếp.
- Dashboard bỏ marketing hero, ưu tiên KPI vận hành, room readiness, workload, incidents và low stock trong viewport đầu. Notifications không còn chiếm block lớn.
- Detail header và key facts gọn; kiểm tra phòng đưa checklist lên trước metadata. Form xuất hiện sớm; giữ dữ liệu mẫu, lỗi inline và các simulation đang hoạt động.
- Shift roster mở theo yêu cầu; Stock Balance & Ledger đặt posted movement ledger trước thông tin balances.
- /dev/screens chuyển thành bảng compact để review 80 screens. /dev/design-system cập nhật navy/blue/white/gray và semantic colors.

## Phạm vi giữ nguyên

React + TypeScript + Vite; 80 screen-level TSX files; 80 required routes; FE/UC mapping; EN/VI; mock data; search/filter/sorting/pagination; navigation; modals và mobile drawer; create/edit/workflow simulation; localStorage; frontend-only public demo và cấu hình deploy. Không thêm login hoặc permission gate.

## Kiểm tra và bằng chứng

- Đo tất cả 80 screens ở EN/VI tại 1440×900 và 1536×864: 320 presentations. Các danh sách có đủ dữ liệu hiển thị ít nhất 5 dòng ngay trong viewport đầu; danh sách ít bản ghi hiển thị toàn bộ dữ liệu sẵn có.
- Topbar 60px; toolbar danh sách 55px. Đã kiểm tra header bảng và records trên User, Room, Cleaning, Incident, Asset, Item, Task và Audit Log.
- Kiểm tra Dashboard, Room Readiness Board, Room Inspection, Inventory Dashboard và Operational Reports để nội dung chính xuất hiện ngay.
- Quét cả 80 routes ở EN/VI trên mobile 390×844: không tràn ngang ở cấp trang.
- Quét màu chữ trên nền đặc của 80 routes: không phát hiện cặp màu chữ dưới ngưỡng kiểm tra. Đây là kiểm tra có mục tiêu, không phải chứng nhận WCAG toàn diện.
- Kiểm tra tương tác: tạo/sửa nhân viên, EN/VI, bộ lọc và reset, role filter, date popup/Escape, KPI collapse, sticky table header, lỗi form và focus.
- Chạy typecheck, lint, 13 test nghiệp vụ/mock data, browser tests và production build.

Ảnh và số đo: [content-first](verification/content-first/). Kết quả desktop: [desktop-audit.json](verification/content-first/desktop-audit.json). Kết quả mobile: [mobile-audit.json](verification/content-first/mobile-audit.json).

Chưa có nghiên cứu usability với nhân viên khách sạn thực tế. Các ảnh marketing Duc Vuong trước đây không còn chi phối bố cục và màu chức năng theo yêu cầu mới.


## Enterprise navy / semantic pastel: latest refactor

Applied the complete c09b86bd attachment directly to HotelOps.

- DataTable removes the redundant Open record / arrow column. The primary field uses the original onOpen handler, including fields with renderers. Headers and data columns remain aligned and fill the available width.
- Record links use navy #34516f, weight 600, no underline, subtle hover and visible keyboard focus.
- Content buttons, selected states, borders and indicators use separate interaction roles. Sidebar remains deep navy.
- Assigned: blue-gray; In Progress: soft cyan; Accepted/Completed/Ready/Active: green; Paused/Pending/Waiting: amber; Blocked/Failed/Rejected: red; Inactive/Disabled/Cancelled: gray. Badges retain labels/icons and readable dark text on pastel backgrounds.
- Every Summary cell, including non-link cells, receives a semantic tint: total/info blue-gray, active green, inactive gray, temporary/warning amber, incidents/overdue red.
- Date popup: two 148px inputs plus reset in one desktop row; 10px padding, 8px gap; wraps only on narrow screens.
- Charts use muted semantic tokens. Design system documents interaction, status and KPI roles.

Main files: src/ui.tsx, src/styles/tokens.css, src/styles/operations.css, src/components/StatusChart.tsx and src/routes/DesignSystem.tsx. Shared components apply across all 80 screens. Mock data, routes, services and business logic remain unchanged.

New evidence: [enterprise](verification/enterprise/).


Verification results: typecheck, lint and production build passed; 13 business/mock tests passed. All 10 browser scenarios passed across the full run and affected-scenario reruns after updating the old Open-record and swatch-count expectations. The enterprise audit covered 80 routes × EN/VI × desktop/mobile = 320 presentations with no page overflow, redundant Open columns, nested record controls or underlined record names. Navigation/action audit verified 37 screens, including modal editors, seeded-ledger notification and employee drill-down (not every record action changes the URL). Exactly 80 screen-level TSX files remain.


## Bright + clean refinement (attachment 7b58264b)

- Retained navy/blue-gray/green/amber/red/gray hues and saturation, increased HSL lightness of content interactions, badge/KPI surfaces and accent borders. Sidebar remains unchanged.
- New surface roles: workspace #fbfcfd, table header #f6f8fb, row hover #f1f6fa; headings #28364d and important text #2e3e56 retain hierarchy.
- Shared DataTable row hover adds only-on-hover underline to the record name. Clicking a non-control cell invokes the original onOpen; nested controls remain independent, and selecting text does not open a record. Primary buttons remain keyboard accessible.
- Modified src/styles/tokens.css, src/styles/operations.css and src/ui.tsx. Tokens also update StatusBadge, Summary, charts, forms, filters, primary actions and the existing design-system showcase across all screens.
- Compact date range, dimensions, semantic mapping, 80 screens/routes, services, mock data and workflow logic remain unchanged. No Open-record column returns.

Status foreground/background contrast remains >=5.20:1. Evidence and screenshots are refreshed in verification/enterprise/.

Final refinement verification: typecheck, lint, build, 13 business tests and 11 browser scenarios passed. The refreshed audit covered 320 presentations across all 80 routes in EN/VI on desktop/mobile with no failures. Row hover, whole-row opening, keyboard Enter and independent checkbox/select interactions were verified. Sidebar remains rgb(17,24,39).


## Unified blue accents and light/dark mode (latest)

The latest user direction supersedes the previous requirement to keep content accents muted. Light mode now uses sidebar-related navy/blue accents (#245aaa primary, #2259a4 links), clearly visible green/amber/red/cyan surfaces, stronger KPI grouping and status borders. Layout remains compact.

Added shared ThemeToggle beside EN/VI in the shell, auth pages, directory and design-system showcase. Light is the default; the explicit selection persists in localStorage (hotelops-theme), across navigation/reload and synchronizes between tabs. Toggle labels support EN/VI. Dark mode uses dedicated workspace, panels, form controls, tables, text, links and semantic tokens rather than a blanket invert filter. Sidebar navy remains #111827 and navigation stays readable.

Main changes: src/components/ThemeToggle.tsx, src/styles/theme.css, src/main.tsx and toggle placement in src/ui.tsx, src/routes/ScreenIndex.tsx, src/routes/DesignSystem.tsx, src/layouts/AuthLayout.tsx. Business logic, datasets, routes and workflows are unchanged. Evidence: verification/themes/.

Validation: typecheck, lint, build and 13 business tests passed. All 12 browser scenarios passed across the full run and affected-test reruns (the old hover-color expectation was updated to the new token). All 80 routes were audited in light and dark on desktop: 160 presentations, no horizontal overflow. Mobile theme toggle/persistence and EN/VI labels passed.


## Purposeful summaries, inline dates and dark surfaces (latest)

- Dashboard uses four separated neutral cards (ready rooms, open incidents, overdue tasks, low stock), subtle semantic dots and contextual links. Removed the joined pastel strip and redundant arrows. Cleaning counts remain in the readiness chart and daily-work navigation.
- Shared Summary defaults to compact inline facts rather than colored boxes; counts and existing links remain available. Compact facts can still collapse.
- Room Readiness Board replaces summary boxes and the duplicate status select with state buttons/counts. Buttons filter the existing room dataset, expose aria-pressed, respect the floor selection and preserve room-detail navigation.
- DataTable moves From/To into the same desktop toolbar row as Search/status/role/department/Reset. Inputs are 122px, with small visible labels. Date semantics, invalid-range validation, result chips and Reset remain intact; mobile still uses the expandable filter area.
- Dark room tiles now use the dark panel surface, readable room titles and metadata, semantic top borders, and a clear hover. Removed the accidental white-room-tile/light-text combination.

Changed shared src/ui.tsx, src/styles/theme.css, DashboardScreen.tsx and RoomReadinessBoardScreen.tsx. No services, mock data or business workflows were changed. Evidence: verification/themes/refined-users.png, refined-dashboard.png and refined-room-readiness.png.


## Unified icon statistic cards (latest reference)

All shared summaries now use the same neutral cards on Dashboard, lists and Room Board: rounded neutral border, unchanged neutral frame background, prominent semantic numbers and colored icons inside softly tinted circles. Removed compact inline statistics and state-chip layouts. Room Board uses these same cards as status filters and keeps aria-pressed plus a clear selected outline. All counts, existing links, floor filtering, date inputs and workflows remain available. Light/dark tokens drive numbers and icons; no full-card semantic color fill is used.

Primary files: src/ui.tsx, src/styles/theme.css and RoomReadinessBoardScreen.tsx. Reference screenshots: verification/themes/icon-cards-*.png.


## Unified navy color bridge (attachment db99f45b)

Sidebar and light content now share --bridge-navy-{950,900,800,700,600,500,300,200,100,50}. Sidebar #182c49 is slightly lighter and bluer; selected navigation #294568, headings #203655, primary actions/record links #355779, focus #4b6a8a, borders #ccd9e8, hover #e1e9f3, workspace #eef3f9. Header uses --bridge-header-surface #eaf0f7 rather than pure white; cards/table use --bridge-card-surface #fdfefe. Existing interaction/surface tokens alias this shared scale in light mode. Semantic status colors and colored statistic numbers/icons are unchanged. Dark mode, layout, component shapes, spacing, data and business logic are preserved.

Modified src/styles/tokens.css and src/styles/theme.css. Typecheck, lint, build and four affected browser scenarios passed. Visual evidence: verification/themes/navy-bridge-dashboard.png.


## Vibrant accent refinement (attachment ebde493f)

The latest request replaces the previous muted interaction direction. Neutral workspace/header/cards/tables stay unchanged; shapes, spacing, navigation, date inputs and workflows are preserved.

Central accents in tokens.css have three levels: --accent-{blue,green,amber,orange,red,cyan,purple}-{100,600,700}. 100 = tint, 600 = icon/large number/chart/action, 700 = readable badge text/hover/emphasis. Light sidebar uses --rich-navy-sidebar #132d57, active menu #234d86. Primary Blue #2563eb powers actions, records, View all, links, focus and selected tabs. Emerald #059669, amber #d97706, orange #ea580c, red #e11d48 and cyan #0891b2 appear only in meaningful data/feedback. Badge foregrounds use darker level 700 on light tints. Statistic card frames remain neutral.

StatusChart now distinguishes pending cleaning/paused (orange), inspection/verification (amber), ready/accepted (emerald), assigned (blue), progress (cyan), rework (red) and blocked (strong red). Tracks remain neutral. Daily work hover/focus/selected affordances use light blue tint and clear blue borders/text; these remain route links rather than fake persistent tabs.

Shared changes: src/styles/tokens.css, src/styles/theme.css, src/components/StatusChart.tsx, src/routes/DesignSystem.tsx. Existing Summary, StatusBadge, DataTable, controls and links consume updated roles throughout HotelOps. Dark charts have appropriate bright foreground variants. Showcase now documents 63 swatches. Visual evidence: verification/themes/vibrant-dashboard.png.

Vibrant refinement verification: typecheck, lint, production build, 13 business tests and all 13 browser scenarios passed. Audited all 80 routes in light/dark (160 presentations) with no horizontal overflow. Badge foreground/tint contrast measured 4.79–6.37:1 across blue/green/amber/red/cyan. Layout and business logic are unchanged.
