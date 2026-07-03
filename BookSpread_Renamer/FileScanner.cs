using System;
using System.IO;
using System.Linq;
using System.Text;

namespace BookSpread_Renamer
{
    // 검색 경로에서 대상 파일(.epub/.pdf)을 찾고 목록을 내보낸다
    internal static class FileScanner
    {
        public static readonly string[] SearchExtensions = { ".epub", ".pdf" };

        public static string[] GetTargetFiles(SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return SourcePathManager.LoadSourcePaths()
                .SelectMany(dir => Directory.GetFiles(dir, "*.*", searchOption))
                .Where(IsTargetFile)
                .Distinct()
                .ToArray();
        }

        public static bool IsTargetFile(string path)
        {
            return SearchExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }

        // 파일 목록을 result.txt로 저장
        public static void GetFileInfo()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string[] targetFiles = GetTargetFiles(SearchOption.AllDirectories);
            string path = Path.Combine(currentDirectory, "result.txt");
            try
            {
                File.WriteAllLines(path, targetFiles, Encoding.UTF8);
                Console.WriteLine($"Total {targetFiles.Length} file paths saved.");
                Console.WriteLine("Result file location: " + path);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving file: " + ex.Message);
            }
        }
    }
}
