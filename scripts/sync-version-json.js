const fs = require('fs');
const path = require('path');

const rootDir = path.resolve(__dirname, '..');
const packageJsonPath = path.join(rootDir, 'package.json');
const versionJsonPath = path.join(rootDir, 'version.json');

const pkg = JSON.parse(fs.readFileSync(packageJsonPath, 'utf8'));

let manifest = {
  version: pkg.version,
  releaseDate: new Date().toISOString().slice(0, 10),
  notes: 'Release update.',
  downloadUrl: 'https://github.com/Irish-Coder69/nys-lottery/releases/latest',
  minimumSupportedVersion: pkg.version
};

if (fs.existsSync(versionJsonPath)) {
  const existing = JSON.parse(fs.readFileSync(versionJsonPath, 'utf8'));
  manifest = {
    ...existing,
    version: pkg.version,
    releaseDate: new Date().toISOString().slice(0, 10),
    downloadUrl: existing.downloadUrl || manifest.downloadUrl
  };
}

fs.writeFileSync(versionJsonPath, `${JSON.stringify(manifest, null, 2)}\n`, 'utf8');
console.log(`Synced version.json to version ${manifest.version}`);
