# UI Overhaul Plan: "Shopping List" Aesthetic

## Goal
Transform the CapyCard UI to match the visual style of the reference "Shopping List" Android app.
**Key Attributes:**
- **Colors:** Teal (Primary) / Deep Purple (Secondary) / Adaptive Grays.
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

### Color Palette (Final Decision)
| Role | Color (Light) | Color (Dark) | Notes |
| :--- | :--- | :--- | :--- |
| **Primary** | `#FF018786` (Teal Darker) | `#FF03DAC5` (Teal) | Main Actions, Focus |
| **Secondary** | `#FF6200EE` (Purple) | `#FFBB86FC` (Purple Light) | Secondary Actions |
| **Background** | `#FFDDDDDD` | `#FF333333` | Global BG |
| **Surface** | `#FFCACACA` | `#FF494949` | Cards |
| **Error** | `#FFB71C1C` | `#FFE57373` | Destructive Actions |

### Shape Resources
- `CornerRadius.Small`: `12` (Inputs, internal cards)
- `CornerRadius.Large`: `28` (Dialogs, Main Floating Cards, FABs)

## 3. Control Styles
- **File:** `CapyCard/CapyCard/Styles/AppStyles.axaml`

### Buttons
- **Style:** Pill shape (`CornerRadius="25"`).
- **Variants:** 
    - `primary`: Background `PrimaryBrush`.
    - `secondary`: Border `PrimaryBrush` (Outlined).
    - `icon`: Circular, Transparent.
    - `list-item`: Transparent, clickable area.

### TextBoxes ("Floating")
- **Style:** 
    - Transparent Background (even on focus).
    - `CornerRadius="12"`.
    - Margin bottom `16`.
    - Focus: Border `PrimaryBrush` (Thickness 1.5).
    - Vertical Content Alignment: Center.

## 4. Implementation Steps (Completed)

1.  [x] **Install Nuget:** `Material.Icons.Avalonia`.
2.  [x] **Setup Colors:** Rewrite `App.axaml` resources.
3.  [x] **Create Styles:** Implement `AppStyles.axaml`.
4.  [x] **Refactor `DeckListView`:**
    - Replace `Path` with `material:MaterialIcon`.
    - Apply "Floating Card" style to list items.
    - Make items fully clickable (`OpenDeckCommand`).
    - Fix Icons (ChevronRight/Down).
5.  [x] **Refactor `CardListView`:**
    - Muted labels for Front/Back.
    - Floating Inputs.
6.  [x] **Refactor `LearnView`:** Apply new colors/buttons.
7.  [x] **Refactor `MainView`:** Ensure navigation/shell matches theme.
8.  [x] **Refactor `DeckDetailView`:** Full layout update (Floating inputs, Pill buttons).

## 5. Final Design Standards (Saved to GEMINI.md)
- **Primary Color:** Teal is King. Used for all main interactions.
- **Interactions:** Inputs must release focus on Escape. Lists must be clickable.
- **Typography:** Labels should be muted/grey, not colored.