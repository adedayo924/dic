# DIC E-Library — Roadmap

Status legend: ✅ done · 🚧 in progress · ⬜ planned

## Phase 0 — Tooling & foundation ✅
- ✅ Per-user .NET 8 SDK wired (PATH prefix documented).
- ✅ Repository initialised (`main`, `.gitignore`, `.gitattributes`), `Directory.Build.props`, `global.json`.
- ✅ Solution structure: 9 source projects + 5 test projects.

## Phase 1 — Core, Data & Security ✅
- ✅ Domain entities, enums, models, options, abstractions.
- ✅ EF Core + SQLite persistence, `InitialCreate` migration, seeding (admin, 7 categories, settings).
- ✅ SQLite FTS5 full-text index (`BookSearch`) with reindex support.
- ✅ `DateTimeOffset` stored as UTC ticks so ordering/filtering works on SQLite.
- ✅ PBKDF2 password hashing; AES-256-GCM content cipher; RSA-PSS signer; DPAPI key store.

## Phase 2 — Document pipeline ✅
- ✅ PdfPig metadata/text extraction.
- ✅ Windows OCR + PDF page rendering (`DIC.ELibrary.Ocr`).
- ✅ OCR text and cover write-back into the catalog.

## Phase 3 — Packaging ✅
- ✅ `.dicpkg` build/inspect/install and `.dicl` catalog export.
- ✅ Integrity (SHA-256) + RSA-PSS signature verification.
- ✅ Auto-generate signing key on first signed build so packages are never unsigned.

## Phase 4 — UI ✅
- ✅ Shared WPF library: MVVM infrastructure, dialogs, theming, converters.
- ✅ Student app: Dashboard, Catalog, Book detail, Reader, My Library, Import, Settings, Login.
- ✅ Admin app: Dashboard, Books, Ingest, Packages, Users, Loans, Reports, Audit, Settings, editors.
- ✅ Composition root shared by both heads; all 16 views verified to instantiate at runtime.

## Phase 5 — Quality ✅
- ✅ Test suites for Core, Security, Data, Package and Pdf layers (70 tests passing).
- ✅ Full solution builds with zero warnings/errors (Debug & Release).
- ✅ Smoke tests: both app heads launch, migrate and seed against a shared data directory.

## Next ⬜
- ⬜ Dark mode / accessibility pass (contrast, keyboard navigation, screen-reader labels, high-DPI).
- ⬜ Continuous integration: GitHub Actions building and testing on `windows-latest`.
- ⬜ Installer / distribution packaging (single-file or MSIX) with per-machine data directory option.
- ⬜ Multi-branch catalog sync over `.dicl` import/merge (conflict resolution, incremental updates).
- ⬜ Localisation infrastructure (resource dictionaries) — currently English-only.
- ⬜ Reader enhancements: search-in-document, textured bookmarks panel, adjustable reading themes.
- ⬜ Reporting: CSV/PDF export of circulation and audit reports.
- ⬜ Backup/restore scheduling and integrity verification surfaced in Admin settings.
- ⬜ Signed catalog trust management UI (import/rotate verification public keys).
