using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BookSpread_Renamer
{
    internal static class SourcePathManager
    {
        public static List<string> LoadSourcePaths()
        {
            var valid = AppConfig.Instance.SourcePaths
                .Where(Directory.Exists)
                .ToList();
            return valid.Count > 0 ? valid : new List<string> { Directory.GetCurrentDirectory() };
        }
    }
}
