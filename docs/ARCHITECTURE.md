# DIC E-Library — Architecture

A standalone, offline-first Windows desktop E-Library. Students browse, read and borrow
books; administrators ingest, curate, package and audit the collection. There is no server:
the SQLite database and book files live under a single shared local data directory.

## Solution layout

| Project | TFM | Purpose |
| --- | --- | --- |
| `DIC.ELibrary.Core` | `net8.0` | Domain entities, enums, models, options, abstractions (interfaces), `ICurrentUser`. No I/O. |
| `DIC.ELibrary.Data` | `net8.0` | EF Core + SQLite persistence, migrations, FTS5 index, service implementations. |
| `DIC.ELibrary.Security` | `net8.0` | PBKDF2 hashing, AES-256-GCM cipher, RSA-PSS signer, DPAPI-protected key store. |
| `DIC.ELibrary.Pdf` | `net8.0` | PDF metadata/text extraction via PdfPig. |
| `DIC.ELibrary.Ocr` | `net8.0-windows` | Windows.Media.Ocr text recognition + Windows.Data.Pdf page rendering. |
| `DIC.ELibrary.Package` | `net8.0` | `.dicpkg` book packages and `.dicl` catalog bundles (ZIP + encrypt + sign). |
| `DIC.ELibrary.UI` | `net8.0-windows` | Shared WPF library: ViewModels, Views, themes, converters, composition root. |
| `DIC.ELibrary.App` | `net8.0-windows` | Student application head. |
| `DIC.ELibrary.Admin` | `net8.0-windows` | Administrator application head. |

Tests live under `tests/` (one xUnit project per engine layer: Core, Security, Data, Package, Pdf).

## Layering

```
App / Admin  ->  UI  ->  { Data, Security, Pdf, Ocr, Package }
                              \  |  /
                               Core
```

- Feature libraries (`Data`, `Security`, `Pdf`, `Ocr`, `Package`) depend only on `Core`.
- Crypto and packaging **abstractions** (`IContentCipher`, `IPackageSigner`, `IKeyManager`,
  `IPdfService`, `IOcrService`, `IPackageService`) are declared in `Core`; implementations live
  in their own projects. This keeps `Core` free of platform dependencies and lets tests stub them.
- `Package` additionally references `Security` and `Data` because building/installing packages
  reads book metadata and writes to the catalog.
- `UI` is the only project that references everything; the two app heads reference `UI`.

## Composition and startup

`DIC.ELibrary.UI/Composition/ELibraryComposition.BuildHost` is the single composition root shared
by both heads:

1. Reads configuration (`appsettings.json` + environment).
2. Creates the data directory and configures Serilog (console + rolling daily file under `logs/`).
3. Registers every layer via `AddELibraryCore/Data/Security/Pdf/Ocr/Package/Ui`.
4. Applies the per-app `AppShellOptions` (product name, palette brushes, staff-role requirement,
   startup ViewModel, navigation items).
5. Initialises `ImageLocator` with the resolved `LibraryOptions`.

Startup then calls `InitializeDatabaseAsync`, which runs `DatabaseInitializer.InitializeAsync`:
`MigrateAsync` → create the FTS5 virtual table → seed defaults (first user, categories, settings).

Both apps share `%LOCALAPPDATA%\DIC\ELibrary`, so a package built in Admin is immediately visible
to the student app on the same machine.

### Navigation

`ShellWindow` hosts a `ContentControl` bound to `ShellViewModel.Current`. Implicit `DataTemplate`s
map each ViewModel type to its View: `Themes/ViewTemplates.xaml` (student) and
`Themes/AdminTemplates.xaml` (admin), merged by `App.xaml` / `Admin.xaml` respectively.

## Data model

Domain entities (`Core/Domain/Entities.cs`): `User`, `Category` (self-referencing tree), `Tag`,
`Book`, `BookTag`, `Loan`, `Favorite`, `Bookmark`, `ReadingProgress`, `AuditLog`, `AppSetting`,
`PackageRecord`. Enums: `UserRole`, `BookStatus`, `OcrStatus`, `LoanStatus`, `AuditAction`.

Notes:

