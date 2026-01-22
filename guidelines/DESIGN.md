# Design & UI Guidelines (UI Overhaul 2026)

CapyCard follows a specific "Shopping List" aesthetic characterized by floating elements, rounded corners, and a Teal/Purple color scheme.

## Visual Style

- **Aesthetic:** Clean, Floating, Rounded ("Material Design 3" inspired).
- **Colors:**
  - **Primary (Accent):** Teal (`#018786` Light / `#03DAC5` Dark). Used for FABs, primary buttons, focus borders.
  - **Secondary:** Deep Purple (`#6200EE` Light / `#BB86FC` Dark).
  - **Labels:** Use `TextMutedBrush` (Grey) for secondary labels (e.g., "Vorderseite"). Avoid using primary colors for static labels.
- **Shapes:**
  - **Buttons:** Pill-Shape (`CornerRadius="25"`).
  - **Cards/Dialogs:** Large rounded corners (`CornerRadius="28"`).
  - **Inputs:** Floating style (`CornerRadius="12"`), transparent background.
- **Icons:** Use `Material.Icons.Avalonia` exclusively. Avoid inline SVG paths.

## Hover Effects

- **Consistency:** Hover colors must be variations of the base color (e.g., a Teal button should become a slightly darker or lighter Teal on hover).
- **Stability:** Avoid drastic changes during hover.
  - Do NOT switch text color between Light/Dark if not necessary.
  - Do NOT switch button types (e.g., from Outlined to Filled) on hover.
  - Do NOT change color families (e.g., from Red to Teal).
- **Implementation:** Usually handled via `Opacity` or subtle brightness shifts in the `ContentPresenter` of the button template.

## Button Styles (Classes)

Use predefined button classes from `Styles/AppStyles.axaml`:

| Class         | Use Case                     | Appearance                 |
| ------------- | ---------------------------- | -------------------------- |
| `primary`     | Main actions (Save, Confirm) | Teal filled, white text    |
| `secondary`   | Secondary actions (Cancel)   | Outlined, teal border/text |
| `destructive` | Delete/Remove actions        | Red filled, white text     |
| `icon`        | Icon-only buttons            | Transparent, circular      |
| `list-item`   | Clickable list items         | Transparent, full-width    |

**Important:** Never use `Classes="primary" Background="{DynamicResource ErrorBrush}"` - this breaks hover states. Use `Classes="destructive"` instead.

## Interaction & UX

- **Navigation:** List items should generally be fully clickable area buttons (`Classes="list-item"`) rather than relying on small icons.
- **Focus:** Input fields should not darken the background on focus. Use a subtle `PrimaryBrush` border instead.
- **Keyboard:** Always handle `Escape` to release focus from floating inputs.
