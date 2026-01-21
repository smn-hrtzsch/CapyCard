### CapyCard Development Guidelines

- Always run a platform-specific build (e.g., 'dotnet build path/to/ios.csproj') after implementing or modifying platform-specific code to catch build errors early.
- When trying to build the .sln file or the Android project in the CapyCard project, rememeber to set the correct JAVA home path to avoid build errors. The correct command is:
  export JAVA_HOME="/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home"
- Always try to build or compile the project, after making changes to ensure everything works as expected. If there are any build errors, provide solutions to fix them.
- When requested a pull request, alway use "main" branch a target branch unless specified otherwise. After creating the pull request, merge it immediately and delete the source branch locally and remotely.
- When requested to create a new release, use the provided release workflow in the GitHub repository. Always target the "main" branch for releases. If not specified, ask the user wether the release should be a major, minor, or patch release. Use a correct title. Provide a concise and clear changelog summarizing the changes made since the last release.

### CapyCard Design Guidelines (UI Overhaul 2026)
- **Aesthetic:** "Shopping List" style – Clean, Floating, Rounded.
- **Colors:**
  - **Primary (Accent):** Teal (`#018786` Light / `#03DAC5` Dark). Used for Main Actions (FAB), Focus borders, Active states.
  - **Secondary:** Deep Purple (`#6200EE` Light / `#FFBB86FC` Dark). Used for decorative elements or secondary actions.
  - **Labels:** Use `TextMutedBrush` (Grey) for secondary labels (e.g. "Vorderseite"), NOT Primary color.
- **Shapes:**
  - **Buttons:** Pill-Shape (`CornerRadius="25"`).
  - **Cards/Dialogs:** Large rounded corners (`CornerRadius="28"`).
  - **Inputs:** Floating style (`CornerRadius="12"`), transparent background, no darkening on focus.
- **Icons:** Use `Material.Icons.Avalonia` exclusively.
- **Hover Effects:**
  - Colors must remain consistent with the base element (e.g., Teal stay Teal).
  - Use subtle overlays (`#08808080`) or Opacity shifts (`0.85`) instead of drastic color or style changes.
  - Do NOT switch text color or button types (Outlined <-> Filled) on hover.
- **Interaction:**
  - List items (Decks) must be fully clickable triggers for navigation.
  - Input fields must release focus on `Escape` key.
