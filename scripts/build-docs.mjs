import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const destination = path.resolve(process.argv.find(a => a.startsWith('--out='))?.slice(6) ?? path.join(root, 'site'));
const check = process.argv.includes('--check');
const origin = 'https://pixelviewport-vision.glassyloach1.chatgpt.site';
const github = 'https://github.com/TheEdster3/PixelViewport/blob/main/docs/';
const pages = [
  ['index', 'docs', 'Overview'], ['demo', 'demo', 'Windows demo'], ['quickstart', 'quickstart', 'Quickstart'],
  ['api', 'api', 'API reference'], ['ownership', 'ownership', 'Ownership & threading'],
  ['recipes', 'recipes', 'Compiled recipes'], ['ai-integration', 'ai-integration', 'AI integration'],
  ['troubleshooting', 'troubleshooting', 'Troubleshooting'], ['performance', 'performance-guide', 'Performance'],
  ['dependencies', 'dependencies', 'Dependencies & licenses']
];
const routes = new Map(pages.map(([source, route]) => [`${source}.md`, `${route}.html`]));
const snippets = fs.readFileSync(path.join(root, 'docs/examples/IntegrationExamples.cs'), 'utf8');
const escape = value => value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
const slug = value => value.toLowerCase().replace(/[^a-z0-9 -]/g, '').trim().replace(/\s+/g, '-');
function link(value) {
  if (value.startsWith('#') || /^https:\/\//.test(value)) return value;
  const [file, fragment] = value.split('#');
  return (routes.get(file) ?? github + file) + (fragment ? '#' + fragment : '');
}
function inline(value) {
  const tokens = [];
  const token = html => `\u0001${tokens.push(html) - 1}\u0002`;
  let result = value.replace(/`([^`]+)`/g, (_, code) => token(`<code>${escape(code)}</code>`));
  result = result.replace(/\[([^\]]+)\]\(([^)]+)\)/g, (_, label, url) => token(`<a href="${escape(link(url))}">${escape(label)}</a>`));
  result = escape(result).replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
  return result.replace(/\u0001(\d+)\u0002/g, (_, id) => tokens[Number(id)]);
}
function expand(source) {
  return source.replace(/<!-- snippet:([\w-]+) -->/g, (_, id) => {
    if (id === 'imports') return '```csharp\n' + snippets.split('namespace PixelViewport.Documentation;')[0].trim() + '\n```';
    const match = snippets.match(new RegExp(`// <doc:${id}>\\r?\\n([\\s\\S]*?)\\s*// </doc:${id}>`));
    if (!match) throw new Error(`Missing compiled snippet: ${id}`);
    const lines = match[1].trimEnd().split(/\r?\n/);
    const indent = Math.min(...lines.filter(s => s.trim()).map(s => s.match(/^ */)[0].length));
    return '```csharp\n' + lines.map(s => s.slice(indent)).join('\n') + '\n```';
  });
}
function render(markdown) {
  const lines = markdown.split(/\r?\n/), result = [], toc = [], used = new Map();
  for (let i = 0; i < lines.length;) {
    const line = lines[i];
    if (!line.trim()) { i++; continue; }
    if (line.startsWith('```')) {
      const language = line.slice(3).trim(), code = []; i++;
      while (i < lines.length && !lines[i].startsWith('```')) code.push(lines[i++]);
      if (i === lines.length) throw new Error('Unclosed code fence');
      i++; result.push(`<div class="doc-code"><span>${escape(language || 'text')}</span><pre><code>${escape(code.join('\n'))}</code></pre></div>`); continue;
    }
    const heading = line.match(/^(#{1,6}) (.+)$/);
    if (heading) {
      const level = heading[1].length, base = slug(heading[2]), count = used.get(base) ?? 0;
      used.set(base, count + 1); const id = base + (count ? '-' + count : '');
      result.push(`<h${level} id="${id}">${inline(heading[2])}</h${level}>`);
      if (level === 2) toc.push([id, heading[2]]); i++; continue;
    }
    if (line.startsWith('|') && /^\|[\s:|-]+\|$/.test(lines[i+1] ?? '')) {
      const cells = row => row.trim().slice(1,-1).split('|').map(s => s.trim());
      const header = cells(line); i += 2; const rows = [];
      while ((lines[i] ?? '').startsWith('|')) rows.push(cells(lines[i++]));
      result.push(`<div class="doc-table" tabindex="0" role="region" aria-label="Reference table"><table><thead><tr>${header.map(c => `<th scope="col">${inline(c)}</th>`).join('')}</tr></thead><tbody>${rows.map(row => '<tr>' + row.map(c => `<td>${inline(c)}</td>`).join('') + '</tr>').join('')}</tbody></table></div>`); continue;
    }
    if (/^\s*(?:[-*] |\d+\. )/.test(line)) {
      const ordered = /^\s*\d+\. /.test(line), tag = ordered ? 'ol' : 'ul', items = [];
      while (i < lines.length && /^\s*(?:[-*] |\d+\. )/.test(lines[i])) items.push(inline(lines[i++].replace(/^\s*(?:[-*] |\d+\. )/, '')));
      result.push(`<${tag}>${items.map(item => `<li>${item}</li>`).join('')}</${tag}>`); continue;
    }
    if (line.startsWith('> ')) { result.push(`<blockquote>${inline(line.slice(2))}</blockquote>`); i++; continue; }
    const paragraph = [line]; i++;
    while (i < lines.length && lines[i].trim() && !/^(?:#|```|\||> |[-*] |\d+\. )/.test(lines[i])) paragraph.push(lines[i++]);
    result.push(`<p>${inline(paragraph.join(' '))}</p>`);
  }
  return { html: result.join('\n'), toc };
}
const documents = pages.map(([source, route, title]) => ({ source, route, title, markdown: expand(fs.readFileSync(path.join(root, 'docs', source + '.md'), 'utf8')) }));
const outputs = new Map();
for (const document of documents) {
  const { html, toc } = render(document.markdown);
  const navigation = documents.map(d => `<a href="${d.route}.html"${d.route === document.route ? ' aria-current="page"' : ''}>${escape(d.title)}</a>`).join('');
  outputs.set(document.route + '.html', `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>${escape(document.title)} — PixelViewport documentation</title><meta name="description" content="PixelViewport Community SDK: ${escape(document.title)}. Versioned contracts, compiled examples, and explicit evaluation boundaries."><link rel="canonical" href="${origin}/${document.route}.html"><link rel="icon" href="favicon.svg"><link rel="stylesheet" href="styles.css"><link rel="stylesheet" href="docs.css"></head>
<body class="docs-body"><a class="skip" href="#doc-content">Skip to content</a><header class="header wrap"><a class="brand" href="index.html"><img class="brand-mark" src="favicon.svg" alt="" width="30" height="30">PixelViewport<span class="brand-vision">VISION</span></a><nav aria-label="Main navigation"><a href="index.html#playground">Live demo</a><a href="docs.html">Docs</a><a class="nav-cta" href="demo.html">Windows download</a></nav></header>
<div class="doc-status wrap"><span>DEVELOPER REFERENCE / COMMUNITY ALPHA</span><a href="llms-full.txt">Plain-text AI context ↗</a></div>
<div class="doc-layout wrap"><aside class="doc-nav"><details open><summary>Documentation</summary><nav aria-label="Documentation">${navigation}</nav></details><div class="doc-source"><span>SOURCE OF TRUTH</span><a href="${github}${document.source}.md">View this document on GitHub ↗</a><a href="sdk-manifest.json">Structured SDK manifest ↗</a></div></aside><main id="doc-content" class="doc-content">${html}<div class="doc-end">Public MIT foundation. Windows verified; Linux unverified. Alpha APIs may change.<br><a href="${github}${document.source}.md">Inspect source and history</a> · <a href="https://github.com/TheEdster3/PixelViewport/issues/new?template=bug.yml">Report an issue</a></div></main><aside class="doc-toc"><span>ON THIS PAGE</span><nav aria-label="On this page">${toc.map(([id, text]) => `<a href="#${id}">${escape(text)}</a>`).join('')}</nav></aside></div>
<footer class="footer wrap"><p>PixelViewport / image display for .NET vision teams.</p><div class="footer-links"><a href="https://github.com/TheEdster3/PixelViewport">GitHub</a><a href="privacy.html">Privacy</a></div></footer></body></html>\n`);
}
outputs.set('llms.txt', '# PixelViewport Vision\n\nPublic Community alpha: vendor-neutral .NET image display and interaction. Windows verified; Linux unverified. No production GPU renderer, WPF adapter, window/level, editable ROI, or acquisition API.\n\n' + documents.map(d => `- [${d.title}](${origin}/${d.route}.html)`).join('\n') + `\n\nFull context: ${origin}/llms-full.txt\nCompiled examples: ${github}examples/IntegrationExamples.cs\nStructured manifest: ${origin}/sdk-manifest.json\nThese files are explicit context resources, not a guarantee of automatic AI discovery.\n`);
outputs.set('llms-full.txt', '# PixelViewport Community SDK — integration context\n\nGenerated from source documentation and compiled examples. Public API only; roadmap items are not implementations. Pin a release and inspect source before relying on an alpha contract.\n\n' + documents.map(d => `\n---\nSOURCE: docs/${d.source}.md\n\n${d.markdown}`).join('\n') + '\n');
const version = fs.readFileSync(path.join(root, 'src/PixelViewport.Imaging/PixelViewport.Imaging.csproj'), 'utf8').match(/<Version>([^<]+)<\/Version>/)[1];
outputs.set('sdk-manifest.json', JSON.stringify({ schemaVersion: 1, product: 'PixelViewport Community', version, targetFramework: 'net8.0', verifiedPlatform: 'Windows', unverifiedPlatforms: ['Linux', 'embedded hardware'], packages: ['Core','Imaging','Avalonia','OpenCvSharp','WinUI'].map(p => `PixelViewport.${p}`), formats: ['Gray8','Gray16LittleEndian','Rgb24','Bgr24','Rgba32','Bgra32'], renderer: 'UI-thread CPU conversion / Avalonia WriteableBitmap', submission: 'LatestFrameMailbox / bounded single pending frame / ownership transfer', inspectionMember: 'PixelSample.BitsPerChannel', unavailable: ['GPU renderer','WPF adapter','window/level','editable ROI','calibrated measurements','camera acquisition'], docs: documents.map(d => ({ title: d.title, url: `${origin}/${d.route}.html`, source: `docs/${d.source}.md` })) }, null, 2) + '\n');
for (const [name, content] of outputs) {
  const target = path.join(destination, name);
  if (check) { if (!fs.existsSync(target) || fs.readFileSync(target, 'utf8') !== content) throw new Error(`Generated documentation is stale: ${name}`); }
  else { fs.mkdirSync(destination, { recursive: true }); fs.writeFileSync(target, content); }
}
// Validate local links and section anchors across the generated portal.
for (const [name, content] of outputs) {
  if (!name.endsWith('.html')) continue;
  for (const match of content.matchAll(/href="([^"<>]+)"/g)) {
    const href = match[1]; if (/^https:\/\//.test(href)) continue;
    const [file, anchor] = href.split('#'), targetName = file || name;
    const target = outputs.get(targetName) ?? (fs.existsSync(path.join(destination, targetName)) ? fs.readFileSync(path.join(destination, targetName), 'utf8') : null);
    if (target === null) throw new Error(`Broken local link in ${name}: ${href}`);
    if (anchor && !target.includes(`id="${anchor}"`)) throw new Error(`Broken anchor in ${name}: ${href}`);
  }
}
console.log(`Documentation ${check ? 'verified' : 'generated'}: ${documents.length} pages, compiled recipes, AI text bundles, and SDK manifest.`);
