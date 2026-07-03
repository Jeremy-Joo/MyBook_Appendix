using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;

namespace BookSpread_Renamer
{
    // 파일명 키워드 기반 카테고리 분류
    internal static class BookClassifier
    {
        // 카테고리별 키워드 사전. 등록 순서가 동점일 때의 우선순위가 된다 (앞쪽이 우선).
        private static readonly OrderedDictionary CategoryKeywords =
            new OrderedDictionary(StringComparer.OrdinalIgnoreCase)
        {
            {
                "컴퓨터",
                new List<string>()
                {
                    ".net", "c#", "c++", "파이썬", "Python", "프로그래밍", "인공지능", "머신러닝", "딥러닝", "chatgpt", "llm",
                    "코딩", "알고리즘", "자바", "JAVA", "자료구조", "Do it", "LINQ", "MFC",
                    "WPF", "Blazor", "XAML", "MAUI", "ASP.NET", "ASPNET", "Qt", "PyQt", "OpenGL",
                    "gRPC", "MSSQL", "ADO.NET", "ADONET",
                    "Claude Code", "Agentic", "Copilot",
                    "아두이노", "Arduino", "Raspberry", "라즈베리",
                    "리눅스", "Linux", "Git", "깃허브", "GitHub",
                    "엑셀", "Excel", "VBA", "매크로", "피벗",
                    "코틀린", "Kotlin", "스위프트", "Swift",
                    "리액트", "React", "TypeScript", "타입스크립트", "Angular", "Flutter",
                    "프롬프트", "Prompt Engineering",
                    "Node.js", "Nodejs", "FastAPI", "REST", "RESTful",
                    "programming", "algorithm", "database", "network", "cybersecurity", "cloud", "devops", "docker", "kubernetes",
                    "API", "Visual Studio", "안드로이드", "Android", "iOS", "아이폰"
                }
            },
            {
                "게임",
                new List<string>() { "게임", "warcraft", "체스", "game", "chess", "rpg", "strategy" }
            },
            {
                "경제",
                new List<string>()
                {
                    "경제", "주식", "재무제표", "재무", "투자", "환율", "etf", "부동산", "메디치", "금융", "김장섭",
                    "차트", "기술적 분석", "퀀트", "절세", "세금", "회계학", "화폐", "암호화폐", "비트코인",
                    "돈", "부자", "부의", "자산", "금리", "달러", "채권", "배당", "연금", "리츠",
                    "인플레", "재테크", "무역", "코인", "증권", "주가", "시세",
                    "economics", "finance", "investment", "stock", "wealth", "money", "trading", "bitcoin", "crypto", "accounting"
                }
            },
            {
                "경영·마케팅",
                new List<string>()
                {
                    "경영", "마케팅", "창업", "브랜드", "스타트업", "광고", "세일즈", "MBA",
                    "management", "marketing", "startup", "entrepreneurship", "branding", "advertising", "sales"
                }
            },
            {
                "과학",
                new List<string>()
                {
                    "물리", "화학", "생리", "진화", "미생물", "재미있는 이야기", "수학",
                    "physics", "chemistry", "biology", "evolution", "mathematics", "science", "quantum", "astronomy"
                }
            },
            {
                "심리학",
                new List<string>()
                {
                    "심리", "뇌과학", "인지과학", "트라우마", "상담", "정신분석",
                    "psychology", "neuroscience", "cognitive", "behavioral", "mental health", "therapy", "trauma"
                }
            },
            {
                "역사",
                new List<string>()
                {
                    "역사", "세계사", "중국사", "유럽사", "제국", "그리스인", "한국사",
                    "history", "civilization", "ancient", "medieval", "renaissance", "dynasty"
                }
            },
            {
                "문학",
                new List<string>() { "소설", "시집", "에세이", "문학", "단편", "평전", "novel", "fiction", "poetry", "literature" }
            },
            {
                "스포츠",
                new List<string>()
                {
                    "스포츠", "축구", "야구", "농구", "배구", "골프", "테니스", "K리그",
                    "F1", "Formula", "레이싱", "racing", "motorsport", "driver", "sport", "soccer", "baseball", "basketball"
                }
            },
            {
                "자기계발",
                new List<string>()
                {
                    "습관", "성공", "동기부여", "시간관리", "마인드", "군주론",
                    "habit", "success", "motivation", "mindset", "leadership", "productivity", "discipline", "confidence"
                }
            },
            {
                "철학·종교",
                new List<string>() { "철학", "종교", "논어", "맹자", "불교", "기독교", "니체", "소크라테스", "philosophy", "ethics", "stoicism", "religion", "bible", "meditation" }
            },
            {
                "사회·정치",
                new List<string>() { "정치", "사회", "법률", "법학", "헌법", "민주주의", "politics", "democracy", "sociology", "justice", "constitution" }
            },
            {
                "건강",
                new List<string>() { "건강", "의학", "응급", "병리", "해부", "health", "medicine", "anatomy", "fitness", "nutrition", "diet", "wellness" }
            },
            {
                "예술·디자인",
                new List<string>() { "디자인", "건축", "미술", "사진", "드로잉", "design", "architecture", "art", "photography", "illustration", "typography" }
            },
            {
                "외국어",
                new List<string>() { "영어", "일본어", "문법", "회화", "단어", "TOEFL", "TOEIC", "TEPS", "grammar", "vocabulary", "pronunciation", "conversation" }
            },
            {
                "범죄·수사",
                new List<string>()
                {
                    "범죄", "수사", "법의학", "fbi", "프로파일링",
                    "crime", "criminal", "detective", "forensic", "investigation", "murder", "profiling",
                    "espionage", "spy", "spycraft", "KGB", "CIA"
                }
            },
            {
                "군사·무기",
                new List<string>()
                {
                    // "무기"는 비유적 제목("삶의 무기", "무기력" 등)에 오탐이 잦아 제외하고 구체 키워드만 사용
                    "전쟁", "총기", "폭발물", "방위",
                    "war", "warfare", "military", "weapon", "combat", "battle", "army", "navy",
                    "survival", "서바이벌", "prepper", "survivalist",
                    "explosive", "lockpick", "lock pick", "anarchist", "improvised"
                }
            },
            {
                "생활실용",
                new List<string>() { "생활", "청소", "수납", "요리", "정리", "cooking", "recipe", "cleaning", "organizing", "household" }
            },
            {
                "글쓰기",
                new List<string>() { "글쓰기", "라이팅", "writing", "copywriting", "storytelling", "screenplay" }
            }
        };

