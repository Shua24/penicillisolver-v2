# PenicilliSolver — System Administrator Manual

This manual is for whoever runs the server. You do not need to know how the
application is built. You need to be able to answer three questions:

1. Is it running?
2. Someone cannot sign in — what do I do?
3. How do I back it up and get it back?

Everything below assumes the application is deployed with **Docker Compose**.
If your deployment uses something else, the ideas still hold but the commands
will differ.

---

## 1. What this thing is

PenicilliSolver is a small web application for a hospital laboratory. It reads
one spreadsheet of antibiotic resistance results and shows, per organism, which
antibiotics still work against it.

Three facts matter for you:

- **It has its own user accounts.** It does not use the hospital's Active
  Directory, LDAP, or single sign-on. Accounts live inside the application.
- **It sends no email.** There is no "forgot password" link, and no mail server
  to configure. Password recovery is done by a person, in the application, or by
  you on the server.
- **It stores everything in two places**: a database file, and one spreadsheet
  file. Back those up and you can rebuild the whole thing.

There are three kinds of user:

| Role | What they can do |
|---|---|
| **Clinical pathologist** | Everything. Manages users, uploads the spreadsheet. |
| **Doctor** | Read-only. |
| **Infectious disease control team** | Read-only by default; a pathologist can grant spreadsheet replace/delete. |

**Only a clinical pathologist can manage accounts.** If there is no active
clinical pathologist, nobody can approve a new user or reset a password. That is
the single most important thing in this manual — see §6.

---

## 2. Before you start: find your deployment's names

The commands below use two names that come from *your* Compose file, not from
this document. Find them once and write them down.

```bash
# From the directory that contains your docker-compose.yml
docker compose ps
```

That lists your services. The application service is usually called `app`,
`web`, or `penicillisolver`. In the commands below it is written as **`app`** —
substitute yours.

```bash
docker compose config --volumes
```

That lists your named volumes. You need the one holding the database. In the
commands below it is written as **`data`** — substitute yours.

If you are unsure which volume holds the database, this shows you what the
container can see:

```bash
docker compose exec app ls -la /app/App_Data
```

### A reference Compose file

**This is the repository's own `docker-compose.yml`**, reproduced here so you can
compare it with yours. The important parts are the `db` service, the healthcheck,
the three volumes and `HOME`.

```yaml
services:
  app:
    image: penicillisolver:latest
    restart: unless-stopped
    depends_on:
      db:
        condition: service_healthy
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://0.0.0.0:8080
      # Keeps the data-protection key ring on the mounted volume instead of
      # /root/.aspnet, which is not persisted.
      HOME: /app
      Database__Provider: Postgres
      ConnectionStrings__DefaultConnection: "Host=db;Port=5432;Database=penicillisolver;Username=penicillisolver;Password=change-me"
      SeedAccounts__ClinicalPathologist__Email: pathologist@hospital.example
      SeedAccounts__ClinicalPathologist__Password: "change-me-before-first-start"
    volumes:
      - appdata:/app/App_Data
      - keys:/app/.aspnet/DataProtection-Keys

  db:
    image: postgres:18-alpine
    restart: unless-stopped
    environment:
      POSTGRES_DB: penicillisolver
      POSTGRES_USER: penicillisolver
      POSTGRES_PASSWORD: change-me
    volumes:
      # postgres:18 moved its data directory. Mounting
      # /var/lib/postgresql/data is the pre-18 recipe and makes this container
      # crash-loop with "there appears to be PostgreSQL data in ... (unused
      # mount/volume)". Mount the parent instead.
      - pgdata:/var/lib/postgresql
    healthcheck:
      # The app migrates on startup and exits if PostgreSQL is not ready yet,
      # so the app waits for this instead of crash-looping.
      test: ["CMD-SHELL", "pg_isready -U penicillisolver -d penicillisolver"]
      interval: 5s
      timeout: 5s
      retries: 10

volumes:
  pgdata:
  appdata:
  keys:
```

**The healthcheck is not optional.** Without it the app starts before PostgreSQL
accepts connections and exits with a connection refusal; `restart: unless-stopped`
then loops it, which looks like a broken application rather than a race.

