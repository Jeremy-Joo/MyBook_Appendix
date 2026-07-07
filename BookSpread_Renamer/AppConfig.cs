using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace BookSpread_Renamer
{
    [XmlRoot("Config")]
    public class AppConfig
    {
        [XmlArray("SourcePaths")]
        [XmlArrayItem("Path")]
        public List<string> SourcePaths { get; set; } = new List<string>();

        [XmlArray("RecentPaths")]
        [XmlArrayItem("Path")]
        public List<string> RecentPaths { get; set; } = new List<string>();

        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BookSpreadRenamer");

        public static readonly string ConfigFile = Path.Combine(ConfigDir, "config.xml");

        private static AppConfig _instance;
        public static AppConfig Instance => _instance ?? (_instance = Load());

        public static AppConfig Load()
        {
            if (!File.Exists(ConfigFile))
                return new AppConfig();
            try
            {
                using (var stream = File.OpenRead(ConfigFile))
                    return (AppConfig)new XmlSerializer(typeof(AppConfig)).Deserialize(stream);
            }
            catch
            {
                return new AppConfig();
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(ConfigDir);
            using (var stream = File.Create(ConfigFile))
                new XmlSerializer(typeof(AppConfig)).Serialize(stream, this);
        }

        // 최근 경로 목록에 추가 (중복 제거 후 맨 앞으로, 최대 20개 유지)
        public void AddRecentPath(string path)
        {
            RecentPaths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            RecentPaths.Insert(0, path);
            if (RecentPaths.Count > 20)
                RecentPaths.RemoveRange(20, RecentPaths.Count - 20);
        }

        // 검색 경로 추가. 이미 있으면 false 반환
        public bool AddSourcePath(string path)
        {
            if (SourcePaths.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
                return false;
            SourcePaths.Add(path);
            AddRecentPath(path);
            Save();
            return true;
        }

        public void RemoveSourcePath(string path)
        {
            SourcePaths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            Save();
        }
    }
}
