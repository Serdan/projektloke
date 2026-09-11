"""Run after `dotnet run build.cs`: verify published links and review regressions."""
from collections import Counter
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import urlsplit, unquote, urljoin
import json

root = Path(__file__).resolve().parents[1] / 'wwwroot'

class Page(HTMLParser):
    def __init__(self, text):
        super().__init__()
        self.ids = []
        self.links = []
        self.statements = []
        self.feed(text)

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if 'id' in attrs: self.ids.append(attrs['id'])
        if tag == 'a' and 'href' in attrs: self.links.append(attrs['href'])
        if tag == 'article' and attrs.get('id', '').startswith('udtalelse-'):
            self.statements.append(attrs['id'])

pages = {p: Page(p.read_text()) for p in root.rglob('*.html')}
checked = 0
for path, page in pages.items():
    assert len(page.ids) == len(set(page.ids)), ('duplicate IDs', path)
    source_route = '/' + str(path.relative_to(root)).removesuffix('index.html')
    for href in page.links:
        url = urlsplit(urljoin('https://projektloke.dk' + source_route, href))
        if url.netloc != 'projektloke.dk': continue
        destination = root / unquote(url.path.lstrip('/'))
        if url.path.endswith('/'): destination /= 'index.html'
        assert destination.is_file(), ('missing internal destination', path, href)
        if url.fragment:
            assert destination in pages and unquote(url.fragment) in pages[destination].ids, ('missing fragment', path, href)
        checked += 1

data = json.loads((root / 'data/index.json').read_text())
material_by_id = {item['id']: item for item in data['materials']}
material_link_by_id = {item['id']: item for item in data['materialLinks']}
assert {'cass-review-2024', 'cass-york-reviews-2024'} <= set(material_by_id), 'Core Cass materials must remain first-class nodes'
assert 'cass-york-reviews-2024' in material_by_id['cass-review-2024'].get('relatedMaterialIds', []), 'Cass must link to its commissioned York evidence package'
assert material_link_by_id['noone-cass-methodology-2025']['materialId'] == 'cass-york-reviews-2024', 'Noone ROBIS critique must target the York reviews'
assert material_link_by_id['bma-cass-review-2026']['materialId'] == 'cass-review-2024', 'BMA statement audit must remain attached to Cass'
assert material_link_by_id['bma-york-reanalysis-2026']['materialId'] == 'cass-york-reviews-2024', 'BMA method reanalysis must attach to York reviews'
cass_page = pages[root / 'materiale/cass-review/index.html']
york_page = pages[root / 'materiale/cass-york-reviews/index.html']
for anchor in ('udtalelse-raabjerg-b12-evidence', 'udtalelse-toft-2024-activism-treatment', 'begivenhed-raabjerg-cass-b12-event'):
    assert anchor in cass_page.ids, ('Cass material missing related record', anchor)
for anchor in ('material-link-noone-cass-methodology-2025', 'material-link-bma-york-reanalysis-2026', 'material-health-youth-evidence-boundary'):
    assert anchor in york_page.ids, ('York material missing related record', anchor)
sst2018_page = pages[root / 'materiale/sst-vejledning-2018/index.html']
sst_draft_page = pages[root / 'materiale/sst-hoeringsudkast-2024/index.html']
for anchor in ('material-health-autism-caution', 'material-health-psychiatric-gate', 'material-health-reference-traceability'):
    assert anchor in sst2018_page.ids, ('2018 guidance missing linked health analysis', anchor)
for anchor in ('material-link-dps-paediatrics-sst-draft-2025', 'material-link-bupdk-sst-draft-2025', 'material-link-dsam-sst-draft-2025', 'material-link-dp-sst-draft-autism-2025', 'material-link-dps-psychiatry-sst-draft-autism-2025', 'begivenhed-raabjerg-dps-hearing-question-2025', 'begivenhed-raabjerg-autism-hearing-question-2025'):
    assert anchor in sst_draft_page.ids, ('SST draft missing direct response or uptake', anchor)
wpath_page = pages[root / 'materiale/wpath-soc8/index.html']
for anchor in ('material-link-york-guideline-quality-wpath-2024', 'material-health-autism-caution', 'material-health-psychiatric-gate', 'material-health-youth-evidence-boundary'):
    assert anchor in wpath_page.ids, ('WPATH SOC8 missing linked critique or health analysis', anchor)
action_plan_page = pages[root / 'materiale/lgbt-handlingsplan-2026-2029/index.html']
for anchor in ('material-health-action-plan-trans-health-2026', 'begivenhed-lgbt-action-plan-2026', 'begivenhed-mf3-government-2026'):
    assert anchor in action_plan_page.ids, ('LGBT action plan missing linked implementation/context record', anchor)
