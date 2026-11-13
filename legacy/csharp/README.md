# Legacy C# implementation

This folder preserves the previous Windows Forms / .NET application source for reference. The repository's active application is the Go project at the repository root.

The old project launched a separate proxy.exe, whose source code was not included. The Go version implements its own UDP client/host relay and does not depend on that binary.

Do not use this project to build the primary release. Its sources are kept here so historical UI and feature behavior can be compared during the migration.
