# Osynt 🌱

A cozy, local-first HTTP client built with **Avalonia and .NET 10**. Postman-style essentials, with parchment, sage green, and an original pixel-art farm. No account or login required.

![Osynt desktop preview](docs/screenshots/osynt-preview.png)

```sh
dotnet run --project src/Osynt.Desktop
```

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Runs on Windows, macOS, and Linux desktops. On Linux, use an X11 or XWayland session with the usual Avalonia native dependencies (fontconfig, libICE, libSM, libX11, and libXi).

### Develop in WSL, run on Windows 11

From the WSL checkout:

```sh
bash scripts/publish-windows.sh
bash scripts/run-windows.sh
```

This builds and launches a **native Windows app**, using Windows interop rather than WSLg. The portable executable is `artifacts/windows/win-x64/Osynt.Desktop.exe`. The build script also prints its Windows Explorer path. You can copy the executable to a Windows folder and double-click it; the self-contained build includes .NET and Avalonia's native libraries, so Windows does not need an SDK or runtime installation. Native libraries extract to Windows' temporary directory on first launch.

For Windows on ARM, publish with `bash scripts/publish-windows.sh win-arm64` and open the executable in `artifacts/windows/win-arm64/`.

You can also build from a Windows checkout with the .NET 10 SDK in PowerShell:

```powershell
.\scripts\Publish-Windows.ps1
.\artifacts\windows\win-x64\Osynt.Desktop.exe
```

Windows stores the workspace in `%LOCALAPPDATA%\Osynt\workspace.json`. The Windows and WSL versions have separate local workspaces; use Osynt collection export/import to move requests between them.

### Try it

The app opens a blank **GET** request with no query parameters, custom headers, or body. Paste your API URL and press **Send**. Local URLs such as `localhost:8080/health` use HTTP if you omit the scheme. A successful empty health response shows **200 OK** and **No response body**.

The app also starts a small **loopback-only Harvest API** on a free port. Select a sample from “The little farm” and press Send to try it without internet access:

- **Get seasonal crops** — change `season` in Params, or the `{{season}}` environment variable. Try `spring`, `summer`, `fall`, or `winter`.
- **Meet the villagers** — a friendly GET response.
- **Plant something new** — POST an editable JSON body and get a `201 Created` response.
- **Check on the farm** — a small health check.

Query parameters stay synchronized between the URL and Params table. Pasting another endpoint replaces the previous query parameters; editing, disabling, or deleting a row updates the URL. Repeated keys are supported, and values are encoded once. Standard headers (`Host`, `Accept`, `User-Agent`, and body length) are set automatically. JSON bodies receive `Content-Type: application/json`; explicit header overrides take precedence.

The **Variables** button edits values used as `{{name}}` in URLs, parameters, headers, and bodies. No environment setup is required for a literal URL.

### ASP.NET Core in WSL

Use the exact address and port printed by `dotnet run` under **Now listening on**. A Windows Osynt build can connect to an API in WSL through `http://localhost:<port>/health`; no query parameters or headers are required for an ordinary health endpoint. A connection refusal usually means that nothing is listening at the selected address or port. Osynt displays the failed host and port in the response pane.

If the API uses HTTPS, its WSL development certificate may need to be trusted in Windows as well. Certificate verification remains enabled; use the API's HTTP development address or configure a trusted certificate. See Microsoft's documentation on [WSL networking](https://learn.microsoft.com/en-us/windows/wsl/networking) and [ASP.NET Core development certificates in WSL](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl#trust-https-certificate-from-windows-subsystem-for-linux).

### Included in the MVP

- Multiple request tabs with independent responses and protection for unsaved changes.
- GET, POST, PUT, PATCH, DELETE, HEAD, and OPTIONS; editable request names.
- Enabled/disabled query parameters and headers, JSON formatting, environment variables.
- Real status, duration, downloaded size, syntax-highlighted JSON, raw text, and response headers.
- Cancel requests; a 30-second timeout and a 5 MB response preview limit.
- Save requests into local collections; search requests; reopen the last 100 history entries.
- Copy or save responses, and import/export **Osynt collection JSON**.
- Original pixel illustrations drawn directly by Avalonia, with no external image assets.

| Shortcut | Action |
| --- | --- |
| Ctrl / ⌘ + Enter | Send or cancel |
| Ctrl / ⌘ + S | Save request |
| Ctrl / ⌘ + N | New request |
| Ctrl / ⌘ + W | Close request tab |

### Storage and scope

Saved collections, environment variables, and request history live in `Osynt/workspace.json` under the operating system's local application data directory (`~/.local/share/Osynt` on most Linux systems). Set `OSYNT_DATA_DIR` to choose another directory. Changes to a request become part of its collection when you click **Save** beside the request name. Workspace writes are atomic; unreadable files are backed up before a replacement is written.

This is a desktop MVP: no cloud sync, account system, scripts/test runner, OAuth flow, multipart uploads, WebSockets, or Postman collection compatibility yet. You can send API credentials through ordinary headers. Workspace JSON and request history are stored in plain text, including headers and bodies; use disposable credentials while trying the MVP. Response saving exports the displayed text, not binary downloads.

### Build and check

```sh
dotnet restore
dotnet build --no-restore -m:1
dotnet run --project tests/Osynt.Checks --no-build
```

The integration runner checks request construction, bidirectional query editing, blank request defaults, automatic headers, real ASP.NET Core GET/POST and empty health responses, redirects, connection errors, cancellation, persistence, import/export, compact layouts, and keyboard interactions in the actual Avalonia window. It returns a nonzero exit code on failure. It needs loopback socket access, but no external API or display server. The ASP.NET framework reference is confined to the test project.

Render desktop previews with Avalonia's headless Skia renderer:

```sh
dotnet run --project tests/Osynt.Checks -- --snapshot artifacts/osynt-preview.png
```

### Architecture

The dependency direction points inward. Domain and Application have no Avalonia, HTTP client, file-system implementation, or NuGet dependencies.

```text
Osynt.Domain          Plain request, response, collection, and workspace models
    ↑
Osynt.Application     Workspace use cases and ports for storage, HTTP, and serialization
    ↑
Osynt.Infrastructure  HttpClient adapter, atomic JSON storage, collection serializer, demo API
    ↑
Osynt.Desktop         Avalonia views, editable view models, and native desktop dialogs
```

`App.axaml.cs` is the composition root: it connects infrastructure implementations to application interfaces. The desktop view models depend on application ports and work with separate editable presentation models; domain entities have no binding notifications. Collection saving, environment changes, history limits, and import/export are application use cases. The sidebar, tabs, request header, editor, and response viewer are separate views. Native file pickers and close dialogs live in desktop services.

`tests/Osynt.Checks` is the integration and rendering runner. Formatting conventions are defined in `.editorconfig`; run `dotnet format whitespace --no-restore` to apply them.
