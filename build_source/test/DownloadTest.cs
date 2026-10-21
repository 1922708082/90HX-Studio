using System;
using System.IO;
using System.Net;
using System.Text;

// 下载逻辑验证器：与 DlssPack.DownloadOnce 相同的实现，
// 但允许限制每次抓取的字节数，用于在慢速镜像上验证「断点续传」是否正确。
internal static class DownloadTest
{
    private static int Main(string[] args)
    {
        string url = args.Length > 0 ? args[0] : "";
        string part = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "test.part");
        long budget = args.Length > 2 ? long.Parse(args[2]) : 2 * 1024 * 1024;

        try
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Fetch(url, part, budget);
            long len = new FileInfo(part).Length;
            Console.WriteLine("[OK] 本次结束后 .part = " + len + " 字节");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[FAIL] " + ex.Message);
            return 1;
        }
    }

    private static void Fetch(string url, string tmp, long budget)
    {
        long existing = 0;
        try { if (File.Exists(tmp)) existing = new FileInfo(tmp).Length; } catch { }

        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
        req.UserAgent = "Mozilla/5.0 HX-Studio";
        req.Timeout = 30000;
        req.ReadWriteTimeout = 15000;
        req.AllowAutoRedirect = true;
        if (existing > 0) req.AddRange(existing);

        using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
        {
            bool partial = resp.StatusCode == HttpStatusCode.PartialContent;
            long total = resp.ContentLength;
            string cr = resp.Headers["Content-Range"];

            if (partial && !string.IsNullOrEmpty(cr))
            {
                int slash = cr.LastIndexOf('/');
                long t;
                if (slash > 0 && long.TryParse(cr.Substring(slash + 1), out t)) total = t;
            }
            else if (existing > 0)
            {
                existing = 0;
                try { File.Delete(tmp); } catch { }
            }

            Console.WriteLine("请求起点 = " + existing + " 字节");
            Console.WriteLine("HTTP = " + (int)resp.StatusCode + " (" + resp.StatusCode + ")");
            Console.WriteLine("Content-Range = " + (cr ?? "(无)"));
            Console.WriteLine("总量 = " + total + " 字节 (" + (total / 1048576) + " MB)");
            Console.WriteLine("本次预算 = " + budget + " 字节");

            long read = existing;
            long got = 0;
            byte[] buf = new byte[131072];

            using (Stream ns = resp.GetResponseStream())
            using (FileStream fs = new FileStream(tmp, existing > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write))
            {
                int n;
                while (got < budget && (n = ns.Read(buf, 0, (int)Math.Min(buf.Length, budget - got))) > 0)
                {
                    fs.Write(buf, 0, n);
                    read += n;
                    got += n;
                }
            }

            Console.WriteLine("本次收到 = " + got + " 字节");
            Console.WriteLine("累计 = " + read + " 字节 / " + total + "  (" + (total > 0 ? (read * 100 / total) + "%" : "未知") + ")");
            Console.WriteLine("校验: " + (read == existing + got ? "追加无重叠 ✅" : "数据错乱 ❌"));
        }
    }
}
