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
statements = {item['id']: item for item in data['statements']}
source_statements = json.loads((root.parent / 'content/data/statements.json').read_text())
source_ids = {item['id'] for item in source_statements}
assert set(statements) == source_ids, 'Public data index must contain every source statement exactly once'
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
