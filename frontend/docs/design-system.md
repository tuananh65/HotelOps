# HotelOps operations design system

The current design is content first: navy navigation, white surfaces, cool gray workspace, dark text and blue interaction accents. It serves daily hotel operations rather than reproducing the public hotel marketing website. Showcase: /dev/design-system.

## Central tokens

Canonical colors: src/styles/tokens.css. Operational component styling: src/styles/operations.css, loaded after the existing compatibility stylesheet.

| Collection | CSS family | Purpose |
| --- | --- | --- |
| Primary | --navy-500 through --navy-950; --color-primary-* | Sidebar and selected navigation |
| Accent | --blue-50 through --blue-800; --color-accent-* | Primary actions, links, focus and informational state |
| Neutral | --gray-50 through --gray-950; --color-neutral-*; --white | Workspace, surfaces, borders and text |
| Success | --green-*; --color-success | Ready, active, completed and available |
| Warning | --amber-*; --color-warning | Pending and below-minimum stock |
| Danger | --red-*; --color-danger | Failed, blocked, critical and overdue |
| Information | --color-info | Work in progress |

Legacy gold/brown/warm and --dv-* variables are compatibility aliases to the operations palette; they no longer define the primary UI colors. The optional --brand-accent primitive is separate from interaction colors. Semantic badges combine text, an icon and a subtle surface tint.

## Dimensions and typography

| Element | Default desktop |
| --- | --- |
| Topbar | 60px, sticky |
| Page header | approximately 70–90px with one short description where useful |
| KPI strip | approximately 76–90px; collapsible into a compact row |
| Filter toolbar | 55px measured at both target desktop widths |
| Table header | 44px, sticky inside the records scroller |
| Data row | 48px, expanding only for content that needs it |
| Page title | 24px, system sans-serif, semibold |
| Body / table | 14px |
| Supporting controls / captions | 12–13px |
| Desktop controls | 36px; mobile record actions 40px |

The main surface uses F8FAFC, white panels, 0F172A primary text, 475569 supporting text and E2E8F0 borders. Primary buttons use blue 700 with white text. Active sidebar items use navy 700 with a blue indicator. Focus, hover, selection and disabled states remain distinct.

## Components and layouts

- Shell: compact sidebar groups and a 60px topbar containing breadcrumbs, EN/VI, screen directory, branch selection, notifications and profile.
- PageHeader: screen name, optional concise operational description, small FE metadata and primary actions.
- Summary: neutral KPI strip, subtle semantic indicator and collapse/expand control. Role, room-type, unit and reference configuration lists omit unnecessary KPI strips.
- DataTable: one-row desktop toolbar with search and applicable status/role/department/priority filters. Date range and domain-specific filters open anchored panels. Reset remains compact. Sorting, pagination and explicit open actions remain available.
- DetailPage: compact entity/status header, key facts and contextual next actions. Inspection screens show their checklist before secondary record information.
- Form/FieldControl: compact fields, retained synthetic examples, inline validation and sticky form actions.
- StatusChart: compact bars and controlled semantic colors.
- ScreenIndex: a compact table of all 80 screens with bilingual names, FE, UC, type, route, open and copy-link actions.

Dashboard prioritizes ready rooms, pending cleaning, incidents, overdue work and low stock. Room readiness and workload charts share a row, followed by incident and stock information. Notifications are a small indicator. The ledger places posted movements before stock balance details. Shift roster details expand on demand.

## Responsive behavior

Desktop targets: 1440×900 and 1536×864, in both EN and VI. Tablet toolbars can wrap. Mobile advanced filters collapse; shared record tables become labeled cards and keep sorting inside the filter panel. Directory and permission matrices retain internal horizontal scrolling. Navigation uses the existing drawer.

Existing 80 screen-level files, FE/UC mapping, routes, mock workflow simulation, browser storage, public demo access and deployment configuration are preserved. See docs/ux-improvements.md and docs/verification/content-first/ for measurements and screenshots.


## Enterprise content roles (current)

