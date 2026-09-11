#:property PublishAot=false

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

var siteRoot = Directory.GetCurrentDirectory();
var root = Path.Combine(siteRoot, "wwwroot");
var contentDataRoot = Path.Combine(siteRoot, "content", "data");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static JsonObject ReadObject(string path) => JsonNode.Parse(File.ReadAllText(path))?.AsObject()
    ?? throw new InvalidOperationException($"Could not parse {path}.");

static JsonArray ReadArray(string path) => JsonNode.Parse(File.ReadAllText(path))?.AsArray()
    ?? throw new InvalidOperationException($"Could not parse {path}.");

static string RequiredString(JsonObject item, string property) => item[property]?.GetValue<string>()
    ?? throw new InvalidOperationException($"Missing string property '{property}'.");

static JsonArray Array(JsonObject item, string property) => item[property] as JsonArray ?? [];

static IEnumerable<string> Strings(JsonObject item, string property) =>
    Array(item, property).Select(node => node?.GetValue<string>() ?? throw new InvalidOperationException($"Null value in '{property}'."));

static Dictionary<string, JsonObject> ById(JsonArray items) => items
    .Select(node => node?.AsObject() ?? throw new InvalidOperationException("Expected object in array."))
    .ToDictionary(item => RequiredString(item, "id"), StringComparer.OrdinalIgnoreCase);

static HashSet<string> StringSet(IEnumerable<string> values) => new(values, StringComparer.OrdinalIgnoreCase);


