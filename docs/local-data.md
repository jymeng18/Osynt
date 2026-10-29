# Local data and recovery

Osynt stores collections, environment variables, the last 100 request outcomes, garden
progress, and preferences in `workspace.json` under the operating system's local app-data
directory (`Osynt`). On Windows this is normally `%LOCALAPPDATA%\Osynt`; on Linux it is
normally `~/.local/share/Osynt`. Set `OSYNT_DATA_DIR` before launching to use another directory.

Each successful replacement keeps the previous saved workspace as `workspace.json.bak`.
Writes use a unique temporary file in the same directory, flush it to disk, then replace
the primary file. Failed writes leave the published application state unchanged.
Use one running Osynt instance per data directory; concurrent instances do not merge edits.

If the primary file is unreadable or missing, Osynt attempts to load the backup and shows
a recovery notice. When an unreadable primary is present, the next save first copies it
to a uniquely named `workspace-unreadable-*.json` file. If neither file can be read, a fresh
workspace opens. These archives are retained for manual recovery.

For manual recovery, close Osynt, copy the entire data directory somewhere safe, then
copy the desired backup to `workspace.json`. Restart Osynt to load it. A backup may be
one save behind, and unsaved tab edits are currently kept only in memory.

These files and collection exports are plain JSON. Request headers, bodies, URLs, history
snapshots, and environment variables can contain credentials or other sensitive data.
Use environment placeholders such as `{{api_token}}` in saved requests to avoid putting
the actual token into collection exports. The environment file still stores the token
locally in plain text. Protect backups and exports as you would the original workspace.
Response bodies are displayed in memory and are only written to a file when you choose
Save response. Osynt does not require an account or upload your workspace to a service.
