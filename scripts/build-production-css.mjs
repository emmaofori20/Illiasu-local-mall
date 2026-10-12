import { createHash } from 'node:crypto';
import { readdirSync, readFileSync, writeFileSync, unlinkSync, existsSync } from 'node:fs';
import { dirname, resolve, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const manifest = resolve(root, 'build/production-css.sha256');
// A failed compilation must never leave a valid production stamp behind.
if (existsSync(manifest)) unlinkSync(manifest);
const result = spawnSync(process.execPath, [
  resolve(root, 'node_modules/@tailwindcss/cli/dist/index.mjs'),
  '-i', './Styles/app.css', '-o', './wwwroot/css/app.css', '--minify',
], { cwd: root, stdio: 'inherit' });
if (result.error) throw result.error;
if (result.status !== 0) process.exit(result.status ?? 1);

function collect(directory, extension) {
  return readdirSync(resolve(root, directory), { withFileTypes: true }).flatMap(entry => {
    const path = `${directory}/${entry.name}`;
    return entry.isDirectory() ? collect(path, extension)
      : entry.isFile() && path.endsWith(extension) ? [path] : [];
  });
}
const paths = [
  ...collect('Styles', '.css'), ...collect('Views', '.cshtml'),
  ...collect('Areas', '.cshtml'), ...collect('wwwroot/js', '.js'),
  'wwwroot/css/tokens.css', 'package.json', 'package-lock.json',
  'scripts/build-production-css.mjs', 'build/ProductionCss.targets', 'wwwroot/css/app.css',
].sort();
const lines = paths.map(path => {
  // Ignore checkout line-ending differences, but hash all other text changes.
  const text = readFileSync(resolve(root, path), 'utf8').replace(/^\uFEFF/, '').replace(/\r\n/g, '\n');
  if (path === 'wwwroot/css/app.css' && !text.trim()) throw new Error('Production CSS is empty.');
  return `${relative(root, resolve(root, path)).replaceAll('\\', '/')}|${createHash('sha256').update(text).digest('hex')}`;
});
writeFileSync(manifest, `${lines.join('\n')}\n`);
console.log(`Recorded production CSS and ${paths.length - 1} input hashes.`);