**Keep `POSTGRES_PASSWORD` and the password inside
`ConnectionStrings__DefaultConnection` identical.** If they differ, the app fails
to connect with an authentication error while `db` looks perfectly healthy.

**Do not set the password only in the compose file and commit it.** Use a `.env`
file next to the compose file (Compose reads it automatically) and keep it out of
version control.

---

## 3. Is it running?

```bash
docker compose ps
```

You want the application service to say `Up` (or `running`). If it says
`Restarting` or `Exited`, look at the log:

```bash
docker compose logs --tail=100 app
```

A healthy start ends with a line like:

```
Now listening on: http://0.0.0.0:8080
```

Then check it actually answers. **The application image has no `curl` or
`wget`**, so probe it from the host, or with bash's built-in TCP support:

```bash
# From the host, if the port is published:
curl -fsS -o /dev/null -w '%{http_code}\n' http://127.0.0.1:8080/Account/Login

# From inside the container, with no extra tools:
docker compose exec -T app bash -c \
  'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /Account/Login HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3 && head -1 <&3'
```

`200` means the process is up and the database is reachable. Anything else means
it is not serving. The `Host:` header is required — without it the application
answers `500`, which looks like a fault but is only a malformed probe.

**The application refuses to start if it has no clinical pathologist.** If the
log ends with:

```
No active clinical pathologist exists, so no account can be activated or managed.
```

then the seed credentials are missing or wrong. See §6.

---

## 4. The one thing users will ask you

> "I forgot my password."

**You cannot fix this yourself, and you should not try.** Send them to a
clinical pathologist. The pathologist signs in, opens **User and team settings**,
finds the account, clicks **Reset password**, types a temporary password, and
tells the person what it is. The person is then forced to choose their own
password the next time they sign in.

Do not create a second account for them. Do not edit the database. Both cause
duplicate accounts and lost history.

**If the person is a clinical pathologist and there is another pathologist
available**, that other pathologist does the same thing. Pathologists can reset
each other.

**If the person is the *only* pathologist**, go to §6.

---

## 5. "It says my account is locked"

After **5 failed sign-in attempts** an account is locked for **5 minutes**. This
is automatic and protects against password guessing. The user sees a page saying
the account is locked.

**Do nothing.** It unlocks itself. Tell them to wait five minutes and try again
with the correct password.

If they have genuinely forgotten the password, that is §4, not this.

---

## 5a. "The spreadsheet could not be saved"

A pathologist tries to upload a spreadsheet and the page says it could not be
saved. Nothing they did is wrong, and **nothing is lost**: the spreadsheet that
was already in use is still in use. The laboratory can keep working on the old
file while you fix this.

What the pathologist sees is deliberately one short sentence that makes no guess
about the cause. **The cause is in the log**, and it is one of three things.
Read it first:

```bash
docker compose logs --since 30m app | grep -A5 "failed while storing it"
```

The logged message states the category in plain English:

**"...there is no free disk space left..."** — the disk holding
`/app/App_Data/spreadsheets` is full. Check from the host:

```bash
df -h /var/lib/docker      # or wherever your docker volumes live
docker compose exec app df -h /app/App_Data/spreadsheets
```

Free space (old uploads accumulate — see §8), then ask them to upload again.

**"...the account the application runs as does not have permission..."** — the
folder exists but the container cannot write into it. Check:

```bash
docker compose exec app ls -ld /app/App_Data/spreadsheets
docker compose exec app touch /app/App_Data/spreadsheets/.probe && echo writable
docker compose exec app rm -f /app/App_Data/spreadsheets/.probe
```

If `touch` fails, fix ownership or mode on the volume and restart the service.

**"...another upload of the same spreadsheet is saved at the same moment."** —
two people uploaded at once. This should no longer be possible: each upload now
writes its own uniquely named file. If you see this in the log, ask them to try
again; if it repeats without two people uploading, report it as a bug.

Keep that log output if you have to raise a bug. It names the exact file and the
underlying operating-system error.

---

## 6. The last clinical pathologist is locked out

This is the emergency. Every recovery action inside the application needs an
active clinical pathologist, so when there is none the application cannot fix
itself. You fix it from the server with a built-in command.

### 6a. First, check whether it is really a lockout

If the account is merely locked for 5 minutes (§5), wait. Only use this section
when the password is genuinely lost, or the account was disabled.

