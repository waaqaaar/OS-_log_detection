using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Styling;
using CyberSentra.Database;
using CyberSentra.ML;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;


namespace CyberSentra
{
    public partial class MlAnomaliesView : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // What the DataGrid binds to
        public ObservableCollection<UserAnomaly> Rows { get; } = new();
        public ObservableCollection<EventRecord> SelectedEvents { get; } = new();
        // Keep the full scored list for filtering/searching
        private List<UserAnomaly> _all = new();

        // Chart bindings
        public ISeries[] ScoreSeries { get; private set; } = Array.Empty<ISeries>();
        public Axis[] XAxes { get; private set; } = Array.Empty<Axis>();
        public Axis[] YAxes { get; private set; } = Array.Empty<Axis>();

        // Details bindings
        private string _selectedUserWindow = "Select a row to see details.";
        public string SelectedUserWindow
        {
            get => _selectedUserWindow;
            set { if (_selectedUserWindow != value) { _selectedUserWindow = value; OnPropertyChanged(); } }
        }

        private string _selectedScoreText = "";
        public string SelectedScoreText
        {
            get => _selectedScoreText;
            set { if (_selectedScoreText != value) { _selectedScoreText = value; OnPropertyChanged(); } }
        }

        private string _selectedIsAnomalyText = "";
        public string SelectedIsAnomalyText
        {
            get => _selectedIsAnomalyText;
            set { if (_selectedIsAnomalyText != value) { _selectedIsAnomalyText = value; OnPropertyChanged(); } }
        }

        public ObservableCollection<string> SelectedReasons { get; } = new();
  


        // For explainability we need target feature rows
        private Dictionary<string, UserFeatureRow> _targetRowByUserWindow = new();
        private float[] _baselineMean = Array.Empty<float>();

        public MlAnomaliesView()
        {
            InitializeComponent();
            DataContext = this;
            Load();
        }

        private void ReloadButton_Click(object? sender, RoutedEventArgs e) => Load();

        private void Load()
        {
            try
            {
                var history = EventRepository.GetEventsSince(DateTime.UtcNow.AddDays(-7));

                var now = DateTime.Now;
                var targetStart = now.AddHours(-24);

                bool TryTime(EventRecord ev, out DateTime t) => DateTime.TryParse(ev.Time, out t);

                var baselineEvents = history.Where(ev => TryTime(ev, out var t) && t < targetStart).ToList();
                var targetEvents = history.Where(ev => TryTime(ev, out var t) && t >= targetStart).ToList();

                var baselineRows = FeatureBuilder.BuildPerUserHourlyFeatures(baselineEvents, lastHours: 7 * 24);
                var targetRows = FeatureBuilder.BuildPerUserHourlyFeatures(targetEvents, lastHours: 24);

                _baselineMean = ComputeMean(baselineRows);
                _targetRowByUserWindow = targetRows.ToDictionary(r => r.User, r => r);

                var scored = AnomalyModel.TrainBaselineScoreTarget(baselineRows, targetRows);

                _all = scored;

                var createdUtc = DateTime.UtcNow;
                var runKey = createdUtc.ToString("yyyy-MM-dd-HH");
                var toSave = scored
                    .Where(s => _targetRowByUserWindow.ContainsKey(s.User))
                    .Select(s =>
                    {
                        var f = _targetRowByUserWindow[s.User].Features; // length 6
                        return new CyberSentra.Database.MlAnomalyRow
                        {
                            RunKey = runKey,
                            UserWindow = s.User,
                            Score = s.Score,
                            IsAnomaly = s.IsAnomaly,

                            F0_TotalEvents = f[0],
                            F1_FailedLogins = f[1],
                            F2_ErrorsFailures = f[2],
                            F3_Warnings = f[3],
                            F4_UniqueProcesses = f[4],
                            F5_UniqueSources = f[5],
                        };
                    })
                    .ToList();

                CyberSentra.Database.MlAnomalyRepository.SaveBatch(createdUtc, toSave);

                ApplyFilters();

                BuildChart(scored);

                // reset selection
                SetSelected(null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ML View] " + ex);
            }
        }

        private void ApplyFilters()
        {
            var q = (SearchBox?.Text ?? "").Trim();
            var onlyAnom = AnomalyOnlySwitch?.IsChecked == true;

            IEnumerable<UserAnomaly> filtered = _all;

            if (onlyAnom)
                filtered = filtered.Where(x => x.IsAnomaly);

            if (!string.IsNullOrWhiteSpace(q))
                filtered = filtered.Where(x => x.User.Contains(q, StringComparison.OrdinalIgnoreCase));

            var list = filtered.ToList();

            Rows.Clear();
            foreach (var s in list)
                Rows.Add(s);
        }