var idRegex = new Regex("\\bid\\s*=\\s*\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);
var hrefRegex = new Regex(@"<a\b[^>]*\bhref\s*=\s*""([^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
var statementRegex = new Regex("<article\\b[^>]*\\bid\\s*=\\s*\"(udtalelse-[^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

var pages = Directory.EnumerateFiles(root, "*.html", SearchOption.AllDirectories)
    .ToDictionary(
        path => Path.GetFullPath(path),
        path =>
        {
            var text = File.ReadAllText(path);
            return new PageInfo(
                Path.GetFullPath(path),
                text,
                idRegex.Matches(text).Select(match => match.Groups[1].Value).ToArray(),
                hrefRegex.Matches(text).Select(match => match.Groups[1].Value).ToArray(),
                statementRegex.Matches(text).Select(match => match.Groups[1].Value).ToArray());
        },
        StringComparer.OrdinalIgnoreCase);

var checkedLinks = 0;
foreach (var (path, page) in pages)
{
    Assert(page.Ids.Length == StringSet(page.Ids).Count, $"duplicate IDs: {path}");

    var relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
    var sourceRoute = "/" + (relative.EndsWith("index.html", StringComparison.OrdinalIgnoreCase)
        ? relative[..^"index.html".Length]
        : relative);
    var baseUri = new Uri("https://projektloke.dk" + sourceRoute);

    foreach (var href in page.Links)
    {
        if (!Uri.TryCreate(baseUri, href, out var url) || !string.Equals(url.Host, "projektloke.dk", StringComparison.OrdinalIgnoreCase))
            continue;

        var decodedPath = Uri.UnescapeDataString(url.AbsolutePath.TrimStart('/'));
        var destination = Path.Combine(root, decodedPath.Replace('/', Path.DirectorySeparatorChar));
        if (url.AbsolutePath.EndsWith('/'))
            destination = Path.Combine(destination, "index.html");
        destination = Path.GetFullPath(destination);

        Assert(File.Exists(destination), $"missing internal destination: {path} -> {href}");
        if (!string.IsNullOrEmpty(url.Fragment))
        {
            var fragment = Uri.UnescapeDataString(url.Fragment[1..]);
            Assert(pages.TryGetValue(destination, out var destinationPage) && destinationPage.Ids.Contains(fragment, StringComparer.Ordinal),
                $"missing fragment: {path} -> {href}");
        }
        checkedLinks++;
    }
}

var data = ReadObject(Path.Combine(root, "data", "index.json"));
var materials = data["materials"]?.AsArray() ?? throw new InvalidOperationException("Missing materials in public index.");
var materialLinks = data["materialLinks"]?.AsArray() ?? throw new InvalidOperationException("Missing materialLinks in public index.");
var evidenceConflicts = data["evidenceConflicts"]?.AsArray() ?? throw new InvalidOperationException("Missing evidenceConflicts in public index.");
var statementsArray = data["statements"]?.AsArray() ?? throw new InvalidOperationException("Missing statements in public index.");
var sourcesArray = data["sources"]?.AsArray() ?? throw new InvalidOperationException("Missing sources in public index.");
var proposalsArray = data["proposals"]?.AsArray() ?? throw new InvalidOperationException("Missing proposals in public index.");
var mediaArray = data["media"]?.AsArray() ?? throw new InvalidOperationException("Missing media in public index.");
var mediaSamplesArray = data["mediaSamples"]?.AsArray() ?? throw new InvalidOperationException("Missing mediaSamples in public index.");

var materialById = ById(materials);
var materialLinkById = ById(materialLinks);
var statementById = ById(statementsArray);

Assert(materialById.ContainsKey("cass-review-2024") && materialById.ContainsKey("cass-york-reviews-2024"), "Core Cass materials must remain first-class nodes.");
Assert(Strings(materialById["cass-review-2024"], "relatedMaterialIds").Contains("cass-york-reviews-2024", StringComparer.OrdinalIgnoreCase), "Cass must link to its commissioned York evidence package.");
Assert(RequiredString(materialLinkById["noone-cass-methodology-2025"], "materialId") == "cass-york-reviews-2024", "Noone ROBIS critique must target the York reviews.");
Assert(RequiredString(materialLinkById["bma-cass-review-2026"], "materialId") == "cass-review-2024", "BMA statement audit must remain attached to Cass.");
Assert(RequiredString(materialLinkById["bma-york-reanalysis-2026"], "materialId") == "cass-york-reviews-2024", "BMA method reanalysis must attach to York reviews.");
Assert(RequiredString(materialLinkById["bma-cass-review-2026"], "evidentiaryRelevance") == "direct", "BMA Cass relevance must remain direct.");
Assert(RequiredString(materialLinkById["mcdeavitt-cass-factcheck-2025"], "evidentiaryRelevance") == "contextual", "McDeavitt Cass relevance must remain contextual.");
Assert(Array(materialLinkById["bma-cass-review-2026"], "claimAssessments").Any(node => RequiredString(node!.AsObject(), "support") == "does-not-support"), "BMA Cass link must preserve negative claim-support boundary.");
Assert(Array(materialLinkById["bma-york-reanalysis-2026"], "claimAssessments").Any(node => RequiredString(node!.AsObject(), "support") == "disputes"), "BMA York link must preserve disputed-method claim.");

PageInfo Page(string relativePath) => pages[Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)))];
void RequireAnchors(PageInfo page, IEnumerable<string> anchors, string message)
{
    var ids = StringSet(page.Ids);
    foreach (var anchor in anchors)
        Assert(ids.Contains(anchor), $"{message}: {anchor}");
}

var cassPage = Page("materiale/cass-review/index.html");
var yorkPage = Page("materiale/cass-york-reviews/index.html");
RequireAnchors(cassPage, ["udtalelse-raabjerg-b12-evidence", "udtalelse-toft-2024-activism-treatment", "begivenhed-raabjerg-cass-b12-event"], "Cass material missing related record");
RequireAnchors(yorkPage, ["material-link-noone-cass-methodology-2025", "material-link-bma-york-reanalysis-2026", "material-health-youth-evidence-boundary"], "York material missing related record");
RequireAnchors(yorkPage, ["claim-noone-york-robis-high-risk", "claim-bma-york-systematic-skew-dispute", "evidence-conflict-york-review-methodology-conflict"], "York material missing claim provenance/conflict record");

