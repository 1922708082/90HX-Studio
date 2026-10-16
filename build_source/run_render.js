const { spawnSync } = require('child_process');
const path = require('path');

const framework = path.join('C:', 'Windows', 'Microsoft.NET', 'Framework64', 'v4.0.30319');
const csc = path.join(framework, 'csc.exe');
const gacMsil = path.join('C:', 'Windows', 'Microsoft.NET', 'assembly', 'GAC_MSIL');
const gac64 = path.join('C:', 'Windows', 'Microsoft.NET', 'assembly', 'GAC_64');

const targetExe = path.join('D:', 'app', 'build_source', 'RenderNewView.exe');

const args = [
  '/target:exe',
  '/optimize+',
  '/out:' + targetExe,
  '/r:System.dll',
  '/r:System.Core.dll',
  '/r:' + path.join(gacMsil, 'WindowsBase', 'v4.0_4.0.0.0__31bf3856ad364e35', 'WindowsBase.dll'),
  '/r:' + path.join(gac64, 'PresentationCore', 'v4.0_4.0.0.0__31bf3856ad364e35', 'PresentationCore.dll'),
  '/r:' + path.join(gacMsil, 'PresentationFramework', 'v4.0_4.0.0.0__31bf3856ad364e35', 'PresentationFramework.dll'),
  '/r:' + path.join(gacMsil, 'System.Xaml', 'v4.0_4.0.0.0__b77a5c561934e089', 'System.Xaml.dll'),
  path.join('D:', 'app', 'build_source', 'RenderNewView.cs')
];

const r = spawnSync(csc, args, { encoding: 'utf8' });
if (r.status === 0) {
  console.log('[OK] 编译成功，开始执行渲染...');
  const run = spawnSync(targetExe, [], { encoding: 'utf8' });
  console.log(run.stdout);
  if (run.stderr) console.error(run.stderr);
} else {
  console.error('编译失败:', r.stderr || r.stdout);
}