- `LibraryDbContext` is used exclusively through `IDbContextFactory<LibraryDbContext>`; service
  implementations create a short-lived context per operation.
- **`DateTimeOffset` is persisted as UTC ticks (`INTEGER`)** via a global value converter in
  `OnModelCreating`. SQLite cannot translate `ORDER BY`/range filters over `DateTimeOffset` columns,
  so storing ticks makes loan/recent/audit queries work with the built-in provider.
- Enums are stored as `int`.
- The FTS5 virtual table `BookSearch(BookId UNINDEXED, Title, Author, Description, Body)` is created
  with raw SQL; `SearchService` keeps it in sync and powers full-text search.
- Migrations live in `Data/Persistence/Migrations`. Regenerate with
  `dotnet ef migrations add <Name> -p src/DIC.ELibrary.Data -s src/DIC.ELibrary.Data -o Persistence/Migrations`.

## Storage layout (`LibraryOptions`)

```
%LOCALAPPDATA%\DIC\ELibrary\
  library.db            # SQLite database (DatabaseFileName)
  books\<bookIdN>\      # original PDF + content.txt (extracted/OCR text)
  covers\               # generated cover images
  packages\             # built .dicpkg / .dicl artifacts
  backups\              # automatic scheduled database backups (library-YYYYMMDD-HHmmss.db)
  logs\                 # rolling Serilog logs (elibrary-YYYYMMDD.log)
  keys\                 # DPAPI key material (see below)
```

Paths stored in the database are relative to the data directory and resolved with
`LibraryOptions.ToAbsolute` / `ToRelative` so the library folder is portable.

## Security

- **Passwords** — `Pbkdf2PasswordHasher`, encoded as `PBKDF2$iterations$saltB64$hashB64`.
  Default 210,000 iterations. `NeedsRehash` detects stale iteration counts / malformed hashes.
- **Content encryption** — `AesGcmContentCipher` (AES-256-GCM). Ciphertext is self-describing
  (nonce + tag + payload). Tampering throws `CryptographicException`.
- **Signing** — `RsaPackageSigner` (RSA-PSS, 2048-bit) signs package/catalog manifests.
- **Key material** — `ProtectedKeyStore` persists `content.key`, `signing-key.dpapi` and
  `verification-key.pub` under `keys/`, protecting private keys with Windows DPAPI
  (`CurrentUser` scope). `IKeyManager` exposes fingerprints, public-key export/import and
  rotation; only Admin can regenerate the signing key.
- **Account lockout** — controlled by `SecurityOptions.MaxFailedAttempts` (0 disables).
- **Administrator lockout protection** — you cannot disable your own account, the last active
  administrator cannot be disabled, and on startup `EnsureSeedAdminAsync` reactivates/seeds an
  administrator if none is active. The admin tool cannot be permanently locked out.
- **Crash handling** — `ExceptionHandling.Attach` routes unhandled UI/background exceptions to
  Serilog and shows an error dialog instead of closing silently.

Seed administrator on first run: username `admin`, password `Admin@123` (change immediately).

## Backup & maintenance

- `IBackupService.BackupAsync` performs an online SQLite backup to a target file (destination
  connections are unpooled so backup files are never left locked); `RestoreAsync` replaces the live
  database; `VerifyAsync` opens a backup read-only, runs `PRAGMA integrity_check` and counts
  `Books`/`Users`; `ListBackupsAsync` enumerates `*.db` files newest-first.
- `IBackupScheduler` persists the automatic-backup policy in application settings
  (`Backup.Enabled`, `Backup.IntervalDays`, `Backup.Keep`, `Backup.Folder`, `Backup.LastRunAt`).
  At startup `RunIfDueAsync` writes `library-YYYYMMDD-HHmmss.db` into the backup folder when due and
  prunes the folder down to `Keep` files. The schedule, "Backup now" and "Verify backup…" actions are
  surfaced in Admin → Settings.

## Packaging (`.dicpkg` / `.dicl`)

`DicpkgService` produces a ZIP container with entries `manifest.json`, `signature.sig`,
`content.bin` (PDF, encrypted) or `content.pdf` (plain), `text.bin`, `cover.png`.