### 6b. Run the recovery command

```bash
docker compose exec -T app dotnet penicillisolver-v2.dll \
    --reset-pathologist-password --email pathologist@hospital.example
```

It will ask for a new password. Type it and press Enter:

```
New password:
Password reset for pathologist@hospital.example. The account must change it at next sign-in.
```

Notes, in order of how likely you are to need them:

- **Two ways to give it the password, and they are not interchangeable.**
  - **Piped** (recommended, works everywhere, including scripts):
    `echo 'New-Password-1!' | docker compose exec -T app dotnet penicillisolver-v2.dll --reset-pathologist-password --email ...`
  - **Typed at the prompt**: drop the `-T` and run it from a real terminal.
    With `-T` there is no terminal attached, so the prompt reads nothing and the
    command exits with `No password was supplied.` That is not a fault; it is
    the wrong form for an interactive shell.
- **The password is never typed on the command line.** It is read from the
  prompt or from a pipe, so it does not end up in shell history or in `ps`.
- **It only works on clinical pathologist accounts.** Point it at a doctor and
  it refuses, on purpose. Doctors are recovered in the application (§4).
- **The account must change the password at next sign-in.** The password you set
  is temporary by design.
- **The password must be at least 6 characters** and contain an uppercase
  letter, a lowercase letter, a digit, and a symbol. If it does not, the command
  says so and changes nothing.
- **The output is one line.** If you see a wall of SQL, you are running an older
  build; the current one logs only warnings.

### 6c. If the command says the account does not exist

```
No account is registered for pathologist@hospital.example.
```

Either the email is misspelled, or the account was deleted. If it was deleted,
the application recreates the seed account on the next restart **only if** the
seed credentials are still configured. Check:

```bash
docker compose exec app printenv | grep SeedAccounts
```

If those are set, restart and the account comes back:

```bash
docker compose restart app
```

### 6d. If you have no shell access at all

Then there is no way back short of editing the database by hand, which you
should not do. This is the one scenario the design cannot cover. Escalate to
whoever deployed the application.

---

## 7. Adding a new user

You do not create accounts. Users register themselves, and a pathologist
approves them.

1. The person opens the application and clicks **Register**.
2. They fill in their name, email, the role they want, and a password.
3. Their account is created **pending** — they can sign in but see nothing.
4. A clinical pathologist opens **User and team settings**, sets the account's
   role, and clicks **Activate**.

Until step 4 the account is useless. If someone says "I registered but I can't
see anything", that is why — it is not a fault.

---

## 8. Backups

Back up **three** things. Miss the third and everyone gets signed out on every
restart.

| What | Where | How |
|---|---|---|
| The database | the `db` service | `pg_dump` (below) |
| The spreadsheet folder | `/app/App_Data/spreadsheets` in the app container | a tar, or the volume |
| The encryption keys | `/app/.aspnet/DataProtection-Keys` | a tar, or the volume |

**The database is not a file you can copy.** It lives inside PostgreSQL, so a
`docker cp` or a copy of the volume folder while the server is running is not a
backup and may not restore. Use `pg_dump`:

```bash
docker compose exec -T db pg_dump \
    -U penicillisolver -Fc penicillisolver \
    > /backup/penicillisolver-$(date +%F).dump
```

That produces one compressed file. Check it is not empty, every time:

```bash
docker compose cp /backup/penicillisolver-$(date +%F).dump db:/tmp/check.dump
docker compose exec -T db pg_restore --list /tmp/check.dump | head
```

You should see a list of tables. **An empty file means the dump failed and you
have no backup**, so run this check before you consider the job done.

The copy is needed because `pg_restore` must seek in the file to read its index,
so it cannot read the archive from a pipe, and `/backup` on your host is not the
same directory as `/backup` inside the container. Verified: piping the archive in
fails with `did not find magic string in file header`, while the copy-and-list
above prints the table list.

If `/backup` is not writable by the container, the dump fails with
`Permission denied`. Write to a directory the container can write, or
`chmod 777 /backup`.

**The spreadsheet folder now holds one file per upload**, each named after a hash
of its own contents (`<long-hex-string>.csv` or `.xlsx`). There is no longer a
single `current-spreadsheet.csv`. Back up **the whole folder, not a named file**;
an older upload's file is what its saved abbreviation mappings describe, so
deleting one silently breaks that history.

