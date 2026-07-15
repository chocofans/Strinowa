using System.IO;
using System.Xml.Linq;

namespace StrinowaWPF;

internal sealed record WinBuildEntry(
    string Version,
    string Branch,
    string Source,
    string Mode,
    string Url,
    DateTime? FoundAt);

internal static class WinBuildCatalog
{
    static readonly object Gate = new();

    public static string Path => AppPaths.WinBuildsPath;

    public static IReadOnlyList<WinBuildEntry> Load()
    {
        lock (Gate)
        {
            try
            {
                if (!File.Exists(Path)) return [];
                var doc = XDocument.Load(Path);
                return doc.Root?.Elements("Build")
                    .Select(element => new WinBuildEntry(
                        (string?)element.Attribute("Version") ?? "",
                        (string?)element.Attribute("Branch") ?? "",
                        ((string?)element.Attribute("Source") ?? "").ToUpperInvariant(),
                        (string?)element.Attribute("Mode") ?? "Game",
                        (string?)element.Attribute("Url") ?? "",
                        DateTime.TryParse((string?)element.Attribute("FoundAt"), out var foundAt) ? foundAt : null))
                    .Where(entry => entry.Version.Length > 0 && entry.Branch.Length > 0 && entry.Source.Length > 0)
                    .ToList() ?? [];
            }
            catch
            {
                return [];
            }
        }
    }

    public static void Upsert(IEnumerable<WinBuildEntry> entries)
    {
        lock (Gate)
        {
            XDocument doc;
            try
            {
                doc = File.Exists(Path) ? XDocument.Load(Path) : new XDocument(new XElement("WinBuilds"));
                if (doc.Root?.Name != "WinBuilds") doc = new XDocument(new XElement("WinBuilds"));
            }
            catch
            {
                doc = new XDocument(new XElement("WinBuilds"));
            }

            var root = doc.Root!;
            foreach (var entry in entries)
            {
                var match = root.Elements("Build").FirstOrDefault(element =>
                    string.Equals((string?)element.Attribute("Version"), entry.Version, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string?)element.Attribute("Branch"), entry.Branch, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string?)element.Attribute("Source"), entry.Source, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string?)element.Attribute("Mode"), entry.Mode, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    match = new XElement("Build");
                    root.Add(match);
                    match.SetAttributeValue("FoundAt", (entry.FoundAt ?? DateTime.UtcNow).ToUniversalTime().ToString("o"));
                }

                match.SetAttributeValue("Version", entry.Version);
                match.SetAttributeValue("Branch", entry.Branch);
                match.SetAttributeValue("Source", entry.Source.ToUpperInvariant());
                match.SetAttributeValue("Mode", entry.Mode);
                match.SetAttributeValue("Url", entry.Url);
                match.SetAttributeValue("LastSeen", DateTime.UtcNow.ToString("o"));
            }

            doc.Save(Path);
        }
    }

    public static int Remove(IEnumerable<WinBuildEntry> entries)
    {
        lock (Gate)
        {
            if (!File.Exists(Path)) return 0;
            var keys = new HashSet<string>(entries.Select(entry =>
                $"{entry.Version}|{entry.Branch}|{entry.Source}|{entry.Mode}"), StringComparer.OrdinalIgnoreCase);
            if (keys.Count == 0) return 0;
            var doc = XDocument.Load(Path);
            if (doc.Root == null) return 0;
            var matches = doc.Root.Elements("Build").Where(element => keys.Contains(
                $"{(string?)element.Attribute("Version")}|{(string?)element.Attribute("Branch")}|{(string?)element.Attribute("Source")}|{(string?)element.Attribute("Mode")}"))
                .ToList();
            foreach (var match in matches) match.Remove();
            if (matches.Count > 0) doc.Save(Path);
            return matches.Count;
        }
    }
}