var sst2018Page = Page("materiale/sst-vejledning-2018/index.html");
var sstDraftPage = Page("materiale/sst-hoeringsudkast-2024/index.html");
RequireAnchors(sst2018Page, ["material-health-autism-caution", "material-health-psychiatric-gate", "material-health-reference-traceability"], "2018 guidance missing linked health analysis");
RequireAnchors(sstDraftPage, ["material-link-dps-paediatrics-sst-draft-2025", "material-link-bupdk-sst-draft-2025", "material-link-dsam-sst-draft-2025", "material-link-dp-sst-draft-autism-2025", "material-link-dps-psychiatry-sst-draft-autism-2025", "begivenhed-raabjerg-dps-hearing-question-2025", "begivenhed-raabjerg-autism-hearing-question-2025"], "SST draft missing direct response or uptake");
RequireAnchors(sstDraftPage, ["claim-dps-paediatrics-restrictive-under-uncertainty", "claim-bupdk-uncertainty-does-not-remove-treatment-risk-balance", "claim-dp-autism-not-automatic-barrier", "claim-dps-psychiatry-autism-special-caution", "evidence-conflict-sst-youth-risk-policy-conflict", "evidence-conflict-sst-autism-threshold-conflict"], "SST draft missing claim provenance/conflict record");

var wpathPage = Page("materiale/wpath-soc8/index.html");
RequireAnchors(wpathPage, ["material-link-york-guideline-quality-wpath-2024", "material-health-autism-caution", "material-health-psychiatric-gate", "material-health-youth-evidence-boundary"], "WPATH SOC8 missing linked critique or health analysis");
var actionPlanPage = Page("materiale/lgbt-handlingsplan-2026-2029/index.html");
RequireAnchors(actionPlanPage, ["material-health-action-plan-trans-health-2026", "begivenhed-lgbt-action-plan-2026", "begivenhed-mf3-government-2026"], "LGBT action plan missing linked implementation/context record");
Assert(Page("politik/index.html").Text.Contains("/materiale/lgbt-handlingsplan-2026-2029/", StringComparison.Ordinal), "Politics page must link to the action-plan material node.");

var judgmentPage = Page("materiale/hoejesteret-faengselsdom-2024/index.html");
RequireAnchors(judgmentPage, ["medie-dr-prison-case-2024", "begivenhed-supreme-court-prison-gender-2024"], "Supreme Court material missing direct media/event record");
Assert(judgmentPage.Text.Contains("/politik/b47/", StringComparison.Ordinal), "Supreme Court material must link to B47 political uptake.");
Assert(Page("politik/b47/index.html").Text.Contains("/materiale/hoejesteret-faengselsdom-2024/", StringComparison.Ordinal), "B47 must link back to the Supreme Court material node.");
Assert((data["schemaVersion"]?.GetValue<int>() ?? 0) >= 17, "First-class media sampling frames require schema version 17+.");

var youthStats = data["youthTreatmentStats"]?.AsArray() ?? throw new InvalidOperationException("Missing youthTreatmentStats in public index.");
var aggregateYouth = youthStats.Select(node => node!.AsObject()).Single(item => RequiredString(item, "year") == "2016–2023 samlet");
Assert(Array(aggregateYouth, "stages").Any(node => RequiredString(node!.AsObject(), "value") == "239"), "Youth-treatment aggregate must preserve the 239 treatment-start baseline.");
Assert(Page("sundhed/index.html").Text.Contains(">239<", StringComparison.Ordinal), "Health page must render the 2016–2023 treatment-start aggregate.");

var b47Data = proposalsArray.Select(node => node!.AsObject()).Single(item => RequiredString(item, "id") == "b47-2024-25");
Assert(Strings(b47Data, "materialIds").Contains("supreme-court-prison-gender-2024", StringComparer.OrdinalIgnoreCase), "Public proposal data must preserve material links.");

var sourceStatements = ReadArray(Path.Combine(contentDataRoot, "statements.json"));
var sourceStatementIds = StringSet(sourceStatements.Select(node => RequiredString(node!.AsObject(), "id")));
Assert(StringSet(statementById.Keys).SetEquals(sourceStatementIds), "Public data index must contain every source statement exactly once.");

