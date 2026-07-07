using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BookSpread_Renamer
{
    // 파일을 포맷(epub/pdf)/연도/카테고리 폴더로 분류·이동한다 (영문 책만 "영문" 하위 폴더 추가)
    internal static class FileOrganizer
    {
        // 검색 경로의 파일을 ResultPDF 아래로 분류하여 이동
        public static void SpreadFile()
        {
            var sources = SourcePathManager.LoadSourcePaths();
            string resultRoot = Path.Combine(sources[0], "ResultPDF");
            Directory.CreateDirectory(resultRoot);

            // ResultPDF 안에 이미 있는 파일은 제외
            string[] targetFiles = FileScanner.GetTargetFiles(SearchOption.AllDirectories)
                .Where(f => !f.StartsWith(resultRoot, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (targetFiles.Length == 0)
            {
                Console.WriteLine("분류할 .epub 또는 .pdf 파일을 찾을 수 없습니다.");
                return;
            }

            // 카테고리별로 파일 묶기
            var byCategory = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in targetFiles)
            {
                string key = BookClassifier.ClassifyCategory(Path.GetFileName(path), path);
                if (!byCategory.ContainsKey(key))
                    byCategory[key] = new List<string>();
                byCategory[key].Add(path);
            }

            int moved = 0;
            int skipped = 0;
            int processed = 0;
            int total = targetFiles.Length;
            Console.WriteLine($"총 {total}개 파일을 분류합니다...");

            // 폴더 구조: 한글|영문 → 카테고리 → epub|pdf → 연도(2024/2025/2026/이전)
            // 통계용 연도 캐시: 이동 후에는 원래 경로에서 메타데이터를 읽을 수 없으므로 이동 전에 저장
            var yearGroups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var category in byCategory)
            {
                foreach (string filePath in category.Value)
                {
                    try
                    {
                        string fileName = Path.GetFileName(filePath);
                        string lang = BookClassifier.IsKoreanBook(fileName) ? "한글" : "영문";
                        string format = Path.GetExtension(filePath).TrimStart('.').ToLower();
                        string yearGroup = PublicationYearReader.GetYearGroup(filePath);
                        yearGroups[filePath] = yearGroup;
                        string targetDir = Path.Combine(resultRoot, lang, category.Key, format, yearGroup);
                        Directory.CreateDirectory(targetDir);
                        string destPath = GetSafeCopyPath(targetDir, fileName);
                        if (!File.Exists(destPath))
                        {
                            File.Move(filePath, destPath);
                            ++moved;
                        }
                        else
                            ++skipped;
                    }
                    catch (Exception ex)
                    {
                        ConsoleProgress.ClearLine();
                        Console.Error.WriteLine($"Error [{category.Key}]: {filePath} -> {ex.Message}");
                    }
                    ConsoleProgress.Show(++processed, total);
                }
            }
            Console.WriteLine();

            Console.WriteLine($"\n분류 완료: {moved}개 이동, {skipped}개 건너뜀");
            Console.WriteLine($"출력 위치: {resultRoot}");
            Console.WriteLine("\n카테고리별 통계:");
            foreach (var category in byCategory)
            {
                int korean = category.Value.Count(f => BookClassifier.IsKoreanBook(Path.GetFileName(f)));
                int english = category.Value.Count - korean;
                string yearStats = string.Join(", ", category.Value
                    .GroupBy(f => yearGroups.TryGetValue(f, out string yg) ? yg : "?")
                    .OrderBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => $"{g.Key} {g.Count()}"));
                Console.WriteLine($"  {category.Key}: {category.Value.Count}개 (한글 {korean}, 영문 {english} | {yearStats})");
            }
        }

        // ResultPDF 안에 이미 분류된 파일을 현재 규칙으로 다시 정리한다.
        // (SpreadFile은 ResultPDF 안의 파일을 건너뛰므로, 키워드를 바꾼 뒤에는 이 기능으로 재분류한다.)
        public static void ReclassifyResultPDF()
        {
            var sources = SourcePathManager.LoadSourcePaths();
            string root = Path.Combine(sources[0], "ResultPDF");
            if (!Directory.Exists(root))
            {
                Console.WriteLine($"ResultPDF 폴더가 없습니다: {root}");
                return;
            }

            string[] files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(FileScanner.IsTargetFile)
                .ToArray();

            if (files.Length == 0)
            {
                Console.WriteLine("ResultPDF에 .epub 또는 .pdf 파일이 없습니다.");
                return;
            }

            int moved = 0;
            int kept = 0;
            Console.WriteLine($"총 {files.Length}개 파일을 검사합니다...");
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                try
                {
                    string fileName = Path.GetFileName(file);
                    string lang = BookClassifier.IsKoreanBook(fileName) ? "한글" : "영문";
                    string format = Path.GetExtension(file).TrimStart('.').ToLower();
                    string yearGroup = PublicationYearReader.GetYearGroup(file);
                    // 폴더 구조: 한글|영문 → 카테고리 → epub|pdf → 연도(2024/2025/2026/이전)
                    string targetDir = Path.Combine(root, lang, BookClassifier.ClassifyCategory(fileName, file), format, yearGroup);

                    // 이미 올바른 위치에 있으면 건너뜀
                    if (string.Equals(Path.GetDirectoryName(file), targetDir, StringComparison.OrdinalIgnoreCase))
                    {
                        kept++;
                    }
                    else
                    {
                        Directory.CreateDirectory(targetDir);
                        string dest = GetSafeCopyPath(targetDir, fileName);
                        File.Move(file, dest);
                        moved++;
                        ConsoleProgress.ClearLine();
                        Console.WriteLine($"[이동] {fileName}");
                        Console.WriteLine($"     → {Path.GetDirectoryName(dest).Substring(root.Length).TrimStart('\\')}");
                    }
                }
                catch (Exception ex)
                {
                    ConsoleProgress.ClearLine();
                    Console.Error.WriteLine($"실패: {Path.GetFileName(file)} -> {ex.Message}");
                }
                ConsoleProgress.Show(i + 1, files.Length);
            }
            Console.WriteLine();

            // 비워진 폴더 정리 (root 자체는 유지)
            int removedDirs = 0;
            foreach (string dir in Directory.GetDirectories(root))
                removedDirs += RemoveEmptyDirectories(dir);

            Console.WriteLine($"\n재분류 완료: {moved}개 이동, {kept}개 유지, 빈 폴더 {removedDirs}개 삭제");
            Console.WriteLine($"위치: {root}");
        }

        // 경로 길이(259자) 초과를 막고, 이름 충돌 시 (1), (2)... 접미사를 붙인다
        private static string GetSafeCopyPath(string directory, string fileName)
        {
            const int MaxPath = 259;
            string ext = Path.GetExtension(fileName);
            string nameOnly = Path.GetFileNameWithoutExtension(fileName);

            // directory + backslash + name + ext
            int available = MaxPath - directory.Length - 1 - ext.Length;
            if (available < 1) available = 1;

            string truncated = nameOnly.Length <= available
                ? nameOnly
                : nameOnly.Substring(0, available);

            string candidate = Path.Combine(directory, truncated + ext);
            int counter = 1;
            while (File.Exists(candidate))
            {
                string suffix = $"({counter++})";
                int trimLen = available - suffix.Length;
                if (trimLen < 1) trimLen = 1;
                string trimmed = truncated.Length <= trimLen ? truncated : truncated.Substring(0, trimLen);
                candidate = Path.Combine(directory, trimmed + suffix + ext);
            }
            return candidate;
        }

        // 하위부터 재귀적으로 비어 있는 폴더를 삭제하고 삭제한 개수를 반환한다.
        private static int RemoveEmptyDirectories(string dir)
        {
            int removed = 0;
            foreach (string sub in Directory.GetDirectories(dir))
                removed += RemoveEmptyDirectories(sub);

            if (!Directory.EnumerateFileSystemEntries(dir).Any())
            {
                try
                {
                    Directory.Delete(dir);
                    removed++;
                }
                catch { }
            }
            return removed;
        }
    }
}
