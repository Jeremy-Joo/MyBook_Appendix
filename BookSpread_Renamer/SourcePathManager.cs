using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BookSpread_Renamer
{
    // 검색 경로(sources.txt) 로드 및 관리
    internal static class SourcePathManager
    {
        private static readonly string AppDir =
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        private static readonly string SourcePathsFile =
            Path.Combine(AppDir, "sources.txt");

        public static List<string> LoadSourcePaths()
        {
            if (!File.Exists(SourcePathsFile))
                return new List<string> { Directory.GetCurrentDirectory() };

            var lines = File.ReadAllLines(SourcePathsFile, Encoding.UTF8)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("#"))
                .ToList();

            var valid = lines.Where(Directory.Exists).ToList();
            return valid.Count > 0 ? valid : new List<string> { Directory.GetCurrentDirectory() };
        }

        public static void ManageSourcePaths()
        {
            while (true)
            {
                Console.Clear();
                var paths = LoadSourcePaths();
                Console.WriteLine("=== 검색 경로 관리 ===");
                Console.WriteLine($"설정 파일: {SourcePathsFile}");
                Console.WriteLine();
                Console.WriteLine("현재 검색 경로:");
                for (int i = 0; i < paths.Count; i++)
                {
                    bool exists = Directory.Exists(paths[i]);
                    Console.WriteLine($"  [{i + 1}] {paths[i]}{(exists ? "" : " (존재하지 않음)")}");
                }

                Console.WriteLine();
                Console.WriteLine("a. 경로 추가   b. 경로 제거   c. 돌아가기");
                Console.Write("선택: ");
                string input = Console.ReadLine()?.Trim().ToLower();

                if (input == "c") break;

                if (input == "a")
                {
                    Console.Write("추가할 경로 입력: ");
                    string newPath = Console.ReadLine()?.Trim();
                    if (string.IsNullOrEmpty(newPath)) continue;
                    if (!Directory.Exists(newPath))
                    {
                        Console.WriteLine($"존재하지 않는 경로입니다: {newPath}");
                        Console.ReadKey();
                        continue;
                    }
                    if (!paths.Any(p => string.Equals(p, newPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        paths.Add(newPath);
                        File.WriteAllLines(SourcePathsFile, paths, Encoding.UTF8);
                        Console.WriteLine($"추가됨: {newPath}");
                    }
                    else
                        Console.WriteLine("이미 등록된 경로입니다.");
                    Console.ReadKey();
                }
                else if (input == "b")
                {
                    if (paths.Count == 0) continue;
                    Console.Write("제거할 번호: ");
                    if (int.TryParse(Console.ReadLine(), out int num) && num >= 1 && num <= paths.Count)
                    {
                        string removed = paths[num - 1];
                        paths.RemoveAt(num - 1);
                        File.WriteAllLines(SourcePathsFile, paths, Encoding.UTF8);
                        Console.WriteLine($"제거됨: {removed}");
                    }
                    else
                        Console.WriteLine("올바른 번호를 입력하세요.");
                    Console.ReadKey();
                }
            }
        }
    }
}