var sourceMedia = ReadArray(Path.Combine(contentDataRoot, "media.json"));
var sourceMediaSamples = ReadArray(Path.Combine(contentDataRoot, "media-samples.json"));
var mediaById = ById(mediaArray);
var mediaSampleById = ById(mediaSamplesArray);
Assert(StringSet(mediaById.Keys).SetEquals(sourceMedia.Select(node => RequiredString(node!.AsObject(), "id"))), "Public data index must contain every media record exactly once.");
Assert(StringSet(mediaSampleById.Keys).SetEquals(sourceMediaSamples.Select(node => RequiredString(node!.AsObject(), "id"))), "Public data index must contain every media sample exactly once.");
var cassSample = mediaSampleById["cass-youth-treatment-2024"];
var cassTargets = Array(cassSample, "targetOutlets").Select(node => node!.AsObject()).ToArray();
Assert(StringSet(cassTargets.Select(item => RequiredString(item, "name"))).SetEquals(["DR", "Berlingske", "Jyllands-Posten", "Kristeligt Dagblad", "Politiken", "Information", "Weekendavisen"]), "Cass sample must preserve its seven-outlet denominator.");
var expectedCassStatuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["DR"] = "verified", ["Berlingske"] = "partial", ["Jyllands-Posten"] = "not-verified",
    ["Kristeligt Dagblad"] = "verified", ["Politiken"] = "not-verified", ["Information"] = "verified", ["Weekendavisen"] = "partial"
};
foreach (var target in cassTargets)
    Assert(RequiredString(target, "status") == expectedCassStatuses[RequiredString(target, "name")], $"Cass outlet verification status regression: {RequiredString(target, "name")}");
foreach (var node in sourceMedia)
{
    var item = node!.AsObject();
    var role = RequiredString(item, "corpusRole");
    var sampleId = item["sampleId"]?.GetValue<string>();
    if (role == "struktureret prøve")
        Assert(sampleId == "cass-youth-treatment-2024", $"Structured media item must point to Cass sample: {RequiredString(item, "id")}");
    else
        Assert(string.IsNullOrWhiteSpace(sampleId), $"Non-structured media item must not have sample id: {RequiredString(item, "id")}");
}
var mediaPage = Page("medier/index.html");
RequireAnchors(mediaPage, ["medieproeve-cass-youth-treatment-2024", "medie-dr-genstart-cass-2024", "medie-kd-cass-review-2024", "medie-information-youth-treatment-2024"], "Media page missing sampling frame or verified observation");
foreach (var outlet in expectedCassStatuses.Keys)
    Assert(mediaPage.Text.Contains(outlet, StringComparison.Ordinal), $"Media page missing Cass target outlet: {outlet}");

var sourceMaterials = ReadArray(Path.Combine(contentDataRoot, "materials.json"));
var sourceMaterialLinks = ReadArray(Path.Combine(contentDataRoot, "material-links.json"));
var sourceEvidenceConflicts = ReadArray(Path.Combine(contentDataRoot, "evidence-conflicts.json"));
Assert(StringSet(materialById.Keys).SetEquals(sourceMaterials.Select(node => RequiredString(node!.AsObject(), "id"))), "Public data index must contain every material exactly once.");
Assert(StringSet(materialLinkById.Keys).SetEquals(sourceMaterialLinks.Select(node => RequiredString(node!.AsObject(), "id"))), "Public data index must contain every material link exactly once.");
Assert(StringSet(evidenceConflicts.Select(node => RequiredString(node!.AsObject(), "id"))).SetEquals(sourceEvidenceConflicts.Select(node => RequiredString(node!.AsObject(), "id"))), "Public data index must contain every evidence conflict exactly once.");

var qualityFields = new[] { "independence", "peerReviewStatus", "methodologicalStrength", "evidenceRole", "qualityNote" };
var allowedQuality = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
{
    ["independence"] = StringSet(["independent", "commissioned-independent", "institutional", "stakeholder"]),
    ["peerReviewStatus"] = StringSet(["peer-reviewed", "not-peer-reviewed", "not-applicable"]),
    ["methodologicalStrength"] = StringSet(["strong", "moderate", "limited", "not-applicable"]),
    ["evidenceRole"] = StringSet(["primary-evidence", "evidence-synthesis", "commentary"]),
};

