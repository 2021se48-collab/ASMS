# ASMS shared UI design charter

## Purpose

ASMS is an offline-first school management system used across schools with
different languages, scripts, devices, and levels of connectivity. Its shared
UI should feel calm, dependable, and clear. The design favors familiar
enterprise patterns, readable density, visible hierarchy, and useful feedback
over decoration.

## Principles

- Keep work-focused pages uncluttered; do not use decorative gradients or
  invented dashboard data.
- Use consistent tokens and shared components instead of one-off controls.
- Make interactive states, errors, loading, and empty states clear.
- Use labels, keyboard focus, semantic roles, and live announcements for
  accessibility.
- Localize every user-facing label and keep content direction aligned with the
  active language.
- Keep assets local so the UI remains usable without external services.

## Color and contrast

The light palette uses primary blue `#1D4ED8`, navy `#14213D`, background
`#F5F7FB`, white surface, text `#172033`, muted text `#64748B`, and border
`#E5EAF2`. A slightly darker muted-text token is used on tinted surfaces to
preserve AA contrast. Success, warning, danger, and information colors have
separate surface and text tokens.

Dark mode uses deep navy surfaces and light text. Semantic text tokens are
chosen separately from their bright decorative colors so badges and messages
remain readable. The UI tests check representative text/background pairs
against the WCAG AA 4.5:1 requirement.

## Tokens

`src/Asms.UI.Shared/wwwroot/css/asms.css` defines light, dark, and
system-preference values. Spacing increments are 4, 8, 12, 16, 24, 32, and
48px. Radii are 6, 10, and 14px, with a pill radius for compact badges.
Shadows have extra-small, small, and medium levels. Typography tokens define
sizes and weights; focus, icon, z-index, and motion values are also centralized.
Motion uses a 150ms ease-out transition and is reduced when the operating
system requests reduced motion.

## Typography and icons

Inter covers Latin glyphs in every locale. Noto Naskh Arabic covers Arabic
script glyphs used by Pashto and Dari. Both are bundled locally; CSS unicode
ranges choose the appropriate face even on RTL pages. The font stack ends with
sans-serif and never requests a serif face. See [FONTS.md](FONTS.md) for source
and licence details.

`Components/Icon.razor` renders the internal inline SVG set. Icons inherit text
color and use shared size and stroke tokens. Decorative icons are hidden from
assistive technology; labeled icons expose an accessible name. Only icons
explicitly marked directional mirror in RTL. Their shapes are from Lucide; the
upstream licence notice is included in [LUCIDE-LICENSE.txt](LUCIDE-LICENSE.txt).

## Direction, dates, and numbers

Layouts use logical CSS properties so inline-start and inline-end follow the
active direction. Components should receive the correct `dir` from the
localized application shell. Mixed-direction values can use `<bdi>` or the
`asms-bidi-isolate` class. Numeric and date strings are formatted by the
application using the user's digit-style and calendar preferences; the shared
styles isolate these values without rewriting their digits.

## Shared components

- `AsmsButton` provides primary, secondary, quiet, and danger actions, with
  size and busy-state support.
- `AsmsCard` provides a reusable content surface.
- `PageHeader` provides a title, optional description, breadcrumbs, and action.
- `StatusBadge` presents semantic status with text and a matching surface.
- `EmptyState` and `ErrorState` provide distinct no-content and retry states.
- `Skeleton` provides text, card, and table loading placeholders.
- `ToastHost` and `IToastService` provide localized live announcements with
  auto-dismiss and a dismiss action.
- `ConfirmDialog` provides a keyboard-operable modal with Escape handling and
  focus management.
- `DataTable<T>` provides sorting, paging, a search slot, row actions, compact
  density, and loading, empty, and error states. It scrolls horizontally on
  narrow screens rather than hiding table content.

These are UI primitives, not new business screens. Pages provide their own
localized content and real data.
