using System.Collections.Generic;
using System.Text;

namespace CMP90HXToolbox
{
    /// <summary>
    /// 显卡伪装型号目录（静态能力表）。
    ///
    /// 由 C# 单一持有，前端只接收 spoof.models 事件后负责渲染，
    /// 不再在页面里维护副本——避免加型号要改两处、还容易不同步。
    /// </summary>
    internal static class SpoofCatalog
    {
        public const string Recommended = "NVIDIA GeForce RTX 3080";

        public sealed class Group
        {
            public string Chip;
            public string Label;
            public string[] Models;

            public Group(string chip, string label, string[] models)
            {
                Chip = chip; Label = label; Models = models;
            }
        }

        public static List<Group> All()
        {
            List<Group> list = new List<Group>();
            list.Add(new Group("GB202", "Blackwell（RTX 50 系 · 6 款）", new string[] { "NVIDIA GeForce RTX 5090", "NVIDIA GeForce RTX 5080", "NVIDIA GeForce RTX 5070 Ti", "NVIDIA GeForce RTX 5070", "NVIDIA GeForce RTX 5060 Ti", "NVIDIA GeForce RTX 5060" }));
            list.Add(new Group("AD102", "Ada（RTX 40 系 · 10 款）", new string[] { "NVIDIA GeForce RTX 4090", "NVIDIA GeForce RTX 4080 SUPER", "NVIDIA GeForce RTX 4080", "NVIDIA GeForce RTX 4070 Ti SUPER", "NVIDIA GeForce RTX 4070 Ti", "NVIDIA GeForce RTX 4070 SUPER", "NVIDIA GeForce RTX 4070", "NVIDIA GeForce RTX 4060 Ti", "NVIDIA GeForce RTX 4060 Ti 16GB", "NVIDIA GeForce RTX 4060" }));
            list.Add(new Group("GA102", "GA102（90HX / RTX 30 系 · 14 款）", new string[] { "NVIDIA GeForce RTX 3090 Ti", "NVIDIA GeForce RTX 3090", "NVIDIA GeForce RTX 3080 Ti", "NVIDIA GeForce RTX 3080", "NVIDIA GeForce RTX 3080 12GB", "NVIDIA RTX A6000", "NVIDIA RTX A5000", "NVIDIA RTX A4500", "NVIDIA A40", "NVIDIA A10", "NVIDIA A10G", "NVIDIA CMP 90HX", "NVIDIA Tesla T40", "NVIDIA Quadro RTX 8000 (Compat)" }));
            list.Add(new Group("GA104", "GA104（70HX / RTX 30 系 · 14 款）", new string[] { "NVIDIA GeForce RTX 3070 Ti", "NVIDIA GeForce RTX 3070", "NVIDIA GeForce RTX 3060 Ti", "NVIDIA GeForce RTX 3080 Laptop GPU", "NVIDIA GeForce RTX 3070 Ti Laptop GPU", "NVIDIA GeForce RTX 3070 Laptop GPU", "NVIDIA RTX A4000", "NVIDIA RTX A3000", "NVIDIA RTX A2000 12GB", "NVIDIA A16", "NVIDIA A2", "NVIDIA CMP 70HX", "NVIDIA GeForce RTX 3060 GA104", "NVIDIA RTX 4000 SFF Ada (Compat)" }));
            list.Add(new Group("TU102", "TU102（50HX / RTX 20 系 · 13 款）", new string[] { "NVIDIA GeForce RTX 2080 Ti", "NVIDIA TITAN RTX", "NVIDIA Quadro RTX 8000", "NVIDIA Quadro RTX 6000", "NVIDIA GeForce RTX 2080 Ti SUPER (Engineering)", "NVIDIA Tesla T40 (TU102)", "NVIDIA GRID RTX 8000", "NVIDIA GRID RTX 6000", "NVIDIA CMP 50HX", "NVIDIA Quadro RTX 6000 Passive", "NVIDIA Quadro RTX 8000 Passive", "NVIDIA GeForce RTX 2080 Ti Rev.A", "NVIDIA TITAN RTX Passive" }));
            list.Add(new Group("TU106", "TU106（40HX / RTX 20 系 · 14 款）", new string[] { "NVIDIA GeForce RTX 2070 SUPER", "NVIDIA GeForce RTX 2070", "NVIDIA GeForce RTX 2060 SUPER", "NVIDIA GeForce RTX 2060 12GB", "NVIDIA GeForce RTX 2060", "NVIDIA GeForce RTX 2070 Max-Q", "NVIDIA GeForce RTX 2060 Mobile", "NVIDIA Quadro RTX 4000", "NVIDIA Quadro RTX 3000", "NVIDIA Tesla T4", "NVIDIA CMP 40HX", "NVIDIA GeForce RTX 2060 KO", "NVIDIA Quadro RTX 4000 Max-Q", "NVIDIA GRID T4-8Q" }));
            list.Add(new Group("TU116", "TU116（30HX / GTX 16 系 · 12 款）", new string[] { "NVIDIA GeForce GTX 1660 Ti", "NVIDIA GeForce GTX 1660 SUPER", "NVIDIA GeForce GTX 1660", "NVIDIA GeForce GTX 1650 SUPER", "NVIDIA GeForce GTX 1650 Ti", "NVIDIA GeForce GTX 1650 (TU116)", "NVIDIA GeForce GTX 1660 Ti Mobile", "NVIDIA GeForce GTX 1660 Ti Max-Q", "NVIDIA CMP 30HX", "NVIDIA T1000 8GB", "NVIDIA T600", "NVIDIA Quadro T1000" }));
            list.Add(new Group("GA100", "GA100（170HX / 数据中心计算卡 · 9 款）", new string[] { "NVIDIA A100-PCIE-40GB", "NVIDIA A100-PCIE-80GB", "NVIDIA A100-SXM4-40GB", "NVIDIA A100-SXM4-80GB", "NVIDIA A100X", "NVIDIA A30", "NVIDIA CMP 170HX", "NVIDIACMP 170HX 8GB", "NVIDIA HGX A100" }));
            return list;
        }

        /// <summary>拼成页面约定的 spoof.models 事件。</summary>
        public static string ToJson()
        {
            List<Group> groups = All();
            StringBuilder sb = new StringBuilder(6000);
            sb.Append("{\"kind\":\"evt\",\"action\":\"spoof.models\",\"payload\":{\"groups\":[");
            for (int i = 0; i < groups.Count; i++)
            {
                Group g = groups[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"chip\":").Append(Quote(g.Chip)).Append(",");
                sb.Append("\"label\":").Append(Quote(g.Label)).Append(",");
                sb.Append("\"models\":[");
                for (int j = 0; j < g.Models.Length; j++)
                {
                    if (j > 0) sb.Append(',');
                    sb.Append(Quote(g.Models[j]));
                }
                sb.Append("]}");
            }
            sb.Append("],\"recommended\":").Append(Quote(Recommended)).Append("}}");
            return sb.ToString();
        }

        private static string Quote(string s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            StringBuilder b = new StringBuilder(s.Length + 4);
            b.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"') b.Append("\\\"");
                else if (c == '\\') b.Append("\\\\");
                else b.Append(c);
            }
            b.Append('"');
            return b.ToString();
        }
    }
}
