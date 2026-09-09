# Deployment

Projekt Loke is deployed as a generated static site served by the minimal native .NET host behind Caddy.

## Production layout

- `/srv/projektloke/app` — self-contained native ASP.NET host.
- `/srv/projektloke/wwwroot/` — generated static site.
- `projektloke.service` — systemd service bound to `127.0.0.1:5080` only.
- Caddy — public HTTP/HTTPS endpoint and automatic TLS for `projektloke.dk` and `www.projektloke.dk`.

The server does not need the .NET runtime or SDK.

## First deployment

Create an Ubuntu Droplet with an SSH key, then from the repository root:

```sh
./deploy/provision.sh <droplet-ip>
./deploy/deploy.sh <droplet-ip>
./deploy/check.sh <droplet-ip>
```

Create DNS A records (and AAAA records when IPv6 is enabled) for both `projektloke.dk` and `www.projektloke.dk`, and make sure the registrar delegates the domain to the authoritative DNS provider, before expecting Caddy to obtain certificates.

## Routine deployment

```sh
./deploy/deploy.sh <droplet-ip>
```

The deploy script rebuilds `wwwroot`, publishes `site/app.cs` as a self-contained Linux x64 native executable, uploads the release, replaces `/srv/projektloke`, and restarts `projektloke.service`.

`check.sh` verifies that the systemd service is active and that the application returns HTTP 200 on its localhost-only endpoint.

## Server exposure

Only ports 22, 80, and 443 are opened by the bootstrap script. The .NET application listens only on localhost and is not directly exposed to the Internet.