**The key ring is the one people miss.** By default the application writes it to
`/root/.aspnet/DataProtection-Keys`, which is *not* on a volume and disappears
when the container is recreated — every restart then signs everyone out. Set
`HOME=/app` in the service environment and mount a volume at
`/app/.aspnet/DataProtection-Keys`; verified: with `HOME=/app` the keys land on
the volume, without it they land in `/root` and are lost.

The files are ordinary files, so a tar covers them:

```bash
docker run --rm \
  -v "$(basename "$PWD")_appdata:/app/App_Data:ro" \
  -v "$(basename "$PWD")_keys:/keys:ro" \
  -v /backup:/backup \
  alpine tar czf /backup/penicillisolver-files-$(date +%F).tar.gz -C / App_Data keys
```

Adjust the volume names to yours (§2). A nightly run is enough at this scale.

**A database snapshot from your platform is an acceptable second option**, and
it restores faster. Take it through the platform's volume snapshot mechanism, not
by copying files, and restore-test it once before you trust it. `pg_dump` is the
portable answer and the one to keep for off-site copies.

---

## 9. Restoring

**First the database.** The application must not be running, or it will write to a
database you are replacing.

```bash
docker compose stop app

docker compose exec -T db pg_restore \
    -U penicillisolver -d penicillisolver --clean --if-exists \
    < /backup/penicillisolver-YYYY-MM-DD.dump
```

`--clean --if-exists` drops the existing tables first, so the restore replaces the
database instead of merging into it. Verified against a real restore: the
spreadsheet row and the accounts came back.

**Then the files**, if you also restored the tar:

```bash
docker run --rm \
  -v "$(basename "$PWD")_appdata:/app/App_Data" \
  -v "$(basename "$PWD")_keys:/keys" \
  -v /backup:/backup \
  alpine sh -c 'rm -rf /app/App_Data/* /keys/* && tar xzf /backup/penicillisolver-files-YYYY-MM-DD.tar.gz -C /'
```

Then start it and check §3:

```bash
docker compose start app
```

The application applies any pending database changes on start, so there is no
separate migration step. Its start-up migration is also why **only one app
container may run**: two would both try to migrate the restore at once.

---

## 10. Updating the application

```bash
docker compose stop app
# put the new image or build in place, per your deployment
docker compose up -d app
docker compose logs --tail=50 app
```

Take a backup first (§8). If the new version fails to start, the log names the
reason, and you can roll back by restoring the backup and the previous image.

---

## 11. Things not to do

- **Do not edit the database by hand.** It is easy to corrupt, and the
  application will not warn you.
- **Do not create accounts manually.** Users register; pathologists approve.
- **Do not delete a user to "reset" them.** You lose their history, and the
  application may recreate the account on restart if it is a seed account.
- **Do not disable or demote the last clinical pathologist.** The application
  refuses this, and it is right to. If you find a way around it, you have locked
  everyone out.
- **Do not put the seed password in a chat message or a ticket.** It is a real
  credential until the account first signs in.

---

## 12. Quick reference

| I need to… | Do this |
|---|---|
| Check it is running | `docker compose ps` |
| See why it will not start | `docker compose logs --tail=100 app` |
| Restart it | `docker compose restart app` |
| Reset a user's password | Send them to a clinical pathologist (§4) |
| Unlock an account | Wait 5 minutes (§5) |
| Recover the last pathologist | `docker compose exec -T app dotnet penicillisolver-v2.dll --reset-pathologist-password --email <address>` (§6) |
| Back up | §8 |
| Restore | §9 |
| Add a user | They register; a pathologist activates (§7) |

---

## 13. What this application does not have

So you are not surprised, and so you can answer honestly when asked:

- **No email.** No password-reset links, no notifications. Nothing to configure.
- **No single sign-on.** Accounts are local to the application.
- **No self-service password reset.** A pathologist resets, in person.
- **No audit log beyond the last password reset.** The settings page shows who
  last reset an account's password and when. It does not keep a history.
- **No health-check endpoint.** Use the login page (§3).
- **No multi-factor authentication.**

If any of these is a requirement for your site, raise it before go-live, not
after.
