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
var statements = Read<Statement[]>(Path.Combine(dataRoot, "statements.json"));
var layout = File.ReadAllText(templatePath);

var sourceById = sources.ToDictionary(source => source.Id, StringComparer.OrdinalIgnoreCase);
var actorById = actors.ToDictionary(actor => actor.Id, StringComparer.OrdinalIgnoreCase);
var allRoutes = pages.Select(page => page.Route)
    .Concat(proposals.Select(proposal => proposal.Route))
    .Concat(actors.Select(actor => actor.Route))
    .Append("/kilder/")
    .ToArray();

Validate(site, pages, sources, actors, proposals, events, relationships, media, statements, sourceById, actorById, allRoutes);

if (Directory.Exists(outputRoot))
    Directory.Delete(outputRoot, recursive: true);

Directory.CreateDirectory(outputRoot);
CopyDirectory(assetsRoot, Path.Combine(outputRoot, "assets"));

var generated = 0;

foreach (var page in pages)
{
    var bodyPath = Path.Combine(contentRoot, "pages", page.Source);
    var body = File.ReadAllText(bodyPath)
        .Replace("{{proposalList}}", RenderProposalList(proposals))
        .Replace("{{progression}}", RenderProgression(proposals))
        .Replace("{{timeline}}", RenderTimeline(events, actorById, sourceById))
        .Replace("{{actorList}}", RenderActorList(actors))
        .Replace("{{relationships}}", RenderRelationships(relationships, actorById, sourceById))
        .Replace("{{mediaList}}", RenderMediaList(media, actorById, sourceById))
        .Replace("{{mediaThemes}}", RenderMediaThemes(media))
        .Replace("{{mediaCount}}", media.Length.ToString(CultureInfo.InvariantCulture))
        .Replace("{{statementList}}", RenderStatementList(statements, actorById, sourceById))
        .Replace("{{statementThemes}}", RenderStatementThemes(statements))
        .Replace("{{statementCount}}", statements.Length.ToString(CultureInfo.InvariantCulture))
        .Replace("{{relationshipCount}}", relationships.Length.ToString(CultureInfo.InvariantCulture))
        .Replace("{{sourceCount}}", sources.Length.ToString(CultureInfo.InvariantCulture))
        .Replace("{{proposalCount}}", proposals.Length.ToString(CultureInfo.InvariantCulture));

    WritePage(page.Route, page.Title, page.Description, body);
}

foreach (var proposal in proposals)
    WritePage(proposal.Route, $"{proposal.Code}: {proposal.Title}", proposal.Summary, RenderProposal(proposal, statements, actorById, sourceById));

foreach (var actor in actors)
    WritePage(actor.Route, actor.Name, actor.Summary, RenderActor(actor, proposals, events, relationships, media, statements, actorById, sourceById));

WritePage(
    "/kilder/",
    "Kilder",
    "Projekt Lokes offentlige kilderegister.",
    RenderSourceIndex(sources));

var publicDataRoot = Path.Combine(outputRoot, "data");
Directory.CreateDirectory(publicDataRoot);
var publicIndex = new
{
    schemaVersion = 7,
    proposals = proposals.OrderByDescending(proposal => proposal.Introduced).Select(proposal => new
    {
        proposal.Id, proposal.Code, proposal.Title, proposal.Route, proposal.Session, proposal.Introduced, proposal.FirstReading,
        proposal.FinalVote, proposal.Status, proposal.ProposerIds, proposal.SupportPartyIds, proposal.VoteFor, proposal.VoteAgainst,
        proposal.VoteAbstain, proposal.VoteNote, proposal.Topics, proposal.SourceIds
    }),
    events = events.OrderByDescending(item => item.Date).Select(item => new
    {
        item.Id, item.Date, item.Kind, item.Title, item.Summary, item.ActorIds, item.RelatedRoute, item.SourceIds
    }),
    actors = actors.OrderBy(actor => actor.Name).Select(actor => new
    {
        actor.Id, actor.Name, actor.Kind, actor.ShortName, actor.Affiliation, actor.Route, actor.Summary, actor.Profile, actor.ProfileSourceIds, actor.SourceIds
    }),
    media = media.OrderByDescending(item => item.Date),
    statements = statements.OrderByDescending(item => item.Date),
    relationships = relationships.OrderByDescending(item => item.Date),
    sources = sources.OrderByDescending(source => source.Published)
};
File.WriteAllText(Path.Combine(publicDataRoot, "index.json"), JsonSerializer.Serialize(publicIndex, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));

Console.WriteLine($"Built {generated} pages from {proposals.Length} proposals, {events.Length} events, {media.Length} media records, {statements.Length} statements, {relationships.Length} relationships, {actors.Length} actors and {sources.Length} sources.");

