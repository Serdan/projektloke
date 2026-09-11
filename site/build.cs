#:property PublishAot=false

using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

var root = Directory.GetCurrentDirectory();
var contentRoot = Path.Combine(root, "content");
var dataRoot = Path.Combine(contentRoot, "data");
var outputRoot = Path.Combine(root, "wwwroot");
var templatePath = Path.Combine(root, "templates", "layout.html");
var assetsRoot = Path.Combine(root, "assets");

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    PropertyNameCaseInsensitive = true
};

T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), jsonOptions)
    ?? throw new InvalidOperationException($"Could not read {Path.GetRelativePath(root, path)}.");

var site = Read<Site>(Path.Combine(contentRoot, "site.json"));
var pages = Read<Page[]>(Path.Combine(contentRoot, "pages.json"));
var sources = Read<Source[]>(Path.Combine(dataRoot, "sources.json"));
var actors = Read<Actor[]>(Path.Combine(dataRoot, "actors.json"));
var proposals = Read<Proposal[]>(Path.Combine(dataRoot, "proposals.json"));
var events = Read<TimelineEvent[]>(Path.Combine(dataRoot, "events.json"));
var relationships = Read<Relationship[]>(Path.Combine(dataRoot, "relationships.json"));
var media = Read<MediaItem[]>(Path.Combine(dataRoot, "media.json"));
var materials = Read<Material[]>(Path.Combine(dataRoot, "materials.json"));
var materialLinks = Read<MaterialLink[]>(Path.Combine(dataRoot, "material-links.json"));
var statements = Read<Statement[]>(Path.Combine(dataRoot, "statements.json"));
var healthcare = Read<HealthcareRecord[]>(Path.Combine(dataRoot, "healthcare.json"));
var youthTreatmentStats = Read<YouthTreatmentStat[]>(Path.Combine(dataRoot, "youth-treatment.json"));
var layout = File.ReadAllText(templatePath);
var themeAliases = Read<Dictionary<string, string>>(Path.Combine(dataRoot, "theme-aliases.json"));
var pageUpdates = Read<Dictionary<string, string>>(Path.Combine(contentRoot, "page-updates.json"));
string[] NormalizeThemes(string[] themes) => themes
    .Select(theme => themeAliases.GetValueOrDefault(theme, theme))
    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
statements = statements.Select(item => item with { Themes = NormalizeThemes(item.Themes) }).ToArray();
media = media.Select(item => item with { Themes = NormalizeThemes(item.Themes) }).ToArray();

var sourceById = sources.ToDictionary(source => source.Id, StringComparer.OrdinalIgnoreCase);
var actorById = actors.ToDictionary(actor => actor.Id, StringComparer.OrdinalIgnoreCase);
var materialById = materials.ToDictionary(material => material.Id, StringComparer.OrdinalIgnoreCase);
var allRoutes = pages.Select(page => page.Route)
    .Concat(proposals.Select(proposal => proposal.Route))
    .Concat(actors.Select(actor => actor.Route))
    .Concat(materials.Select(material => material.Route))
    .Append("/kilder/")
    .ToArray();

Validate(site, pages, sources, actors, proposals, events, relationships, media, materials, materialLinks, statements, healthcare, youthTreatmentStats, sourceById, actorById, materialById, allRoutes);

foreach (var route in allRoutes)
    if (!pageUpdates.TryGetValue(route, out var date) || !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        throw new InvalidOperationException($"Missing or invalid update date for {route}.");

if (Directory.Exists(outputRoot))
    Directory.Delete(outputRoot, recursive: true);

Directory.CreateDirectory(outputRoot);
CopyDirectory(assetsRoot, Path.Combine(outputRoot, "assets"));

var generated = 0;

foreach (var page in pages)
{
    var bodyPath = Path.Combine(contentRoot, "pages", page.Source);
    var body = File.ReadAllText(bodyPath)
        .Replace("{{proposalList}}", RenderProposalList(proposals, chronological: page.Route == "/"))
        .Replace("{{progression}}", RenderProgression(proposals))
        .Replace("{{politicsPrehistorySources}}", RenderInlineSources(["ft-b80-2021", "ft-inu-minors-legal-gender-2023", "ft-inu-minors-samraad-2023", "ft-s9-emrk-2023"], sourceById))
        .Replace("{{currentGovernmentSources}}", RenderInlineSources(["regeringen-mf3-2026", "firkloever-regeringsgrundlag-2026"], sourceById))
        .Replace("{{lgbtActionPlanSources}}", RenderInlineSources(["lgbt-action-plan-2026-2029", "lgbt-action-plan-funding-2026", "firkloever-regeringsgrundlag-2026"], sourceById))
        .Replace("{{minorLegalGenderSources}}", RenderInlineSources(["retsinfo-cpr-law-current-2026", "cpr-minor-legal-gender-guidance-current", "ft-s9-emrk-2023", "ft-inu94-minor-legal-gender-law-2024", "sst-koensidentitet-revision-2025"], sourceById))
        .Replace("{{minorLegalGenderSnapshotSources}}", RenderInlineSources(["jp-minor-legal-gender-2024"], sourceById))
        .Replace("{{b72Sources}}", RenderInlineSources(["ft-b72-proposal"], sourceById))
        .Replace("{{educationSources}}", RenderInlineSources(["emu-lgbt-fagene", "normstormerne-elevundervisning", "ft-buu107-normkritik-2022", "ft-buu346-normstormerne-2022", "ft-s188-normstormerne-2023", "ft-s430-gender-education-2023", "ft-f1-background-2024", "ft-b145-background", "vive-controversial-topics-2025"], sourceById))
        .Replace("{{sportSources}}", RenderInlineSources(["dbu-gender-hearing-2023", "dbu-gender-board-2023", "dbufyn-gender-report-2024", "dbujylland-self-id-reject-2025", "dbusjaelland-gender-rules-2025", "dbusjaelland-gender-dispensation", "ft-kuu-d-dbu-2024"], sourceById))
        .Replace("{{facebookHostilitySources}}", RenderInlineSources(["trygfonden-facebook-hate-2025"], sourceById))
        .Replace("{{b47MediaSources}}", RenderInlineSources(["ft-b47-proposal", "ft-b47-background"], sourceById))
        .Replace("{{cassMediaPilotSources}}", RenderInlineSources(["cass-final-report-2024", "dr-genstart-koentrovers-2024", "rug-kd-cass-2024", "aau-information-youth-treatment-2024", "york-puberty-suppression-review-2024", "york-adolescent-hormones-review-2024"], sourceById))
        .Replace("{{drrTransmissionSources}}", RenderInlineSources(["ft-liu-bilag64-drr", "ft-liu-spm21-drr", "ft-liu-bilag66-fstb"], sourceById))
        .Replace("{{folkemoedeSources}}", RenderInlineSources(["civilstyrelsen-folkemoede-2024", "civilstyrelsen-folkemoede-2025"], sourceById))
        .Replace("{{timeline}}", RenderTimeline(events, actorById, sourceById))
        .Replace("{{actorList}}", RenderActorList(actors))
        .Replace("{{materialList}}", RenderMaterialList(materials))
        .Replace("{{materialCount}}", materials.Length.ToString(CultureInfo.InvariantCulture))
        .Replace("{{relationships}}", RenderRelationships(relationships, actorById, sourceById))
        .Replace("{{mediaList}}", RenderMediaList(media, actorById, sourceById))
        .Replace("{{mediaThemes}}", RenderMediaThemes(media))
        .Replace("{{mediaCount}}", media.Length.ToString(CultureInfo.InvariantCulture))
        .Replace("{{statementList}}", RenderStatementList(statements, actorById, sourceById))
        .Replace("{{statementThemes}}", RenderStatementThemes(statements))
        .Replace("{{statementCount}}", statements.Length.ToString(CultureInfo.InvariantCulture))
        .Replace("{{relationshipCount}}", relationships.Length.ToString(CultureInfo.InvariantCulture))
        .Replace("{{sourceCount}}", sources.Length.ToString(CultureInfo.InvariantCulture))
        .Replace("{{healthcareTheory}}", RenderHealthcare(healthcare, "teori", sourceById))
        .Replace("{{healthcarePractice}}", RenderHealthcare(healthcare, "praksis", sourceById))
        .Replace("{{healthcareQuestions}}", RenderHealthcare(healthcare, "spørgsmål", sourceById))
        .Replace("{{healthcareSources}}", RenderSources(healthcare.SelectMany(item => item.SourceIds).Distinct(StringComparer.OrdinalIgnoreCase), sourceById))
        .Replace("{{youthTreatmentStats}}", RenderYouthTreatmentStats(youthTreatmentStats, sourceById))
        .Replace("{{proposalCount}}", proposals.Length.ToString(CultureInfo.InvariantCulture));

    WritePage(page.Route, page.Title, page.Description, body);
}

foreach (var proposal in proposals)
    WritePage(proposal.Route, $"{proposal.Code}: {proposal.Title}", proposal.Summary, RenderProposal(proposal, statements, materialById, actorById, sourceById));

foreach (var actor in actors)
    WritePage(actor.Route, actor.Name, actor.Summary, RenderActor(actor, proposals, events, relationships, media, statements, actorById, sourceById));

foreach (var material in materials)
    WritePage(material.Route, material.Title, material.Summary, RenderMaterial(material, materialLinks, proposals, media, relationships, events, statements, healthcare, materialById, actorById, sourceById));

WritePage(
    "/kilder/",
    "Kilder",
    "Projekt Lokes offentlige kilderegister.",
    RenderSourceIndex(sources));

var publicDataRoot = Path.Combine(outputRoot, "data");
Directory.CreateDirectory(publicDataRoot);
var publicIndex = new
{
    schemaVersion = 13,
    themeAliases,
    pageUpdates,
    proposals = proposals.OrderByDescending(proposal => proposal.Introduced).Select(proposal => new
    {
        proposal.Id, proposal.Code, proposal.Title, proposal.Route, proposal.Session, proposal.Introduced, proposal.FirstReading,
        proposal.FinalVote, proposal.Status, proposal.ProposerIds, proposal.SupportPartyIds, proposal.VoteFor, proposal.VoteAgainst,
        proposal.VoteAbstain, proposal.VoteNote, proposal.Topics, proposal.SourceIds, proposal.MaterialIds
    }),
    events = events.OrderByDescending(item => item.Date).Select(item => new
    {
        item.Id, item.Date, item.Kind, item.Title, item.Summary, item.ActorIds, item.RelatedRoute, item.SourceIds, item.MaterialIds
    }),
    actors = actors.OrderBy(actor => actor.Name).Select(actor => new
    {
        actor.Id, actor.Name, actor.Kind, actor.ShortName, actor.Affiliation, actor.Route, actor.Summary, actor.Profile, actor.ProfileSourceIds, actor.SourceIds
    }),
    media = media.OrderByDescending(item => item.Date),
    materials = materials.OrderByDescending(item => item.Date),
    materialLinks,
    statements = statements.OrderByDescending(item => item.Date),
    relationships = relationships.OrderByDescending(item => item.Date),
    healthcare,
    youthTreatmentStats,
    sources = sources.OrderByDescending(source => source.Published ?? "")
};
File.WriteAllText(Path.Combine(publicDataRoot, "index.json"), JsonSerializer.Serialize(publicIndex, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));

Console.WriteLine($"Built {generated} pages from {proposals.Length} proposals, {events.Length} events, {media.Length} media records, {materials.Length} materials, {materialLinks.Length} material links, {statements.Length} statements, {relationships.Length} relationships, {actors.Length} actors and {sources.Length} sources.");

void WritePage(string route, string pageTitle, string description, string body)
{
    var nav = RenderNavigation(site.Navigation, route);
    var title = route == "/" ? site.Title : $"{pageTitle} · {site.Title}";

    var html = layout
        .Replace("{{language}}", Encode(site.Language))
        .Replace("{{pageClass}}", route == "/" ? "home-page" : "inner-page")
        .Replace("{{pageUpdated}}", pageUpdates.TryGetValue(route, out var updated)
            ? $"<p class=\"page-updated\">Siden opdateret <time datetime=\"{Encode(updated)}\">{FormatDate(updated)}</time> · <a href=\"/rettelser/\">Rettelser og kontakt</a></p>" : "")
        .Replace("{{title}}", Encode(title))
        .Replace("{{description}}", Encode(description))
        .Replace("{{siteTitle}}", Encode(site.Title))
        .Replace("{{navigation}}", nav)
        .Replace("{{content}}", body)
        .Replace("{{year}}", DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture));

    var outputPath = ResolveOutputPath(outputRoot, route);
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllText(outputPath, html);
    Console.WriteLine($"{route} -> {Path.GetRelativePath(root, outputPath)}");
    generated++;
}

