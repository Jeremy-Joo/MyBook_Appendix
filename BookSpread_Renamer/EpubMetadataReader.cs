using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace BookSpread_Renamer
{
    // EPUB 내부 OPF 메타데이터(dc:subject, dc:title)를 읽는다.
    // 파일명만으로 카테고리가 안 잡히는 은유적 제목(예: "부분과 전체")의 보조 분류에 쓴다.
    internal static class EpubMetadataReader
    {
        // <dc:subject ...>문학</dc:subject> — 여러 개일 수 있어 전부 수집한다.
        private static readonly Regex SubjectRegex = new Regex(
            @"<dc:subject[^>]*>([^<]+)</dc:subject>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // <dc:title ...>...</dc:title>
        private static readonly Regex TitleRegex = new Regex(
            @"<dc:title[^>]*>([^<]+)</dc:title>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // EPUB의 dc:subject/dc:title 텍스트를 합쳐 반환한다. epub이 아니거나 읽기 실패 시 빈 문자열.
        public static string ReadSubjectAndTitle(string path)
        {
            if (!path.EndsWith(".epub", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            try
            {
                using (var fs = File.OpenRead(path))
                {
                    // ZIP 시그니처(PK) 확인: 손상되었거나 ZIP이 아닌 파일은 조용히 건너뜀
                    if (fs.ReadByte() != 'P' || fs.ReadByte() != 'K') return string.Empty;
                    fs.Seek(0, SeekOrigin.Begin);

                    using (var archive = new ZipArchive(fs, ZipArchiveMode.Read))
                    {
                        var opf = archive.Entries.FirstOrDefault(e =>
                            e.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase));
                        if (opf == null) return string.Empty;

                        using (var reader = new StreamReader(opf.Open(), Encoding.UTF8))
                        {
                            string xml = reader.ReadToEnd();
                            var sb = new StringBuilder();
                            foreach (Match m in SubjectRegex.Matches(xml))
                                sb.Append(m.Groups[1].Value).Append(' ');
                            foreach (Match m in TitleRegex.Matches(xml))
                                sb.Append(m.Groups[1].Value).Append(' ');
                            return WebUtility.HtmlDecode(sb.ToString()).Trim();
                        }
                    }
                }
            }
            catch { }   // 손상된 ZIP 등은 폴백(빈 문자열)
            return string.Empty;
        }
    }
}
