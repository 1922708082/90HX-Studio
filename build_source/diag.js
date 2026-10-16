const fs = require('fs');
const path = require('path');

const framework64 = path.join('C:', 'Windows', 'Microsoft.NET', 'Framework64', 'v4.0.30319');
const framework32 = path.join('C:', 'Windows', 'Microsoft.NET', 'Framework', 'v4.0.30319');

function check(p) { console.log((fs.existsSync(p) ? '[OK]   ' : '[MISS] ') + p); }

console.log('=== csc 位置 ===');
check(path.join(framework64, 'csc.exe'));
check(path.join(framework32, 'csc.exe'));

console.log('=== GAC 路径 ===');
const gacMsil = path.join('C:', 'Windows', 'Microsoft.NET', 'assembly', 'GAC_MSIL');
const gac64 = path.join('C:', 'Windows', 'Microsoft.NET', 'assembly', 'GAC_64');
const gac32 = path.join('C:', 'Windows', 'Microsoft.NET', 'assembly', 'GAC_32');

check(path.join(gacMsil, 'WindowsBase', 'v4.0_4.0.0.0__31bf3856ad364e35', 'WindowsBase.dll'));
check(path.join(gac64, 'PresentationCore', 'v4.0_4.0.0.0__31bf3856ad364e35', 'PresentationCore.dll'));
check(path.join(gacMsil, 'PresentationFramework', 'v4.0_4.0.0.0__31bf3856ad364e35', 'PresentationFramework.dll'));
check(path.join(gacMsil, 'System.Xaml', 'v4.0_4.0.0.0__b77a5c561934e089', 'System.Xaml.dll'));

console.log('=== 源文件 ===');
check(path.join('D:', 'app', 'build_source', 'src', 'App.cs'));
check(path.join('D:', 'app', 'build_source', 'src', 'NativeCore.cs'));
check(path.join('D:', 'app', 'build_source', 'src', 'SystemDetector.cs'));
check(path.join('D:', 'app', 'build_source', 'src', 'MainWindow.xaml'));
check(path.join('D:', 'app', 'core.pkg'));
check(path.join('D:', 'app', 'app.ico'));
check(path.join('D:', 'app', 'build_source', 'app.manifest'));

// 尝试列出 GAC_64 下所有 PresentationCore 版本
console.log('=== PresentationCore 版本目录 ===');
const pc = path.join(gac64, 'PresentationCore');
if (fs.existsSync(pc)) { console.log(fs.readdirSync(pc).join('\n')); }
const pcMsil = path.join(gacMsil, 'PresentationCore');
if (fs.existsSync(pcMsil)) { console.log('[GAC_MSIL] ' + fs.readdirSync(pcMsil).join(', ')); }