politics_html = (root / 'politik/index.html').read_text()
assert '/materiale/lgbt-handlingsplan-2026-2029/' in politics_html, 'Politics page must link to the action-plan material node'
judgment_page = pages[root / 'materiale/hoejesteret-faengselsdom-2024/index.html']
for anchor in ('medie-dr-prison-case-2024', 'begivenhed-supreme-court-prison-gender-2024'):
    assert anchor in judgment_page.ids, ('Supreme Court material missing direct media/event record', anchor)
judgment_html = (root / 'materiale/hoejesteret-faengselsdom-2024/index.html').read_text()
assert '/politik/b47/' in judgment_html, 'Supreme Court material must link to B47 political uptake'
b47_html = (root / 'politik/b47/index.html').read_text()
assert '/materiale/hoejesteret-faengselsdom-2024/' in b47_html, 'B47 must link back to the Supreme Court material node'
assert data['schemaVersion'] >= 14, 'Explicit material source-quality fields require schema version 14+'
b47_data = next(item for item in data['proposals'] if item['id'] == 'b47-2024-25')
assert 'supreme-court-prison-gender-2024' in b47_data.get('materialIds', []), 'Public proposal data must preserve material links'
statements = {item['id']: item for item in data['statements']}
source_statements = json.loads((root.parent / 'content/data/statements.json').read_text())
source_ids = {item['id'] for item in source_statements}
assert set(statements) == source_ids, 'Public data index must contain every source statement exactly once'
source_materials = json.loads((root.parent / 'content/data/materials.json').read_text())
source_material_links = json.loads((root.parent / 'content/data/material-links.json').read_text())
assert {item['id'] for item in data['materials']} == {item['id'] for item in source_materials}, 'Public data index must contain every material exactly once'
assert {item['id'] for item in data['materialLinks']} == {item['id'] for item in source_material_links}, 'Public data index must contain every material link exactly once'
quality_fields = ('independence', 'peerReviewStatus', 'methodologicalStrength', 'evidenceRole', 'qualityNote')
allowed_quality = {
    'independence': {'independent', 'commissioned-independent', 'institutional', 'stakeholder'},
    'peerReviewStatus': {'peer-reviewed', 'not-peer-reviewed', 'not-applicable'},
    'methodologicalStrength': {'strong', 'moderate', 'limited', 'not-applicable'},
    'evidenceRole': {'primary-evidence', 'evidence-synthesis', 'commentary'},
}
public_materials = {item['id']: item for item in data['materials']}
public_material_links = {item['id']: item for item in data['materialLinks']}
for collection_name, source_items, public_items in (
    ('material', source_materials, public_materials),
    ('material link', source_material_links, public_material_links),
):
    for item in source_items:
        for field in quality_fields:
            assert field in item and item[field], (f'missing {collection_name} source-quality field', item['id'], field)
            assert public_items[item['id']].get(field) == item[field], (f'public {collection_name} source-quality field mismatch', item['id'], field)
        for field, allowed in allowed_quality.items():
            assert item[field] in allowed, (f'unknown {collection_name} source-quality value', item['id'], field, item[field])
for material in source_materials:
    material_path = root / material['route'].lstrip('/') / 'index.html'
    assert material_path.is_file(), ('missing material page', material['id'], material['route'])
    material_page = pages[material_path]
    expected_links = {f"material-link-{item['id']}" for item in source_material_links if item['materialId'] == material['id']}
    assert expected_links.issubset(set(material_page.ids)), ('missing material links', material['id'], expected_links - set(material_page.ids))
    material_html = material_path.read_text()
    assert material_html.count('data-source-quality') == 1 + len(expected_links), ('missing rendered source-quality blocks', material['id'])
    for field in ('data-independence', 'data-peer-review', 'data-methodological-strength', 'data-evidence-role'):
        assert field in material_html, ('missing rendered source-quality attribute', material['id'], field)
static_ids = {item.removeprefix('udtalelse-') for item in pages[root / 'retorik/index.html'].statements}
assert static_ids == source_ids, 'Static HTML must retain every statement without JS'
for key in ('edberg-b47-karen', 'edberg-b47-assigned-reality'):
    assert statements[key]['affiliation'] == 'Danmarksdemokraterne'
for key in ('vermund-l61-how-many-sexes', 'vermund-l61-strategic-identity'):
    assert statements[key]['affiliation'] == 'Liberal Alliance'
for key in ('lund-b145-protect', 'thiesen-b47-hamster-goldfish'):
    assert len(statements[key]['excerpt'].split()) > 10
    assert statements[key]['passage']
themes = Counter()
for item in data['statements']:
    assert len(item['themes']) == len(set(item['themes']))
    assert not set(item['themes']).intersection(data['themeAliases'])
    themes.update(item['themes'])
aliases = data['themeAliases']
expected_themes = Counter()
for item in source_statements:
    expected_themes.update({aliases.get(theme, theme) for theme in item['themes']})
assert themes == expected_themes, 'Published theme counts must match normalized source data'
print(f'Passed: {len(pages)} pages, {checked} internal links, historical affiliations, quotations, theme counts and complete static content.')