Content interaction tokens are independent of sidebar navigation:
- --interaction-primary #3b5675 (buttons), --interaction-link #34516f (records), --interaction-hover #29435f, --interaction-focus #526e8d.
- --interaction-surface #edf2f7, --interaction-selected #dce6ef, --interaction-border #8298b0.
- --status-{assigned,progress,success,warning,danger,disabled}-{surface,text}: distinct muted backgrounds and readable foregrounds.
- --kpi-{info,success,warning,danger,neutral}-surface: subtle summary grouping.
- --chart-primary: muted blue-gray. Charts retain semantic success/warning/danger tones.

Sidebar keeps its original navy navigation. Legacy content aliases resolve to the new roles inside content scopes. Record names use weight 600 without underline; redundant Open record columns are removed. Dates fit a compact single row on desktop.


## Bright refinement (latest)

Hue and HSL saturation are retained; lightness rises for interaction and semantic tokens. Primary #3f5c7d, record #385778, workspace #fbfcfd, header #f6f8fb, row hover #f1f6fa, heading #28364d. Semantic status/KPI surfaces are lighter while text/icon foregrounds stay dark enough to read. Measured status text/background contrast: 5.20–6.48:1; primary on white 6.91:1; records on white 7.49:1. Sidebar variables remain unchanged.

Clickable rows have subtle background hover/focus-within. Record names keep weight 600 and no default underline; underline appears on row/name hover. Clicking ordinary cells calls the existing onOpen handler. Links, buttons, inputs, selects, textareas, labels, summary, interactive roles and opt-out elements do not trigger row opening. Keyboard users retain the primary button and visible focus; text selection does not navigate. No layout/spacing changes.


## Unified navy color bridge (attachment db99f45b)

Sidebar and light content now share --bridge-navy-{950,900,800,700,600,500,300,200,100,50}. Sidebar #182c49 is slightly lighter and bluer; selected navigation #294568, headings #203655, primary actions/record links #355779, focus #4b6a8a, borders #ccd9e8, hover #e1e9f3, workspace #eef3f9. Header uses --bridge-header-surface #eaf0f7 rather than pure white; cards/table use --bridge-card-surface #fdfefe. Existing interaction/surface tokens alias this shared scale in light mode. Semantic status colors and colored statistic numbers/icons are unchanged. Dark mode, layout, component shapes, spacing, data and business logic are preserved.

Modified src/styles/tokens.css and src/styles/theme.css. Typecheck, lint, build and four affected browser scenarios passed. Visual evidence: verification/themes/navy-bridge-dashboard.png.


## Vibrant accent refinement (attachment ebde493f)

The latest request replaces the previous muted interaction direction. Neutral workspace/header/cards/tables stay unchanged; shapes, spacing, navigation, date inputs and workflows are preserved.

Central accents in tokens.css have three levels: --accent-{blue,green,amber,orange,red,cyan,purple}-{100,600,700}. 100 = tint, 600 = icon/large number/chart/action, 700 = readable badge text/hover/emphasis. Light sidebar uses --rich-navy-sidebar #132d57, active menu #234d86. Primary Blue #2563eb powers actions, records, View all, links, focus and selected tabs. Emerald #059669, amber #d97706, orange #ea580c, red #e11d48 and cyan #0891b2 appear only in meaningful data/feedback. Badge foregrounds use darker level 700 on light tints. Statistic card frames remain neutral.

StatusChart now distinguishes pending cleaning/paused (orange), inspection/verification (amber), ready/accepted (emerald), assigned (blue), progress (cyan), rework (red) and blocked (strong red). Tracks remain neutral. Daily work hover/focus/selected affordances use light blue tint and clear blue borders/text; these remain route links rather than fake persistent tabs.

Shared changes: src/styles/tokens.css, src/styles/theme.css, src/components/StatusChart.tsx, src/routes/DesignSystem.tsx. Existing Summary, StatusBadge, DataTable, controls and links consume updated roles throughout HotelOps. Dark charts have appropriate bright foreground variants. Showcase now documents 63 swatches. Visual evidence: verification/themes/vibrant-dashboard.png.
