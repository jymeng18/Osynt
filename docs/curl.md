# cURL requests

Use **cURL → Import cURL…** above the request editor to paste a single command. Osynt parses the
text into a new unsaved tab, preserving the current tab. It never runs the command,
reads referenced files, or sends the imported request automatically.

Imports accept Bash/POSIX quoting, escaped quotes, and backslash line continuations.
Commands copied in Windows cmd or PowerShell syntax need their quoting converted first.
The input limit is 5 MB of UTF-8 text.

Supported request options include:

- URLs supplied directly or with `--url`, and standard methods with `-X` / `--request`.
- Headers with `-H` / `--header`, including repeated values, suppression (`Name:`), and
  explicit empty values (`Name;`). `-A` / `--user-agent` and `-e` / `--referer` also work.
- Literal `-d` / `--data`, `--data-ascii`, `--data-raw`, `--data-binary`, and `--json` data.
- Literal `--data-urlencode` values and `-G` / `--get` query construction.
- Basic `-u` / `--user` credentials, `--basic`, and `--oauth2-bearer` tokens.
- `-I` / `--head` and whole-second `-m` / `--max-time` values from 1 to 3600.
- Common display, location, compression, and glob-off flags. These use Osynt's own
  networking behavior rather than starting a cURL process.

An unsupported option produces an error while retaining the pasted command for editing.
File references, stdin, multiple URLs, shell variables/commands, pipes, TLS verification
overrides, cookie files, proxy options, multipart uploads, and external cURL config files
are not imported. Use literal data and environment placeholders in the editor instead.

Imported requests inherit Osynt's automatic headers and redirect/decompression policy.
The default timeout is 30 seconds unless the command supplies a supported `--max-time`.
Osynt follows redirects; an imported command without `--location` still follows them.

**cURL → Copy as cURL** builds a POSIX command from the current request using resolved environment
variables. It includes enabled headers, the outgoing payload, credentials, and timeout.
Single quotes, newlines, and literal shell characters are quoted for a POSIX shell.
Treat copied commands as sensitive: they can contain actual tokens and passwords.
The command represents the initial request, not session cookies from previous sends;
redirected method handling can differ between cURL's explicit `--request` behavior and .NET.
