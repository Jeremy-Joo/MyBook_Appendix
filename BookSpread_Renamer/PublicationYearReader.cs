using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BookSpread_Renamer
{
    // EPUB/PDF 메타데이터에서 출판연도를 추출한다
    internal static class PublicationYearReader
    {
        // EPUB OPF의 <dc:date>2026-01-23</dc:date> 형식에서 출판연도 추출
        private static readonly Regex EpubDateRegex = new Regex(
            @"<dc:date[^>]*>[^<]*?(\d{4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // PDF Info 사전의 /CreationDate (D:20260123...) 형식
        private static readonly Regex PdfCreationDateRegex = new Regex(
            @"/CreationDate\s*\(D:(\d{4})",
            RegexOptions.Compiled);

        // PDF XMP 메타데이터의 CreateDate (xmp:CreateDate="2024-..." 또는 <xmp:CreateDate>2024-...)
        private static readonly Regex PdfXmpDateRegex = new Regex(
            @"CreateDate[>=""']*\s*[""']?(\d{4})-",
            RegexOptions.Compiled);

        // 연도 그룹: 2024년 이후는 연도별 폴더(2024, 2025, 2026, ...), 그 이전은 "이전"
        // 출판연도(EPUB/PDF 메타데이터)를 우선 사용하고, 없으면 파일 수정 날짜로 폴백한다.
        public static string GetYearGroup(string path)
        {
            int year = GetPublicationYear(path);
            return year >= 2024 ? year.ToString() : "이전";
        }

        // 출판연도 추출: EPUB은 dc:date, PDF는 CreationDate/XMP CreateDate.
        // 유효한 연도를 찾지 못하면 파일 수정 날짜의 연도를 반환한다.
        public static int GetPublicationYear(string path)
        {
            string ext = Path.GetExtension(path);
            int year = 0;
            if (ext.Equals(".epub", StringComparison.OrdinalIgnoreCase))
                year = GetEpubYear(path);
            else if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                year = GetPdfYear(path);

            // 비정상 값(미래 연도, 너무 오래된 값)은 버리고 폴백
            if (year < 1900 || year > DateTime.Now.Year + 1)
                year = 0;

            return year != 0 ? year : File.GetLastWriteTime(path).Year;
        }

        // EPUB(ZIP) 내부 OPF 파일에서 <dc:date>의 연도를 읽는다. 실패 시 0.
        private static int GetEpubYear(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    // ZIP 시그니처(PK) 확인: 손상되었거나 ZIP이 아닌 파일은 예외 없이 건너뜀
                    int b1 = fs.ReadByte();
                    int b2 = fs.ReadByte();
                    if (b1 != 'P' || b2 != 'K') return 0;
                    fs.Seek(0, SeekOrigin.Begin);

                    using (var archive = new ZipArchive(fs, ZipArchiveMode.Read))
                    {
                        var opf = archive.Entries.FirstOrDefault(e =>
                            e.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase));
                        if (opf == null) return 0;

                        using (var reader = new StreamReader(opf.Open(), Encoding.UTF8))
                        {
                            var m = EpubDateRegex.Match(reader.ReadToEnd());
                            if (m.Success) return int.Parse(m.Groups[1].Value);
                        }
                    }
                }
            }
            catch { }   // 손상된 ZIP 등은 폴백 처리
            return 0;
        }

        // PDF 앞뒤 일부에서 CreationDate/XMP CreateDate의 연도를 읽는다. 실패 시 0.
        // 메타데이터가 여러 개면 더 이른 연도를 택한다 (재생성된 PDF의 CreationDate는
        // 다운로드/변환 시점이라 출판연도보다 늦은 경우가 많다).
        private static int GetPdfYear(string path)
        {
            try
            {
                const int chunk = 128 * 1024;
                long len = new FileInfo(path).Length;
                byte[] buffer;
                using (var fs = File.OpenRead(path))
                {
                    if (len <= chunk * 2)
                    {
                        buffer = new byte[len];
                        fs.Read(buffer, 0, buffer.Length);
                    }
                    else
                    {
                        // Info 사전과 XMP는 보통 파일 맨 앞이나 맨 뒤에 있다
                        buffer = new byte[chunk * 2];
                        fs.Read(buffer, 0, chunk);
                        fs.Seek(-chunk, SeekOrigin.End);
                        fs.Read(buffer, chunk, chunk);
                    }
                }

                string text = Encoding.ASCII.GetString(buffer);
                int best = 0;
                var m = PdfCreationDateRegex.Match(text);
                if (m.Success) best = int.Parse(m.Groups[1].Value);
                m = PdfXmpDateRegex.Match(text);
                if (m.Success)
                {
                    int xmp = int.Parse(m.Groups[1].Value);
                    if (best == 0 || (xmp >= 1900 && xmp < best)) best = xmp;
                }
                return best;
            }
            catch { }
            return 0;
        }
    }
}
