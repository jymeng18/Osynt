# Production roadmap

Work top to bottom. Check off an item only after implementation and relevant validation.
The existing app already supports requests, collections, history, environment variables,
cancellation, bounded response previews, and a persistent garden.

## P0 — Reliability and delivery

- [ ] Add CI for .NET build/integration checks and garden tests.
- [ ] Make HTTP header handling deterministic: enabled defaults, duplicate headers, and invalid names.
- [ ] Add configurable per-request timeouts with validation, persistence, and editor controls.
- [ ] Harden workspace writes with unique temporary files and recoverable backups.
- [ ] Validate persisted/imported identifiers and reject ambiguous collection structures.
- [ ] Document local data location, recovery, and sensitive-data handling.
- [ ] Add versioned release packaging and a Windows smoke-test release checklist.

## P1 — Everyday API workflows

- [ ] Add raw text and form body modes with matching content types.
- [ ] Add Bearer and Basic authentication helpers with environment-variable support.
- [ ] Import requests from cURL and copy requests as cURL.
- [ ] Import and export Postman collections with clear unsupported-feature notices.
- [ ] Duplicate saved requests and move requests between folders.
- [ ] Search collections by folder name, request method, name, and URL; show empty results clearly.
- [ ] Find text in responses, copy response bodies/headers, and save responses to disk.
- [ ] Show response truncation and content type clearly; handle binary previews gracefully.
- [ ] Add named environments and an environment selector.
- [ ] Persist open tabs and recover unsaved drafts after restart.
- [ ] Provide an explicit redirect policy and show final response URL.

## P2 — Polish and accessibility

- [ ] Add accessible labels to icon buttons, query rows, and request controls.
- [ ] Document keyboard shortcuts and add keyboard navigation checks.
- [ ] Add visible no-results states and actionable first-run guidance.
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

Track completed batches and any platform limitations here as work proceeds.
