const fs = require('fs');
const path = require('path');

const rootDir = path.resolve(__dirname, '..');
const packageJsonPath = path.join(rootDir, 'package.json');
const versionJsonPath = path.join(rootDir, 'version.json');
const nativeCsprojPath = path.join(rootDir, 'native', 'NysLottery.Native', 'NysLottery.Native.csproj');
const installerIssPath = path.join(rootDir, 'native', 'installer', 'NysLotteryNative.iss');

function parseSemver(version) {
  const match = String(version).trim().match(/^(\d+)\.(\d+)\.(\d+)$/);
  if (!match) {
    throw new Error(`Invalid semver: ${version}`);
  }

  return {
    major: Number.parseInt(match[1], 10),
    minor: Number.parseInt(match[2], 10),
    patch: Number.parseInt(match[3], 10)
  };
}

function formatSemver(v) {
  return `${v.major}.${v.minor}.${v.patch}`;
}

function bump(version, level) {
  const next = parseSemver(version);

  if (level === 'major') {
    next.major += 1;
    next.minor = 0;
    next.patch = 0;
  } else if (level === 'minor') {
    next.minor += 1;
    next.patch = 0;
  } else {
    next.patch += 1;
  }

  return formatSemver(next);
}

function updatePackageJson(newVersion) {
  const pkg = JSON.parse(fs.readFileSync(packageJsonPath, 'utf8'));
  pkg.version = newVersion;
  fs.writeFileSync(packageJsonPath, `${JSON.stringify(pkg, null, 2)}\n`, 'utf8');
}

function updateVersionManifest(newVersion) {
  let manifest = {
    version: newVersion,
    releaseDate: new Date().toISOString().slice(0, 10),
    notes: 'Release update.',
    downloadUrl: 'https://github.com/Irish-Coder69/nys-lottery/releases/latest',
    minimumSupportedVersion: newVersion
  };

  if (fs.existsSync(versionJsonPath)) {
    const existing = JSON.parse(fs.readFileSync(versionJsonPath, 'utf8'));
    manifest = {
      ...existing,
      version: newVersion,
      releaseDate: new Date().toISOString().slice(0, 10),
      downloadUrl: existing.downloadUrl || manifest.downloadUrl
    };
  }

  fs.writeFileSync(versionJsonPath, `${JSON.stringify(manifest, null, 2)}\n`, 'utf8');
}

function updateNativeCsproj(newVersion) {
  let content = fs.readFileSync(nativeCsprojPath, 'utf8');
  content = content.replace(/<Version>[^<]+<\/Version>/, `<Version>${newVersion}</Version>`);
  content = content.replace(/<AssemblyVersion>[^<]+<\/AssemblyVersion>/, `<AssemblyVersion>${newVersion}.0</AssemblyVersion>`);
  content = content.replace(/<FileVersion>[^<]+<\/FileVersion>/, `<FileVersion>${newVersion}.0</FileVersion>`);
  fs.writeFileSync(nativeCsprojPath, content, 'utf8');
}

function updateInstallerIss(newVersion) {
  let content = fs.readFileSync(installerIssPath, 'utf8');
  content = content.replace(/#define MyAppVersion\s+"[^"]+"/, `#define MyAppVersion "${newVersion}"`);
  fs.writeFileSync(installerIssPath, content, 'utf8');
}

function main() {
  const level = (process.argv[2] || 'patch').toLowerCase();
  if (!['patch', 'minor', 'major'].includes(level)) {
    throw new Error(`Unknown bump level: ${level}`);
  }

  const pkg = JSON.parse(fs.readFileSync(packageJsonPath, 'utf8'));
  const newVersion = bump(pkg.version, level);

  updatePackageJson(newVersion);
  updateVersionManifest(newVersion);
  updateNativeCsproj(newVersion);
  updateInstallerIss(newVersion);

  console.log(`Version bumped (${level}): ${pkg.version} -> ${newVersion}`);
}

main();
