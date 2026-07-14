using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BookSpread_Renamer
{
    // 파일을 포맷(epub/pdf)/연도/카테고리 폴더로 분류·이동한다 (영문 책만 "영문" 하위 폴더 추가)
    internal static class FileOrganizer
    {
        // 지정 폴더가 이미 ResultPDF(또는 그 하위)면 그 ResultPDF를 그대로 출력 루트로 쓴다.
        // (ResultPDF 안에 또 ResultPDF가 생기는 중첩 방지)
        private static string GetResultRoot(string source)
        {
            for (var d = new DirectoryInfo(source.TrimEnd('\\', '/')); d != null; d = d.Parent)
                if (d.Name.Equals("ResultPDF", StringComparison.OrdinalIgnoreCase))
                    return d.FullName;
            return Path.Combine(source, "ResultPDF");
        }

        // 검색 경로의 파일을 ResultPDF 아래로 분류하여 이동
        public static void SpreadFile()
        {
            var sources = SourcePathManager.LoadSourcePaths();
            string resultRoot = GetResultRoot(sources[0]);
            Directory.CreateDirectory(resultRoot);

            // ResultPDF 안에 이미 있는 파일은 제외
            string[] targetFiles = FileScanner.GetTargetFiles(SearchOption.AllDirectories)
                .Where(f => !f.StartsWith(resultRoot + "\\", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (targetFiles.Length == 0)
            {
                Console.WriteLine("분류할 .epub 또는 .pdf 파일을 찾을 수 없습니다.");
                Console.WriteLine("(이미 ResultPDF 안에 있는 파일을 재정리하려면 Reclassify를 사용하세요.)");
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

            // 시리즈 탐지용 파일 정보 수집
            var infos = new List<(string path, string lang, string category, string format)>();
            foreach (var category in byCategory)
                foreach (string filePath in category.Value)
                    infos.Add((filePath,
                        BookClassifier.IsKoreanBook(Path.GetFileName(filePath)) ? "한글" : "영문",
                        category.Key,
                        Path.GetExtension(filePath).TrimStart('.').ToLower()));
            var seriesMap = DetectSeries(infos);

            int moved = 0;
            int skipped = 0;
            int processed = 0;
            int total = targetFiles.Length;
            Console.WriteLine($"총 {total}개 파일을 분류합니다...");

            // 폴더 구조: 한글|영문 → 카테고리 → epub|pdf → 연도(2024/2025/2026/이전)
            // 시리즈(같은 제목의 권 번호가 2개 이상)는 연도 대신 시리즈 제목 폴더로 묶는다.
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
                        string leaf = seriesMap.TryGetValue(filePath, out string seriesTitle) ? seriesTitle : yearGroup;
                        string targetDir = Path.Combine(resultRoot, lang, category.Key, format, leaf);
                        // 이미 올바른 위치면 건너뜀 (재실행 시 파일명에 (1)이 붙는 것 방지)
                        if (string.Equals(Path.GetDirectoryName(filePath), targetDir, StringComparison.OrdinalIgnoreCase))
                        {
                            ++skipped;
                        }
                        else
                        {
                            Directory.CreateDirectory(targetDir);
                            string destPath = GetSafeCopyPath(targetDir, fileName);
                            File.Move(filePath, destPath);
                            ++moved;
                        }
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
            string root = GetResultRoot(sources[0]);
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

            // 1차: 분류 정보 수집 (카테고리는 EPUB 메타데이터까지 읽으므로 한 번만 계산)
            Console.WriteLine($"총 {files.Length}개 파일을 분석합니다...");
            var infos = new List<(string path, string lang, string category, string format)>(files.Length);
            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileName(files[i]);
                infos.Add((files[i],
                    BookClassifier.IsKoreanBook(fileName) ? "한글" : "영문",
                    BookClassifier.ClassifyCategory(fileName, files[i]),
                    Path.GetExtension(files[i]).TrimStart('.').ToLower()));
                ConsoleProgress.Show(i + 1, files.Length);
            }
            Console.WriteLine();
            var seriesMap = DetectSeries(infos);

            // 2차: 이동. 시리즈(권 번호 2개 이상)는 연도 대신 시리즈 제목 폴더로.
            int moved = 0;
            int kept = 0;
            for (int i = 0; i < infos.Count; i++)
            {
                var info = infos[i];
                try
                {
                    string fileName = Path.GetFileName(info.path);
                    string leaf = seriesMap.TryGetValue(info.path, out string seriesTitle)
                        ? seriesTitle
                        : PublicationYearReader.GetYearGroup(info.path);
                    string targetDir = Path.Combine(root, info.lang, info.category, info.format, leaf);

                    // 이미 올바른 위치에 있으면 건너뜀
                    if (string.Equals(Path.GetDirectoryName(info.path), targetDir, StringComparison.OrdinalIgnoreCase))
                    {
                        kept++;
                    }
                    else
                    {
                        Directory.CreateDirectory(targetDir);
                        string dest = GetSafeCopyPath(targetDir, fileName);
                        File.Move(info.path, dest);
                        moved++;
                        ConsoleProgress.ClearLine();
                        Console.WriteLine($"[이동] {fileName}");
                        Console.WriteLine($"     → {Path.GetDirectoryName(dest).Substring(root.Length).TrimStart('\\')}");
                    }
                }
                catch (Exception ex)
                {
                    ConsoleProgress.ClearLine();
                    Console.Error.WriteLine($"실패: {Path.GetFileName(info.path)} -> {ex.Message}");
                }
                ConsoleProgress.Show(i + 1, infos.Count);
            }
            Console.WriteLine();

            // 비워진 폴더 정리 (root 자체는 유지)
            int removedDirs = 0;
            foreach (string dir in Directory.GetDirectories(root))
                removedDirs += RemoveEmptyDirectories(dir);

            int seriesCount = seriesMap.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            Console.WriteLine($"\n재분류 완료: {moved}개 이동, {kept}개 유지, 시리즈 {seriesCount}개 묶음, 빈 폴더 {removedDirs}개 삭제");
            Console.WriteLine($"위치: {root}");
        }

        // 시리즈 탐지: 언어·카테고리·포맷이 같고 기본 제목이 같은 파일이
        // 서로 다른 권 번호로 2개 이상 있으면 시리즈로 본다.
        // 반환: 파일 경로 → 시리즈 폴더명 (시리즈가 아닌 파일은 미포함)
        private static Dictionary<string, string> DetectSeries(
            List<(string path, string lang, string category, string format)> files)
        {
            var volumes = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            var parsed = new List<(string path, string groupKey, string baseTitle)>();
            foreach (var f in files)
            {
                if (!SeriesDetector.TryParse(Path.GetFileNameWithoutExtension(f.path), out string baseTitle, out int volume))
                    continue;
                string groupKey = $"{f.lang}|{f.category}|{f.format}|{baseTitle}";
                if (!volumes.TryGetValue(groupKey, out HashSet<int> vols))
                    volumes[groupKey] = vols = new HashSet<int>();
                vols.Add(volume);
                parsed.Add((f.path, groupKey, baseTitle));
            }

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in parsed)
                if (volumes[p.groupKey].Count >= 2)
                    result[p.path] = p.baseTitle;
            return result;
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