void VerifyQuality(string collectionName, JsonArray sourceItems, Dictionary<string, JsonObject> publicItems)
{
    foreach (var node in sourceItems)
    {
        var item = node!.AsObject();
        var id = RequiredString(item, "id");
        foreach (var field in qualityFields)
        {
            var value = RequiredString(item, field);
            Assert(!string.IsNullOrWhiteSpace(value), $"missing {collectionName} source-quality field: {id} / {field}");
            Assert(publicItems[id][field]?.GetValue<string>() == value, $"public {collectionName} source-quality field mismatch: {id} / {field}");
        }
        foreach (var (field, allowed) in allowedQuality)
            Assert(allowed.Contains(RequiredString(item, field)), $"unknown {collectionName} source-quality value: {id} / {field}");

        if (collectionName == "material link")
        {
            Assert(StringSet(["direct", "substantial", "contextual"]).Contains(RequiredString(item, "evidentiaryRelevance")), $"invalid material-link evidentiary relevance: {id}");
            Assert(!string.IsNullOrWhiteSpace(RequiredString(item, "relevanceNote")), $"missing material-link relevance note: {id}");
            foreach (var claimNode in Array(item, "claimAssessments"))
            {
                var claim = claimNode!.AsObject();
                Assert(!string.IsNullOrWhiteSpace(RequiredString(claim, "id")), $"missing claim id: {id}");
                Assert(StringSet(["supports", "partially-supports", "disputes", "does-not-support"]).Contains(RequiredString(claim, "support")), $"invalid claim support: {id}");
                Assert(StringSet(["direct", "substantial", "contextual"]).Contains(RequiredString(claim, "relevance")), $"invalid claim relevance: {id}");
                Assert(StringSet(["strong", "moderate", "limited", "not-applicable"]).Contains(RequiredString(claim, "methodologicalStrengthOverride")), $"invalid claim strength override: {id}");
                Assert(!string.IsNullOrWhiteSpace(RequiredString(claim, "claim")) && !string.IsNullOrWhiteSpace(RequiredString(claim, "note")), $"incomplete claim assessment: {id}");
                Assert(Array(claim, "sourceIds").Count > 0, $"missing claim provenance: {id} / {RequiredString(claim, "id")}");
            }
        }
    }
}

VerifyQuality("material", sourceMaterials, materialById);
VerifyQuality("material link", sourceMaterialLinks, materialLinkById);

var sourceRegistryIds = StringSet(sourcesArray.Select(node => RequiredString(node!.AsObject(), "id")));
var claims = sourceMaterialLinks
    .Select(node => node!.AsObject())
    .SelectMany(link => Array(link, "claimAssessments").Select(claim => (Link: link, Claim: claim!.AsObject())))
    .ToArray();
var claimIds = claims.Select(item => RequiredString(item.Claim, "id")).ToArray();
Assert(claimIds.Length == StringSet(claimIds).Count, "Claim assessment IDs must be globally unique.");
var claimById = claims.ToDictionary(item => RequiredString(item.Claim, "id"), item => item, StringComparer.OrdinalIgnoreCase);
foreach (var (link, claim) in claims)
    Assert(StringSet(Strings(claim, "sourceIds")).IsSubsetOf(sourceRegistryIds), $"unknown claim source: {RequiredString(link, "id")} / {RequiredString(claim, "id")}");

foreach (var node in sourceEvidenceConflicts)
{
    var conflict = node!.AsObject();
    var id = RequiredString(conflict, "id");
    Assert(StringSet(["methodological-disagreement", "clinical-interpretation-disagreement"]).Contains(RequiredString(conflict, "kind")), $"invalid evidence conflict kind: {id}");
    Assert(StringSet(["direct-conflict", "partial-overlap"]).Contains(RequiredString(conflict, "scope")), $"invalid evidence conflict scope: {id}");
    Assert(StringSet(["unresolved", "partially-resolved", "resolved"]).Contains(RequiredString(conflict, "resolutionStatus")), $"invalid conflict resolution: {id}");
    var assessmentIds = Strings(conflict, "assessmentIds").ToArray();
    Assert(assessmentIds.Length >= 2 && assessmentIds.Length == StringSet(assessmentIds).Count, $"invalid conflict assessments: {id}");
    Assert(StringSet(assessmentIds).IsSubsetOf(StringSet(claimById.Keys)), $"unknown conflict claim: {id}");
    Assert(assessmentIds.All(claimId => RequiredString(claimById[claimId].Link, "materialId") == RequiredString(conflict, "materialId")), $"cross-material conflict: {id}");
    Assert(StringSet(Strings(conflict, "sourceIds")).IsSubsetOf(sourceRegistryIds), $"unknown conflict source: {id}");
}

