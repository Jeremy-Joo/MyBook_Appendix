using System;

namespace BookSpread_Renamer
{
    // 콘솔 한 줄 진행률 표시
    internal static class ConsoleProgress
    {
        // 진행 상황을 콘솔 한 줄에 갱신 표시한다 (처리/전체, 진행율, 남은 수량)
        public static void Show(int done, int total)
        {
            double percent = total == 0 ? 100.0 : done * 100.0 / total;
            Console.Write($"\r진행: {done}/{total} ({percent:F1}%) | 남음: {total - done}    ");
        }

        // 진행 줄을 지워서 일반 로그 출력이 겹치지 않게 한다
        public static void ClearLine()
        {
            Console.Write("\r" + new string(' ', 79) + "\r");
        }
    }
}
