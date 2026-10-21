import io, sys

ROOT = r'D:\app\build_source'
ok = []
fail = []

def patch(rel, old, new, note):
    p = ROOT + '\\' + rel
    s = io.open(p, encoding='utf-8').read()
    if new.strip() and new.strip() in s:
        ok.append(note + ' (already)')
        return
    if old not in s:
        fail.append(note + ' -> anchor not found')
        return
    s = s.replace(old, new, 1)
    io.open(p, 'w', encoding='utf-8').write(s)
    ok.append(note)

# ---------- P0-1 dual.state 字段归一化（前端映射，不动协议） ----------
patch('web\\js\\app.js',
"""          case 'dual.state':
            state.dualState = payload;""",
"""          case 'dual.state': {
            /* 后端发的是嵌套结构 render/display/virtual + boundGames 计数，
               前端历史代码读的是扁平的 hasRenderGpu/renderGpuName…
               这里做一次归一化，避免「显示卡/渲染卡永远未检测到」。
               注意：字段以宿主为准，前端只做映射，不再另存一份数据。 */
            const d0 = payload || {};
            const r0 = d0.render || {};
            const g0 = d0.display || {};
            const v0 = d0.virtual || {};
            d0.hasRenderGpu = !!r0.present;
            d0.renderGpuName = r0.name || '';
            d0.renderGpuPciId = r0.pciId || '';
            d0.hasDisplayGpu = !!g0.present;
            d0.displayGpuName = g0.name || '';
            d0.displayGpuPciId = g0.pciId || '';
            d0.hasVirtualGpu = !!v0.present;
            d0.virtualGpuName = v0.name || '';
            if (typeof d0.boundGames === 'number') d0.boundGamesCount = d0.boundGames;
            if (!Array.isArray(d0.boundApps)) {
              d0.boundApps = Array.isArray(d0.bindings) ? d0.bindings : [];
            }
            state.dualState = d0;""",
'P0-1 dual.state 归一化')

# 上一处改完后是 case 块，需要补右括号（原 case 是单句无括号）
patch('web\\js\\app.js',
"""            state.dualState = d0;
            break;""",
"""            state.dualState = d0;
            break;
          }""",
'P0-1 case 块闭合')

# ---------- P0-2 overlay.state 补 3 个前端必读字段 ----------
patch('src\\HardwareBridge.cs',
"""            sb.Append("\\"nvAppInstalled\\":").Append(Bool(st.NvAppInstalled)).Append(",");""",
"""            // 前端还要读这三个：不补就等于永远显示「未解锁 / —」
            sb.Append("\\"altRReady\\":").Append(Bool(patched)).Append(",");
            sb.Append("\\"altZReady\\":").Append(Bool(patched)).Append(",");
            sb.Append("\\"cefCacheSize\\":\\"").Append(patched ? "已清理" : "—").Append("\\",");
            sb.Append("\\"nvAppInstalled\\":").Append(Bool(st.NvAppInstalled)).Append(",");""",
'P0-2 overlay 补 altR/altZ/cefCacheSize')

# ---------- P0-3 dlss.preflight：把 res 的 data 写进 state 并重渲染 ----------
patch('web\\js\\app.js',
"""          } else if (action === 'dlss.preflight' && Array.isArray(data.items)) {""",
"""          } else if (action === 'dlss.preflight' && Array.isArray(data.items)) {
            /* 宿主只回 res、从不推同 action 的 evt，所以这里必须把结果
               落到 state 并重渲染，否则「前置检查」区块永远是未执行。 */
            state.dlssPreflight = data;
            try { renderDlssPreflightSection(); } catch (e) { }""",
'P0-3 preflight 结果入 state')

# ---------- P1-4 兜底 catch 必须回执，否则按钮永久卡死 ----------
patch('src\\WebShell.cs',
"""                catch (Exception ex)
                {
                    LogCrash("命令分发异常", ex);""",
"""                catch (Exception ex)
                {
                    LogCrash("命令分发异常", ex);
                    // 不回执 = 该按钮永久 disabled + is-loading，只能关窗重开
                    try { ReplyFail(id, action, "INTERNAL", ex.Message); } catch { }""",
'P1-4 兜底 catch 回执')

# ---------- P2-11 预检项字段名 ----------
patch('web\\js\\app.js',
"""escapeHtml(item.label)""",
"""escapeHtml(item.name || item.label || '')""",
'P2-11 预检项读 name')

# ---------- P2-9 sensors.valid 不要被前端无条件覆盖 ----------
patch('web\\js\\app.js',
"""        sensors.valid = true;""",
"""        // 不要覆盖后端给的 valid：读失败时应显示「不可用」而不是 0°C/0%""",
'P2-9 sensors.valid')

print('OK:')
for x in ok: print('  +', x)
print('FAIL:')
for x in fail: print('  !', x)
