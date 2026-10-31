# Architecture

Osynt separates domain rules, application workflows, external adapters, and desktop presentation.

```text
Desktop presentation → Application → Domain
Infrastructure       → Application → Domain
Desktop startup      → Infrastructure (composition only)
```

The desktop executable references Infrastructure to construct the HTTP client, local demo,
and file store in `App`. Views and view models use application services and domain values.
`WorkspaceComposition` wires one workspace lifetime; it does not perform application work.
Interfaces belong to Application at external boundaries (`IRequestExecutor`, `IWorkspaceStore`,
and `ICollectionSerializer`). Internal services are concrete, with explicit constructor dependencies.

## Ownership

| Component | Responsibility |
| --- | --- |
| Domain | Request/workspace values, independent copies, garden growth and preference rules |
| `WorkspaceSession` | Loading, initialization, serialized reads and atomic state commits |
| `CollectionService` | Saving requests and importing/exporting collections |
| `HistoryService` | Bounded history and the matching garden growth in one commit |
| `EnvironmentService` | Validating variables and tracking the local demo environment |
| `GardenService` | Reading garden progress and saving preferences |
| `WorkflowService` | Persisting ordered request copies, sequencing runs, and committing response captures |
| `ResponseFields` | Enumerating JSON fields and validating atomic capture sets |
| `SendRequest` | Capturing request/environment snapshots, executing, and recording outcomes |
| Infrastructure | HTTP, transport diagnostics, bounded response decoding, JSON, files, local demo |
| `ICurlRequestSerializer` | cURL text conversion at the external-format boundary; never executes commands |
| `WorkspaceViewModel` | Navigation and refresh coordination between features |
| `RequestTabsViewModel` | Open/selected tabs, save, close and disposal |
| `RequestSendingViewModel` | Send/cancel presentation and response/error status |
| `RequestPaneViewModel` | Editor sections, pair commands and JSON formatting |
| `LibraryViewModel` | Search, saved collections and history presentation |
| `EnvironmentViewModel`, `GardenViewModel` | Feature-specific presentation and preference actions |
| `RequestCommandsViewModel` | Importing cURL into an unsaved tab and generating copyable commands |
| `WorkspaceStatus` | User-facing notices and save-failure presentation |

Views bind to feature paths such as `Requests.Selected`, `Library.Search`, and
`Sending.SendCommand`. Add feature behavior to its owner instead of expanding the shell
or introducing forwarding properties for every command.

## State and failure semantics

Feature services share a `WorkspaceSession`. A commit clones the latest state, applies
the change, saves it, then publishes it. If saving fails, both the in-memory state and
the visible feature state retain their previous values. A lock serializes commits so
overlapping sends cannot overwrite each other's history or growth. HTTP awaits happen
outside this lock. The current store is synchronous and intended for a local workspace.

Public reads return independent copies, including nested requests. Editing query results
cannot change persisted state. Internal read callbacks must return detached values; writes
must go through `Commit`. Each query copies only the feature data it needs.

`SendRequest` captures the document and environment before awaiting transport. HTTP error
statuses are responses; expected execution failures use the application-owned
`RequestExecutionException`. Infrastructure translates transport exceptions into that contract.
Cancellation propagates without adding history. A completed request records exactly one
history entry; only 2xx statuses grant growth, in the same transaction.

A history-save failure is returned separately from an execution failure. The response
remains available, the status reports the save failure, and the request is never automatically
resent. A tab owns cancellation while its send operation owns disposal of the cancellation
source, including when the tab is closed during a request.

## Validation

```sh
dotnet build Osynt.slnx --no-restore -m:1
dotnet run --project tests/Osynt.Checks --no-restore
```

The check runner covers dependency direction, state isolation, atomic rollback, concurrent
sends, cancellation, transport integration, and the actual desktop bindings through Avalonia's
headless renderer. The Windows-only WebView2 probe remains in `tests/Osynt.WindowsProbe`.
