const { spawnSync } = require('child_process');
const path = require('path');

const framework = path.join('C:', 'Windows', 'Microsoft.NET', 'Framework64', 'v4.0.30319');
const csc = path.join(framework, 'csc.exe');
const gacMsil = path.join('C:', 'Windows', 'Microsoft.NET', 'assembly', 'GAC_MSIL');
const gac64 = path.join('C:', 'Windows', 'Microsoft.NET', 'assembly', 'GAC_64');

const outExe = path.join('D:', 'app', 'build_source', 'RenderAllTabsV2.exe');

const args = [
  '/target:exe',
  '/optimize+',
  '/out:' + outExe,
  '/r:System.dll',
  '/r:System.Core.dll',
  '/r:' + path.join(gacMsil, 'WindowsBase', 'v4.0_4.0.0.0__31bf3856ad364e35', 'WindowsBase.dll'),
  '/r:' + path.join(gac64, 'PresentationCore', 'v4.0_4.0.0.0__31bf3856ad364e35', 'PresentationCore.dll'),
  '/r:' + path.join(gacMsil, 'PresentationFramework', 'v4.0_4.0.0.0__31bf3856ad364e35', 'PresentationFramework.dll'),
  '/r:' + path.join(gacMsil, 'System.Xaml', 'v4.0_4.0.0.0__b77a5c561934e089', 'System.Xaml.dll'),
  path.join('D:', 'app', 'build_source', 'RenderAllTabsV2.cs')
];

const r = spawnSync(csc, args, { encoding: 'utf8' });
console.log('status:', r.status, 'error:', r.error ? r.error.message : 'none');
if (r.stdout) console.log('STDOUT:\n' + r.stdout);
if (r.stderr) console.log('STDERR:\n' + r.stderr);
