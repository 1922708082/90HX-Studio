import io

ROOT = r'D:\app\build_source'
ok, fail = [], []

def patch(rel, old, new, note, once=True):
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

# ---- P1-4：把 id/action 提到 try 外层，兜底 catch 才能回执 ----
patch('src\\WebShell.cs',
"""        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {""",
"""        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            // 提到 try 外层：兜底 catch 里也要能拿到 id/action 才能回执，
            // 否则一条消息处理炸了，前端那个按钮就永久 disabled + is-loading。
            string id = null;
            string action = null;
            try
            {""",
'P1-4a id/action 提到外层')

patch('src\\WebShell.cs',
"""                string action = ExtractJsonString(json, "action");
                string id = ExtractJsonString(json, "id");""",
"""                action = ExtractJsonString(json, "action");
                id = ExtractJsonString(json, "id");""",
'P1-4b 改成赋值')

patch('src\\WebShell.cs',
"""            catch (Exception ex)
            {
                LogCrash("处理网页消息失败", ex);
            }
        }""",
"""            catch (Exception ex)
            {
                LogCrash("处理网页消息失败", ex);
                // 兜底回执：宁可让用户看到一个明确的错误，也不要让按钮静默卡死。
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(action))
                {
                    try { ReplyFail(id, action, "INTERNAL", ex.Message); } catch { }
                }
            }
        }""",
'P1-4c catch 补 ReplyFail')

# ---- 黑窗探针：主窗口显示后枚举子进程，把「谁在启动时冒出来」写进日志 ----
patch('src\\App.cs',
"""        [STAThread]
        public static void Main(string[] args)
        {""",
"""        /// <summary>
        /// 黑窗探针：启动 3 秒后枚举本进程的子进程并写日志。
        /// 目的不是修，而是拿到证据——之前把闪屏和两处 cmd 都排除了，
        /// 一直靠猜；有了这份清单就能一眼看出是哪个子进程弹的黑窗。
        /// </summary>
        private static void ProbeChildProcessesAsync()
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    System.Threading.Thread.Sleep(3000);
                    int self = System.Diagnostics.Process.GetCurrentProcess().Id;
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    sb.AppendLine("[探针] 本进程 PID=" + self + " 的子进程：");
                    using (System.Management.ManagementObjectSearcher searcher =
                           new System.Management.ManagementObjectSearcher(
                               "SELECT ProcessId,Name,CommandLine FROM Win32_Process WHERE ParentProcessId=" + self))
                    {
                        int n = 0;
                        foreach (System.Management.ManagementObject mo in searcher.Get())
                        {
                            n++;
                            sb.AppendLine("  - " + mo["Name"] + " pid=" + mo["ProcessId"] +
                                          " cmd=" + mo["CommandLine"]);
                        }
                        if (n == 0) sb.AppendLine("  （无子进程）");
                    }
                    try { App.AppendAuditLine(sb.ToString()); } catch { }
                }
                catch (Exception ex)
                {
                    try { App.AppendAuditLine("[探针] 失败: " + ex.Message); } catch { }
                }
            });
        }

        [STAThread]
        public static void Main(string[] args)
        {
            try { ProbeChildProcessesAsync(); } catch { }""",
'黑窗探针')

print('OK:')
for x in ok: print('  +', x)
print('FAIL:')
for x in fail: print('  !', x)
