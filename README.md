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
  they can re-enable it. If you have failed five times in a row, the account is
  locked for five minutes — wait and try again.
- **Forgot your password?** There is no email in this application, so there is
  no reset link. Ask a clinical pathologist to reset it for you; they will give
  you a temporary password, and you will be asked to choose your own the next
  time you sign in. If you are signed in already, change it yourself at
  `/Account/ChangePassword`.

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
  `10.0.403` (confirmed via `dotnet --version`).
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
| Run all tests | `dotnet test` | `155` unit + `42` integration = **197 passed, 0 failed** |
| Live dev loop | `dotnet watch run` (or the VS Code "watch" task) | available (SDK `10.0.403`) |

The two test projects are xunit and both live under `tests/`:

- `tests/PenicilliSolver.UnitTests` — 155 tests.
- `tests/PenicilliSolver.IntegrationTests` — 42 tests. They bind the real
  host via `WebApplicationFactory` and redirect the spreadsheet storage
  directory to a temp folder, so a test run writes nothing into
  `src/App_Data`.

Run the app with the default (development) environment;
`dotnet run --project src/penicillisolver-v2.csproj` works, and the
`.vscode/launch.json` opens the browser at the listening URL.

### Dependency security

NuGet's audit runs on every restore and reports known-vulnerable packages as
`NU1902` (moderate) or `NU1903` (high) warnings. To check the graph on demand,
from the repo root:

```bash
dotnet list src/penicillisolver-v2.csproj package --vulnerable --include-transitive
```

If it reports anything, do not silence the warning — find which reference pulls
the package (`dotnet nuget why src/penicillisolver-v2.csproj <PackageId>`), then
move that reference to a version whose dependency is patched, or pin the
package explicitly to the patched version. Both `NU1902` and `NU1903` are
build warnings rather than errors, so a vulnerable build still succeeds and
will otherwise go unnoticed.

The `System.Security.Cryptography.Pkcs` and `System.Security.Cryptography.Xml`
warnings this repo used to show came from `EPPlus` 8.7.1, which depended on
version `10.0.10` of both. `EPPlus` 8.7.2 depends on `10.0.13`, where the
advisories are patched.

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
  `Data Source=penicillisolver.db`, so the database itself needs no setup — but
  the host still refuses to start until seed credentials are set (next
  section).

- **Seeding and the startup refusal**: the seeder creates the three roles, one
  seed account per role, and the team-permission row, then the host **throws**
  unless at least one *active clinical pathologist* exists. Both are covered in
  the next section, because they are the whole account-bootstrap story.

### Bootstrapping the first accounts

The app never generates an account on its own, and it will not run without one.
On every start the host migrates the database and then runs the identity seeder
(`Program.cs` → `MigrateAsync` → `IdentitySeeder.SeedAsync`), which creates the
three roles, creates one seed account per role from configuration, creates the
team-permission row, and then **throws** unless at least one *active clinical
pathologist* exists.

That last check is the reason this section exists. Activation by a clinical
pathologist is the only way an account ever gains a role, so an empty
pathologist pool is a permanent lockout: nobody can approve a registration and
nobody can grant a role. A fresh clone with no seed credentials therefore
refuses to start, with this exact output:

```
fail: Startup[0]
      Skipping the ClinicalPathologist seed account because the configuration key 'SeedAccounts:ClinicalPathologist:Email' is missing or blank. ...
Unhandled exception. System.InvalidOperationException: No active clinical pathologist exists, so no account can be activated or managed. ...
```

Set the credentials before the first start. In development, user secrets are
the intended store (they live outside the repo, in
`~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`):

```bash
# Run from the repository root (the folder that contains src/).
dotnet user-secrets set "SeedAccounts:ClinicalPathologist:Email" "you@example.test" --project src/penicillisolver-v2.csproj
dotnet user-secrets set "SeedAccounts:ClinicalPathologist:Password" "<a password>" --project src/penicillisolver-v2.csproj
```

The other two roles are optional — only the clinical pathologist is required to
start:

```bash
dotnet user-secrets set "SeedAccounts:OtherDoctor:Email" "doctor@example.test" --project src/penicillisolver-v2.csproj
dotnet user-secrets set "SeedAccounts:OtherDoctor:Password" "<a password>" --project src/penicillisolver-v2.csproj
dotnet user-secrets set "SeedAccounts:InfectiousDiseaseControlTeam:Email" "team@example.test" --project src/penicillisolver-v2.csproj
dotnet user-secrets set "SeedAccounts:InfectiousDiseaseControlTeam:Password" "<a password>" --project src/penicillisolver-v2.csproj
```

User secrets are read in the Development environment only; outside it the same
keys come from environment variables (`SeedAccounts__ClinicalPathologist__Email`,
`SeedAccounts__ClinicalPathologist__Password`, and so on).

A seeded account is created `Active`, with its role already assigned and its
email marked confirmed, so it can sign in immediately — no activation step.

**Password rules.** Identity's defaults apply: at least 6 characters, one
uppercase letter, one lowercase letter, one digit, and one non-alphanumeric
character. A password that misses any of them is rejected at seeding time and
the host then refuses to start. The real output for `weakpass`:

```
Could not create the ClinicalPathologist seed account for weak-test@example.test: PasswordRequiresNonAlphanumeric: Passwords must have at least one non alphanumeric character.; PasswordRequiresDigit: Passwords must have at least one digit ('0'-'9').; PasswordRequiresUpper: Passwords must have at least one uppercase ('A'-'Z').
```

