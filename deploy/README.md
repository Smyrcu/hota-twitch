# Deployment

Backend runs as a self-contained linux-x64 build on a single Ubuntu server behind Caddy,
supervised by systemd. See `docs/design.md` section 4
for the design rationale.

## Layout on the server

```
/opt/hota-twitch/releases/<sha>/     one directory per published build
/opt/hota-twitch/current             symlink to the release currently in service
/var/lib/hota-twitch/hota.db         SQLite database, owner hota
/etc/hota-twitch/env                 secrets, mode 0600, owner hota
/etc/systemd/system/hota-twitch.service
/etc/caddy/Caddyfile
```

Two local users: `hota` (system account, no login, owns the data directory and runs the
service) and `deploy` (SSH key login only, owns the release directories, used by GitHub
Actions).

## Bootstrap

Run once per server, and safe to re-run — it only creates what is missing and never touches
an existing `/etc/hota-twitch/env` or an existing `authorized_keys`.

```bash
scp -r deploy root@<host>:/root/hota-twitch-deploy
ssh root@<host> '/root/hota-twitch-deploy/bootstrap.sh'
```

After the first run:

1. Edit `/etc/hota-twitch/env` on the server and fill in `TWITCH_CLIENT_ID` and
   `TWITCH_EXTENSION_SECRET`.
2. Add the `deploy` user's public key to `/home/deploy/.ssh/authorized_keys` (see "Deploy key"
   below).
3. Point `DNS` at the server and make sure Cloudflare's SSL mode is "Full" — Caddy cannot
   obtain a certificate through the proxy otherwise.

The `hota-twitch` unit is enabled but has nothing to run until the first release lands, so
`systemctl status hota-twitch` reporting a failed start is expected until then.

## How a release lands

`.github/workflows/backend.yml` does, on every push to `main`
that touches `backend/`:

1. Restore, build, test, `dotnet publish` a self-contained `linux-x64` build.
2. Copy the output to `/opt/hota-twitch/releases/<git-sha>` on the server as `deploy`.
3. `ln -sfn /opt/hota-twitch/releases/<git-sha> /opt/hota-twitch/current`.
4. `sudo systemctl restart hota-twitch` (the only command `deploy` may run under sudo,
   alongside `sudo systemctl status hota-twitch`).
5. Verify `https://hota.smyrcu.net/health`.

## Rolling back

Releases are kept side by side under `releases/`, so rolling back does not need a new build:

```bash
ssh deploy@<host>
ln -sfn /opt/hota-twitch/releases/<previous-sha> /opt/hota-twitch/current
sudo systemctl restart hota-twitch
sudo systemctl status hota-twitch
```

Old release directories are not pruned automatically; remove ones you no longer want to keep
as a rollback target with `rm -rf /opt/hota-twitch/releases/<sha>` once its `current` symlink
no longer points to it.

## Where secrets live

- `/etc/hota-twitch/env` on the server: `TWITCH_CLIENT_ID`, `TWITCH_EXTENSION_SECRET`,
  `ASPNETCORE_URLS`, `ConnectionStrings__Default`. Mode 0600, owner `hota`. Never committed —
  `env.example` in this directory documents the keys with empty/placeholder values.
- GitHub Actions secrets on `Smyrcu/hota-twitch`: `DEPLOY_HOST`, `DEPLOY_USER`,
  `DEPLOY_SSH_KEY`, `DEPLOY_KNOWN_HOSTS`. Consumed by `.github/workflows/backend.yml` to reach
  the server as `deploy`.

## Deploy key

The `DEPLOY_SSH_KEY` GitHub secret is a dedicated ed25519 keypair, not reused anywhere else.
To rotate it:

```bash
tmp=$(mktemp -d)
ssh-keygen -t ed25519 -N '' -C 'github-actions-deploy' -f "${tmp}/deploy_key"

ssh root@<host> "cat >> /home/deploy/.ssh/authorized_keys" < "${tmp}/deploy_key.pub"

gh secret set DEPLOY_SSH_KEY -R Smyrcu/hota-twitch < "${tmp}/deploy_key"

rm -rf "${tmp}"
```

Then remove the old public key line from `/home/deploy/.ssh/authorized_keys` on the server
once the next workflow run has confirmed the new key works.

`DEPLOY_HOST`, `DEPLOY_USER` and `DEPLOY_KNOWN_HOSTS` do not need to change when rotating the
key; `DEPLOY_KNOWN_HOSTS` only needs updating if the server's host key changes
(`ssh-keyscan -t ed25519 <host>`).
