### CapyCard Development Guidelines

- Always run a platform-specific build (e.g., 'dotnet build path/to/ios.csproj') after implementing or modifying platform-specific code to catch build errors early.
- When trying to build the .sln file or the Android project in the CapyCard project, rememeber to set the correct JAVA home path to avoid build errors. The correct command is:
  export JAVA_HOME="/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home"
- Always try to build or compile the project, after making changes to ensure everything works as expected. If there are any build errors, provide solutions to fix them.
- When requested a pull request, alway use "main" branch a target branch unless specified otherwise. After creating the pull request, merge it immediately and delete the source branch locally and remotely.
- When requested to create a new release, use the provided release workflow in the GitHub repository. Always target the "main" branch for releases. If not specified, ask the user wether the release should be a major, minor, or patch release. Use a correct title. Provide a concise and clear changelog summarizing the changes made since the last release.