void WritePage(string route, string pageTitle, string description, string body)
{
    var nav = RenderNavigation(site.Navigation, route);
    var title = route == "/" ? site.Title : $"{pageTitle} · {site.Title}";

    var html = layout
        .Replace("{{language}}", Encode(site.Language))
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
    Statement[] statements,
    IReadOnlyDictionary<string, Source> sourceById,
    IReadOnlyDictionary<string, Actor> actorById,
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
    RequireUnique(statements.Select(item => item.Id), "statement id");
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
    }

    foreach (var item in events)
    {
        RequireSources($"event {item.Id}", item.SourceIds, sourceById);
        RequireActors($"event {item.Id}", item.ActorIds, actorById);

        if (!allRoutes.Contains(item.RelatedRoute, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Event {item.Id} points to unknown route {item.RelatedRoute}.");
    }

    foreach (var item in media)
    {
        RequireSources($"media {item.Id}", item.SourceIds, sourceById);
        RequireActors($"media {item.Id}", [item.AuthorActorId], actorById);
        if (item.OutletActorId is not null)
            RequireActors($"media {item.Id}", [item.OutletActorId], actorById);
        if (item.OutletActorId is null && string.IsNullOrWhiteSpace(item.OutletLabel))
            throw new InvalidOperationException($"Media item {item.Id} has no outlet.");
    }

    foreach (var statement in statements)
    {
        RequireSources($"statement {statement.Id}", statement.SourceIds, sourceById);
        RequireActors($"statement {statement.Id}", [statement.ActorId], actorById);
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

static string RenderProposalList(IEnumerable<Proposal> proposals) => string.Join(
    Environment.NewLine,
    proposals.OrderByDescending(proposal => proposal.Introduced).Select(proposal =>
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
          <article class="timeline-item">
            <div class="timeline-date">{FormatDateCompact(item.Date)}</div>
            <div>
              <p class="label">{Encode(item.Kind)}</p>
              <h2><a href="{Encode(item.RelatedRoute)}">{Encode(item.Title)}</a></h2>
              <p>{Encode(item.Summary)} {citations}</p>
              <p class="timeline-actors">{actors}</p>
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
      <a class="entity-card" href="{Encode(actor.Route)}">
        <span class="entity-kind">{Encode(actor.Kind switch { "party" => "Parti", "organization" => "Organisation", "media" => "Medie", _ => actor.Affiliation ?? "Person" })}</span>
        <strong>{Encode(actor.Name)}</strong>
        <span>{Encode(actor.Summary)}</span>
      </a>
    """));

static string RenderMediaList(
    IEnumerable<MediaItem> media,
    IReadOnlyDictionary<string, Actor> actorById,
    IReadOnlyDictionary<string, Source> sourceById) => string.Join(
    Environment.NewLine,
    media.OrderByDescending(item => item.Date).Select(item =>
    {
        var author = actorById[item.AuthorActorId];
        var outlet = item.OutletActorId is not null
            ? $"<a href=\"{Encode(actorById[item.OutletActorId].Route)}\">{Encode(actorById[item.OutletActorId].Name)}</a>"
            : Encode(item.OutletLabel ?? "Ukendt medie");
        return $"""
          <article class="media-record">
            <div class="media-record-meta">
              <time datetime="{Encode(item.Date)}">{FormatDate(item.Date)}</time>
              <span>{Encode(item.Kind)}</span>
            </div>
            <h3>{Encode(item.Title)}</h3>
            <p class="media-byline"><a href="{Encode(author.Route)}">{Encode(author.Name)}</a> · {outlet}</p>
            <p>{Encode(item.Summary)} {RenderInlineSources(item.SourceIds, sourceById)}</p>
            <div class="topic-row">{string.Join("", item.Themes.Select(theme => $"<span>{Encode(theme)}</span>"))}</div>
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
      <div class="frame-row">
        <span>{Encode(group.Key)}</span>
        <strong>{group.Count()}</strong>
      </div>
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
          <article class="relationship-card">
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
            _ => item.Position
        };
        return $"""
          <article class="statement-record statement-{Encode(item.Position)}">
            <div class="statement-record-meta">
              <time datetime="{Encode(item.Date)}">{FormatDate(item.Date)}</time>
              <span>{Encode(label)}</span>
              <span>{Encode(item.Kind)}</span>
            </div>
            <p class="statement-speaker"><a href="{Encode(actor.Route)}">{Encode(actor.Name)}</a>{(string.IsNullOrWhiteSpace(actor.Affiliation) ? "" : $" · {Encode(actor.Affiliation)}")}</p>
            <blockquote><p>“{Encode(item.Excerpt)}”</p></blockquote>
            <p>{Encode(item.Context)} {RenderInlineSources(item.SourceIds, sourceById)}</p>
            <div class="topic-row">{string.Join("", item.Themes.Select(theme => $"<span>{Encode(theme)}</span>"))}</div>
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
      <div class="frame-row">
        <span>{Encode(group.Key)}</span>
        <strong>{group.Count()}</strong>
      </div>
    """));
}
static string RenderProposal(
    Proposal proposal,
    IEnumerable<Statement> statements,
    IReadOnlyDictionary<string, Actor> actorById,
    IReadOnlyDictionary<string, Source> sourceById)
{
    var proposers = proposal.ProposerIds.Select(id => actorById[id]).ToArray();
    var supporters = proposal.SupportPartyIds.Select(id => actorById[id]).ToArray();
    var relatedStatements = statements.Where(item => string.Equals(item.RelatedRoute, proposal.Route, StringComparison.OrdinalIgnoreCase)).ToArray();
    var statementSection = relatedStatements.Length == 0 ? "" : $"""
      <div class="case-section">
        <p class="kicker">Retorik i debatten</p>
        <div class="statement-list compact-statements">{RenderStatementList(relatedStatements, actorById, sourceById)}</div>
      </div>
    """;
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

static string RenderSourceIndex(IEnumerable<Source> sources) => $"""
  <section class="page-hero shell">
    <p class="kicker">Dokumentation</p>
    <h1>Kilder</h1>
    <p class="lede">Det offentlige kilderegister samler de dokumenter, som de publicerede sider bygger på. Hver kilde har et stabilt internt ID, så relationerne kan valideres ved build.</p>
  </section>
  <section class="shell section-block">
    <div class="source-index">
      {string.Join(Environment.NewLine, sources.OrderByDescending(source => source.Published).Select(source => RenderSource(source)))}
    </div>
  </section>
""";

static string RenderSources(IEnumerable<string> sourceIds, IReadOnlyDictionary<string, Source> sourceById) =>
    $"<ol class=\"source-list\">{string.Join(Environment.NewLine, sourceIds.Select((id, index) => $"<li>{RenderSource(sourceById[id], index + 1)}</li>"))}</ol>";

static string RenderSource(Source source, int? number = null)
{
    var prefix = number is null ? "" : $"<span class=\"source-number\">[{number}]</span> ";
    return $"""
      <article class="source-record" id="source-{Encode(source.Id)}">
        <p>{prefix}<a href="{Encode(source.Url)}" rel="external noreferrer">{Encode(source.Title)}</a></p>
        <p class="source-meta">{Encode(source.Publisher)} · {Encode(source.Type)} · {FormatDate(source.Published)} · hentet {FormatDate(source.Accessed)}</p>
        <p>{Encode(source.Note)}</p>
        <code>{Encode(source.Id)}</code>
      </article>
    """;
}

static string RenderInlineSources(IEnumerable<string> sourceIds, IReadOnlyDictionary<string, Source> sourceById) => string.Join(
    " ",
    sourceIds.Select((id, index) => $"<a class=\"citation\" href=\"{Encode(sourceById[id].Url)}\" rel=\"external noreferrer\" aria-label=\"Kilde: {Encode(sourceById[id].Title)}\">[{index + 1}]</a>"));

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

static string FormatDate(string value) => DateOnly.Parse(value, CultureInfo.InvariantCulture).ToString("d. MMMM yyyy", CultureInfo.GetCultureInfo("da-DK"));
static string FormatDateCompact(string value) => DateOnly.Parse(value, CultureInfo.InvariantCulture).ToString("dd.MM.yy", CultureInfo.InvariantCulture);
static string Encode(string value) => HtmlEncoder.Default.Encode(value);

sealed record Site(string Title, string Language, NavigationItem[] Navigation);
sealed record NavigationItem(string Label, string Route);
sealed record Page(string Route, string Title, string Description, string Source);
sealed record Source(string Id, string Title, string Publisher, string Type, string Url, string Published, string Accessed, string Note);
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
    string[] Topics);
sealed record TimelineEvent(string Id, string Date, string Kind, string Title, string Summary, string[] ActorIds, string RelatedRoute, string[] SourceIds);
sealed record MediaItem(string Id, string Date, string Kind, string Title, string AuthorActorId, string? OutletActorId, string? OutletLabel, string Summary, string[] Themes, string[] SourceIds);
sealed record Statement(string Id, string Date, string ActorId, string Kind, string Excerpt, string Context, string Position, string[] Themes, string RelatedRoute, string[] SourceIds);
sealed record Relationship(string Id, string Date, string Kind, string FromActorId, string? ToActorId, string? ToRoute, string? ToLabel, string Summary, string[] SourceIds);
