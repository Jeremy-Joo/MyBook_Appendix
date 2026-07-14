using System.Linq;
using System.Text.RegularExpressions;

namespace BookSpread_Renamer
{
    // 파일명에서 시리즈 기본 제목과 권 번호를 추출한다.
    // 예: "삼국지 01 도원에 피는 의 (나관중 저)" → ("삼국지", 1)
    //     "내일의 부 1_알파편"                → ("내일의 부", 1)
    internal static class SeriesDetector
    {
        // 저자/출판사 표기 괄호 제거용
        private static readonly Regex ParenRegex = new Regex(
            @"[\(\[][^)\]]*[\)\]]",
            RegexOptions.Compiled);

        // "제목 3", "제목 03", "제목 3권" 형태. 연도 오탐을 막기 위해 1~2자리 숫자만 인정하고,
        // 숫자 뒤에 한글·영문이 바로 붙으면("3천만원", "7피") 권 번호로 보지 않는다.
        private static readonly Regex VolumeRegex = new Regex(
            @"^(?<base>.{2,}?)[\s_\-]+(?<vol>\d{1,2})\s*(권|부|편)?(?=$|[\s_\-.,)~:;])",
            RegexOptions.Compiled);

        public static bool TryParse(string nameWithoutExt, out string baseTitle, out int volume)
        {
            baseTitle = null;
            volume = 0;

            string cleaned = ParenRegex.Replace(nameWithoutExt, " ").Trim();
            var m = VolumeRegex.Match(cleaned);
            if (!m.Success) return false;

            string b = m.Groups["base"].Value.Trim(' ', '_', '-', '.');
            if (b.Length < 2) return false;
            // "04_1"처럼 숫자만 남는 제목은 시리즈로 보지 않는다
            if (b.All(c => char.IsDigit(c) || c == ' ')) return false;

            volume = int.Parse(m.Groups["vol"].Value);
            if (volume < 1 || volume > 99) return false;

            baseTitle = b;
            return true;
        }
    }
}