static void Validate(
    Site site,
    Page[] pages,
    Source[] sources,
    Actor[] actors,
    Proposal[] proposals,
    TimelineEvent[] events,
    Relationship[] relationships,
    MediaItem[] media,
    Material[] materials,
    MaterialLink[] materialLinks,
    Statement[] statements,
    HealthcareRecord[] healthcare,
    YouthTreatmentStat[] youthTreatmentStats,
    IReadOnlyDictionary<string, Source> sourceById,
    IReadOnlyDictionary<string, Actor> actorById,
    IReadOnlyDictionary<string, Material> materialById,
    string[] allRoutes)
{
    if (string.IsNullOrWhiteSpace(site.Title))
        throw new InvalidOperationException("Site title is required.");

    RequireUnique(pages.Select(page => page.Route), "manual page route");
    RequireUnique(sources.Select(source => source.Id), "source id");
    RequireUnique(actors.Select(actor => actor.Id), "actor id");
    RequireUnique(proposals.Select(proposal => proposal.Id), "proposal id");
    RequireUnique(events.Select(item => item.Id), "event id");
    RequireUnique(relationships.Select(item => item.Id), "relationship id");
    RequireUnique(media.Select(item => item.Id), "media id");
    RequireUnique(materials.Select(item => item.Id), "material id");
    RequireUnique(materialLinks.Select(item => item.Id), "material link id");
    RequireUnique(statements.Select(item => item.Id), "statement id");
    RequireUnique(healthcare.Select(item => item.Id), "healthcare id");
    RequireUnique(allRoutes, "generated route");

    foreach (var route in allRoutes)
    {
        if (!route.StartsWith('/'))
            throw new InvalidOperationException($"Route must start with '/': {route}");

        if (route != "/" && !route.EndsWith('/') && !route.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Route must end with '/' or '.html': {route}");
    }

    foreach (var actor in actors)
    {
        RequireSources($"actor {actor.Id}", actor.SourceIds, sourceById);
        RequireSources($"actor profile {actor.Id}", actor.ProfileSourceIds ?? [], sourceById);
    }

    foreach (var proposal in proposals)
    {
        RequireSources($"proposal {proposal.Id}", proposal.SourceIds, sourceById);
        RequireActors($"proposal {proposal.Id}", proposal.ProposerIds, actorById);
        RequireActors($"proposal {proposal.Id}", proposal.SupportPartyIds, actorById);
        foreach (var materialId in proposal.MaterialIds ?? [])
            if (!materialById.ContainsKey(materialId))
                throw new InvalidOperationException($"Proposal {proposal.Id} points to unknown material {materialId}.");
    }

    foreach (var item in events)
    {
        RequireSources($"event {item.Id}", item.SourceIds, sourceById);
        RequireActors($"event {item.Id}", item.ActorIds, actorById);
        foreach (var materialId in item.MaterialIds ?? [])
            if (!materialById.ContainsKey(materialId))
                throw new InvalidOperationException($"Event {item.Id} points to unknown material {materialId}.");

        var target = item.RelatedRoute.Split('#', 2);
        if (!allRoutes.Contains(target[0], StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Event {item.Id} points to unknown route {item.RelatedRoute}.");
        if (target.Length == 2 && !(
            target[0] == "/medier/" && media.Any(record => "medie-" + record.Id == target[1]) ||
            target[0] == "/netvaerk/" && relationships.Any(record => "relation-" + record.Id == target[1])))
            throw new InvalidOperationException($"Event {item.Id} points to unknown record {item.RelatedRoute}.");
    }

    foreach (var item in media)
    {
        RequireSources($"media {item.Id}", item.SourceIds, sourceById);
        if (item.CorpusRole is not ("aktørspor" or "politisk kildegrundlag" or "struktureret prøve"))
            throw new InvalidOperationException($"Media item {item.Id} has unknown corpus role {item.CorpusRole}.");
        if (item.AuthorActorId is not null)
            RequireActors($"media {item.Id}", [item.AuthorActorId], actorById);
        if (item.OutletActorId is not null)
            RequireActors($"media {item.Id}", [item.OutletActorId], actorById);
        if (item.OutletActorId is null && string.IsNullOrWhiteSpace(item.OutletLabel))
            throw new InvalidOperationException($"Media item {item.Id} has no outlet.");
        foreach (var materialId in item.MaterialIds ?? [])
            if (!materialById.ContainsKey(materialId))
                throw new InvalidOperationException($"Media item {item.Id} points to unknown material {materialId}.");
    }

    foreach (var material in materials)
    {
        RequireSources($"material {material.Id}", material.SourceIds, sourceById);
        if (!material.Route.StartsWith("/materiale/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Material {material.Id} has invalid route {material.Route}.");
        foreach (var relatedId in material.RelatedMaterialIds ?? [])
        {
            if (!materialById.ContainsKey(relatedId))
                throw new InvalidOperationException($"Material {material.Id} points to unknown related material {relatedId}.");
            if (string.Equals(relatedId, material.Id, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Material {material.Id} cannot relate to itself.");
        }
    }

    foreach (var link in materialLinks)
    {
        if (!materialById.ContainsKey(link.MaterialId))
            throw new InvalidOperationException($"Material link {link.Id} points to unknown material {link.MaterialId}.");
        if (link.Category is not ("critique" or "rebuttal" or "response"))
            throw new InvalidOperationException($"Material link {link.Id} has unknown category {link.Category}.");
        RequireSources($"material link {link.Id}", link.SourceIds, sourceById);
    }

    foreach (var statement in statements)
    {
        RequireSources($"statement {statement.Id}", statement.SourceIds, sourceById);
        RequireActors($"statement {statement.Id}", [statement.ActorId], actorById);
        foreach (var materialId in statement.MaterialIds ?? [])
            if (!materialById.ContainsKey(materialId))
                throw new InvalidOperationException($"Statement {statement.Id} points to unknown material {materialId}.");
        if (actorById[statement.ActorId].Kind == "person" && string.IsNullOrWhiteSpace(statement.Affiliation))
            throw new InvalidOperationException($"Statement {statement.Id} needs an affiliation at the time of the statement.");
        if (!allRoutes.Contains(statement.RelatedRoute, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Statement {statement.Id} points to unknown route {statement.RelatedRoute}.");
    }

    foreach (var relation in relationships)
    {
        RequireSources($"relationship {relation.Id}", relation.SourceIds, sourceById);
        RequireActors($"relationship {relation.Id}", [relation.FromActorId], actorById);
        if (relation.ToActorId is not null)
            RequireActors($"relationship {relation.Id}", [relation.ToActorId], actorById);
        if (relation.ToRoute is not null && !allRoutes.Contains(relation.ToRoute, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Relationship {relation.Id} points to unknown route {relation.ToRoute}.");
        if (relation.ToActorId is null && relation.ToRoute is null && string.IsNullOrWhiteSpace(relation.ToLabel))
            throw new InvalidOperationException($"Relationship {relation.Id} has no target.");
    }

    foreach (var item in healthcare)
    {
        if (item.Track is not ("teori" or "praksis" or "spørgsmål"))
            throw new InvalidOperationException($"Healthcare item {item.Id} has unknown track {item.Track}.");
        RequireSources($"healthcare {item.Id}", item.SourceIds, sourceById);
        foreach (var materialId in item.MaterialIds ?? [])
            if (!materialById.ContainsKey(materialId))
                throw new InvalidOperationException($"Healthcare item {item.Id} points to unknown material {materialId}.");
    }

    foreach (var item in youthTreatmentStats)
    {
        if (item.Stages.Length == 0)
            throw new InvalidOperationException($"Youth treatment stat {item.Year} has no stages.");
        RequireSources($"youth treatment stat {item.Year}", item.SourceIds, sourceById);
    }
}

static void RequireUnique(IEnumerable<string> values, string label)
{
    var duplicate = values
        .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
        .FirstOrDefault(group => group.Count() > 1);

    if (duplicate is not null)
        throw new InvalidOperationException($"Duplicate {label}: {duplicate.Key}");
}

static void RequireSources(string owner, IEnumerable<string> ids, IReadOnlyDictionary<string, Source> sourceById)
{
    foreach (var id in ids)
        if (!sourceById.ContainsKey(id))
            throw new InvalidOperationException($"Unknown source '{id}' referenced by {owner}.");
}

static void RequireActors(string owner, IEnumerable<string> ids, IReadOnlyDictionary<string, Actor> actorById)
{
    foreach (var id in ids)
        if (!actorById.ContainsKey(id))
            throw new InvalidOperationException($"Unknown actor '{id}' referenced by {owner}.");
}

static string RenderProposalList(IEnumerable<Proposal> proposals, bool chronological = false) => string.Join(
    Environment.NewLine,
    (chronological ? proposals.OrderBy(proposal => proposal.Introduced) : proposals.OrderByDescending(proposal => proposal.Introduced)).Select(proposal =>
    {
        var progression = proposal.FinalVote is null
            ? $"Fremsat {FormatDate(proposal.Introduced)} · {Encode(proposal.Status)}"
            : $"Fremsat {FormatDate(proposal.Introduced)} · Endelig afstemning {FormatDate(proposal.FinalVote)}";

        return $"""
      <article class="evidence-card">
        <div class="evidence-card-topline">
          <span class="case-code">{Encode(proposal.Code)}</span>
          <span class="status-tag">{Encode(proposal.Status)}</span>
        </div>
        <h3><a href="{Encode(proposal.Route)}">{Encode(proposal.Title)}</a></h3>
        <p>{Encode(proposal.Summary)}</p>
        <p class="evidence-meta">{progression}</p>
      </article>
    """;
    }));

static string RenderProgression(IEnumerable<Proposal> proposals) => $"""
  <div class="progression-grid">
    {string.Join(Environment.NewLine, proposals.OrderBy(proposal => proposal.Introduced).Select((proposal, index) => $"""
      <a class="progression-step" href="{Encode(proposal.Route)}">
        <span class="progression-number">{index + 1:00}</span>
        <span class="progression-date">{FormatDate(proposal.Introduced)}</span>
        <strong>{Encode(proposal.Code)}</strong>
        <span class="progression-title">{Encode(proposal.Title)}</span>
        <span class="topic-row">{string.Join("", proposal.Topics.Select(topic => $"<span>{Encode(topic)}</span>"))}</span>
      </a>
    """))}
  </div>
""";

static string RenderTimeline(
    IEnumerable<TimelineEvent> events,
    IReadOnlyDictionary<string, Actor> actorById,
    IReadOnlyDictionary<string, Source> sourceById) => string.Join(
    Environment.NewLine,
    events.OrderByDescending(item => item.Date).Select(item =>
    {
        var actors = string.Join(" · ", item.ActorIds.Select(id => $"<a href=\"{Encode(actorById[id].Route)}\">{Encode(actorById[id].ShortName ?? actorById[id].Name)}</a>"));
        var citations = RenderInlineSources(item.SourceIds, sourceById);
        return $"""
          <article class="timeline-item" id="begivenhed-{Encode(item.Id)}" data-record data-date="{Encode(item.Date)}" data-kind="{Encode(item.Kind)}">
            <div class="timeline-date">{FormatDateCompact(item.Date)}</div>
            <div>
              <p class="label">{Encode(item.Kind)}</p>
              <h2><a href="{Encode(item.RelatedRoute)}">{Encode(item.Title)}</a></h2>
              <p>{Encode(item.Summary)} {citations}</p>
              <p class="timeline-actors">{actors}</p>
              <a class="record-link" href="/tidslinje/#begivenhed-{Encode(item.Id)}">Link til begivenheden</a>
            </div>
          </article>
        """;
    }));

static string RenderActorList(IEnumerable<Actor> actors)
{
    var parties = actors.Where(actor => actor.Kind == "party").OrderBy(actor => actor.Name);
    var organizations = actors.Where(actor => actor.Kind == "organization").OrderBy(actor => actor.Name);
    var media = actors.Where(actor => actor.Kind == "media").OrderBy(actor => actor.Name);
    var people = actors.Where(actor => actor.Kind == "person").OrderBy(actor => actor.Name);

    return $"""
      <div class="actor-section">
        <h3>Partier</h3>
        <div class="entity-grid">{RenderActorCards(parties)}</div>
      </div>
      <div class="actor-section">
        <h3>Organisationer</h3>
        <div class="entity-grid">{RenderActorCards(organizations)}</div>
      </div>
      <div class="actor-section">
        <h3>Medier</h3>
        <div class="entity-grid">{RenderActorCards(media)}</div>
      </div>
      <div class="actor-section">
        <h3>Personer</h3>
        <div class="entity-grid">{RenderActorCards(people)}</div>
      </div>
    """;
}

static string RenderActorCards(IEnumerable<Actor> actors) => string.Join(
    Environment.NewLine,
    actors.Select(actor => $"""
      <a class="entity-card" data-record data-kind="{Encode(actor.Kind switch { "party" => "Parti", "organization" => "Organisation", "media" => "Medie", _ => "Person" })}" href="{Encode(actor.Route)}">
        <span class="entity-kind">{Encode(actor.Kind switch { "party" => "Parti", "organization" => "Organisation", "media" => "Medie", _ => actor.Affiliation ?? "Person" })}</span>
        <strong>{Encode(actor.Name)}</strong>
        <span>{Encode(actor.Summary)}</span>
      </a>
    """));

static string RenderMaterialList(IEnumerable<Material> materials) => string.Join(
    Environment.NewLine,
    materials.OrderByDescending(item => item.Date).Select(item => $"""
      <a class="entity-card" data-record data-kind="{Encode(item.Kind)}" href="{Encode(item.Route)}">
        <span class="entity-kind">{Encode(item.Kind)}</span>
        <strong>{Encode(item.Title)}</strong>
        <span>{Encode(item.Summary)}</span>
      </a>
    """));

static string RenderMaterial(
    Material material,
    IEnumerable<MaterialLink> materialLinks,
    IEnumerable<Proposal> proposals,
    IEnumerable<MediaItem> media,
    IEnumerable<Relationship> relationships,
    IEnumerable<TimelineEvent> events,
    IEnumerable<Statement> statements,
    IEnumerable<HealthcareRecord> healthcare,
    IReadOnlyDictionary<string, Material> materialById,
    IReadOnlyDictionary<string, Actor> actorById,
    IReadOnlyDictionary<string, Source> sourceById)
{
    var links = materialLinks.Where(item => string.Equals(item.MaterialId, material.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
    var critiques = links.Where(item => item.Category == "critique").ToArray();
    var checks = links.Where(item => item.Category == "rebuttal").ToArray();
    var responses = links.Where(item => item.Category == "response").ToArray();
    var relatedProposals = proposals.Where(item => (item.MaterialIds ?? []).Contains(material.Id, StringComparer.OrdinalIgnoreCase)).ToArray();
    var relatedMedia = media.Where(item => (item.MaterialIds ?? []).Contains(material.Id, StringComparer.OrdinalIgnoreCase)).ToArray();
    var relatedRelationships = relationships.Where(item => string.Equals(item.ToRoute, material.Route, StringComparison.OrdinalIgnoreCase)).ToArray();
    var relatedEvents = events.Where(item => (item.MaterialIds ?? []).Contains(material.Id, StringComparer.OrdinalIgnoreCase)).ToArray();
    var relatedStatements = statements.Where(item => (item.MaterialIds ?? []).Contains(material.Id, StringComparer.OrdinalIgnoreCase)).ToArray();
    var relatedHealthcare = healthcare.Where(item => (item.MaterialIds ?? []).Contains(material.Id, StringComparer.OrdinalIgnoreCase)).ToArray();
    var relatedMaterials = (material.RelatedMaterialIds ?? []).Select(id => materialById[id]).ToArray();

    string RenderLinkCards(IEnumerable<MaterialLink> items) => string.Join(Environment.NewLine, items.Select(item => $"""
      <article class="evidence-card" id="material-link-{Encode(item.Id)}">
        <p class="evidence-meta">{Encode(item.Kind)}</p>
        <h3>{Encode(item.Title)}</h3>
        <p>{Encode(item.Summary)} {RenderInlineSources(item.SourceIds, sourceById)}</p>
      </article>
    """));

    var critiqueHtml = critiques.Length == 0 ? "<p>Ingen særskilt registreret ekspertkritik endnu.</p>" : RenderLinkCards(critiques);
    var checkHtml = checks.Length == 0 ? "<p>Ingen særskilt registreret efterprøvning endnu.</p>" : RenderLinkCards(checks);
    var responseHtml = responses.Length == 0 ? "<p>Ingen særskilt registrerede faglige svar endnu.</p>" : RenderLinkCards(responses);
    var proposalHtml = relatedProposals.Length == 0 ? "<p>Ingen direkte koblede politiske forslag endnu.</p>" : RenderProposalList(relatedProposals);
    var mediaHtml = relatedMedia.Length == 0 ? "<p>Ingen direkte koblede medieregistreringer endnu.</p>" : RenderMediaList(relatedMedia, actorById, sourceById);
    var relationshipHtml = relatedRelationships.Length == 0 ? "<p>Ingen direkte koblede politiske/netværksrelationer endnu.</p>" : RenderRelationships(relatedRelationships, actorById, sourceById);
    var eventHtml = relatedEvents.Length == 0 ? "<p>Ingen direkte koblede tidslinjepunkter endnu.</p>" : RenderTimeline(relatedEvents, actorById, sourceById);
    var statementHtml = relatedStatements.Length == 0 ? "<p>Ingen direkte koblede udtalelser endnu.</p>" : RenderStatementList(relatedStatements, actorById, sourceById);
    var healthcareHtml = relatedHealthcare.Length == 0 ? "<p>Ingen direkte koblede sundhedsanalyser endnu.</p>" : string.Join(Environment.NewLine, relatedHealthcare.Select(item => $"""
      <article class="evidence-card" id="material-health-{Encode(item.Id)}">
        <p class="evidence-meta">{Encode(item.Status)}</p>
        <h3><a href="/sundhed/#sundhed-{Encode(item.Id)}">{Encode(item.Title)}</a></h3>
        <p>{Encode(item.Summary)}</p>
        <p><strong>Vurdering:</strong> {Encode(item.Analysis)}</p>
        <p class="evidence-meta">{RenderInlineSources(item.SourceIds, sourceById)}</p>
      </article>
    """));
    var relatedMaterialHtml = relatedMaterials.Length == 0 ? "" : $"""
      <div class="case-section">
        <p class="kicker">Relateret materiale</p>
        <h2>Dokumenter i samme evidenskæde</h2>
        <div class="entity-grid">{RenderMaterialList(relatedMaterials)}</div>
      </div>
    """;

    return $"""
      <section class="page-hero shell case-hero">
        <p class="kicker">{Encode(material.Kind)} · {FormatDate(material.Date)}</p>
        <h1>{Encode(material.Title)}</h1>
        <p class="lede">{Encode(material.Summary)} {RenderInlineSources([material.SourceIds[0]], sourceById)}</p>
      </section>

      <section class="shell section-block case-layout">
        <div class="case-main">
          <p class="kicker">Analyse</p>
          <h2>Hvad materialet er — og ikke er</h2>
          <div class="profile-copy">{string.Join(Environment.NewLine, material.Analysis.Select(paragraph => $"<p>{Encode(paragraph)}</p>"))}</div>

          {relatedMaterialHtml}

          <div class="case-section">
            <p class="kicker">Ekspertkritik</p>
            <h2>Kritik knyttet direkte til materialet</h2>
            <div class="evidence-list">{critiqueHtml}</div>
          </div>

          <div class="case-section">
            <p class="kicker">Efterprøvning og modkritik</p>
            <h2>Uenighed om kritikken</h2>
            <div class="evidence-list">{checkHtml}</div>
          </div>

          <div class="case-section">
            <p class="kicker">Faglige svar</p>
            <h2>Høringssvar og andre direkte reaktioner</h2>
            <div class="evidence-list">{responseHtml}</div>
          </div>

          <div class="case-section">
            <p class="kicker">Sundhed</p>
            <h2>Analyser der bruger materialet</h2>
            <div class="evidence-list">{healthcareHtml}</div>
          </div>

          <div class="case-section">
            <p class="kicker">Politik</p>
            <h2>Forslag der bruger materialet</h2>
            <div class="evidence-list">{proposalHtml}</div>
          </div>

          <div class="case-section">
            <p class="kicker">Retorik og brug</p>
            <h2>Udtalelser der henviser til materialet</h2>
            <div class="statement-list compact-statements">{statementHtml}</div>
          </div>

          <div class="case-section">
            <p class="kicker">Kronologi</p>
            <h2>På tidslinjen</h2>
            <div class="timeline-preview">{eventHtml}</div>
          </div>

          <div class="case-section">
            <p class="kicker">Medier</p>
            <h2>Direkte koblet dækning</h2>
            <div class="media-list compact-media">{mediaHtml}</div>
          </div>

          <div class="case-section">
            <p class="kicker">Politisk og organisatorisk uptake</p>
            <h2>Dokumenterede forbindelser</h2>
            <div class="relationship-list compact-relations">{relationshipHtml}</div>
          </div>
        </div>

        <aside class="case-sources">
          <p class="kicker">Grundmateriale</p>
          <h2>Primære og centrale kilder</h2>
          {RenderSources(material.SourceIds, sourceById)}
        </aside>
      </section>
    """;
}

static string RenderMediaList(
    IEnumerable<MediaItem> media,
    IReadOnlyDictionary<string, Actor> actorById,
    IReadOnlyDictionary<string, Source> sourceById) => string.Join(
    Environment.NewLine,
    media.OrderByDescending(item => item.Date).Select(item =>
    {
        var authorName = item.AuthorActorId is not null
            ? actorById[item.AuthorActorId].Name
            : item.AuthorLabel;
        var author = item.AuthorActorId is not null
            ? $"<a href=\"{Encode(actorById[item.AuthorActorId].Route)}\">{Encode(actorById[item.AuthorActorId].Name)}</a>"
            : string.IsNullOrWhiteSpace(item.AuthorLabel) ? "" : Encode(item.AuthorLabel);
        var outlet = item.OutletActorId is not null
            ? $"<a href=\"{Encode(actorById[item.OutletActorId].Route)}\">{Encode(actorById[item.OutletActorId].Name)}</a>"
            : Encode(item.OutletLabel ?? "Ukendt medie");
        var byline = string.IsNullOrWhiteSpace(author) ? outlet : $"{author} · {outlet}";
        var actorData = string.IsNullOrWhiteSpace(authorName) ? Array.Empty<string>() : new[] { authorName };
        return $"""
          <article class="media-record" id="medie-{Encode(item.Id)}" data-record data-date="{Encode(item.Date)}" data-actors="{Encode(JsonSerializer.Serialize(actorData))}" data-themes="{Encode(JsonSerializer.Serialize(item.Themes))}">
            <div class="media-record-meta">
              <time datetime="{Encode(item.Date)}">{FormatDate(item.Date)}</time>
              <span>{Encode(item.Kind)}</span>
              <span>{Encode(item.CorpusRole)}</span>
            </div>
            <h3><a href="/medier/#medie-{Encode(item.Id)}">{Encode(item.Title)}</a></h3>
            <p class="media-byline">{byline}</p>
            <p>{Encode(item.Summary)} {RenderInlineSources(item.SourceIds, sourceById)}</p>
            <div class="topic-row">{RenderTopicLinks(item.Themes, "/medier/")}</div>
          </article>
        """;
    }));

static string RenderMediaThemes(IEnumerable<MediaItem> media)
{
    var themes = media
        .SelectMany(item => item.Themes)
        .GroupBy(frame => frame, StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(group => group.Count())
        .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase);

    return string.Join(Environment.NewLine, themes.Select(group => $"""
      <a class="frame-row" href="/medier/?tema={Uri.EscapeDataString(group.Key)}#{Encode("medie-" + media.OrderByDescending(item => item.Date).First(item => item.Themes.Contains(group.Key, StringComparer.OrdinalIgnoreCase)).Id)}">
        <span>{Encode(group.Key)}</span>
        <strong>{group.Count()}</strong>
      </a>
    """));
}
static string RenderRelationships(
    IEnumerable<Relationship> relationships,
    IReadOnlyDictionary<string, Actor> actorById,
    IReadOnlyDictionary<string, Source> sourceById) => string.Join(
    Environment.NewLine,
    relationships.OrderBy(item => item.Date).Select(item =>
    {
        var from = actorById[item.FromActorId];
        var target = item.ToActorId is not null
            ? $"<a href=\"{Encode(actorById[item.ToActorId].Route)}\">{Encode(actorById[item.ToActorId].Name)}</a>"
            : item.ToRoute is not null
                ? $"<a href=\"{Encode(item.ToRoute)}\">{Encode(item.ToLabel ?? item.ToRoute)}</a>"
                : Encode(item.ToLabel ?? "Ukendt mål");
        return $"""
          <article class="relationship-card" id="relation-{Encode(item.Id)}" data-record data-date="{Encode(item.Date)}" data-kind="{Encode(item.Kind)}">
            <div class="relationship-meta">
              <time datetime="{Encode(item.Date)}">{FormatDate(item.Date)}</time>
              <span>{Encode(item.Kind)}</span>
            </div>
            <div class="relationship-edge">
              <a href="{Encode(from.Route)}">{Encode(from.Name)}</a>
              <span aria-hidden="true">→</span>
              <span>{target}</span>
            </div>
            <p>{Encode(item.Summary)} {RenderInlineSources(item.SourceIds, sourceById)}</p>
            <a class="record-link" href="/netvaerk/#relation-{Encode(item.Id)}">Link til relationen</a>
          </article>
        """;
    }));

static string RenderStatementList(
    IEnumerable<Statement> statements,
    IReadOnlyDictionary<string, Actor> actorById,
    IReadOnlyDictionary<string, Source> sourceById) => string.Join(
    Environment.NewLine,
    statements.OrderByDescending(item => item.Date).Select(item =>
    {
        var actor = actorById[item.ActorId];
        var label = item.Position switch
        {
            "restriction" => "Restriktiv position",
            "defense" => "Forsvar for rettigheder",
            "critique" => "Kritik af restriktion",
            "trans-critical" => "Transkritisk position",
            _ => item.Position
        };
        return $"""
          <article class="statement-record statement-{Encode(item.Position)}" id="udtalelse-{Encode(item.Id)}" data-record data-date="{Encode(item.Date)}" data-actors="{Encode(JsonSerializer.Serialize(new[] { actor.Name }))}" data-themes="{Encode(JsonSerializer.Serialize(item.Themes))}">
            <div class="statement-record-meta">
              <time datetime="{Encode(item.Date)}">{FormatDate(item.Date)}</time>
              <span>{Encode(label)}</span>
              <span>{Encode(item.Kind)}</span>
            </div>
            <p class="statement-speaker"><a href="{Encode(actor.Route)}">{Encode(actor.Name)}</a>{(string.IsNullOrWhiteSpace(item.Affiliation) ? "" : $" · {Encode(item.Affiliation)}")}</p>
            <blockquote><p>“{Encode(item.Excerpt)}”</p></blockquote>
            <p><strong>Kontekst og analyse:</strong> {Encode(item.Context)}</p>
            <p class="statement-sources">{string.Join(" · ", item.SourceIds.Select(id => $"<a href=\"{Encode(sourceById[id].Url)}\" rel=\"external noreferrer\">{Encode(sourceById[id].Title)}</a>"))}</p>
            {(string.IsNullOrWhiteSpace(item.Passage) ? "" : $"<p class=\"passage-reference\">Find passagen: {Encode(item.Passage)}</p>")}
            <p><a class="record-link" href="/retorik/#udtalelse-{Encode(item.Id)}">Link til udtalelsen</a></p>
            <div class="topic-row">{RenderTopicLinks(item.Themes, "/retorik/")}</div>
          </article>
        """;
    }));

static string RenderStatementThemes(IEnumerable<Statement> statements)
{
    var themes = statements
        .SelectMany(item => item.Themes)
        .GroupBy(theme => theme, StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(group => group.Count())
        .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase);

    return string.Join(Environment.NewLine, themes.Select(group => $"""
      <a class="frame-row" href="/retorik/?tema={Uri.EscapeDataString(group.Key)}#{Encode("udtalelse-" + statements.OrderByDescending(item => item.Date).First(item => item.Themes.Contains(group.Key, StringComparer.OrdinalIgnoreCase)).Id)}">
        <span>{Encode(group.Key)}</span>
        <strong>{group.Count()}</strong>
      </a>
    """));
}
static string RenderProposal(
    Proposal proposal,
    IEnumerable<Statement> statements,
    IReadOnlyDictionary<string, Material> materialById,
    IReadOnlyDictionary<string, Actor> actorById,
    IReadOnlyDictionary<string, Source> sourceById)
{
    var proposers = proposal.ProposerIds.Select(id => actorById[id]).ToArray();
    var supporters = proposal.SupportPartyIds.Select(id => actorById[id]).ToArray();
    var relatedStatements = statements.Where(item => string.Equals(item.RelatedRoute, proposal.Route, StringComparison.OrdinalIgnoreCase)).ToArray();
    var relatedMaterials = (proposal.MaterialIds ?? []).Select(id => materialById[id]).ToArray();
    var materialSection = relatedMaterials.Length == 0 ? "" : $"""
      <div class="case-section">
        <p class="kicker">Relateret materiale</p>
        <h2>Dokumenter brugt i sagen</h2>
        <div class="entity-grid">{RenderMaterialList(relatedMaterials)}</div>
      </div>
    """;
    var statementSection = relatedStatements.Length == 0 ? "" : $"""
      <div class="case-section">
        <p class="kicker">Retorik i debatten</p>
        <div class="statement-list compact-statements">{RenderStatementList(relatedStatements, actorById, sourceById)}</div>
      </div>
    """;
    var baselineSection = proposal.Code == "B47" ? $"""
      <div class="case-section">
        <p class="kicker">Omfang · grundtal</p>
        <h2>Hvor stor er ordningen?</h2>
        <div class="fact-grid">
          <div><span>2014–2025</span><strong>3.129</strong><span>tildelinger af nyt personnummer</span></div>
          <div><span>2024/25</span><strong>455</strong><span>ansøgninger</span></div>
          <div><span>2024/25</span><strong>438</strong><span>tildelinger</span></div>
        </div>
        <p>Digitaliseringsministeriets nyeste fundne CPR-opgørelse dækker 1. september 2014 til 31. august 2025 og registrerer i alt 3.797 ansøgninger og 3.129 tildelinger. For de seneste seks opgørelsesår var ansøgninger/tildelinger: 326/280 i 2019/20, 423/321 i 2020/21, 425/370 i 2021/22, 448/381 i 2022/23, 471/390 i 2023/24 og 455/438 i 2024/25. Opgørelsen advarer om, at enkelte tidligere tal kan blive korrigeret som følge af manuel sagsbehandling, fejlrettelser og justeringer. {RenderInlineSources(["cpr-legal-gender-stats-2025"], sourceById)}</p>
        <p>Ved B47's førstebehandling oplyste ligestillingsministeren, at 80 af de daværende 2.691 tildelinger frem til 31. august 2024 var efterfulgt af gentildeling af det oprindelige personnummer, hvilket ministeren beskrev som cirka 3 pct. Det er et ældre, særskilt opgørelsestidspunkt og skal ikke genberegnes mod 2025-totalen. Gentildeling af oprindeligt personnummer er desuden ikke i sig selv en fuldstændig måling af alle former for fortrydelse eller utilfredshed. {RenderInlineSources(["ft-b47-spm1-baseline-2025", "ft-b47-debate"], sourceById)}</p>
      </div>
    """ : "";
    var changingRoomSection = proposal.Code == "B47" ? $"""
      <div class="case-section">
        <p class="kicker">Institutionel praksis · omklædningsrum</p>
        <h2>Juridisk køn gav ikke automatisk adgang til kvindernes omklædningsrum</h2>
        <p>Ligebehandlingsnævnet havde allerede før B47 behandlet konkrete konflikter mellem transpersoners ligebehandling og andre brugeres blufærdighed. I 2016 fik en person med juridisk kønsskifte ikke medhold i en klage over henvisning til separat omklædning. I 2023 anvendte nævnet de nyere udtrykkelige beskyttelser af kønsidentitet, kønsudtryk og kønskarakteristika: nævnet fandt en formodning om direkte forskelsbehandling, men vurderede, at hensynet til andre gæsters blufærdighed var et legitimt mål, og at familieomklædning i den konkrete sag var en hensigtsmæssig og nødvendig løsning. {RenderInlineSources(["lbn-changing-room-2016", "lbn-changing-room-2023", "lbn-annual-report-2023"], sourceById)}</p>
        <div class="notice compact-notice">
          <p class="label">Rækkevidde</p>
          <p>Afgørelserne viser ikke, at enhver udelukkelse fra et kønsopdelt rum er lovlig, og de må ikke bruges som mål for hvor ofte sådanne konflikter opstår. De viser derimod, at dansk ligestillingsret allerede havde en konkret proportionalitetsmekanisme: juridisk køn eller kønsidentitet tilsidesatte ikke automatisk blufærdighedshensyn, og institutioner stod ikke uden et retligt redskab til at afveje hensynene.</p>
        </div>
      </div>
    """ : "";

    var hasVote = proposal.FinalVote is not null && proposal.VoteFor is not null && proposal.VoteAgainst is not null && proposal.VoteAbstain is not null;
    var statusDetail = hasVote
        ? $"<strong>{proposal.VoteFor}–{proposal.VoteAgainst}</strong><span>endelig afstemning</span>"
        : "<strong>Ingen</strong><span>endelig afstemning</span>";
    var voteFacts = hasVote
        ? $"""
          <div><span>Endelig afstemning</span><strong>{FormatDate(proposal.FinalVote!)}</strong></div>
          <div><span>Resultat</span><strong>{proposal.VoteFor} for · {proposal.VoteAgainst} imod · {proposal.VoteAbstain} hverken/eller</strong></div>
        """
        : $"""
          <div><span>Videre forløb</span><strong>{Encode(proposal.Status)}</strong></div>
          <div><span>Endelig afstemning</span><strong>Ingen</strong></div>
        """;
    var voteCitation = proposal.VoteSourceId is null ? "" : RenderInlineSources([proposal.VoteSourceId], sourceById);
    var voteNote = string.IsNullOrWhiteSpace(proposal.VoteNote) ? "" : $"<p class=\"evidence-meta\">{Encode(proposal.VoteNote)}</p>";
    var voteSection = hasVote
        ? $"""
          <div class="case-section">
            <p class="kicker">Endelig afstemning</p>
            <h2>Partier for forslaget</h2>
            <p>Folketingets afstemningsregistrering viser følgende partigrupper for forslaget. {voteCitation}</p>
            <div class="party-chips">{string.Join("", supporters.Select(actor => $"<a href=\"{Encode(actor.Route)}\">{Encode(actor.ShortName ?? actor.Name)}</a>"))}</div>
            {voteNote}
            <h3>Partier imod</h3>
            <p>{Encode(string.Join(", ", proposal.OpposeParties))}.</p>
          </div>
        """
        : """
          <div class="case-section">
            <p class="kicker">Parlamentarisk status</p>
            <h2>Ingen endelig afstemning</h2>
            <p>Forslaget nåede ikke en endelig afstemning i Folketinget. Det betyder, at der ikke kan udledes en samlet partistemme fra sagen.</p>
          </div>
        """;

    return $"""
      <section class="page-hero shell case-hero">
        <p class="kicker">Beslutningsforslag · {Encode(proposal.Session)}</p>
        <div class="case-heading">
          <div>
            <p class="case-code large">{Encode(proposal.Code)}</p>
            <h1>{Encode(proposal.Title)}</h1>
            <p class="lede">{Encode(proposal.Summary)} {RenderInlineSources([proposal.SourceIds[0]], sourceById)}</p>
          </div>
          <div class="case-status">
            <span class="status-tag">{Encode(proposal.Status)}</span>
            {statusDetail}
          </div>
        </div>
      </section>

      <section class="band subdued">
        <div class="shell fact-grid">
          <div><span>Fremsat</span><strong>{FormatDate(proposal.Introduced)}</strong></div>
          <div><span>1. behandling</span><strong>{FormatDate(proposal.FirstReading)}</strong></div>
          {voteFacts}
        </div>
      </section>

      <section class="shell section-block case-layout">
        <div class="case-main">
          <p class="kicker">Analyse</p>
          <h2>Hvorfor sagen er central</h2>
          <p class="analysis-text">{Encode(proposal.Analysis)}</p>

          {baselineSection}
          {changingRoomSection}
          {materialSection}

          <div class="case-section">
            <p class="kicker">Forslagsstillere</p>
            <div class="entity-grid">{RenderActorCards(proposers)}</div>
          </div>

          {voteSection}
          {statementSection}
        </div>

        <aside class="case-sources">
          <p class="kicker">Dokumentation</p>
          <h2>Kilder</h2>
          {RenderSources(proposal.SourceIds, sourceById)}
        </aside>
      </section>
    """;
}

static string RenderActor(
    Actor actor,
    IEnumerable<Proposal> proposals,
    IEnumerable<TimelineEvent> events,
    IEnumerable<Relationship> relationships,
    IEnumerable<MediaItem> media,
    IEnumerable<Statement> statements,
    IReadOnlyDictionary<string, Actor> actorById,
    IReadOnlyDictionary<string, Source> sourceById)
{
    var relatedProposals = proposals.Where(proposal =>
        proposal.ProposerIds.Contains(actor.Id, StringComparer.OrdinalIgnoreCase) ||
        proposal.SupportPartyIds.Contains(actor.Id, StringComparer.OrdinalIgnoreCase)).ToArray();
    var relatedEvents = events.Where(item => item.ActorIds.Contains(actor.Id, StringComparer.OrdinalIgnoreCase)).OrderByDescending(item => item.Date).ToArray();
    var relatedRelationships = relationships.Where(item =>
        string.Equals(item.FromActorId, actor.Id, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(item.ToActorId, actor.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
    var relatedMedia = media.Where(item =>
        string.Equals(item.AuthorActorId, actor.Id, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(item.OutletActorId, actor.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
    var relatedStatements = statements.Where(item => string.Equals(item.ActorId, actor.Id, StringComparison.OrdinalIgnoreCase)).ToArray();

    var proposalHtml = relatedProposals.Length == 0
        ? "<p>Ingen publicerede sager endnu.</p>"
        : RenderProposalList(relatedProposals);

    var eventHtml = relatedEvents.Length == 0
        ? "<p>Ingen publicerede tidslinjepunkter endnu.</p>"
        : string.Join(Environment.NewLine, relatedEvents.Select(item => $"""
            <li><time datetime="{Encode(item.Date)}">{FormatDate(item.Date)}</time> · <a href="{Encode(item.RelatedRoute)}">{Encode(item.Title)}</a></li>
          """));
    var relationshipHtml = relatedRelationships.Length == 0
        ? "<p>Ingen særskilt registrerede netværksrelationer endnu.</p>"
        : RenderRelationships(relatedRelationships, actorById, sourceById);
    var mediaHtml = relatedMedia.Length == 0
        ? "<p>Ingen særskilt registrerede mediespor endnu.</p>"
        : RenderMediaList(relatedMedia, actorById, sourceById);
    var statementHtml = relatedStatements.Length == 0
        ? "<p>Ingen særskilt registrerede udtalelser endnu.</p>"
        : RenderStatementList(relatedStatements, actorById, sourceById);
    var profileCitations = RenderInlineSources(actor.ProfileSourceIds ?? [], sourceById);
    var profileHtml = actor.Profile is not { Length: > 0 } ? "" : $"""
      <section class="shell section-block actor-profile">
        <div class="section-heading compact">
          <p class="kicker">Profil</p>
          <h2>Rolle i den dokumenterede udvikling</h2>
        </div>
        <div class="profile-copy">{string.Join(Environment.NewLine, actor.Profile.Select(paragraph => $"<p>{Encode(paragraph)}</p>"))}</div>
        {(string.IsNullOrWhiteSpace(profileCitations) ? "" : $"<p class=\"evidence-meta\">Profilkilder: {profileCitations}</p>")}
      </section>
    """;

    return $"""
      <section class="page-hero shell actor-hero">
        <p class="kicker">{Encode(actor.Kind switch { "party" => "Parti", "organization" => "Organisation", "media" => "Medie", _ => "Person" })}</p>
        <h1>{Encode(actor.Name)}</h1>
        {(string.IsNullOrWhiteSpace(actor.Affiliation) ? "" : $"<p class=\"actor-affiliation\">{Encode(actor.Affiliation)}</p>")}
        <p class="lede">{Encode(actor.Summary)} {RenderInlineSources(actor.SourceIds, sourceById)}</p>
      </section>

      {profileHtml}

      <section class="shell section-block">
        <div class="section-heading compact">
          <p class="kicker">Dokumenterede forbindelser</p>
          <h2>Sager</h2>
        </div>
        <div class="evidence-list">{proposalHtml}</div>
      </section>

      <section class="shell section-block">
        <p class="kicker">Udtalelser</p>
        <h2>Dokumenteret retorik</h2>
        <div class="statement-list compact-statements">{statementHtml}</div>
      </section>

      <section class="shell section-block">
        <p class="kicker">Medier</p>
        <h2>Dokumenterede mediespor</h2>
        <div class="media-list compact-media">{mediaHtml}</div>
      </section>

      <section class="shell section-block">
        <p class="kicker">Netværk</p>
        <h2>Dokumenterede relationer</h2>
        <div class="relationship-list compact-relations">{relationshipHtml}</div>
      </section>

      <section class="band subdued">
        <div class="shell section-block compact-block">
          <p class="kicker">Kronologi</p>
          <h2>På tidslinjen</h2>
          <ul class="event-list">{eventHtml}</ul>
        </div>
      </section>

      <section class="shell section-block">
        <p class="kicker">Kilder til denne profil</p>
        {RenderSources(actor.SourceIds.Concat(actor.ProfileSourceIds ?? []).Distinct(StringComparer.OrdinalIgnoreCase), sourceById)}
      </section>
    """;
}

static string RenderHealthcare(
    IEnumerable<HealthcareRecord> records,
    string track,
    IReadOnlyDictionary<string, Source> sourceById) => string.Join(
    Environment.NewLine,
    records.Where(item => string.Equals(item.Track, track, StringComparison.OrdinalIgnoreCase)).Select(item => $"""
      <article class="evidence-card" id="sundhed-{Encode(item.Id)}">
        <p class="evidence-meta">{Encode(item.Status)}</p>
        <h3>{Encode(item.Title)}</h3>
        <p>{Encode(item.Summary)}</p>
        <p><strong>Vurdering:</strong> {Encode(item.Analysis)}</p>
        <p class="evidence-meta">Kilder: {RenderInlineSources(item.SourceIds, sourceById)}</p>
      </article>
    """));

static string RenderYouthTreatmentStats(
    IEnumerable<YouthTreatmentStat> stats,
    IReadOnlyDictionary<string, Source> sourceById) => string.Join(
    Environment.NewLine,
    stats.Select(item => $"""
      <article class="evidence-card">
        <p class="kicker">{Encode(item.Year)}</p>
        <div class="card-grid three">
          {string.Join(Environment.NewLine, item.Stages.Select((stage, index) => $"""
            <div class="card static-card">
              <span class="card-index">{index + 1:00}</span>
              <h3>{Encode(stage.Value)}</h3>
              <p>{Encode(stage.Label)}</p>
            </div>
          """))}
        </div>
        <p class="evidence-meta">{Encode(item.Note)} {RenderInlineSources(item.SourceIds, sourceById)}</p>
      </article>
    """));

static string RenderSourceIndex(IEnumerable<Source> sources) => $"""
  <section class="page-hero shell">
    <p class="kicker">Dokumentation</p>
    <h1>Kilder</h1>
    <p class="lede">Det offentlige kilderegister samler de dokumenter, som de publicerede sider bygger på. Find originalteksten, se hvem der har udgivet den, og hvornår vi har gennemgået den.</p>
  </section>
  <section class="shell section-block">
    <div class="source-collection" data-collection="sources"><div class="source-index">
      {string.Join(Environment.NewLine, sources.OrderByDescending(source => source.Published).Select(source => RenderSource(source)))}
    </div></div>
  </section>
""";

static string RenderSources(IEnumerable<string> sourceIds, IReadOnlyDictionary<string, Source> sourceById) =>
    $"<ol class=\"source-list\">{string.Join(Environment.NewLine, sourceIds.Select((id, index) => $"<li>{RenderSource(sourceById[id], index + 1)}</li>"))}</ol>";

static string RenderSource(Source source, int? number = null)
{
    var prefix = number is null ? "" : $"<span class=\"source-number\">[{number}]</span> ";
    return $"""
      <article class="source-record" id="source-{Encode(source.Id)}" data-record data-kind="{Encode(source.Type)}">
        <p>{prefix}<a href="{Encode(source.Url)}" rel="external noreferrer">{Encode(source.Title)}</a></p>
        <p class="source-meta">{Encode(source.Publisher)} · {Encode(source.Type)} · {(source.Published is null ? "uden angivet publikationsdato" : FormatDate(source.Published))} · hentet {FormatDate(source.Accessed)}</p>
        <p>{Encode(source.Note)}</p>
        <code>{Encode(source.Id)}</code>
      </article>
    """;
}

static string RenderInlineSources(IEnumerable<string> sourceIds, IReadOnlyDictionary<string, Source> sourceById)
{
    var sources = sourceIds.Select(id => sourceById[id]).ToArray();
    var baseLabels = sources.Select(CitationLabelBase).ToArray();
    var totals = baseLabels
        .GroupBy(label => label, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
    var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    return string.Join(
        " ",
        sources.Select((source, index) =>
        {
            var label = baseLabels[index];
            if (totals[label] > 1)
            {
                var occurrence = seen.GetValueOrDefault(label) + 1;
                seen[label] = occurrence;
                label += occurrence <= 26
                    ? ((char)('a' + occurrence - 1)).ToString()
                    : occurrence.ToString(CultureInfo.InvariantCulture);
            }

            return $"<a class=\"citation\" href=\"{Encode(source.Url)}\" rel=\"external noreferrer\" aria-label=\"Kilde: {Encode(source.Title)}\">[{Encode(label)}]</a>";
        }));
}

static string CitationLabelBase(Source source)
{
    var publisher = source.Publisher switch
    {
        "Folketinget" => "FT",
        "Folketingstidende" => "FT",
        "Sundhedsstyrelsen" => "SST",
        "World Professional Association for Transgender Health" => "WPATH",
        "Odense Universitetshospital" => "OUH",
        "Aalborg Universitetshospital" => "AAUH",
        "Region Hovedstaden" => "Region H",
        "Region Hovedstadens Psykiatri" => "Region H",
        "Region Hovedstaden / Sundhedsjobs.dk" => "Region H",
        "Region Hovedstaden / Folketinget" => "Region H / FT",
        "Sundhedsstyrelsen / Folketinget" => "SST / FT",
        "Sundhedsstyrelsen / Rambøll" => "SST / Rambøll",
        "Indenrigs- og Sundhedsministeriet / Folketinget" => "ISM / FT",
        "The Lancet Regional Health – Europe / PubMed" => "Lancet RH Europe",
        "POV International" => "POV",
        "Det Konservative Folkeparti" => "Konservative",
        "Dansk Folkepartis folketingsgruppe / LOCAL EYES" => "DF / LOCAL EYES",
        "LGB Alliance UK" => "LGB Alliance",
        "Højesteret" => "Højesteret",
        "Danmarks Fængsler / Kriminalforsorgen" => "Kriminalforsorgen",
        _ => source.Publisher
    };
    var year = source.Published is { Length: >= 4 } ? $" {source.Published[..4]}" : "";
    return $"{publisher}{year}";
}

static string RenderNavigation(NavigationItem[] items, string currentRoute) => string.Join(
    Environment.NewLine,
    items.Select(item =>
    {
        var isCurrent = item.Route == "/"
            ? currentRoute == "/"
            : currentRoute.StartsWith(item.Route, StringComparison.OrdinalIgnoreCase);
        var current = isCurrent ? " aria-current=\"page\"" : "";
        return $"<a href=\"{Encode(item.Route)}\"{current}>{Encode(item.Label)}</a>";
    }));

static string ResolveOutputPath(string outputRoot, string route)
{
    if (route == "/")
        return Path.Combine(outputRoot, "index.html");

    var relative = route.TrimStart('/');
    if (relative.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        return Path.Combine(outputRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    return Path.Combine(outputRoot, relative.Replace('/', Path.DirectorySeparatorChar), "index.html");
}

static void CopyDirectory(string source, string destination)
{
    if (!Directory.Exists(source))
        return;

    Directory.CreateDirectory(destination);

    foreach (var file in Directory.EnumerateFiles(source))
        File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);

    foreach (var directory in Directory.EnumerateDirectories(source))
        CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
}

static string RenderTopicLinks(IEnumerable<string> themes, string route) => string.Join("", themes.Select(theme =>
    $"<a href=\"{route}?tema={Uri.EscapeDataString(theme)}#materiale\">{Encode(theme)}</a>"));

static string FormatDate(string value) => DateOnly.Parse(value, CultureInfo.InvariantCulture).ToString("d. MMMM yyyy", CultureInfo.GetCultureInfo("da-DK"));
static string FormatDateCompact(string value) => DateOnly.Parse(value, CultureInfo.InvariantCulture).ToString("dd.MM.yy", CultureInfo.InvariantCulture);
static string Encode(string value) => HtmlEncoder.Default.Encode(value);

sealed record Site(string Title, string Language, NavigationItem[] Navigation);
sealed record NavigationItem(string Label, string Route);
sealed record Page(string Route, string Title, string Description, string Source);
sealed record Source(string Id, string Title, string Publisher, string Type, string Url, string? Published, string Accessed, string Note);
sealed record HealthcareRecord(string Id, string Track, string Status, string Title, string Summary, string Analysis, string[] SourceIds, string[]? MaterialIds);
sealed record YouthTreatmentStat(string Year, YouthTreatmentStage[] Stages, string Note, string[] SourceIds);
sealed record YouthTreatmentStage(string Value, string Label);
sealed record Actor(string Id, string Name, string Kind, string? ShortName, string? Affiliation, string Route, string Summary, string[]? Profile, string[]? ProfileSourceIds, string[] SourceIds);
sealed record Proposal(
    string Id,
    string Code,
    string Title,
    string Route,
    string Session,
    string Introduced,
    string FirstReading,
    string? FinalVote,
    string Status,
    string Summary,
    string Analysis,
    string[] ProposerIds,
    string[] SupportPartyIds,
    string[] OpposeParties,
    int? VoteFor,
    int? VoteAgainst,
    int? VoteAbstain,
    string? VoteSourceId,
    string? VoteNote,
    string[] SourceIds,
    string[] Topics,
    string[]? MaterialIds);
sealed record TimelineEvent(string Id, string Date, string Kind, string Title, string Summary, string[] ActorIds, string RelatedRoute, string[] SourceIds, string[]? MaterialIds);
sealed record MediaItem(string Id, string Date, string Kind, string Title, string? AuthorActorId, string? AuthorLabel, string? OutletActorId, string? OutletLabel, string CorpusRole, string Summary, string[] Themes, string[] SourceIds, string[]? MaterialIds);
sealed record Material(string Id, string Title, string ShortTitle, string Kind, string Date, string Route, string Summary, string[] Analysis, string[] SourceIds, string[]? RelatedMaterialIds);
sealed record MaterialLink(string Id, string MaterialId, string Category, string Kind, string Title, string Summary, string[] SourceIds);
sealed record Statement(string Id, string Date, string ActorId, string Kind, string Excerpt, string Context, string Position, string[] Themes, string RelatedRoute, string[] SourceIds, string? Affiliation, string? Passage, string[]? MaterialIds);
sealed record Relationship(string Id, string Date, string Kind, string FromActorId, string? ToActorId, string? ToRoute, string? ToLabel, string Summary, string[] SourceIds);
