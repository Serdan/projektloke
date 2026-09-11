# Projekt Loke

Projekt Loke documents the rise of bigotry in Denmark, with a primary focus on transphobia: its political organization, rhetoric, media treatment, activist networks, policy proposals, and international connections.

## Structure

- `site/` — public static website, generator and curated publishable evidence data.
- `research/` — research workspace. `workbench/` is private/unpublished; `published/` is deliberately public-safe research.
- `sources/` — source acquisition and preservation. The canonical public source metadata remains in `site/content/data/sources.json`.
- `correspondence/` — outreach, drafts, sent messages and replies. Private by default.
- `project/` — methodology, editorial policy, roadmap and administration; `private/` is for non-public project notes.

## Publication boundary

This GitHub repository is public. Working research, correspondence, archived source files and private project notes are ignored by Git by default. Moving material into a tracked/public area is an explicit publication decision.

## Principles

- Prefer primary sources wherever possible.
- Distinguish documented fact, synthesis, interpretation and open questions.
- Preserve enough sourcing to make important claims independently verifiable.
- Track corrections and uncertainty rather than silently smoothing over them.
- Treat organized opposition to minority rights as a subject for investigation, not merely as one side of an abstract culture-war debate.

## Build and verify

From `site/`:

```sh
dotnet run build.cs
dotnet run tests/verify_release.cs
```
