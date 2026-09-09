# Projekt Loke — site

A small static site generated before deployment and served by a minimal ASP.NET Core host.

No `.csproj` and no site framework are required. The build and server are .NET file-based apps.

## Layout

- `content/site.json` — site-wide metadata and navigation.
- `content/pages.json` — manifest for manually authored structural pages.
- `content/pages/*.html` — authored HTML fragments.
- `content/data/sources.json` — stable source records used by published claims.
- `content/data/actors.json` — people, parties and later organizations/media actors.
- `content/data/proposals.json` — political cases and vote metadata.
- `content/data/events.json` — dated events used to generate the timeline.
- `content/data/relationships.json` — sourced links between actors, political cases and external networks.
- `content/data/media.json` — media/platform records and recurring frames.
- `content/data/statements.json` — short parliamentary excerpts with context, themes and position.
- `templates/layout.html` — shared document shell.
- `assets/` — authored CSS and progressively enhanced JavaScript.
- `build.cs` — static-site generator and evidence-model validator.
- `app.cs` — minimal static-file server.
- `wwwroot/` — generated output; safe to delete and rebuild.

## Build

From this directory:

```sh
dotnet run build.cs
```

The generator validates routes and cross-references, recreates `wwwroot/`, copies assets, renders structural pages, and generates case pages, actor profiles, the timeline and the source register from typed JSON data.

## Run locally

```sh
dotnet run app.cs
```

The server serves only the generated `wwwroot/` tree, resolves clean trailing-slash routes to their generated `index.html`, and returns the generated `404.html` for unmatched routes.

## Design rules

1. Generated HTML must remain useful without JavaScript.
2. Client-side code is progressive enhancement only.
3. Repeated editorial structures become typed data and generated pages rather than duplicated markup.
4. Source citations live with content data so factual pages cannot easily become detached from their evidence.
5. Build-time validation should catch broken relations before publication.
6. Keep dependencies at zero unless a dependency clearly removes more complexity than it adds.

## Current evidence model

The published model now covers political proposals, votes, actors, organizations, media/platform records, network relationships, contextualized parliamentary statements and timeline events. The same records generate human-readable pages and `/data/index.json`.

Build validation fails on duplicate routes/IDs, unknown source or actor references, and links from events, relationships or statements to routes that do not exist.

Likely next extensions are archived source URLs, correction history, richer source provenance and optional client-side filtering/search over the generated public JSON index.

## Relationship records

Network claims are stored separately from narrative prose. Each relationship has a date, type, source actor, target, summary and one or more source IDs. Targets can be another actor, a generated route such as a parliamentary case, or a deliberately unresolved external label when the primary source does not identify the other party precisely.

The build validates actor/source IDs and generated-route targets. This is intentional: a documented contact must not silently become a stronger claim about coordination or control.
