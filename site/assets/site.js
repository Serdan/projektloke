// The complete evidence remains in the HTML when JavaScript is unavailable.
for (const collection of document.querySelectorAll('[data-collection]')) {
  const records = [...collection.querySelectorAll('[data-record]')];
  if (!records.length) continue;
  const normalize = text => text.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('da-DK');
  const readList = (record, key) => JSON.parse(record.dataset[key] || '[]');
  const data = records.map(record => ({
    record, text: normalize(record.textContent), date: record.dataset.date || '',
    actors: readList(record, 'actors'), themes: readList(record, 'themes'), kind: record.dataset.kind || ''
  }));
  const prefix = collection.dataset.collection;
  const form = document.createElement('form');
  form.className = 'collection-filters';
  form.setAttribute('role', 'search');
  form.setAttribute('aria-label', 'Søg og filtrer materialet');
  const fields = {};
  function field(name, label, type = 'search', options = null) {
    const wrapper = document.createElement('label');
    wrapper.htmlFor = `${prefix}-${name}`;
    wrapper.textContent = label;
    const input = document.createElement(options ? 'select' : 'input');
    input.id = wrapper.htmlFor;
    input.name = name;
    if (options) {
      for (const value of ['', ...options]) {
        const option = document.createElement('option');
        option.value = value;
        option.textContent = value || 'Alle';
        input.append(option);
      }
    } else input.type = type;
    if (name === 'q') {
      input.placeholder = 'Navn, citat eller emne';
      wrapper.className = 'search-field';
    }
    wrapper.append(input);
    form.append(wrapper);
    fields[name] = input;
  }
  const choices = key => [...new Set(data.flatMap(item => item[key]).filter(Boolean))].sort((a,b) => a.localeCompare(b, 'da'));
  field('q', 'Søg i materialet');
  if (choices('actors').length) field('aktor', 'Aktør', 'text', choices('actors'));
  if (choices('themes').length) field('tema', 'Tema', 'text', choices('themes'));
  if (choices('kind').length) field('type', 'Type', 'text', choices('kind'));
  if (data.some(item => item.date)) {
    field('fra', 'Fra dato', 'date');
    field('til', 'Til dato', 'date');
  }
  const reset = document.createElement('button');
  reset.type = 'button';
  reset.textContent = 'Nulstil filtre';
  reset.className = 'filter-reset';
  form.append(reset);
  const status = document.createElement('p');
  status.className = 'filter-status';
  status.setAttribute('role', 'status');
  status.setAttribute('aria-live', 'polite');
  const empty = document.createElement('p');
  empty.className = 'filter-empty';
  empty.textContent = 'Ingen resultater. Prøv et andet søgeord eller nulstil filtrene.';
  empty.hidden = true;
  collection.prepend(form, status, empty);

  function saveUrl() {
    const url = new URL(location.href);
    let target;
    try { target = document.getElementById(decodeURIComponent(url.hash.slice(1))); } catch {}
    if (target?.closest('[data-record]')?.hidden) url.hash = '';
    for (const [name, input] of Object.entries(fields)) {
      if (input.value) url.searchParams.set(name, input.value);
      else url.searchParams.delete(name);
    }
    history.replaceState(null, '', url);
  }
  function apply(updateUrl = false) {
    const get = name => fields[name]?.value || '';
    const words = normalize(get('q')).trim().split(/\s+/).filter(Boolean);
    let count = 0;
    for (const item of data) {
      const match = words.every(word => item.text.includes(word)) &&
        (!get('aktor') || item.actors.includes(get('aktor'))) &&
        (!get('tema') || item.themes.includes(get('tema'))) &&
        (!get('type') || item.kind === get('type')) &&
        (!get('fra') || item.date >= get('fra')) &&
        (!get('til') || item.date <= get('til'));
      item.record.hidden = !match;
      if (match) count++;
    }
    for (const section of collection.querySelectorAll('.actor-section')) {
      section.hidden = ![...section.querySelectorAll('[data-record]')].some(record => !record.hidden);
    }
    const invalidRange = get('fra') && get('til') && get('fra') > get('til');
    status.textContent = invalidRange ? 'Fra-datoen skal ligge før eller på til-datoen.' : `Viser ${count} af ${records.length} registreringer.`;
    empty.hidden = count !== 0 || Boolean(invalidRange);
    if (updateUrl) saveUrl();
  }
  function clear() {
    for (const input of Object.values(fields)) input.value = '';
    apply(true);
  }
  function loadUrl() {
    const params = new URLSearchParams(location.search);
    for (const [name, input] of Object.entries(fields)) {
      const value = params.get(name) || '';
      if (input.tagName === 'SELECT' && value && ![...input.options].some(option => option.value === value)) {
        const option = document.createElement('option');
        option.value = value;
        option.textContent = value;
        input.append(option);
      }
      input.value = value;
    }
    apply();
    revealTarget();
  }
  function revealTarget() {
    let id;
    try { id = decodeURIComponent(location.hash.slice(1)); } catch { return; }
    const target = document.getElementById(id);
    if (!target || !collection.contains(target)) return;
    const record = target.closest('[data-record]');
    if (record?.hidden) clear();
    target.scrollIntoView({block:'start'});
  }
  form.addEventListener('input', () => apply(true));
  form.addEventListener('change', () => apply(true));
  form.addEventListener('submit', event => { event.preventDefault(); apply(true); });
  reset.addEventListener('click', clear);
  addEventListener('popstate', loadUrl);
  addEventListener('hashchange', revealTarget);
  loadUrl();
}