**Restarts are safe.** Seeding is idempotent: an account whose email is already
taken is left alone apart from having the role assigned if it is missing, and
the log says so (`The ClinicalPathologist seed account ... already exists.`).
The configured password is used only when the account is first created —
changing it later does not change the existing account's password (verified:
after changing the configured password and restarting, the old password still
signed in). The app has no password-change or password-reset page, so choose
the seed password carefully; changing it means deleting the account and letting
the next start recreate it.

**Deleting a seed account does not retire it.** If the configuration still
names that email, the next start recreates the account (verified: deleting the
doctor seed account from the settings page and restarting logged
`Created the OtherDoctor seed account for doctor-test@example.test.`). Remove
the configuration keys as well if the account is meant to stay gone.

### Registering the rest of the lab

There is no administrator "create user" command and no CLI. An account appears
in exactly two ways: the seeder above, or the public registration form.

1. The person opens `/Account/Register` and enters a display name, email, the
   role they are asking for, and a password (same complexity rules as above).
   The account is created **Pending**, and the page says so: "Akun Anda telah
   dibuat dan menunggu aktivasi oleh dokter spesialis patologi klinik."
2. They can sign in, but see only the awaiting-activation notice — no
   spreadsheet, no tools. (Verified: a pending account signs in and lands on
   the notice.)
3. A clinical pathologist signs in, opens `/settings/user-roles`, picks the
   account's role from the dropdown, and clicks **Activate**. Both actions are
   needed: registration records the *requested* role but does not grant it, so
   a fresh registration shows `—` in the role column until the pathologist
   chooses one. (Verified: a registered account went from `Pending` / `—` to
   `Active` / `OtherDoctor`.)

Registration is open to anyone who can reach the app. That is deliberate: a
pending account can do nothing, so the gate is the pathologist's activation,
not the form. No email is sent anywhere — the app registers a no-op email
sender and does not require a confirmed address — so no SMTP server is needed.
The settings page also refuses to disable or demote the last active clinical
pathologist, and refuses to put an account back into the pending state.

### Recovering a password (no email involved)

There is no email in this application, so there is no "forgot password" link.
Recovery is deliberately manual, and there are exactly three paths:

**1. A signed-in user changes their own password** at `/Account/ChangePassword`.
The current password is required. The change rotates the security stamp, so
every other session for that account is signed out immediately.

**2. A clinical pathologist resets someone else's password** from
`/settings/user-roles`. The **Reset password** button opens a field for a
temporary password; the pathologist reads it off the screen and hands it over in
person. The account is then forced to choose a new password at its next
sign-in — it cannot reach any other page until it does. The reset is recorded on
the account and shown in the settings table ("Password reset by … on …").

Two refusals are built in:

- a **disabled** account cannot be reset, because a new password would not let
  it sign in. Activate it first, then reset.
- a non-pathologist cannot reset anyone, including themselves through this
  route — they use path 1.

**3. An operator breaks a total lockout from the server.** Every in-application
recovery action needs an active clinical pathologist, so when there is none the
app cannot recover itself. The break-glass command is the way back:

```bash
# Run from the publish directory, with the same configuration the app uses.
echo 'New-Password-1!' | dotnet penicillisolver-v2.dll \
    --reset-pathologist-password --email pathologist@example.test
```

It reads the password from **stdin**, never from the command line, so it does
not land in shell history or in `ps`. It only touches clinical pathologist
accounts, and it forces a change at the next sign-in. The settings page shows
that reset as "Password reset from the server", because there is no signed-in
actor to name.

**The seeded pathologist is forced to change its password at first sign-in.**
A seed password is written down in deployment notes, and re-running the seeder
does not change an existing account's password, so without this the bootstrap
credential would be permanent. The flag is set only when the account is created,
never on an existing one, so a restart does not re-arm it.

### Code layout, at a glance

```
src/
  Program.cs                 host wiring: Blazor, Identity, DbContext, seeder
  Components/                Razor UI: Pages (Upload, SpreadsheetViewer,
                             AntibioticMapping, UserRoleSettings, Home, …)
                             plus Account (Login, Register, Logout,
                             ChangePassword) and the must-change gate
  Data/                      EF Core: ApplicationDbContext, migrations, provider selector
  Domain/                    entities, value objects, role constants, ranking services
  Services/                  application services: upload, storage, query,
                             abbreviation mapping, user administration,
                             password recovery (in-app and break-glass)
tests/
  PenicilliSolver.UnitTests/         155 xunit tests
  PenicilliSolver.IntegrationTests/  42 xunit tests (real host, temp storage dir)
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
  `SpreadsheetUploadService.cs` at 277 lines — the cap holds. When you add to
  a file that is near the cap, split it into a `partial` file; the codebase
  already does this, e.g. `IdentitySeeder.cs` / `IdentitySeeder.Accounts.cs`
  and `UserRoleSettings.razor` / `UserRoleSettings.razor.cs`.)
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
# Assuming you're close to the directory
cd ./penicillisolver-v2
dotnet tool restore          # once: restores dotnet-ef 10.0.12
dotnet build                 # whole solution
dotnet test                  # unit + integration, expect 197 passed
```

> On a fresh clone the first `dotnet build` / `dotnet test` will restore
> NuGet packages from the network; on this machine the cache is already warm,
> so the runs above are offline.
