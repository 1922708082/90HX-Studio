# -*- coding: utf-8 -*-
"""一次性工具：把单文件 index.html 拆成 web/ 下的模块化源文件。

产物：
  web/index.template.html   外壳模板（含 {{CSS}} / {{PAGES}} / {{JS}} 占位）
  web/css/01-base.css ... 05-overrides.css
  web/pages/<page>.html     10 个页面片段
  web/js/app.js             前端逻辑

合并由 scripts/merge-web.ps1 完成，生成可直接打开/内嵌的 index.html。
"""
import io
import os
import re

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(BASE, 'web', 'index.html')

s = io.open(SRC, encoding='utf-8').read()

# ---------- 1. 定位三大区域 ----------
m_style = re.search(r'<style>(.*?)</style>', s, re.S)
m_script = re.search(r'<script>(.*?)</script>', s, re.S)
assert m_style and m_script, '找不到 style/script 区域'

css = m_style.group(1)
js = m_script.group(1)

# ---------- 2. 切分 CSS（保持原始顺序，cascade 不变）----------
css_lines = css.split('\n')
cuts = [0, 385, 968, 2909, 3689, len(css_lines)]
names = ['01-base.css', '02-layout.css', '03-components.css',
         '04-pages.css', '05-overrides.css']
os.makedirs(os.path.join(BASE, 'web', 'css'), exist_ok=True)
for i, nm in enumerate(names):
    chunk = '\n'.join(css_lines[cuts[i]:cuts[i + 1]])
    io.open(os.path.join(BASE, 'web', 'css', nm), 'w', encoding='utf-8').write(
        '/* %s · 由 index.html 拆分，顺序不可调换（层叠覆盖依赖此顺序） */\n%s' % (nm, chunk))
print('CSS 拆分:', ', '.join(names))

# ---------- 3. 提取 10 个页面 ----------
os.makedirs(os.path.join(BASE, 'web', 'pages'), exist_ok=True)
pages = []
for m in re.finditer(r'<div id="view-([a-z]+)" class="view-panel[^"]*">', s):
    name = m.group(1)
    start = m.start()
    # 从 start 起做 div 深度扫描
    depth = 0
    i = start
    end = None
    for tag in re.finditer(r'<div\b[^>]*>|</div>', s[start:]):
        if tag.group(0).startswith('</'):
            depth -= 1
            if depth == 0:
                end = start + tag.end()
                break
        else:
            depth += 1
    assert end, '页面 %s 未能闭合' % name
    pages.append((name, start, end))

print('页面数:', len(pages))
page_files = []
for name, a, b in pages:
    body = s[a:b]
    fn = os.path.join(BASE, 'web', 'pages', name + '.html')
    io.open(fn, 'w', encoding='utf-8').write(
        '<!-- PAGE: %s · 单独维护，构建时注入 index.html 的 #pageHost -->\n%s\n' % (name, body))
    page_files.append(name)

# ---------- 4. 生成模板 ----------
tpl = s
# 4.1 页面区：把所有页面 div 替换成一个占位符
marker = '          <!-- {{PAGES}} 构建时按 pages/ 顺序注入 -->\n'
first_a = pages[0][1]
last_b = pages[-1][2]
tpl = tpl[:first_a] + marker + tpl[last_b:]
# 4.2 样式区
tpl = tpl.replace(m_style.group(0), '<style>\n{{CSS}}\n  </style>', 1)
# 4.3 脚本区（重新定位，前面替换后位置变了）
m2 = re.search(r'<script>(.*?)</script>', tpl, re.S)
tpl = tpl[:m2.start()] + '<script>\n{{JS}}\n  </script>' + tpl[m2.end():]

io.open(os.path.join(BASE, 'web', 'index.template.html'), 'w', encoding='utf-8').write(tpl)
print('模板已生成，行数:', tpl.count('\n') + 1)

# ---------- 5. 脚本 ----------
os.makedirs(os.path.join(BASE, 'web', 'js'), exist_ok=True)
io.open(os.path.join(BASE, 'web', 'js', 'app.js'), 'w', encoding='utf-8').write(js.strip('\n') + '\n')
print('JS 行数:', js.count('\n') + 1)