- `BuildAsync(bookId, outputPath)` — reads the book, optionally encrypts content/text, writes the
  manifest, and signs it.
- Before signing, if `PackageOptions.Sign` is enabled and no signing key exists yet, the service
  auto-generates one via `IKeyManager` so packages are never emitted unsigned.
- `InspectAsync(path)` — verifies SHA-256 integrity and the RSA signature and returns the manifest
  **without** extracting content.
- `InstallAsync(path)` — verifies, then materialises the book (and its assets) into the catalog.
- `ExportCatalogAsync(path)` — writes the full catalog as a signed/encrypted `.dicl` bundle.

Behaviour is governed by `PackageOptions` (`Encrypt`, `Sign`, `Extension = .dicpkg`,
`CatalogExtension = .dicl`).

## Document pipeline

- **PdfPig** (`PdfPigDocumentService`) extracts metadata and embedded text — used for digitally-born
  PDFs and per-page reading. It also powers reader search: `IPdfService.SearchAsync` scans page text
  case-insensitively and returns one `PdfTextMatch` (page number + snippet + occurrence count) per
  matching page, which the reader uses to jump between hits.
- **OCR** (`DIC.ELibrary.Ocr`) uses Windows.Media.Ocr for scanned books and Windows.Data.Pdf to
  render pages to images. `OcrOptions` controls language, page limits and the "skip if text already
  present" heuristic. Extracted text is written back via `IBookService.ApplyOcrTextAsync`; covers via
  `SetCoverAsync`.

## UI

MVVM using CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`).
Conventions:

- Any class using generated members must be declared `partial`.
- `UseWPF` projects do **not** get `System.IO` in implicit usings — add `using System.IO;` when
  using `Path`/`File`/`Directory`.
- Raw `&` in XAML text must be `&amp;`.

UI services: `DialogService` (dialogs), `ModalService` (modal windows), `ImageLocator` (resolves
cover/text paths). Shared brushes and control styles live in `Themes/Palette.xaml` and
`Themes/Controls.xaml`; value converters in `Themes/Converters.xaml`.

## Testing

One xUnit project per engine layer; each builds an in-memory/temp-dir host through the same DI
extensions used in production (`TestLibraryHost` in `Data.Tests`). Platform services are stubbed
(`StubPdfService`). Password iterations are dropped to 1,000 for speed.

```
$env:PATH = "$env:USERPROFILE\.dotnet;$env:USERPROFILE\.dotnet\tools;$env:PATH"
dotnet test DIC.ELibrary.sln -c Debug
```

## Build and run

The .NET 8 SDK is installed per-user (not machine-wide), so prefix commands:

```powershell
$env:PATH = "$env:USERPROFILE\.dotnet;$env:USERPROFILE\.dotnet\tools;$env:PATH"
dotnet build DIC.ELibrary.sln -c Release
dotnet run --project src/DIC.ELibrary.Admin
dotnet run --project src/DIC.ELibrary.App
```

## Deployment

Publish a self-contained, single-file build (no .NET runtime required on the target machine):

```powershell
dotnet publish src/DIC.ELibrary.App/DIC.ELibrary.App.csproj -c Release -p:PublishProfile=win-x64
```

The profile (`src/DIC.ELibrary.App/Properties/PublishProfiles/win-x64.pubxml`) targets `win-x64`,
self-contained, single-file with compression, and emits only `DIC.ELibrary.App.exe` plus
`appsettings.json` under `bin/Release/net8.0-windows10.0.19041.0/publish/win-x64/`. CI publishes the
same profile and uploads it as the `DIC.ELibrary-win-x64` build artifact.

### Per-machine configuration

Configuration precedence is: command line > environment variables > machine file > `appsettings.json`.
An administrator can pin shared settings (for example a shared catalog directory) for every user on
the machine by creating `%ProgramData%\DIC\ELibrary\appsettings.json`:

```json
{ "Library": { "DataDirectory": "C:\\DIC\\ELibrary-Data" } }
```

Environment variables (`Library__DataDirectory`) and command-line arguments
(`--Library:DataDirectory=...`) also override the default, which remains
`%LOCALAPPDATA%\DIC\ELibrary` per user.
