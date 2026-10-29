# Production roadmap

Work top to bottom. Check off an item only after implementation and relevant validation.
The existing app already supports requests, collections, history, environment variables,
cancellation, bounded response previews, and a persistent garden.

## P0 — Reliability and delivery

- [x] Add CI for .NET build/integration checks and garden tests.
- [x] Make HTTP header handling deterministic: explicit default suppression, repeated custom headers, resolved overrides, and invalid names.
- [x] Add configurable per-request timeouts with validation, persistence, and editor controls.
- [x] Harden workspace writes with unique temporary files and recoverable backups.
- [x] Validate persisted identifiers, normalize imported identities, and reject ambiguous stored collection structures.
- [x] Document local data location, recovery, and sensitive-data handling.
- [ ] Add versioned release packaging and a Windows smoke-test release checklist.

## P1 — Everyday API workflows

- [x] Add raw text and form body modes with matching content types.
- [x] Add Bearer and Basic authentication helpers with environment-variable support.
- [ ] Import requests from cURL and copy requests as cURL.
- [ ] Import and export Postman collections with clear unsupported-feature notices.
- [x] Create folders, duplicate saved requests, and move requests between folders.
- [x] Search collections by folder name, request method, name, and URL; show empty results clearly.
- [ ] Find text in responses, copy response bodies/headers, and save responses to disk.
- [ ] Show response truncation and content type clearly; handle binary previews gracefully.
- [ ] Add named environments and an environment selector.
- [ ] Persist open tabs and recover unsaved drafts after restart.
- [ ] Provide an explicit redirect policy and show final response URL.

## P2 — Polish and accessibility

- [ ] Add accessible labels to icon buttons, query rows, and request controls.
- [ ] Document keyboard shortcuts and add keyboard navigation checks.
- [x] Add visible no-results states and actionable empty-library/history guidance.
- [ ] Add response timing breakdowns and configurable preview wrapping.
- [ ] Add theme, font-size, and reduced-motion preferences.
- [ ] Keep long operations responsive and surface unexpected errors without losing edits.
- [ ] Add garden fallback guidance on platforms without WebView2.

## P3 — Broader functionality

- [ ] Add multipart uploads and file-backed request bodies.
- [ ] Add cookie inspection and explicit cookie-jar management.
- [ ] Add collection runs with assertions and exportable run results.
- [ ] Add OpenAPI import and request generation.
- [ ] Add optional proxy configuration and client certificates.
- [ ] Add opt-in secret storage using operating-system credential facilities.
- [ ] Publish platform support and performance budgets backed by smoke checks.

## Validation record

- Timeout and CI batch: 95 desktop/application/integration checks pass, including a real
  timeout cancellation and an actual numeric-control binding. Garden unit checks pass.
  CI execution on GitHub and native Windows rendering remain separate from local checks.
- Recovery and headers batch: 101 checks pass. Coverage includes corrupt/missing primary
  recovery, backup retention, failed replacement rollback and cleanup, duplicate identities,
  templated header overrides, repeated custom values, and header injection rejection.
  The first GitHub desktop CI job also passed.
- Body formats batch: 107 checks pass, including real ASP.NET text/form requests,
  Unicode and reserved-character encoding, disabled fields, retained drafts, storage
  and export round trips, and desktop picker/add/remove interactions.
  The initial GitHub workflow completed successfully for both desktop and garden.
- Library search batch: 111 checks pass. Multi-term matching covers folder, method,
  name, and URL; filtered history and empty folders have explicit results; clearing
  search restores the previous collapsed folders. Desktop checks verify empty-state visibility.
- Authentication batch: 116 checks pass, including server-observed Bearer/Basic headers,
  UTF-8 Basic credentials, explicit header precedence, invalid credential rejection,
  persistence/isolation, and masked desktop input bindings.
- Organization batch: 122 checks pass. Copies have fresh identities and independent
  nested payload/authentication data. Moves persist to the chosen folder, include destinations
  hidden by search, preserve unsaved tab edits, and roll back when saving fails.
