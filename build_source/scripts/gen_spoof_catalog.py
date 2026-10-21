# -*- coding: utf-8 -*-
"""从 js/app.js 的 MOCK.spoofModels 生成 src/SpoofCatalog.cs

一次性工具：确保 C# 侧型号表与页面现有那份完全一致，避免手抄错漏。
"""
import io
import json
import os
import re

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
src = io.open(os.path.join(BASE, 'web', 'js', 'app.js'), encoding='utf-8').read()

m = re.search(r'spoofModels:\s*\{(.*?)\n        \},', src, re.S)
assert m, '未找到 spoofModels'
body = m.group(1)

groups = []
for gm in re.finditer(
        r"chip:\s*'([^']*)',\s*\n\s*label:\s*'([^']*)',\s*\n\s*models:\s*\[(.*?)\]",
        body, re.S):
    chip, label, models_blob = gm.group(1), gm.group(2), gm.group(3)
    models = re.findall(r"'([^']*)'", models_blob)
    groups.append((chip, label, models))

rec = re.search(r"recommended:\s*'([^']*)'", body)
recommended = rec.group(1) if rec else 'NVIDIA GeForce RTX 3080'

print('解析到分组:', len(groups), '总型号:', sum(len(g[2]) for g in groups))
for chip, label, models in groups:
    print('  %-8s %-28s %d 款' % (chip, label, len(models)))

lines = []
lines.append('using System.Collections.Generic;')
lines.append('using System.Text;')
lines.append('')
lines.append('namespace CMP90HXToolbox')
lines.append('{')
lines.append('    /// <summary>')
lines.append('    /// 显卡伪装型号目录（静态能力表）。')
lines.append('    ///')
lines.append('    /// 由 C# 单一持有，前端只接收 spoof.models 事件后负责渲染，')
lines.append('    /// 不再在页面里维护副本——避免加型号要改两处、还容易不同步。')
lines.append('    /// </summary>')
lines.append('    internal static class SpoofCatalog')
lines.append('    {')
lines.append('        public const string Recommended = "%s";' % recommended)
lines.append('')
lines.append('        public sealed class Group')
lines.append('        {')
lines.append('            public string Chip;')
lines.append('            public string Label;')
lines.append('            public string[] Models;')
lines.append('')
lines.append('            public Group(string chip, string label, string[] models)')
lines.append('            {')
lines.append('                Chip = chip; Label = label; Models = models;')
lines.append('            }')
lines.append('        }')
lines.append('')
lines.append('        public static List<Group> All()')
lines.append('        {')
lines.append('            List<Group> list = new List<Group>();')
for chip, label, models in groups:
    arr = ', '.join('"%s"' % x.replace('"', '\\"') for x in models)
    lines.append('            list.Add(new Group("%s", "%s", new string[] { %s }));'
                 % (chip, label.replace('"', '\\"'), arr))
lines.append('            return list;')
lines.append('        }')
lines.append('')
lines.append('        /// <summary>拼成页面约定的 spoof.models 事件。</summary>')
lines.append('        public static string ToJson()')
lines.append('        {')
lines.append('            List<Group> groups = All();')
lines.append('            StringBuilder sb = new StringBuilder(6000);')
lines.append('            sb.Append("{\\"kind\\":\\"evt\\",\\"action\\":\\"spoof.models\\",\\"payload\\":{\\"groups\\":[");')
lines.append('            for (int i = 0; i < groups.Count; i++)')
lines.append('            {')
lines.append('                Group g = groups[i];')
lines.append('                if (i > 0) sb.Append(\',\');')
lines.append('                sb.Append("{\\"chip\\":").Append(Quote(g.Chip)).Append(",");')
lines.append('                sb.Append("\\"label\\":").Append(Quote(g.Label)).Append(",");')
lines.append('                sb.Append("\\"models\\":[");')
lines.append('                for (int j = 0; j < g.Models.Length; j++)')
lines.append('                {')
lines.append('                    if (j > 0) sb.Append(\',\');')
lines.append('                    sb.Append(Quote(g.Models[j]));')
lines.append('                }')
lines.append('                sb.Append("]}");')
lines.append('            }')
lines.append('            sb.Append("],\\"recommended\\":").Append(Quote(Recommended)).Append("}}");')
lines.append('            return sb.ToString();')
lines.append('        }')
lines.append('')
lines.append('        private static string Quote(string s)')
lines.append('        {')
lines.append('            if (string.IsNullOrEmpty(s)) return "\\"\\"";')
lines.append('            StringBuilder b = new StringBuilder(s.Length + 4);')
lines.append('            b.Append(\'"\');')
lines.append('            for (int i = 0; i < s.Length; i++)')
lines.append('            {')
lines.append('                char c = s[i];')
lines.append('                if (c == \'"\') b.Append("\\\\\\"");')
lines.append('                else if (c == \'\\\\\') b.Append("\\\\\\\\");')
lines.append('                else b.Append(c);')
lines.append('            }')
lines.append('            b.Append(\'"\');')
lines.append('            return b.ToString();')
lines.append('        }')
lines.append('    }')
lines.append('}')

out = os.path.join(BASE, 'src', 'SpoofCatalog.cs')
io.open(out, 'w', encoding='utf-8', newline='\n').write('\n'.join(lines) + '\n')
print('\n已生成:', out)
