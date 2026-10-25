# Osynt 🌱

A cozy, local-first HTTP client built with **Avalonia and .NET 10**. Postman-style essentials, with parchment, sage green, and an original pixel-art farm. No account or login required.

```sh
dotnet run --project src/Osynt.Desktop
```

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Runs on Windows, macOS, and Linux desktops. On Linux, use an X11 or XWayland session with the usual Avalonia native dependencies (fontconfig, libICE, libSM, libX11, and libXi).

### Try it

The app starts a small **loopback-only Harvest API** on a free port and sends the first sample request. Everything in “The little farm” works without internet access:

- **Get seasonal crops** — change `season` in Params, or the `{{season}}` environment variable. Try `spring`, `summer`, `fall`, or `winter`.
- **Meet the villagers** — a friendly GET response.
- **Plant something new** — POST an editable JSON body and get a `201 Created` response.
- **Check on the farm** — a small health check.

Paste any `http://` or `https://` URL to use your own API. Choose a method, edit query parameters and headers, and add a text or JSON body. The environment button edits variables used as `{{name}}` in URLs, parameters, headers, and bodies. Use **Use local demo** to switch back.

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

Saved collections, environment variables, and request history live in `Osynt/workspace.json` under the operating system's local application data directory (`~/.local/share/Osynt` on most Linux systems). Set `OSYNT_DATA_DIR` to choose another directory. Changes to a request become part of its collection when you click **Save request**. Workspace writes are atomic; unreadable files are backed up before a replacement is written.

This is a desktop MVP: no cloud sync, account system, scripts/test runner, OAuth flow, multipart uploads, WebSockets, or Postman collection compatibility yet. You can send API credentials through ordinary headers. Workspace JSON and request history are stored in plain text, including headers and bodies; use disposable credentials while trying the MVP. Response saving exports the displayed text, not binary downloads.

### Build and check

```sh
dotnet restore
dotnet build --no-restore -m:1
dotnet run --project tests/Osynt.Checks --no-build
```

The integration runner checks request construction, real local GET/POST responses, errors, cancellation, the preview size limit, persistence, import/export, and keyboard interactions in the actual Avalonia window. It returns a nonzero exit code on failure. It needs loopback socket access, but no external API or display server.

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
