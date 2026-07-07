using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BookSpread_Renamer
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<string> _sourcePaths = new ObservableCollection<string>();
        private volatile bool _busy;

        public MainWindow()
        {
            InitializeComponent();
            PathListBox.ItemsSource = _sourcePaths;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var config = AppConfig.Instance;
            foreach (var p in config.SourcePaths)
                _sourcePaths.Add(p);

            RecentPathCombo.ItemsSource = config.RecentPaths;
            if (config.RecentPaths.Count > 0)
                RecentPathCombo.SelectedIndex = 0;

            // Console 출력을 로그 TextBox로 리디렉션
            var writer = new TextBoxWriter(LogBox, Dispatcher);
            Console.SetOut(writer);
            Console.SetError(writer);

            // 진행률 콜백 연결
            ConsoleProgress.OnProgress = (done, total) =>
                Dispatcher.Invoke(() =>
                {
                    ProgressBar.Maximum = total;
                    ProgressBar.Value = done;
                    StatusText.Text = total == 0 ? "" :
                        $"{done}/{total} ({done * 100.0 / total:F1}%)";
                });
        }

        private void AddPath_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "검색할 폴더를 선택하세요",
                ShowNewFolderButton = false
            })
            {
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string path = dlg.SelectedPath;
                    if (AppConfig.Instance.AddSourcePath(path))
                    {
                        _sourcePaths.Add(path);
                        RefreshRecent();
                    }
                    else
                        MessageBox.Show("이미 등록된 경로입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void RemovePath_Click(object sender, RoutedEventArgs e)
        {
            if (PathListBox.SelectedItem is string selected)
            {
                AppConfig.Instance.RemoveSourcePath(selected);
                _sourcePaths.Remove(selected);
            }
        }

        private void AddRecentPath_Click(object sender, RoutedEventArgs e)
        {
            if (RecentPathCombo.SelectedItem is string path &&
                !_sourcePaths.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
            {
                if (AppConfig.Instance.AddSourcePath(path))
                    _sourcePaths.Add(path);
            }
        }

        private void RefreshRecent()
        {
            RecentPathCombo.ItemsSource = null;
            RecentPathCombo.ItemsSource = AppConfig.Instance.RecentPaths;
            if (AppConfig.Instance.RecentPaths.Count > 0)
                RecentPathCombo.SelectedIndex = 0;
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogBox.Clear();
        }

        private async void RunOperation(string name, Action op)
        {
            if (_busy) return;
            _busy = true;
            SetButtons(false);
            LogBox.Clear();
            ProgressBar.Value = 0;
            StatusText.Text = $"{name} 실행 중...";
            try
            {
                await Task.Run(op);
                StatusText.Text = $"{name} 완료";
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"\n오류: {ex.Message}");
                StatusText.Text = $"{name} 실패";
            }
            finally
            {
                _busy = false;
                SetButtons(true);
            }
        }

        private void SetButtons(bool enabled)
        {
            foreach (var btn in ActionPanel.Children.OfType<Button>())
                btn.IsEnabled = enabled;
        }

        private void SpreadFile_Click(object sender, RoutedEventArgs e) =>
            RunOperation("Spread File", FileOrganizer.SpreadFile);
        private void CleanNames_Click(object sender, RoutedEventArgs e) =>
            RunOperation("Clean File Names", FileNameCleaner.RenameFiles);
        private void GetFileList_Click(object sender, RoutedEventArgs e) =>
            RunOperation("Get File List", FileScanner.GetFileInfo);
        private void RemoveDuplicates_Click(object sender, RoutedEventArgs e) =>
            RunOperation("Remove Duplicates", DuplicateRemover.RemoveDuplicates);
        private void CleanBroken_Click(object sender, RoutedEventArgs e) =>
            RunOperation("Clean Broken Filenames", FileNameCleaner.CleanBrokenFilenames);
        private void Reclassify_Click(object sender, RoutedEventArgs e) =>
            RunOperation("Reclassify ResultPDF", FileOrganizer.ReclassifyResultPDF);
    }

    // Console.Out/Error → TextBox 리디렉터
    internal sealed class TextBoxWriter : TextWriter
    {
        private readonly TextBox _box;
        private readonly System.Windows.Threading.Dispatcher _d;

        public TextBoxWriter(TextBox box, System.Windows.Threading.Dispatcher d)
        {
            _box = box;
            _d = d;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            // 콘솔 캐리지리턴(\r) 업데이트는 TextBox에서 의미 없으므로 제거
            string clean = value.Replace("\r", "");
            if (string.IsNullOrEmpty(clean)) return;
            _d.BeginInvoke(new Action(() =>
            {
                _box.AppendText(clean);
                _box.ScrollToEnd();
            }));
        }

        public override void WriteLine(string value) => Write((value ?? "") + "\n");
    }
}
