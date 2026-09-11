# Deployment

Projekt Loke is deployed as a generated static site served by the minimal native .NET host behind Caddy.

## Production layout

- `/srv/projektloke/app` — self-contained native ASP.NET host.
- `/srv/projektloke/wwwroot/` — generated static site.
- `projektloke.service` — sandboxed systemd service bound to `127.0.0.1:5080` only.
- Caddy — public HTTP/HTTPS endpoint and automatic TLS for `projektloke.dk` and `www.projektloke.dk`.
- `projektloke-deploy` — non-root SSH deployment account. It may update `/srv/projektloke` and start/stop/restart only `projektloke.service` through sudo.

The server does not need the .NET runtime or SDK.

## First deployment

Create an Ubuntu Droplet with an SSH key, then from the repository root:

```sh
./deploy/provision.sh <droplet-ip>
./deploy/deploy.sh <droplet-ip>
./deploy/check.sh <droplet-ip>
```

`provision.sh` initially connects as root, creates the dedicated deployment account using the existing root authorized keys, applies the SSH/Caddy/systemd hardening, enables unattended upgrades, creates a 1 GiB swap file, and disables future root SSH login.

Create DNS A records (and AAAA records when IPv6 is enabled) for both `projektloke.dk` and `www.projektloke.dk`, and make sure the registrar delegates the domain to the authoritative DNS provider, before expecting Caddy to obtain certificates.

## Routine deployment

```sh
./deploy/deploy.sh <droplet-ip>
```

The deploy script connects as `projektloke-deploy`, rebuilds `wwwroot`, runs the file-based C# release verifier (`dotnet run tests/verify_release.cs`), publishes `site/app.cs` as a self-contained Linux x64 native executable, uploads the release, replaces `/srv/projektloke`, and restarts `projektloke.service`. A failed release verification stops deployment before anything is uploaded.

`check.sh` verifies that the systemd service is active and that the application returns HTTP 200 on its localhost-only endpoint.

## Security posture

- UFW defaults to deny and exposes only SSH, HTTP and HTTPS.
- SSH is key-only; direct root login, X11 forwarding, TCP forwarding and tunnels are disabled.
- Caddy requires matching TLS SNI/Host, accepts only GET/HEAD, applies request timeouts/header limits, removes server-identification headers, and sends CSP/HSTS/browser-hardening headers.
- The .NET process has no Linux capabilities, is restricted to localhost networking, has a 256 MiB memory ceiling, and uses additional systemd filesystem/kernel/process isolation.
- Caddy runs with only `CAP_NET_BIND_SERVICE` rather than the broader package defaults.
- A 1 GiB swap file is provisioned for short-lived memory pressure.
- Unattended upgrades are enabled; normal host maintenance should still periodically confirm pending upgrades and reboot requirements.

Only ports 22, 80, and 443 are opened by the bootstrap script. The .NET application listens only on localhost and is not directly exposed to the Internet.
