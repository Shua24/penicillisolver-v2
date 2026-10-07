# PenicilliSolver 2

PenicilliSolver 2 is a web app that reads a lab's antimicrobial-resistance
(antibiogram) spreadsheet and tells you, per organism, which antibiotics that
organism is still most susceptible to. One shared spreadsheet serves everyone in the
lab; different people can do more or less depending on their role.

This documentation consists of these sections:

1. [**What Users Can Do**](#1-what-users-can-do) — for clinicians and lab staff who just want to
   use the app. No programming knowledge needed.
2. [**Developer Onboarding**](#2-developer-onboarding) — for engineers who want to build, run,
   and change the code.

---

## 1. What Users Can Do

### Signing in

- **New here?** Register with your display name, email, and a role you would
  like to have. Your account is created *pending*: a clinical pathologist
  must activate it before you can do anything in the app. Until then, signing
  in only shows an "awaiting activation" notice.
- **Already registered?** Sign in with your email and password. If your
  account is still pending, you will see the activation notice, not the tools.
- **Can't sign in?** Your account may be *disabled* by a pathologist. Only
  they can re-enable it.

### What each role can do

There are three roles. What you can do depends on which one you hold:

| Role | What they can do |
|---|---|
| **Clinical Pathologist** | Everything. Upload or replace the shared spreadsheet, add or remove antibiotic abbreviations, manage other people's roles and activation, and set the team's permissions. The only role with administration power. |
| **Doctor** | Read-only. Open the susceptibility rankings and view the abbreviation list, but cannot upload, edit mappings, or administer the app. |
| **Infectious Disease Control Team** | Read by default. A pathologist can additionally let the team replace or remove the spreadsheet. Both of these are *off* on a fresh system and granted one at a time. |

Every role must also have an *active* account; a pending or disabled account
gets no tools at all.

### Looking up susceptibility rankings

On the **susceptibility ranking** page:

1. Type or pick an organism. The picker matches any part of the name, so
   typing `aureus` finds *Staphylococcus aureus*.
2. Choose how many rows to show (from 1 up to every antibiotic in the file;
   the default is the **top 3**).
3. The table lists that organism's most susceptible antibiotics: their rank,
   name, and the percentage of isolates that are *susceptible*. The highest
   percentage comes first — that is the drug the organism is still most
   susceptible to (the one a treatment is most likely to work against). A drug
   reported at 0.00, or one the file never tested, sinks to the bottom.

Antibiotics the file never tested against that organism appear after the
measured ones with a dash in place of a percentage, so an "untested" drug is
never mistaken for a 0%. If more than one organism matches, you pick which
one you meant; if none match, the page says so.

### Mapping antibiotic abbreviations

The **abbreviation mappings** page lists every shortened antibiotic name
found in the *current* spreadsheet, with its stored full name (if one
exists). Anyone who can read the spreadsheet can view it; only a **clinical
pathologist** can add, edit, or remove meanings. Meanings belong to one
particular file, so the same abbreviation may mean different drugs in
different uploads.

If the file already spells out every antibiotic in full, there is nothing to
map and the worksheet is withheld.

### Uploading the shared spreadsheet (pathologists only)

On the **upload** page you pick one file — a `.csv` or `.xlsx` up to 10 MB.
There is only ever one shared reference spreadsheet, so replacing it affects
everyone: if a file already exists you must explicitly confirm the
replacement. Two safety behaviours are built in:

- A new file is checked before it takes over, so a file that fails to parse
  never destroys the working one.
- When a file is replaced, the meanings you already mapped for its
  abbreviations carry forward automatically; only genuinely new
  abbreviations arrive unmapped.

After a successful upload the page shows the current file's name, when it was
uploaded, its format, and how many organisms and antibiotics it contains.

### Managing users and the team (pathologists only)

The **user and team settings** page lists every account with its role and
status. From there you can:

- assign any of the three roles,
- activate a pending account or disable one,
- toggle the Infectious Disease Control Team's two spreadsheet permissions
  (may replace / may remove the current file) independently.

Changes take effect when that account next signs in; already-open sessions
pick them up within about 30 minutes. The app will not let you disable or
demote the last active clinical pathologist, and it will not put an account
back into the pending state.

### Trying it with the sample files

Two ready-to-use spreadsheets live in `excel-samples/` and can be uploaded
straight away:

- **`amr.csv`** spells every antibiotic out in full, so it exercises the
  "nothing to map" path.
- **`amr.xlsx`** uses abbreviated antibiotic names (like `AMK %S`, `CRO %S`)
  and short organism codes, so it exercises the abbreviation-mapping
  workflow.

Both are far under the 10 MB limit and let you run the whole flow: upload,
rank, and map.

---

## 2. Developer Onboarding

This is a C# / ASP.NET Core Blazor (interactive server) app targeting
**.NET 10**. It runs on SQLite in development and PostgreSQL in production,
and is built to serve a small group of users (tens, not thousands) as a
modular monolith.

Everything below was verified by running the commands on the live machine;
the quoted figures are real outputs, not guesses.

### Prerequisites

- **.NET 10 SDK** — the repo targets `net10.0` and builds with SDK
  `10.0.112` (confirmed via `dotnet --version`).
- **PostgreSQL** (production only). Development uses SQLite and needs
  nothing installed.
- The EF tooling is a local .NET tool pinned in `dotnet-tools.json`
  (`dotnet-ef` `10.0.12`); restore it once with `dotnet tool restore` from
  the repo root. It is already in the local tool store here, so no network
  fetch is needed.

### Build, test, run

| Task | Command | Verified result |
|---|---|---|
| Build the whole solution | `dotnet build` | `Build succeeded.` · `0 Warning(s)` · `0 Error(s)` |
| Run all tests | `dotnet test` | `137` unit + `31` integration = **168 passed, 0 failed** |
| Live dev loop | `dotnet watch run` (or the VS Code "watch" task) | available (SDK `10.0.112-dev`) |

The two test projects are xunit and both live under `tests/`:

- `tests/PenicilliSolver.UnitTests` — 137 tests.
- `tests/PenicilliSolver.IntegrationTests` — 31 tests. They bind the real
  host via `WebApplicationFactory` and redirect the spreadsheet storage
  directory to a temp folder, so a test run writes nothing into
  `src/App_Data`.

Run the app with the default (development) environment;
`dotnet run --project src/penicillisolver-v2.csproj` works, and the
`.vscode/launch.json` opens the browser at the listening URL.

### Database: what happens on first run

On every start the host runs EF Core migrations and then the identity seeder
(`Program.cs` → `MigrateAsync` → `IdentitySeeder.SeedAsync`):

- **Provider selection** (`DatabaseProviderSelector`): the `Database:Provider`
  config key chooses the provider — `Sqlite` maps to EF Core SQLite, anything
  else to Npgsql. When the key is *unset*, SQLite is used only in
  development; production defaults to PostgreSQL. The committed
  `appsettings.json` therefore ships `"Provider": "Postgres"` with an *empty*
  `ConnectionStrings:DefaultConnection`. **A blank connection string throws
  at startup**, so a production deploy must supply `DefaultConnection` (via
  `appsettings`, the environment, or user secrets) or the app will not start.
  Development (`appsettings.Development.json`) points at a relative
  `Data Source=penicillisolver.db`, so a fresh checkout just works out of the
  box with no setup.

- **Seeding**: creates the three roles, one optional seed account per role
  (credentials from `SeedAccounts:<Role>:Email` / `:Password` — empty by
  default, so each is skipped with a logged error, which never blocks
  startup), the team-permission row (update and delete both denied by
  default), and a guaranteed **bootstrap clinical pathologist**
  (`bootstrap.clinicalpathologist@localhost.invalid`). That last account is
  created with a random 24-character password that is **printed to the log
  exactly once** — grab it from the startup output, then change it.

### Code layout, at a glance

```
src/
  Program.cs                 host wiring: Blazor, Identity, DbContext, seeder
  Components/                Razor UI: Pages (Upload, SpreadsheetViewer,
                             AntibioticMapping, UserRoleSettings, Home, …)
                             plus Account (Login, Register, Logout)
  Data/                      EF Core: ApplicationDbContext, migrations, provider selector
  Domain/                    entities, value objects, role constants, ranking services
  Services/                  application services: upload, storage, query,
                             abbreviation mapping, user administration
tests/
  PenicilliSolver.UnitTests/         137 xunit tests
  PenicilliSolver.IntegrationTests/  31 xunit tests (real host, temp storage dir)
excel-samples/                   amr.csv + amr.xlsx reference uploads
```

The solution is a binary `penicillisolver-v2.slnx` with three projects
(`src` plus the two test projects). `bin/`, `obj/`, `App_Data/`, and the
SQLite databases are all gitignored runtime state, never source.

### Project conventions

These are hard rules for anyone modifying the code — read them before a PR:

- **Standard libraries first**, third-party packages later.
- **Use LINQ** for SQL queries and for spreadsheet operations.
- **Every source file stays under 300 lines.** (Currently the longest is
  `AntibioticMapping.razor.cs` at 295 lines — the cap holds. When you add to
  a file that is near the cap, split it into a `partial` file; the codebase
  already does this, e.g. `IdentitySeeder.cs` / `IdentitySeeder.Bootstrap.cs`.)
- **C# naming conventions**: PascalCase for types and members, camelCase for
  locals and parameters, and **full words — no abbreviations** in
  identifiers.
- **Assign every method return value to a named local before using it**
  (no chained calls straight into a `return` or `if`). This is a deliberate
  readability rule, not a style preference.
- **All tests live under `tests/`.**
- Keep the front end simple: the CSS is intentionally basic and left to be
  tweaked by hand.

### Running the build/tests the way this repo does

```bash
cd /home/aulus/Sources/penicillisolver-v2
dotnet tool restore          # once: restores dotnet-ef 10.0.12
dotnet build                 # whole solution
dotnet test                  # unit + integration, expect 168 passed
```

> On a fresh clone the first `dotnet build` / `dotnet test` will restore
> NuGet packages from the network; on this machine the cache is already warm,
> so the runs above are offline.
