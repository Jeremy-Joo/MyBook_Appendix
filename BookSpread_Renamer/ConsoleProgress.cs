using System;

namespace BookSpread_Renamer
{
    internal static class ConsoleProgress
    {
        // WPF 진행률 콜백 (done, total) — UI 스레드에서 안전하게 호출하도록 MainWindow가 설정
        public static Action<int, int> OnProgress;

        public static void Show(int done, int total)
        {
            if (OnProgress != null)
            {
                OnProgress(done, total);
            }
            else
            {
                double percent = total == 0 ? 100.0 : done * 100.0 / total;
                Console.Write($"\r진행: {done}/{total} ({percent:F1}%) | 남음: {total - done}    ");
            }
        }

        public static void ClearLine()
        {
            if (OnProgress == null)
                Console.Write("\r" + new string(' ', 79) + "\r");
        }
    }
}