foreach (var node in sourceMaterials)
{
    var material = node!.AsObject();
    var materialId = RequiredString(material, "id");
    var route = RequiredString(material, "route").Trim('/');
    var materialPath = Path.GetFullPath(Path.Combine(root, route.Replace('/', Path.DirectorySeparatorChar), "index.html"));
    Assert(File.Exists(materialPath), $"missing material page: {materialId}");
    var materialPage = pages[materialPath];
    var expectedLinks = StringSet(sourceMaterialLinks
        .Select(linkNode => linkNode!.AsObject())
        .Where(link => RequiredString(link, "materialId") == materialId)
        .Select(link => "material-link-" + RequiredString(link, "id")));
    Assert(expectedLinks.IsSubsetOf(StringSet(materialPage.Ids)), $"missing material links: {materialId}");
    Assert(Regex.Matches(materialPage.Text, "data-source-quality", RegexOptions.IgnoreCase).Count == 1 + expectedLinks.Count, $"missing rendered source-quality blocks: {materialId}");
    foreach (var field in new[] { "data-independence", "data-peer-review", "data-methodological-strength", "data-evidence-role" })
        Assert(materialPage.Text.Contains(field, StringComparison.Ordinal), $"missing rendered source-quality attribute: {materialId} / {field}");
}

var staticIds = StringSet(Page("retorik/index.html").Statements.Select(id => id["udtalelse-".Length..]));
Assert(staticIds.SetEquals(sourceStatementIds), "Static HTML must retain every statement without JS.");
foreach (var key in new[] { "edberg-b47-karen", "edberg-b47-assigned-reality" })
    Assert(RequiredString(statementById[key], "affiliation") == "Danmarksdemokraterne", $"historical affiliation regression: {key}");
foreach (var key in new[] { "vermund-l61-how-many-sexes", "vermund-l61-strategic-identity" })
    Assert(RequiredString(statementById[key], "affiliation") == "Liberal Alliance", $"historical affiliation regression: {key}");
foreach (var key in new[] { "lund-b145-protect", "thiesen-b47-hamster-goldfish" })
{
    Assert(RequiredString(statementById[key], "excerpt").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length > 10, $"quotation regression: {key}");
    Assert(!string.IsNullOrWhiteSpace(RequiredString(statementById[key], "passage")), $"missing passage: {key}");
}

var aliases = data["themeAliases"]?.AsObject() ?? throw new InvalidOperationException("Missing themeAliases.");
var publishedThemeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
foreach (var node in statementsArray)
{
    var statement = node!.AsObject();
    var themes = Strings(statement, "themes").ToArray();
    Assert(themes.Length == StringSet(themes).Count, $"duplicate published themes: {RequiredString(statement, "id")}");
    Assert(themes.All(theme => !aliases.ContainsKey(theme)), $"un-normalized published theme: {RequiredString(statement, "id")}");
    foreach (var theme in themes)
        publishedThemeCounts[theme] = publishedThemeCounts.GetValueOrDefault(theme) + 1;
}

var expectedThemeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
foreach (var node in sourceStatements)
{
    var statement = node!.AsObject();
    foreach (var sourceTheme in StringSet(Strings(statement, "themes")))
    {
        var normalized = aliases[sourceTheme]?.GetValue<string>() ?? sourceTheme;
        expectedThemeCounts[normalized] = expectedThemeCounts.GetValueOrDefault(normalized) + 1;
    }
}
Assert(publishedThemeCounts.Count == expectedThemeCounts.Count && expectedThemeCounts.All(pair => publishedThemeCounts.GetValueOrDefault(pair.Key) == pair.Value), "Published theme counts must match normalized source data.");

Console.WriteLine($"Passed: {pages.Count} pages, {checkedLinks} internal links, historical affiliations, quotations, theme counts and complete static content.");
sealed record PageInfo(string Path, string Text, string[] Ids, string[] Links, string[] Statements);
