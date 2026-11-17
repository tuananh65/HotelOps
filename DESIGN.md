---
name: HotelOps Cacao
description: Internal hotel operations with two-level navigation
colors:
  primary: "#785039"
  primary-hover: "#543525"
  rail: "#392b26"
  canvas: "#faf6ee"
  surface: "#ffffff"
  selected: "#f4e9d7"
  ink: "#42342c"
  muted: "#75604e"
  border: "#e5d8c4"
typography:
  headline:
    fontFamily: 'system-ui, -apple-system, "Segoe UI", sans-serif'
    fontSize: "28px"
    fontWeight: 650
    letterSpacing: "-0.025em"
  body:
    fontFamily: 'system-ui, -apple-system, "Segoe UI", sans-serif'
    fontSize: "14px"
    lineHeight: 1.5
  label:
    fontFamily: 'system-ui, -apple-system, "Segoe UI", sans-serif'
    fontSize: "12px"
rounded:
  control: "6px"
  panel: "10px"
spacing:
  small: "8px"
  medium: "16px"
  large: "24px"
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.surface}"
    rounded: "{rounded.control}"
    padding: "9px 14px"
---
# Design System: HotelOps Cacao

## Overview

Direction approved by the user: C + 8, two-level navigation and cacao–cream. This is an operational web application used on desktop and mobile. Forms, account directories and work queues lead the page; navigation stays available without competing with the task.

## Colors

The cocoa rail anchors the app; cream is the workspace, white is the working surface, and pale cocoa marks selection. Source: frontend/src/styles/cacao.css, imported last by main.tsx. Existing navy/blue aliases are compatibility names; their interaction values now resolve to cocoa. Success, warning, danger and disabled status colours retain their semantic meaning.

## Typography

System sans throughout. Page titles use 28px/650 on desktop and 24px on mobile. Body text is 14px, secondary labels 12px and compact rail labels 10px. Retain meaningful labels and associated inputs; do not use colour as the sole indicator of status.

## Layout

Desktop: 74px icon rail plus 218px secondary navigation, then a flexible workspace. Below 1050px the navigation region narrows to 250px. At 760px and below the two levels become a dismissible drawer. Topbar is at least 64px. Content padding is 32px 28px on desktop and 24px 16px on mobile. User quick view uses a 280px inspector, moving below the list at narrower widths. Wide data stays scrollable within its own region.

## Elevation & Depth

New surfaces use borders and tonal separation. The drawer backdrop identifies the navigation layer. Legacy module shadows remain where already present and are not the template for new components.

## Shapes

Controls use 6px radii, working panels 10px. Inputs have visible warm borders. Tables remain rectangular and use horizontal row separators.

## Components

The shell reads authorized navigation from the existing API. Group buttons select a functional menu; NavLink marks the active route. Unavailable navigation hides feature links and ends its loading state. Authentication forms retain their lifecycle across session changes.

Directory rows group identity, branch/department, roles, status and quick view without removing source fields. Quick view reads only the current directory response; full profile remains a separate route.

## Do's and Don'ts

- Do load cacao.css after existing module styles; preserve semantic status colours.
- Do reuse the shared shell and form/table rules instead of introducing another sidebar.
- Do retain backend-driven authorization and every existing route.
- Don't infer permissions or fabricate business records to make a screen look complete.
- Don't treat mocked browser checks as real API/database verification.

Legacy eyebrow labels, mixed Vietnamese/English copy and remaining module-specific dimensions are not new design conventions.
