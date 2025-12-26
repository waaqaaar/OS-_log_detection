using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CyberSentra.Database;
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
    public partial class MlAnomalyHistoryView : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ObservableCollection<string> RunKeys { get; } = new();

        // What the grid binds to (we show Status here too)
        public ObservableCollection<RowVm> Rows { get; } = new();

        private List<MlAnomalyRow> _allRows = new();

        public ISeries[] ScoreSeries { get; private set; } = Array.Empty<ISeries>();
        public Axis[] XAxes { get; private set; } = Array.Empty<Axis>();
        public Axis[] YAxes { get; private set; } = Array.Empty<Axis>();

        private string _compareSummary = "Pick Run (A) and Compare (B) to see New/Resolved/Repeated.";
        public string CompareSummary
        {
            get => _compareSummary;
            set { _compareSummary = value; OnPropertyChanged(); }
        }

        private class RunStat
        {
            public string RunKey { get; set; } = "";
            public int AnomalyCount { get; set; }
            public double MaxScore { get; set; }
        }

        private List<RunStat> _runStats = new();

        // Compare sets (UserWindow)
        private HashSet<string> _anomA = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _anomB = new(StringComparer.OrdinalIgnoreCase);

        // Current selected keys
        private string? _runAKey;
        private string? _runBKey;

        public MlAnomalyHistoryView()
        {
            InitializeComponent();
            DataContext = this;
            LoadRunKeys();
        }

        private void LoadRunKeys()
        {
            try
            {
                RunKeys.Clear();

                var keys = MlAnomalyRepository.LoadRecentRunKeys(limit: 72).ToList();
                foreach (var k in keys)
                    RunKeys.Add(k);

                _runStats = MlAnomalyRepository.LoadRunStats(limit: 72)
                    .Select(x => new RunStat { RunKey = x.RunKey, AnomalyCount = x.AnomalyCount, MaxScore = x.MaxScore })
                    .OrderBy(x => x.RunKey)
                    .ToList();

                BuildTrendChart(_runStats);

                if (RunKeys.Count > 0)
                {
                    RunCombo.SelectedIndex = 0; // triggers LoadRun(A)
                    if (RunKeys.Count > 1)
                        CompareRunCombo.SelectedIndex = 1; // triggers CompareRuns(A,B)
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ML History] " + ex);
            }
        }

        private void BuildTrendChart(List<RunStat> stats)
        {
            var labels = stats.Select(s => FormatRunKey(s.RunKey)).ToArray();
            var anomCounts = stats.Select(s => (double)s.AnomalyCount).ToArray();
            var maxScores = stats.Select(s => s.MaxScore).ToArray();

            ScoreSeries = new ISeries[]
            {
                new ColumnSeries<double> { Name = "Anomaly Count", Values = anomCounts },
                new LineSeries<double> { Name = "Max Score", Values = maxScores }
            };

            XAxes = new[] { new Axis { Labels = labels, LabelsRotation = 0 } };
            YAxes = new[] { new Axis { MinLimit = 0 } };

            OnPropertyChanged(nameof(ScoreSeries));
            OnPropertyChanged(nameof(XAxes));
            OnPropertyChanged(nameof(YAxes));
        }

        private void RunCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (RunCombo.SelectedItem is string runKey)
            {
                _runAKey = runKey;
                LoadRun(runKey);
            }

            // If compare already selected, update compare sets + statuses
            if (_runAKey is string a && CompareRunCombo.SelectedItem is string b)
                CompareRuns(a, b);
        }

        private void CompareRunCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (RunCombo.SelectedItem is string a && CompareRunCombo.SelectedItem is string b)
                CompareRuns(a, b);
        }

        private void CompareRuns(string runA, string runB)
        {
            _runAKey = runA;
            _runBKey = runB;

            // anomalies only for A/B
            _anomA = MlAnomalyRepository.LoadByRunKey(runA)
                .Where(x => x.IsAnomaly)
                .Select(x => x.UserWindow)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _anomB = MlAnomalyRepository.LoadByRunKey(runB)
                .Where(x => x.IsAnomaly)
                .Select(x => x.UserWindow)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var newOnes = _anomB.Except(_anomA).Count();
            var resolved = _anomA.Except(_anomB).Count();
            var repeated = _anomA.Intersect(_anomB).Count();

            CompareSummary =
                $"A={FormatRunKey(runA)}  B={FormatRunKey(runB)}    New(B\\A)={newOnes}, Resolved(A\\B)={resolved}, Repeated={repeated}";

            // Rebuild table statuses based on compare sets
            ApplyFilters();
        }

        private void LoadRun(string runKey)
        {
            try
            {
                _allRows = MlAnomalyRepository.LoadByRunKey(runKey);
                ApplyFilters();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ML History] " + ex);
            }
        }

        private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) => ApplyFilters();
        private void AnomalyOnlySwitch_Changed(object? sender, RoutedEventArgs e) => ApplyFilters();

        private void ApplyFilters()
        {
            var q = (SearchBox?.Text ?? "").Trim();
            var onlyAnom = AnomalyOnlySwitch?.IsChecked == true;

            IEnumerable<MlAnomalyRow> filtered = _allRows;

            if (onlyAnom)
                filtered = filtered.Where(x => x.IsAnomaly);

            if (!string.IsNullOrWhiteSpace(q))
                filtered = filtered.Where(x => x.UserWindow.Contains(q, StringComparison.OrdinalIgnoreCase));

            // Build RowVm with compare status
            Rows.Clear();
            foreach (var r in filtered)
            {
                Rows.Add(new RowVm
                {
                    RunKey = r.RunKey,
                    UserWindow = r.UserWindow,
                    Score = r.Score,
                    IsAnomaly = r.IsAnomaly,
                    CreatedUtc = r.CreatedUtc,
                    Status = GetStatusForRow(r)
                });
            }

            // refresh CompareSummary text only (table updates via ObservableCollection)
            OnPropertyChanged(nameof(CompareSummary));
        }

        private string GetStatusForRow(MlAnomalyRow r)
        {
            // Status only makes sense if both runs are selected and we’re currently viewing A rows
            if (string.IsNullOrWhiteSpace(_runAKey) || string.IsNullOrWhiteSpace(_runBKey))
                return "—";

            // Only classify anomalies meaningfully
            if (!r.IsAnomaly) return "—";

            var uw = r.UserWindow ?? "";

            // If viewing run A table:
            // Resolved = anomaly in A but not in B
            // Repeated = anomaly in both
            if (_anomA.Contains(uw) && !_anomB.Contains(uw)) return "Resolved";
            if (_anomA.Contains(uw) && _anomB.Contains(uw)) return "Repeated";

            return "—";
        }

        // Optional: show New anomalies when user switches RunCombo to B
        // If you switch RunCombo to runB, Status becomes:
        // New = anomaly in B not in A
        // Repeated = in both
        private string GetStatusForRowWhenViewingB(MlAnomalyRow r)
        {
            if (string.IsNullOrWhiteSpace(_runAKey) || string.IsNullOrWhiteSpace(_runBKey))
                return "—";
            if (!r.IsAnomaly) return "—";

            var uw = r.UserWindow ?? "";
            if (_anomB.Contains(uw) && !_anomA.Contains(uw)) return "New";
            if (_anomB.Contains(uw) && _anomA.Contains(uw)) return "Repeated";
            return "—";
        }

        // View full row details
        private async void ViewRow_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Control c) return;
            if (c.DataContext is not RowVm r) return;

            var text = new TextBox
            {
                Text = r.UserWindow ?? "",
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };

            var scroll = new ScrollViewer { Content = text };

            var panel = new StackPanel { Spacing = 10, Margin = new Thickness(14) };
            panel.Children.Add(new TextBlock
            {
                Text = "ML Row Details",
                FontSize = 16,
                FontWeight = Avalonia.Media.FontWeight.Bold
            });

            panel.Children.Add(new TextBlock
            {
                Text = $"Run: {r.RunKey}   |   Score: {r.Score:0.000}   |   Anomaly: {r.IsAnomaly}   |   Status: {r.Status}",
                FontSize = 12,
                Opacity = 0.7
            });

            panel.Children.Add(scroll);

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

        private static string FormatRunKey(string? runKey)
        {
            if (string.IsNullOrWhiteSpace(runKey)) return "";
            var parts = runKey.Split('-');
            if (parts.Length < 4) return runKey;
            return $"{parts[1]}-{parts[2]} {parts[3]}:00";
        }

        // ViewModel for DataGrid (adds Status)
        public class RowVm
        {
            public string RunKey { get; set; } = "";
            public string UserWindow { get; set; } = "";
            public double Score { get; set; }
            public bool IsAnomaly { get; set; }
            public string CreatedUtc { get; set; } = "";
            public string Status { get; set; } = "—";
        }
    }
}
