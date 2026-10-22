# -*- coding: utf-8 -*-
"""前端 ↔ 后端 契约一致性审计

四张清单做交叉差集，找出「配不齐」的所有位置：
  A. 前端会发的命令      sendCmd('x')  +  data-action="x"
  B. 前端会处理的事件    evt 分支里的 case 'x'
  C. 后端实现了的命令    WebShell 里的 action == "x"
  D. 后端会发出的事件    C# 里 "action":"x"

告警类型：
  1. 前端发 → 后端没实现      （点了没反应 / 弹 NOT_SUPPORTED）
  2. 前端等 → 后端没发         （页面永远停在等待态）
  3. 后端发 → 前端不处理       （死事件，白推）
"""
import io
import os
import re

BASE = r'D:\app\build_source'
WEB = os.path.join(BASE, 'web')
SRC = os.path.join(BASE, 'src')


def read(p):
    return io.open(p, encoding='utf-8', errors='ignore').read()


js = read(os.path.join(WEB, 'js', 'app.js'))
html = read(os.path.join(WEB, 'index.html'))

cs_files = []
for f in os.listdir(SRC):
    if f.endswith('.cs'):
        cs_files.append(os.path.join(SRC, f))
cs = '\n'.join(read(f) for f in cs_files)

# ---------- A. 前端会发的命令 ----------
sent = set(re.findall(r"sendCmd\(\s*'([a-zA-Z][a-zA-Z0-9.]*)'", js))
# 变量形式 sendCmd(action, ...)：把这些 action 从外层 case 里补进来
for m in re.finditer(r"case\s+'([a-zA-Z][a-zA-Z0-9.]*)':\s*(.*?)break;", js, re.S):
    if 'sendCmd(action' in m.group(2) or 'sendCmd(pc.action' in m.group(2):
        sent.add(m.group(1))
# data-action 里由点击委托转成命令的
for a in re.findall(r'data-action="([a-zA-Z][a-zA-Z0-9.]*)"', html):
    sent.add(a)
# 二次确认弹窗里的 action（modal.execute 用 sendCmd(pc.action) 变量形式发送，
# 之前漏了这一类：unlock.install / snapshot.restore / factory.reset 等都从这里来）
for a in re.findall(r'data-confirm-type="([a-zA-Z][a-zA-Z0-9.]*)"', js):
    sent.add(a)
for a in re.findall(r"confirmType === '([a-zA-Z][a-zA-Z0-9.]*)'", js):
    sent.add(a)

# ---------- B. 前端会处理的事件 ----------
# 只认「把 payload 直接塞进 state 的」那些 case，即真正的 evt 处理器；
# 命令应答分支里虽然也叫 case，但走的是 handleCommandResponse，不能混算。
evt_handled = set()
for m in re.finditer(r"case\s+'([a-zA-Z][a-zA-Z0-9.]*)':\s*\n\s*state\.[A-Za-z0-9]+\s*=\s*payload", js):
    evt_handled.add(m.group(1))
for m in re.finditer(r"case\s+'([a-zA-Z][a-zA-Z0-9.]*)':\s*\n\s*state\.status\s*=\s*'ready'", js):
    evt_handled.add(m.group(1))
# 裸事件（无 state，只 render）
for m in re.finditer(r"case\s+'([a-zA-Z][a-zA-Z0-9.]*)':\s*\n\s*(?:pushEvt|render)[A-Za-z]*", js):
    pass

# ---------- C. 后端实现了的命令 ----------
implemented = set(re.findall(r'action\s*==\s*"([a-zA-Z][a-zA-Z0-9.]*)"', cs))
implemented |= set(re.findall(r'action\s*==\s*string\.Equals\([^,]*,\s*"([a-zA-Z][a-zA-Z0-9.]*)"', cs))

# ---------- D. 后端发出的事件 ----------
emitted = set(re.findall(r'\\"action\\":\s*\\"([a-zA-Z][a-zA-Z0-9.]*)\\"', cs))
emitted |= set(re.findall(r'"action":"([a-zA-Z][a-zA-Z0-9.]*)"', cs))
# 用常量拼接的（如 ThemeEvt/VersionEvt 传入 action 名）
emitted |= set(re.findall(r'Evt\("([a-zA-Z][a-zA-Z0-9.]*)"', cs))

# 前端只在前端内部消化的动作（不发给宿主）
local_only = {
    'nav.switch', 'modal.confirm', 'modal.close', 'modal.execute', 'ui.toggleTheme',
    'ui.togglePerfLow', 'ui.toggleSafetyCover', 'unlock.selectCard', 'unlock.selectChannel',
    'snapshot.filterKind', 'logs.filterLevel', 'logs.loadMore', 'logs.scrollToBottom',
    'power.setPreset', 'dlss.preflight',   # dual.bind/unbind 会真发给宿主且未实现，不能再当纯前端动作
}

print('=' * 68)
print('前端发出的动作 %d 个 | 后端实现 %d 个 | 后端事件 %d 个 | 前端处理 %d 个'
      % (len(sent), len(implemented), len(emitted), len(evt_handled)))
print('=' * 68)

missing_host = sorted(a for a in sent - implemented - local_only)
missing_evt = sorted(a for a in evt_handled - emitted - local_only - implemented)
dead_evt = sorted(a for a in emitted - evt_handled)

print('\n[1] 前端会发、后端没实现（点了会报 NOT_SUPPORTED）: %d' % len(missing_host))
for a in missing_host:
    print('    -', a)

print('\n[2] 前端在等、后端从不发（页面停在等待态）: %d' % len(missing_evt))
for a in missing_evt:
    print('    -', a)

# 注意：[3] 目前是文本匹配的假阳性重灾区（只认 case 下一行直接赋值），
# 不要用它做构建门禁，仅作参考；要修先修识别器再纳入。
print('\n[3] 后端会发、前端不处理（白推）: %d' % len(dead_evt))
for a in dead_evt:
    print('    -', a)

print('\n[4] 后端已实现（供核对）:')
print('    ', ', '.join(sorted(implemented)))
print('\n[5] 后端已发事件（供核对）:')
print('    ', ', '.join(sorted(emitted)))
