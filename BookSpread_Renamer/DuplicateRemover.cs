using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace BookSpread_Renamer
{
    // 비슷한 이름 또는 같은 크기의 중복 파일을 찾아 제거한다
    internal static class DuplicateRemover
    {
        // "(1)", "(2)" 같은 복사본 숫자 접미사
        private static readonly Regex NumberedSuffixRegex = new Regex(
            @"\s*\(\d+\)$",
            RegexOptions.Compiled);

        public static void RemoveDuplicates()
        {
            string[] targetFiles = FileScanner.GetTargetFiles(SearchOption.AllDirectories);
            if (targetFiles.Length == 0)
            {
                Console.WriteLine("현재 디렉터리에서 .epub 또는 .pdf 파일을 찾을 수 없습니다.");
                return;
            }

            var duplicateGroups = new List<List<string>>();
            var alreadyGrouped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1단계: 비슷한 이름으로 그룹화 (숫자 접미사 제거 후 비교)
            var byName = targetFiles
                .GroupBy(f => NormalizeName(Path.GetFileName(f)), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);

            foreach (var group in byName)
            {
                var list = group.ToList();
                duplicateGroups.Add(list);
                foreach (var f in list) alreadyGrouped.Add(f);
            }

            // 2단계: 파일 크기가 같은 파일로 그룹화 (이미 처리된 파일 제외)
            var bySize = targetFiles
                .Where(f => !alreadyGrouped.Contains(f))
                .GroupBy(f => new FileInfo(f).Length)
                .Where(g => g.Count() > 1);

            foreach (var group in bySize)
                duplicateGroups.Add(group.ToList());

            if (duplicateGroups.Count == 0)
            {
                Console.WriteLine("중복 파일이 없습니다.");
                return;
            }

            // 삭제 대상 결정: 각 그룹에서 이름이 가장 짧은 파일(원본)을 유지
            var toDelete = new List<string>();
            foreach (var group in duplicateGroups)
            {
                var sorted = group
                    .OrderBy(f => Path.GetFileName(f).Length)
                    .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                string keep = sorted[0];
                long fileSize = new FileInfo(keep).Length;
                Console.WriteLine($"\n[유지] {Path.GetFileName(keep)} ({fileSize / 1024.0:F1} KB)");

                foreach (string del in sorted.Skip(1))
                {
                    Console.WriteLine($"  [삭제 예정] {Path.GetFileName(del)}");
                    toDelete.Add(del);
                }
            }

            Console.WriteLine($"\n총 {toDelete.Count}개 파일을 삭제합니다. 계속하시겠습니까? (y/n)");
            if (!string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("취소되었습니다.");
                return;
            }

            int deleted = 0;
            foreach (string file in toDelete)
            {
                try
                {
                    File.Delete(file);
                    deleted++;
                    Console.WriteLine($"삭제: {Path.GetFileName(file)}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"삭제 실패: {Path.GetFileName(file)} -> {ex.Message}");
                }
            }

            Console.WriteLine($"\n총 {deleted}개 파일 삭제 완료.");
        }

        private static string NormalizeName(string fileName)
        {
            string ext = Path.GetExtension(fileName);
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            return NumberedSuffixRegex.Replace(nameWithoutExt, "").Trim() + ext;
        }
    }
}
