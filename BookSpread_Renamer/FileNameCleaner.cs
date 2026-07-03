using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace BookSpread_Renamer
{
    // 파일명 정리: 사이트 표식 제거, 깨진 이름 복구
    internal static class FileNameCleaner
    {
        // 사이트 도메인 괄호 (z-lib, 1lib, anna)
        private static readonly Regex SiteParenRegex = new Regex(
            @"\s*\([^)]*(?:z-lib|1lib|anna)[^)]*\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // -- 저자 -- 출판사 -- 연도 형식의 메타데이터 제거
        private static readonly Regex MetadataRegex = new Regex(
            @"\s*--\s+.+$",
            RegexOptions.Compiled);

        // _OceanofPDF.com_ 접두사 감지
        private static readonly Regex OceanOfPdfRegex = new Regex(
            @"^_OceanofPDF\.com_",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // «», ≪≫ 특수 괄호 제거
        private static readonly Regex SpecialBracketRegex = new Regex(
            @"[«»≪≫][^«»≪≫]*[«»≪≫]\s*",
            RegexOptions.Compiled);

        private static readonly string[] FixedRemoveStrings =
        {
            "(Z-Library)",
            "-- Anna's Archive",
            "(한_영 AI교차번역)",
            " -- ",
            " - ",
            " --",
            " -"
        };

        public static void RenameFiles()
        {
            string[] targetFiles = FileScanner.GetTargetFiles();
            int renamedCount = 0;

            foreach (string filePath in targetFiles)
            {
                string fileName = Path.GetFileName(filePath);
                string newName = fileName;
                bool changed = false;

                // 1. _OceanofPDF.com_ 접두사 제거 + 언더바 → 공백
                if (OceanOfPdfRegex.IsMatch(newName))
                {
                    string ext = Path.GetExtension(newName);
                    string name = OceanOfPdfRegex.Replace(Path.GetFileNameWithoutExtension(newName), "");
                    newName = name.Replace('_', ' ').Trim() + ext;
                    changed = true;
                }

                // 2. 사이트 도메인 괄호 제거 (z-lib, 1lib, anna)
                if (SiteParenRegex.IsMatch(newName))
                {
                    newName = SiteParenRegex.Replace(newName, string.Empty).Trim();
                    changed = true;
                }

                // 3. «Must Have» 등 특수 괄호 제거
                if (SpecialBracketRegex.IsMatch(newName))
                {
                    newName = SpecialBracketRegex.Replace(newName, string.Empty).Trim();
                    changed = true;
                }

                // 4. -- 저자 -- 출판사 -- 연도 메타데이터 제거
                string nameOnly = Path.GetFileNameWithoutExtension(newName);
                string extOnly = Path.GetExtension(newName);
                if (MetadataRegex.IsMatch(nameOnly))
                {
                    newName = MetadataRegex.Replace(nameOnly, "").Trim() + extOnly;
                    changed = true;
                }

                // 5. 고정 문자열 제거
                foreach (string oldValue in FixedRemoveStrings)
                {
                    if (newName.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        newName = newName.Replace(oldValue, string.Empty).Trim();
                        changed = true;
                    }
                }

                // 6. 앞뒤 공백 정리
                string withoutExtension = Path.GetFileNameWithoutExtension(newName);
                if (withoutExtension != withoutExtension.Trim())
                {
                    newName = withoutExtension.Trim() + Path.GetExtension(newName);
                    changed = true;
                }

                if (changed)
                {
                    string newPath = Path.Combine(Path.GetDirectoryName(filePath), newName);
                    try
                    {
                        if (!File.Exists(newPath))
                        {
                            File.Move(filePath, newPath);
                            ++renamedCount;
                            Console.WriteLine($"Renamed: {fileName}");
                            Console.WriteLine($"      -> {newName}");
                        }
                        else
                            Console.WriteLine($"Skipped (already exists): {newName}");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error renaming file: {fileName} -> {ex.Message}");
                    }
                }
            }
            Console.WriteLine($"\nTotal {renamedCount} files renamed.");
        }

        public static void CleanBrokenFilenames()
        {
            string[] targetFiles = FileScanner.GetTargetFiles(SearchOption.AllDirectories);
            if (targetFiles.Length == 0)
            {
                Console.WriteLine("현재 디렉터리에서 .epub 또는 .pdf 파일을 찾을 수 없습니다.");
                return;
            }

            var autoFix = new List<(string path, string newName)>();
            var mojibakeSuspects = new List<string>();

            foreach (string filePath in targetFiles)
            {
                string fileName = Path.GetFileName(filePath);
                if (IsMojibakeSuspect(fileName))
                {
                    mojibakeSuspects.Add(filePath);
                    continue;
                }
                string cleaned = CleanFileName(fileName);
                if (cleaned != fileName)
                    autoFix.Add((filePath, cleaned));
            }

            // 자동 정리
            if (autoFix.Count > 0)
            {
                int count = 0;
                foreach (var (path, newName) in autoFix)
                {
                    string newPath = Path.Combine(Path.GetDirectoryName(path), newName);
                    try
                    {
                        if (!File.Exists(newPath))
                        {
                            File.Move(path, newPath);
                            count++;
                            Console.WriteLine($"  [정리] {Path.GetFileName(path)}");
                            Console.WriteLine($"       → {newName}");
                        }
                        else
                            Console.WriteLine($"건너뜀 (이미 존재): {newName}");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"실패: {Path.GetFileName(path)} -> {ex.Message}");
                    }
                }
                Console.WriteLine($"\n총 {count}개 파일 정리 완료.");
            }

            // 인코딩 깨짐 의심 파일 (수동 처리 필요)
            if (mojibakeSuspects.Count > 0)
            {
                Console.WriteLine($"\n인코딩 깨짐 의심 파일: {mojibakeSuspects.Count}개 (수동 처리 필요)");
                foreach (string path in mojibakeSuspects)
                    Console.WriteLine($"  [의심] {Path.GetFileName(path)}");
            }

            if (autoFix.Count == 0 && mojibakeSuspects.Count == 0)
                Console.WriteLine("깨진 파일명이 없습니다.");
        }

        private static string CleanFileName(string fileName)
        {
            string ext = Path.GetExtension(fileName);
            string name = Path.GetFileNameWithoutExtension(fileName);

            // URL 인코딩 (%XX 패턴) 디코딩
            if (Regex.IsMatch(name, @"%[0-9A-Fa-f]{2}"))
            {
                try { name = Uri.UnescapeDataString(name); }
                catch { }
            }

            // HTML 엔티티 디코딩 (&amp; &lt; 등)
            if (name.Contains('&'))
                name = WebUtility.HtmlDecode(name);

            // 제어 문자 제거 (0x00-0x1F, 0x7F)
            name = new string(name.Where(c => c >= 0x20 && c != 0x7F).ToArray());

            // Windows 파일명 불법 문자 → 언더바로 치환
            foreach (char illegal in new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' })
                name = name.Replace(illegal, '_');

            return name.Trim() + ext;
        }

        private static bool IsMojibakeSuspect(string fileName)
        {
            // Latin-1 Supplement (À-ÿ, U+00C0-U+00FF) 문자가 3개 이상이고 한글이 없으면 의심
            int latinCount = fileName.Count(c => c >= 'À' && c <= 'ÿ');
            bool hasKorean = fileName.Any(c => c >= '가' && c <= '힣');
            return latinCount >= 3 && !hasKorean;
        }
    }
}
