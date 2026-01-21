# UI Overhaul Plan: "Shopping List" Aesthetic

## Goal
Transform the CapyCard UI to match the visual style of the reference "Shopping List" Android app.
**Key Attributes:**
- **Colors:** Deep Purple / Teal / Adaptive Grays.
- **Shapes:** Rounded corners (28dp for Cards/Dialogs, 12dp for Inputs/Buttons).
- **Icons:** Unified Material Design icons.
- **Layout:** "Floating" elements with spacing from edges.

## 1. Dependencies
- **Action:** Add `Material.Icons.Avalonia` for consistent iconography.
- **Why:** Current inline SVG paths are inconsistent and hard to manage.
- **Command:** `dotnet add package Material.Icons.Avalonia`

## 2. Global Resources (Colors & Shapes)
- **File:** `CapyCard/CapyCard/App.axaml`
- **Action:** Replace existing `Light`/`Dark` dictionaries with new palette.

### Color Palette (Mapping)
| Role | Android Name | Color (Light) | Color (Dark) |
| :--- | :--- | :--- | :--- |
| **Primary** | `purple_500` | `#FF6200EE` | `#FFBB86FC` |
| **Secondary** | `teal_200` | `#FF03DAC5` | `#FF03DAC5` |
| **Background** | `colorSurface` | `#DDDDDD` | `#333333` |
| **Card Bg** | `colorSurfaceSecondary` | `#CACACA` | `#494949` |
| **Text Main** | `text_primary` | `#333333` | `#DDDDDD` |
| **Text Muted** | `text_secondary` | `#808080` | `#A5A5A7` |
| **Error** | `colorError` | `#B71C1C` | `#E57373` |

### Shape Resources
- `CornerRadius.Small`: `12` (Inputs, internal cards)
- `CornerRadius.Large`: `28` (Dialogs, Main Floating Cards, FABs)

## 3. Control Styles
- **File:** Create `CapyCard/CapyCard/Styles/AppStyles.axaml` (and include in App.axaml).

### Buttons
- **Style:** Pill shape (`CornerRadius="25"`).
- **Variants:** 
    - `Solid`: Background `button_background_adaptive`.
    - `Outlined`: Border `input_stroke_adaptive`.
    - `Text`: No border/bg, colorful text.

### TextBoxes ("Floating")
- **Style:** 
    - Transparent Border (or subtle).
    - `CornerRadius="12"`.
    - Margin around the control to make it "float" if it has a background, or wrap it in a Card.
    - Focus state: Highlight Border with `teal_200` or `purple_500`.

### Cards (List Items)
- **Style:** `Border` with `CornerRadius="12"` (or 28 depending on context).
- **Shadow:** Use `BoxShadow` for depth (Elevation).

### Dialogs
- **Style:** Overlay Border with `CornerRadius="28"`.
- **Background:** `colorSurface` (or `colorSurfaceSecondary` based on xml).

## 4. Implementation Steps (Iterative)

1.  [ ] **Install Nuget:** `Material.Icons.Avalonia`.
2.  [ ] **Setup Colors:** Rewrite `App.axaml` resources.
3.  [ ] **Create Styles:** Implement `AppStyles.axaml`.
4.  [ ] **Refactor `DeckListView`:**
    - Replace `Path` with `material:MaterialIcon`.
    - Apply "Floating Card" style to list items.
    - Update `NewDeckTextBox` to be floating.
5.  [ ] **Refactor `CardListView`:** Apply similar changes.
6.  [ ] **Refactor `LearnView`:** Apply new colors/buttons.
7.  [ ] **Refactor `MainView`:** Ensure navigation/shell matches theme.

## 5. Risks & Considerations
- **Dark Mode:** Ensure all new Brushes have a Dark variant defined in `App.axaml`.
- **Contrast:** Check text contrast on the new `light_gray` / `dark_gray` backgrounds.
- **Platform Specifics:** Check `Behaviors/MacTextEditingBehavior.cs` integration with new TextBox styles (ensure styles uses `BasedOn` or includes the behavior).
