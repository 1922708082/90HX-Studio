const { spawnSync } = require('child_process');
const path = require('path');

const framework = path.join('C:', 'Windows', 'Microsoft.NET', 'Framework64', 'v4.0.30319');
const csc = path.join(framework, 'csc.exe');
const gacMsil = path.join('C:', 'Windows', 'Microsoft.NET', 'assembly', 'GAC_MSIL');
const gac64 = path.join('C:', 'Windows', 'Microsoft.NET', 'assembly', 'GAC_64');

const targetExe = path.join('D:', 'app', 'CMP 90HX 控制中心.exe');

const args = [
  '/target:winexe',
  '/optimize+',
  '/win32manifest:' + path.join('D:', 'app', 'build_source', 'app.manifest'),
  '/win32icon:' + path.join('D:', 'app', 'app.ico'),
  '/resource:' + path.join('D:', 'app', 'build_source', 'src', 'MainWindow.xaml') + ',MainWindow.xaml',
  '/resource:' + path.join('D:', 'app', 'core.pkg') + ',core.pkg',
  '/out:' + targetExe,
  '/r:System.dll',
  '/r:System.Core.dll',
  '/r:System.Windows.Forms.dll',
  '/r:System.Drawing.dll',
  '/r:System.IO.Compression.dll',
  '/r:' + path.join(gacMsil, 'WindowsBase', 'v4.0_4.0.0.0__31bf3856ad364e35', 'WindowsBase.dll'),
  '/r:' + path.join(gac64, 'PresentationCore', 'v4.0_4.0.0.0__31bf3856ad364e35', 'PresentationCore.dll'),
  '/r:' + path.join(gacMsil, 'PresentationFramework', 'v4.0_4.0.0.0__31bf3856ad364e35', 'PresentationFramework.dll'),
  '/r:' + path.join(gacMsil, 'System.Xaml', 'v4.0_4.0.0.0__b77a5c561934e089', 'System.Xaml.dll'),
  path.join('D:', 'app', 'build_source', 'src', 'SystemDetector.cs'),
  path.join('D:', 'app', 'build_source', 'src', 'NativeCore.cs'),
  path.join('D:', 'app', 'build_source', 'src', 'App.cs')
];

const r = spawnSync(csc, args, { encoding: 'utf8' });
console.log('=== status:', r.status, 'signal:', r.signal, 'error:', r.error ? r.error.message : 'none');
if (r.stdout) console.log('=== STDOUT ===\n' + r.stdout);
if (r.stderr) console.log('=== STDERR ===\n' + r.stderr);
if (r.status === 0) console.log('[OK] 编译成功: ' + targetExe);