        private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) => ApplyFilters();

        private void AnomalyOnlySwitch_Changed(object? sender, RoutedEventArgs e) => ApplyFilters();

        private void RowsGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (RowsGrid.SelectedItem is UserAnomaly ua)
            {
                SetSelected(ua);
                LoadRelatedLogs(ua);
            }
            else
            {
                SetSelected(null);
                SelectedEvents.Clear();
            }
        }


        private void SetSelected(UserAnomaly? ua)
        {
            SelectedReasons.Clear();

            if (ua == null)
            {
                SelectedUserWindow = "Select a row to see details.";
                SelectedScoreText = "";
                SelectedIsAnomalyText = "";
                return;
            }

            SelectedUserWindow = ua.User;
            SelectedScoreText = $"Score: {ua.Score:0.000}";
            SelectedIsAnomalyText = $"Flagged: {(ua.IsAnomaly ? "YES" : "no")}";

            if (_targetRowByUserWindow.TryGetValue(ua.User, out var row) && _baselineMean.Length == row.Features.Length)
            {
                foreach (var r in BuildReasons(row.Features, _baselineMean))
                    SelectedReasons.Add(r);
            }
            else
            {
                SelectedReasons.Add("No feature details available for this window.");
            }
        }

        private void LoadRelatedLogs(UserAnomaly ua)
        {
            SelectedEvents.Clear();

            if (!TryParseUserAndBucket(ua.User, out var user, out var bucketStart))
                return;

            var bucketEnd = bucketStart.AddHours(1);

            var logs = EventRepository.LoadEventsForUserWindow(user, bucketStart, bucketEnd);
            foreach (var ev in logs)
                SelectedEvents.Add(ev);


        }

        private bool TryParseUserAndBucket(string userWindow, out string user, out DateTime bucketStart)
        {
            user = "";
            bucketStart = default;

            // "DOMAIN\user | <time text>"
            var parts = userWindow.Split('|');
            if (parts.Length < 2) return false;

            user = parts[0].Trim();
            var bucketText = parts[1].Trim();

            // 1) If bucketText already parses directly, use it
            if (DateTime.TryParse(bucketText, out bucketStart))
                return true;

            // 2) Try common exact formats
            var year = DateTime.Now.Year;

            string[] formats =
            {
        "MM-dd HH:mm",
        "MM-dd HH:00",
        "MM-dd HH:mm:ss",
        "MM/dd HH:mm",
        "MM/dd HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy/MM/dd HH:mm",
        "yyyy/MM/dd HH:mm:ss",
    };

            // If no year in text, prepend it
            bool hasYear = bucketText.Length >= 4 && char.IsDigit(bucketText[0]) && char.IsDigit(bucketText[1]) &&
                           char.IsDigit(bucketText[2]) && char.IsDigit(bucketText[3]);

            foreach (var fmt in formats)
            {
                var candidate = hasYear ? bucketText : $"{year}-{bucketText}";

                if (DateTime.TryParseExact(
                        candidate,
                        fmt,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out bucketStart))
                    return true;
            }

            return false;
        }


        private static float[] ComputeMean(List<UserFeatureRow> rows)
        {
            if (rows.Count == 0) return Array.Empty<float>();

            int d = rows[0].Features.Length;
            var mean = new float[d];

            foreach (var r in rows)
                for (int i = 0; i < d; i++)
                    mean[i] += r.Features[i];

            for (int i = 0; i < d; i++)
                mean[i] /= rows.Count;

            return mean;
        }

        private static List<string> BuildReasons(float[] x, float[] mean)
        {
            // Features (6):
            // 0 TotalEvents, 1 FailedLogins, 2 Errors/Failures, 3 Warnings, 4 UniqueProcesses, 5 UniqueSources
            string[] names = { "Total events", "Failed logins", "Errors/Failures", "Warnings", "Unique processes", "Unique sources" };

            var diffs = x.Select((v, i) => new
            {
                i,
                delta = v - mean[i],
                v
            })
            .OrderByDescending(a => a.delta)
            .Take(3)
            .ToList();

            var outList = new List<string>();

            foreach (var d in diffs)
            {
                if (d.delta <= 0)
                    continue;

                outList.Add($"{names[d.i]} is higher than baseline (value {d.v:0.###}).");
            }

            if (outList.Count == 0)
                outList.Add("No strong feature deviation from baseline (might be borderline or score-based).");

            return outList;
        }

        private void BuildChart(List<UserAnomaly> scored)
        {
            var bucketScores = scored
                .Select(x => new { Bucket = ExtractBucketText(x.User), x.Score })
                .Where(x => x.Bucket != null)
                .GroupBy(x => x.Bucket!)
                .Select(g => new { Bucket = g.Key, Score = g.Max(v => v.Score) })
                .OrderBy(x => x.Bucket)
                .ToList();

            var labels = bucketScores.Select(x => x.Bucket).ToArray();
            var values = bucketScores.Select(x => (double)x.Score).ToArray();

            ScoreSeries = new ISeries[]
            {
                new LineSeries<double> { Values = values }
            };

            XAxes = new[]
            {
                new Axis { Labels = labels, LabelsRotation = 0 }
            };

            YAxes = new[]
            {
                new Axis { MinLimit = 0 }
            };

            // Refresh bindings
            DataContext = null;
            DataContext = this;
        }

        private string? ExtractBucketText(string userWindow)
        {
            var parts = userWindow.Split('|');
            if (parts.Length < 2) return null;
            return parts[1].Trim();
        }
        private async void ViewEventDetails_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Control c) return;
            if (c.DataContext is not EventRecord ev) return;

            var tb = new TextBox
            {
                Text = ev.Details ?? "",
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
               // VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
               // HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var copyBtn = new Button { Content = "Copy", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
            copyBtn.Click += async (_, __) =>
            {
                var top = TopLevel.GetTopLevel(this);
                if (top?.Clipboard != null)
                    await top.Clipboard.SetTextAsync(tb.Text);
            };

            var panel = new StackPanel { Spacing = 10, Margin = new Thickness(14) };
            panel.Children.Add(new TextBlock { Text = "Log Details", FontSize = 16, FontWeight = Avalonia.Media.FontWeight.Bold });
            panel.Children.Add(new TextBlock
            {
                Text = $"{ev.Time} • {ev.Type} • {ev.Severity} • {ev.Process}",
                Opacity = 0.8,
                FontSize = 12
            });
            panel.Children.Add(copyBtn);
            panel.Children.Add(tb);

            var win = new Window
            {
                Title = "Details",
                Width = 900,
                Height = 520,
                Content = panel
            };

            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner != null) await win.ShowDialog(owner);
            else win.Show();
        }

    }
}