        // 파일명 키워드로 분류한다. 매칭이 없으면 "기타".
        public static string ClassifyCategory(string fileName)
        {
            return ClassifyByText(fileName);
        }

        // 파일명으로 분류하되, "기타"로 떨어지면 EPUB 메타데이터(dc:subject/title)로 한 번 더 시도한다.
        // 파일명에 카테고리 단어가 없는 은유적 제목(예: "부분과 전체")을 보조로 건진다.
        // 파일명 분류가 성공하면 메타데이터를 읽지 않으므로(성능·회귀 안전), 기존 동작은 그대로 유지된다.
        public static string ClassifyCategory(string fileName, string filePath)
        {
            string category = ClassifyByText(fileName);
            if (category != "기타") return category;

            string meta = EpubMetadataReader.ReadSubjectAndTitle(filePath);
            if (!string.IsNullOrEmpty(meta))
            {
                string metaCategory = ClassifyByText(meta);
                if (metaCategory != "기타") return metaCategory;
            }
            return "기타";
        }

        // 주어진 텍스트에서 키워드 점수가 가장 높은 카테고리를 반환한다. 매칭이 없으면 "기타".
        // 동점이면 CategoryKeywords에 먼저 등록된 카테고리가 우선한다.
        // 처음 매칭되는 카테고리를 그냥 고르면, 설명에 우연히 들어간 단어
        // (예: .NET 책 제목의 "game", "management") 때문에 오분류되는 문제를 막는다.
        private static string ClassifyByText(string text)
        {
            string key = "기타";
            int bestScore = 0;
            foreach (DictionaryEntry entry in CategoryKeywords)
            {
                List<string> keywords = (List<string>)entry.Value;
                int score = keywords.Count(kw => MatchesKeyword(text, kw));
                if (score > bestScore)
                {
                    bestScore = score;
                    key = (string)entry.Key;
                }
            }
            return key;
        }

        public static bool IsKoreanBook(string fileName)
        {
            return fileName.Any(c => c >= '가' && c <= '힣');
        }

        private static bool MatchesKeyword(string fileName, string keyword)
        {
            // 순수 영어 단어는 ASCII 영숫자 경계로 매칭해 오탐 방지 (예: "art"가 "part"에 매칭되지 않음).
            // .NET의 \b는 한글을 단어문자로 취급하므로 "ChatGPT를", "Python으로"처럼 한글이 붙으면
            // 경계가 사라져 매칭에 실패한다. 그래서 ASCII 영숫자만 경계로 보는 룩어라운드를 쓴다.
            bool isPureEnglish = keyword.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));
            if (isPureEnglish)
                return Regex.IsMatch(fileName,
                    @"(?<![A-Za-z0-9])" + Regex.Escape(keyword) + @"(?![A-Za-z0-9])",
                    RegexOptions.IgnoreCase);
            return fileName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
