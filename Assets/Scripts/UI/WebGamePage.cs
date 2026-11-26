using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LastArchive
{
    internal static class WebGamePage
    {
        internal sealed record Asset(byte[] Bytes, string ContentType)
        {
            public string ETag { get; } = "\"" + Convert.ToHexString(SHA256.HashData(Bytes)) + "\"";
        }

        private static readonly string Index = Encoding.UTF8.GetString(ReadResource("index.html"));
        private static readonly Dictionary<string, Asset> Assets = new()
        {
            ["/assets/game.css"] = new(ReadResource("game.css"), "text/css; charset=utf-8"),
            ["/assets/game.js"] = new(ReadResource("game.js"), "text/javascript; charset=utf-8"),
            ["/assets/archive-city.jpg"] = new(ReadResource("archive-city.jpg"), "image/jpeg"),
            ["/assets/expedition-sites.jpg"] = new(ReadResource("expedition-sites.jpg"), "image/jpeg")
        };

        internal static string GetIndexHtml(bool standalone = false)
        {
            if (!standalone) return Index;
            string css = Encoding.UTF8.GetString(Assets["/assets/game.css"].Bytes);
            foreach (var (path, asset) in Assets)
                if (asset.ContentType.StartsWith("image/", StringComparison.Ordinal))
                    css = css.Replace(path, $"data:{asset.ContentType};base64,{Convert.ToBase64String(asset.Bytes)}");
            string js = Encoding.UTF8.GetString(Assets["/assets/game.js"].Bytes);
            return Index
                .Replace("""<link rel="stylesheet" href="/assets/game.css">""", "<style>" + css + "</style>")
                .Replace("""<script defer src="/assets/game.js"></script>""", "")
                .Replace("</body>", "<script>" + js + "</script></body>");
        }

        internal static bool TryGetAsset(string path, out Asset asset) => Assets.TryGetValue(path, out asset);

        private static byte[] ReadResource(string name)
        {
            using var stream = typeof(WebGamePage).Assembly.GetManifestResourceStream("LastArchive.Web." + name);
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            return bytes.ToArray();
        }
    }
}
