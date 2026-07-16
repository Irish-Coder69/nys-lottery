const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');

const rootDir = path.resolve(__dirname, '..');
const packageJsonPath = path.join(rootDir, 'package.json');
const bumpScript = path.join(rootDir, 'scripts', 'bump-version.js');
const issScript = path.join(rootDir, 'native', 'installer', 'NysLotteryNative.iss');
const nativeCsproj = path.join(rootDir, 'native', 'NysLottery.Native', 'NysLottery.Native.csproj');

const args = process.argv.slice(2);
const dryRun = args.includes('--dry-run');
const levelArg = args.find((a) => ['patch', 'minor', 'major'].includes(a));
const level = levelArg || 'patch';

function run(command, commandArgs, options = {}) {
  const printable = `${command} ${commandArgs.join(' ')}`.trim();
  console.log(`\n> ${printable}`);

  if (dryRun) {
    return { status: 0, stdout: '', stderr: '' };
  }

  const result = spawnSync(command, commandArgs, {
    cwd: rootDir,
    stdio: 'inherit',
    shell: false,
    ...options
  });

  if (result.status !== 0) {
    throw new Error(`Command failed: ${printable}`);
  }

  return result;
}

function runCapture(command, commandArgs) {
  const result = spawnSync(command, commandArgs, {
    cwd: rootDir,
    encoding: 'utf8'
  });

  return {
    status: result.status,
    stdout: (result.stdout || '').trim(),
    stderr: (result.stderr || '').trim()
  };
}

function getVersion() {
  const pkg = JSON.parse(fs.readFileSync(packageJsonPath, 'utf8'));
  return pkg.version;
}

function findIsccPath() {
  const candidates = [
    path.join(process.env.LOCALAPPDATA || '', 'Programs', 'Inno Setup 6', 'ISCC.exe'),
    path.join(process.env.ProgramFiles || '', 'Inno Setup 6', 'ISCC.exe'),
    path.join(process.env['ProgramFiles(x86)'] || '', 'Inno Setup 6', 'ISCC.exe')
  ].filter(Boolean);

  for (const candidate of candidates) {
    if (fs.existsSync(candidate)) {
      return candidate;
    }
  }

  return null;
}

function getCurrentBranch() {
  const result = runCapture('git', ['rev-parse', '--abbrev-ref', 'HEAD']);
  if (result.status === 0 && result.stdout) {
    return result.stdout;
  }

  return 'master';
}

function commitReleaseFiles(version) {
  // Include all pending workspace changes so every release is fully reproducible from one commit.
  run('git', ['add', '-A']);

  const status = runCapture('git', ['diff', '--cached', '--name-only']);
  if (status.status !== 0 || !status.stdout) {
    console.log('No changes to commit for this release.');
    return;
  }

  run('git', ['commit', '-m', `Release v${version}`]);
  run('git', ['push']);
}

function releaseOnGitHub(version, installerPath, branch) {
  const tag = `v${version}`;

  if (dryRun) {
    console.log(`DRY RUN: would create new release ${tag} with ${installerPath}`);
    return;
  }

  run('gh', [
    'release',
    'create',
    tag,
    installerPath,
    '--target',
    branch,
    '--title',
    `${tag} - Native Windows Installer`,
    '--notes',
    'Automated native installer release.'
  ]);

  console.log(`Created new release ${tag}.`);
}

function main() {
  console.log(`Native release automation (level=${level}, dryRun=${dryRun})`);

  run(process.execPath, [bumpScript, level]);

  let version = getVersion();
  if (!dryRun) {
    while (runCapture('gh', ['release', 'view', `v${version}`]).status === 0) {
      // Guarantee every execution maps to a brand-new release tag.
      run(process.execPath, [bumpScript, 'patch']);
      version = getVersion();
    }
  } else {
    console.log(`DRY RUN: skipping unique-tag bump loop for v${version}.`);
  }

  const branch = getCurrentBranch();
  const installerFileName = `NysLottery-Native-Setup-${version}.exe`;
  const installerPath = path.join('native', 'installer-output', installerFileName);

  commitReleaseFiles(version);

  run('dotnet', [
    'publish',
    nativeCsproj,
    '-c',
    'Release',
    '-r',
    'win-x64',
    '-p:PublishSingleFile=true',
    '--self-contained',
    'true'
  ]);

  const isccPath = findIsccPath();
  if (!isccPath) {
    throw new Error('ISCC.exe not found. Install Inno Setup 6 first.');
  }

  run(isccPath, [issScript]);

  if (!dryRun && !fs.existsSync(path.join(rootDir, installerPath))) {
    throw new Error(`Installer not found: ${installerPath}`);
  }

  releaseOnGitHub(version, installerPath, branch);

  console.log(`\nCompleted native release v${version}.`);
}

main();
