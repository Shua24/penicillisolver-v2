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

This is the shape the commands in this manual were verified against. Compare it
with yours; the important parts are the three volumes and `HOME`.

```yaml
services:
  app:
    image: penicillisolver:latest
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://0.0.0.0:8080
      # Keeps the data-protection key ring on the mounted volume instead of
      # /root/.aspnet, which is not persisted.
      HOME: /app
      Database__Provider: Sqlite
      ConnectionStrings__DefaultConnection: "Data Source=/data/penicillisolver.db"
      SeedAccounts__ClinicalPathologist__Email: pathologist@hospital.example
      SeedAccounts__ClinicalPathologist__Password: "change-me-before-first-start"
    volumes:
      - data:/data
      - appdata:/app/App_Data
      - keys:/app/.aspnet/DataProtection-Keys

volumes:
  data:
  appdata:
  keys:
```

**If your deployment uses PostgreSQL instead of SQLite**, change two lines:

```yaml
      Database__Provider: Postgres
      ConnectionStrings__DefaultConnection: "Host=db;Database=penicillisolver;Username=penicillisolver;Password=..."
```

and add a `db` service with a volume of its own. **That variant has not been
verified** — the application's database migrations are currently SQLite-shaped
and it will refuse to start against PostgreSQL. Do not switch to PostgreSQL
until that is fixed; see `DEPLOYMENT.md` §5.

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

| What | Where | Why |
|---|---|---|
| The database | the `data` volume | all accounts and settings |
| The spreadsheet | `/app/App_Data/spreadsheets` in the container | the uploaded antibiogram |
| The encryption keys | `/app/.aspnet/DataProtection-Keys` | keeps sessions valid across restarts |

**The key ring is the one people miss.** By default the application writes it to
`/root/.aspnet/DataProtection-Keys`, which is *not* on a volume and disappears
when the container is recreated — every restart then signs everyone out. Set
`HOME=/app` in the service environment and mount a volume at
`/app/.aspnet/DataProtection-Keys`; verified: with `HOME=/app` the keys land on
the volume, without it they land in `/root` and are lost.

A nightly copy is enough at this scale. Stop the service first so the database
file is consistent:

```bash
docker compose stop app

docker run --rm \
  -v "$(basename "$PWD")_data:/data:ro" \
  -v /backup:/backup \
  alpine tar czf /backup/penicillisolver-$(date +%F).tar.gz -C /data .

docker compose start app
```

Adjust the volume name to yours (§2). **The container writes as root**, so
`/backup` must be writable by root — a directory owned by your own user with
mode `755` will fail with `Permission denied`. Either `chmod 777` the backup
directory, or add `--user "$(id -u):$(id -g)"` to the `docker run` and make sure
the volume's files are readable by that user.

Verify the archive is not empty:

```bash
tar tzf /backup/penicillisolver-$(date +%F).tar.gz | head
```

You should see `penicillisolver.db` and its `-wal` / `-shm` companions listed.
An empty listing means you backed up the wrong volume.

---

## 9. Restoring

```bash
docker compose stop app

docker run --rm \
  -v "$(basename "$PWD")_data:/data" \
  -v /backup:/backup \
  alpine sh -c 'rm -rf /data/* && tar xzf /backup/penicillisolver-YYYY-MM-DD.tar.gz -C /data'

docker compose start app
```

Then check §3. The application applies any pending database changes on start, so
there is no separate migration step.

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
