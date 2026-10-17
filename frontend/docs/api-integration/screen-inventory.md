# Kiểm kê toàn bộ 80 màn hình

Mọi màn hình đã được gắn vào facade/gate API hoặc hướng dẫn xác thực đúng vai trò của màn hình. Cột kiểm tra là HTTP fixtures, không phải back-end thật. Không có màn hình bị bỏ lại trên nguồn dữ liệu prototype.

|ID|Màn hình|Route|UC|Đọc/điều phối API|Kết quả kiểm tra|
|---|---|---|---|---|---|
|1|Sign In|/login|UC-001|POST /api/auth/login|Browser HTTP fixtures; not real API|
|2|Forgot Password|/forgot-password|UC-001|Không gọi API; hướng dẫn liên hệ Administrator theo MSG14|Browser HTTP fixtures; not real API|
|3|Change Password|/change-password|UC-003|POST /api/auth/password|Browser HTTP fixtures; not real API|
|4|Profile|/profile|UC-002, UC-003|GET /api/auth/session|Browser HTTP fixtures; not real API|
|5|Dashboard|/dashboard|UC-131, UC-132, UC-133, UC-134|GET /api/dashboards/operations|Browser HTTP fixtures; not real API|
|6|Notification Center|/notifications|UC-124, UC-125, UC-126, UC-127, UC-128, UC-129|GET /api/notifications + GET /api/ui/screens/6/context|Browser HTTP fixtures; not real API|
|7|Notification Detail|/notifications/:id|UC-130|GET /api/notifications/{id} + GET /api/ui/screens/7/context|Browser HTTP fixtures; not real API|
|8|User List|/users|UC-004, UC-006, UC-141|GET /api/users + GET /api/ui/screens/8/context|Browser HTTP fixtures; not real API|
|9|User Detail|/users/:id|UC-005, UC-006, UC-007, UC-010, UC-011|GET /api/users/{id} + GET /api/ui/screens/9/context|Browser HTTP fixtures; not real API|
|10|Role List|/roles|UC-008|GET /api/roles + GET /api/ui/screens/10/context|Browser HTTP fixtures; not real API|
|11|Role Detail|/roles/:id|UC-008, UC-009|GET /api/roles/{id} + GET /api/ui/screens/11/context|Browser HTTP fixtures; not real API|
|12|Permission Matrix|/permissions|UC-009|GET context: roles + GET /api/ui/screens/12/context|Browser HTTP fixtures; not real API|
|13|Branch List|/branches|UC-012|GET /api/branches + GET /api/ui/screens/13/context|Browser HTTP fixtures; not real API|
|14|Branch Detail|/branches/:id|UC-012, UC-013|GET /api/branches/{id} + GET /api/ui/screens/14/context|Browser HTTP fixtures; not real API|
|15|Floor/Area List|/locations|UC-014|GET /api/locations + GET /api/ui/screens/15/context|Browser HTTP fixtures; not real API|
|16|Floor/Area Detail|/locations/:id|UC-015, UC-016|GET /api/locations/{id} + GET /api/ui/screens/16/context|Browser HTTP fixtures; not real API|
|17|Room Type List|/room-types|UC-017, UC-142|GET /api/room-types + GET /api/ui/screens/17/context|Browser HTTP fixtures; not real API|
|18|Room Type Detail|/room-types/:id|UC-018, UC-019|GET /api/room-types/{id} + GET /api/ui/screens/18/context|Browser HTTP fixtures; not real API|
|19|Room List|/rooms|UC-020, UC-023|GET /api/rooms + GET /api/ui/screens/19/context|Browser HTTP fixtures; not real API|
|20|Room Detail|/rooms/:id|UC-021, UC-022, UC-023|GET /api/rooms/{id} + GET /api/ui/screens/20/context|Browser HTTP fixtures; not real API|
|21|Department List|/departments|UC-024|GET /api/departments + GET /api/ui/screens/21/context|Browser HTTP fixtures; not real API|
|22|Department Detail|/departments/:id|UC-025, UC-026|GET /api/departments/{id} + GET /api/ui/screens/22/context|Browser HTTP fixtures; not real API|
|23|Room Readiness Board|/room-readiness|UC-027|GET context: rooms + GET /api/ui/screens/23/context|Browser HTTP fixtures; not real API|
|24|Cleaning Task List|/cleaning/tasks|UC-031, UC-037|GET /api/cleaning-tasks + GET /api/ui/screens/24/context|Browser HTTP fixtures; not real API|
|25|Create Cleaning Task|/cleaning/tasks/new|UC-028|GET /api/cleaning-tasks + GET /api/ui/screens/25/context|Browser HTTP fixtures; not real API|
|26|Cleaning Task Detail|/cleaning/tasks/:id|UC-031, UC-032, UC-033, UC-034, UC-037|GET /api/cleaning-tasks/{id} + GET /api/ui/screens/26/context|Browser HTTP fixtures; not real API|
|27|Cleaning Assignment|/cleaning/assignment|UC-029, UC-030|GET /api/cleaning-tasks + GET /api/ui/screens/27/context|Browser HTTP fixtures; not real API|
|28|Rework Task List|/rework|UC-035|GET /api/cleaning-reworks + GET /api/ui/screens/28/context|Browser HTTP fixtures; not real API|
|29|Rework Task Detail|/rework/:id|UC-035, UC-036|GET /api/cleaning-reworks/{id} + GET /api/ui/screens/29/context|Browser HTTP fixtures; not real API|
|30|Room Turnover History|/rooms/turnover|UC-038|GET context: cleaning + GET /api/ui/screens/30/context|Browser HTTP fixtures; not real API|
|31|Inspection Checklist List|/checklists|UC-039|GET /api/inspection-checklists + GET /api/ui/screens/31/context|Browser HTTP fixtures; not real API|
|32|Inspection Checklist Detail|/checklists/:id|UC-039, UC-040|GET /api/inspection-checklists/{id} + GET /api/ui/screens/32/context|Browser HTTP fixtures; not real API|
|33|Inspection Queue|/inspections/queue|UC-041|GET /api/inspections + GET /api/ui/screens/33/context|Browser HTTP fixtures; not real API|
|34|Room Inspection|/inspections/:id/perform|UC-042, UC-043|GET /api/inspections/{id} + GET /api/ui/screens/34/context|Browser HTTP fixtures; not real API|
|35|Inspection Result|/inspections/:id/result|UC-044, UC-045|GET /api/inspections/{id} + GET /api/ui/screens/35/context|Browser HTTP fixtures; not real API|
|36|Request Rework|/inspections/:id/rework|UC-046|GET /api/inspections/{id} + GET /api/ui/screens/36/context|Browser HTTP fixtures; not real API|
|37|Reinspection|/inspections/:id/reinspect|UC-047|GET /api/inspections/{id} + GET /api/ui/screens/37/context|Browser HTTP fixtures; not real API|
|38|Inspection History|/inspections/history|UC-048|GET context: inspections + GET /api/ui/screens/38/context|Browser HTTP fixtures; not real API|
|39|Incident List|/incidents|UC-049, UC-143|GET /api/incidents + GET /api/ui/screens/39/context|Browser HTTP fixtures; not real API|
|40|Report Incident|/incidents/new|UC-049, UC-051|GET /api/incidents + GET /api/ui/screens/40/context|Browser HTTP fixtures; not real API|
|41|Incident Detail|/incidents/:id|UC-050, UC-051, UC-052, UC-056, UC-057, UC-059, UC-060|GET /api/incidents/{id} + GET /api/ui/screens/41/context|Browser HTTP fixtures; not real API|
|42|Maintenance Assignment|/maintenance/assignment|UC-053|GET /api/incidents + GET /api/ui/screens/42/context|Browser HTTP fixtures; not real API|
|43|Maintenance Task List|/maintenance/tasks|UC-054, UC-062, UC-144|GET /api/maintenance-work + GET /api/ui/screens/43/context|Browser HTTP fixtures; not real API|
|44|Maintenance Task Detail|/maintenance/tasks/:id|UC-054, UC-055, UC-058, UC-062|GET /api/maintenance-work/{id} + GET /api/ui/screens/44/context|Browser HTTP fixtures; not real API|
|45|Preventive Maintenance Schedule List|/maintenance/schedules|UC-061|GET /api/maintenance-schedules + GET /api/ui/screens/45/context|Browser HTTP fixtures; not real API|
|46|Preventive Maintenance Schedule Detail|/maintenance/schedules/:id|UC-061, UC-062|GET /api/maintenance-schedules/{id} + GET /api/ui/screens/46/context|Browser HTTP fixtures; not real API|
|47|Reference Data|/reference-data|UC-063, UC-064|GET /api/reference-values + GET /api/ui/screens/47/context|Browser HTTP fixtures; not real API|
|48|Asset List|/assets|UC-065, UC-071, UC-073|GET /api/assets + GET /api/ui/screens/48/context|Browser HTTP fixtures; not real API|
|49|Asset Detail|/assets/:id|UC-066, UC-067, UC-068, UC-069, UC-070, UC-071, UC-074|GET /api/assets/{id} + GET /api/ui/screens/49/context|Browser HTTP fixtures; not real API|
|50|Asset Maintenance History|/assets/:id/history|UC-072|GET /api/assets/{id} + GET /api/ui/screens/50/context|Browser HTTP fixtures; not real API|
|51|Warehouse List|/inventory/warehouses|UC-075|GET /api/warehouses + GET /api/ui/screens/51/context|Browser HTTP fixtures; not real API|
|52|Warehouse Detail|/inventory/warehouses/:id|UC-075|GET /api/warehouses/{id} + GET /api/ui/screens/52/context|Browser HTTP fixtures; not real API|
|53|Inventory Dashboard|/inventory/dashboard|UC-089, UC-128|GET context: items + GET /api/ui/screens/53/context|Browser HTTP fixtures; not real API|
|54|Item List|/inventory/items|UC-076|GET /api/inventory-items + GET /api/ui/screens/54/context|Browser HTTP fixtures; not real API|
|55|Item Detail|/inventory/items/:id|UC-076, UC-077, UC-078|GET /api/inventory-items/{id} + GET /api/ui/screens/55/context|Browser HTTP fixtures; not real API|
|56|Unit of Measurement|/inventory/units|UC-077|GET /api/units + GET /api/ui/screens/56/context|Browser HTTP fixtures; not real API|
|57|Stock Transaction List|/inventory/transactions|UC-079, UC-081, UC-082, UC-083|GET /api/stock-documents + GET /api/ui/screens/57/context|Browser HTTP fixtures; not real API|
|58|Stock Transaction Detail|/inventory/transactions/:id|UC-080, UC-082, UC-084, UC-088|GET /api/stock-documents/{id} + GET /api/ui/screens/58/context|Browser HTTP fixtures; not real API|
|59|Stocktake List|/inventory/stocktakes|UC-085|GET /api/stocktakes + GET /api/ui/screens/59/context|Browser HTTP fixtures; not real API|
|60|Stocktake Detail|/inventory/stocktakes/:id|UC-085, UC-086, UC-087|GET /api/stocktakes/{id} + GET /api/ui/screens/60/context|Browser HTTP fixtures; not real API|
|61|Stock Balance & Ledger|/inventory/ledger|UC-089|GET context: transactions + GET /api/ui/screens/61/context|Browser HTTP fixtures; not real API|
|62|Material Request List|/material-requests|UC-091, UC-099|GET /api/material-requests + GET /api/ui/screens/62/context|Browser HTTP fixtures; not real API|
|63|Material Request Detail|/material-requests/:id|UC-090, UC-092, UC-093, UC-094, UC-095, UC-096, UC-097, UC-098, UC-099|GET /api/material-requests/{id} + GET /api/ui/screens/63/context|Browser HTTP fixtures; not real API|
|64|Replenishment List|/replenishments|UC-100, UC-101|GET /api/replenishments + GET /api/ui/screens/64/context|Browser HTTP fixtures; not real API|
|65|Replenishment Detail|/replenishments/:id|UC-101|GET /api/replenishments/{id} + GET /api/ui/screens/65/context|Browser HTTP fixtures; not real API|
|66|Task List|/tasks|UC-102, UC-107|GET /api/tasks + GET /api/ui/screens/66/context|Browser HTTP fixtures; not real API|
|67|Task Detail|/tasks/:id|UC-103, UC-105, UC-106, UC-107, UC-108, UC-109, UC-110, UC-111, UC-112|GET /api/tasks/{id} + GET /api/ui/screens/67/context|Browser HTTP fixtures; not real API|
|68|Task Assignment|/tasks/assignment|UC-104|GET /api/tasks + GET /api/ui/screens/68/context|Browser HTTP fixtures; not real API|
|69|Workload & Time Logs|/workload|UC-113|GET context: tasks + GET /api/ui/screens/69/context|Browser HTTP fixtures; not real API|
|70|Overdue Work|/overdue|UC-127|GET context: tasks + GET /api/ui/screens/70/context|Browser HTTP fixtures; not real API|
|71|Shift Management|/shifts|UC-138|GET /api/shifts + GET /api/ui/screens/71/context|Browser HTTP fixtures; not real API|
|72|Shift Detail|/shifts/:id|UC-138|GET /api/shifts/{id} + GET /api/ui/screens/72/context|Browser HTTP fixtures; not real API|
|73|Workday Record|/workdays|UC-139|GET context: shifts + GET /api/ui/screens/73/context|Browser HTTP fixtures; not real API|
|74|Shift Handover List|/handovers|UC-140|GET /api/shift-handovers + GET /api/ui/screens/74/context|Browser HTTP fixtures; not real API|
|75|Shift Handover Detail|/handovers/:id|UC-140|GET /api/shift-handovers/{id} + GET /api/ui/screens/75/context|Browser HTTP fixtures; not real API|
|76|Internal Request List|/internal-requests|UC-114, UC-123|GET /api/internal-requests + GET /api/ui/screens/76/context|Browser HTTP fixtures; not real API|
|77|Internal Request Detail|/internal-requests/:id|UC-115, UC-116, UC-117, UC-118, UC-119, UC-120, UC-121, UC-122, UC-123|GET /api/internal-requests/{id} + GET /api/ui/screens/77/context|Browser HTTP fixtures; not real API|
|78|Operational Reports|/reports|UC-135|GET /api/reports/{type} + GET /api/ui/screens/78/context|Browser HTTP fixtures; not real API|
|79|Audit Log|/audit-logs|UC-136|GET /api/audit-logs + GET /api/ui/screens/79/context|Browser HTTP fixtures; not real API|
|80|Audit Log Detail|/audit-logs/:id|UC-137|GET /api/audit-logs/{id} + GET /api/ui/screens/80/context|Browser HTTP fixtures; not real API|
