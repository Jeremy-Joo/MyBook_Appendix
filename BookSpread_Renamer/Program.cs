using System;

namespace BookSpread_Renamer
{
    // 메인 메뉴: 각 기능은 담당 클래스에 위임한다.
    //   FileOrganizer        - 분류 이동 / 재분류
    //   FileNameCleaner      - 파일명 정리 / 깨진 파일명 복구
    //   FileScanner          - 대상 파일 검색 / 목록 저장
    //   DuplicateRemover     - 중복 파일 제거
    //   SourcePathManager    - 검색 경로 관리
    //   BookClassifier       - 카테고리 분류 규칙
    //   PublicationYearReader- 출판연도 추출
    internal class Program
    {
        private static void Main(string[] args)
        {
            while (true)
            {
                var sourcePaths = SourcePathManager.LoadSourcePaths();
                Console.WriteLine("Select Option:");
                Console.WriteLine($"[검색 경로: {string.Join(", ", sourcePaths)}]");
                Console.WriteLine("1. Spread File (파일을 분류하여 이동)");
                Console.WriteLine("2. Clean File Names (파일 이름 정리)");
                Console.WriteLine("3. Get File List (파일 목록을 텍스트 파일로 저장)");
                Console.WriteLine("4. Remove Duplicates (중복 파일 제거)");
                Console.WriteLine("5. Clean Broken Filenames (깨진 파일명 정리)");
                Console.WriteLine("6. Manage Source Paths (검색 경로 관리)");
                Console.WriteLine("7. Reclassify ResultPDF (기존 분류 결과를 현재 규칙으로 재정리)");
                Console.WriteLine("8. Exit (종료)");
                Console.Write("Enter your choice (1-8): ");
                switch (Console.ReadLine())
                {
                    case "1":
                        FileOrganizer.SpreadFile();
                        break;
                    case "2":
                        FileNameCleaner.RenameFiles();
                        break;
                    case "3":
                        FileScanner.GetFileInfo();
                        break;
                    case "4":
                        DuplicateRemover.RemoveDuplicates();
                        break;
                    case "5":
                        FileNameCleaner.CleanBrokenFilenames();
                        break;
                    case "6":
                        SourcePathManager.ManageSourcePaths();
                        break;
                    case "7":
                        FileOrganizer.ReclassifyResultPDF();
                        break;
                    case "8":
                        Console.WriteLine("프로그램을 종료합니다.");
                        return;
                    default:
                        Console.WriteLine("Invalid choice. Please select a valid option.");
                        break;
                }
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                Console.Clear();
            }
        }
    }
}
