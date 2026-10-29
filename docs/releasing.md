# Windows packaging and release checks

The project version lives in `Directory.Build.props`. Update it before a release.
The HTTP User-Agent and assembly metadata use that version.

Run **Windows packages** from GitHub Actions to create portable x64 and ARM64 ZIPs with
SHA-256 checksums and `release-info.json` source-revision metadata (including whether the
working tree had uncommitted changes). A `v<version>` tag
also starts this workflow; the tag must match the project version. The workflow uploads
build artifacts and does not automatically publish a GitHub Release. ARM64 is cross-built
and needs a separate ARM64 machine for its runtime checks.

From Windows PowerShell, the equivalent local commands are:

```powershell
./scripts/Package-Windows.ps1 -Runtime win-x64
./scripts/Package-Windows.ps1 -Runtime win-arm64
```

Archives appear in `artifacts/packages`. Extract the ZIP and launch `Osynt.Desktop.exe`.
The app is self-contained; .NET does not need to be installed. The garden requires the
Microsoft Edge WebView2 Runtime. Workspace data stays in the local data directory rather
than beside the executable. See [local data and recovery](local-data.md).

Before publishing a release:

- Confirm the Checks workflow and Windows packages workflow both pass for the release commit.
- Verify each ZIP against its `.sha256` file and check the version/revision metadata.
- Extract x64 on a clean Windows machine, then use `scripts/Test-WindowsBuild.ps1` to
  verify a responding window. Also test ARM64 on an ARM64 machine.
- Run the app without .NET installed and confirm the bundled garden works offline with WebView2.
- Send GET, JSON POST, text POST, form POST, and authenticated requests to a local API.
- Cancel a slow request and verify the configured timeout reports a useful message.
- Save, duplicate, move, rename, export, import, and delete requests; restart and verify persistence.
- Back up a sample data directory, corrupt its primary file, and confirm backup recovery.
- Verify keyboard navigation, compact-window controls, and unsaved-change prompts.
- Confirm existing user data survives upgrading from the previous version.

Publish the packages only after these checks pass. Sign executable packages and document
platform-specific limitations before distributing to production users.
