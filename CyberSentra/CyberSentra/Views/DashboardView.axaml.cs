using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CyberSentra.Database;
using CyberSentra.ML;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CyberSentra
{
    public partial class DashboardView : UserControl, INotifyPropertyChanged
    {

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        public ObservableCollection<string> TopTechniques { get; } = new();

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        // Simple properties for header
        // ===== Header properties =====
        private string _platformName = string.Empty;
        public string PlatformName
        {
            get => _platformName;
            set
            {
                if (_platformName != value)
                {
                    _platformName = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _appStatus = string.Empty;
        public string AppStatus
        {
            get => _appStatus;
            set
            {
                if (_appStatus != value)
                {
                    _appStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _lastLogUpdateText = string.Empty;
        public string LastLogUpdateText
        {
            get => _lastLogUpdateText;
            set
            {
                if (_lastLogUpdateText != value)
                {
                    _lastLogUpdateText = value;
                    OnPropertyChanged();
                }
            }
        }

        // ===== Main metrics =====
        private int _securityIndex;
        public int SecurityIndex
        {
            get => _securityIndex;
            set
            {
                if (_securityIndex != value)
                {
                    _securityIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SecurityIndexText)); // depends on SecurityIndex
                }
            }
        }

        public string SecurityIndexText => $"{SecurityIndex}%";

        private int _totalEvents;
        public int TotalEvents
        {
            get => _totalEvents;
            set
            {
                if (_totalEvents != value)
                {
                    _totalEvents = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _anomalyCount;
        public int AnomalyCount
        {
            get => _anomalyCount;
            set
            {
                if (_anomalyCount != value)
                {
                    _anomalyCount = value;
                    OnPropertyChanged();
                }
            }
        }


        private int _mlAnomalyCount;
        public int MlAnomalyCount
        {
            get => _mlAnomalyCount;
            set
            {
                if (_mlAnomalyCount != value)
                {
                    _mlAnomalyCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<string> TopAnomalousUsers { get; } = new();




        private int _threatCount;
        public int ThreatCount
        {
            get => _threatCount;
            set
            {
                if (_threatCount != value)
                {
                    _threatCount = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _failedLoginCount;
        public int FailedLoginCount
        {
            get => _failedLoginCount;
            set
            {
                if (_failedLoginCount != value)
                {
                    _failedLoginCount = value;
                    OnPropertyChanged();
                }
            }
        }

        // Timer for near real-time refresh
        // private readonly DispatcherTimer _timer;

        // Recent anomalies list for DataGrid
        public ObservableCollection<AnomalyItem> RecentAnomalies { get; } = new();

        public DashboardView()
        {
            InitializeComponent();



            InitHeader();
            InitFromEvents();
            LoadRecentAnomalies();
            // Use the view itself as DataContext since you don't use MVVM
            DataContext = this;
            //_timer = new DispatcherTimer
            //{
            //    Interval = TimeSpan.FromSeconds(10) // change interval if needed
            //};
            //_timer.Tick += Timer_Tick;
            //_timer.Start();

        }


        private void InitHeader()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                PlatformName = "Platform: Windows";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                PlatformName = "Platform: Linux";
            else
                PlatformName = "Platform: Unknown";

            AppStatus = "Running";
            LastLogUpdateText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static void NormalizeInPlace(List<UserFeatureRow> rows)
        {
            if (rows.Count == 0) return;

            int dims = rows[0].Features.Length;
            var min = new float[dims];
            var max = new float[dims];

            for (int j = 0; j < dims; j++)
            {
                min[j] = float.MaxValue;
                max[j] = float.MinValue;
            }

            foreach (var r in rows)
            {
                for (int j = 0; j < dims; j++)
                {
                    min[j] = Math.Min(min[j], r.Features[j]);
                    max[j] = Math.Max(max[j], r.Features[j]);
                }
            }

            foreach (var r in rows)
            {
                for (int j = 0; j < dims; j++)
                {
                    var denom = (max[j] - min[j]);
                    r.Features[j] = denom < 1e-6 ? 0f : (r.Features[j] - min[j]) / denom;
                }
            }
        }

        private void InitFromEvents()
        {
            var events = EventContext.GetCurrentEvents();

            TotalEvents = events.Count();

            FailedLoginCount = events
                .Count(e => e.Details.Contains("failed", StringComparison.OrdinalIgnoreCase));

            AnomalyCount = events
                .Count(e =>
                    e.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase) ||
                    e.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase) ||
                    e.Severity.Contains("Failure", StringComparison.OrdinalIgnoreCase));

            // Get threats
            var threats = ThreatDetector.GetThreats(events);
            ThreatCount = threats.Count;

            // ---- COMPUTE TOP MITRE TECHNIQUES ----
            TopTechniques.Clear();

            var top = threats
                .GroupBy(t => t.Technique)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => $"{g.Key}  ({g.Count()} hits)");

            foreach (var t in top)
                TopTechniques.Add(t);


            // ---- Compute security index ----
            var total = TotalEvents == 0 ? 1 : TotalEvents;
            double anomalyRate = (double)AnomalyCount / total;
            double threatRate = (double)ThreatCount / total;

            // Cap to avoid collapsing to 0 on noisy systems
            anomalyRate = Math.Min(anomalyRate, 0.30); // max 30% influence
            threatRate = Math.Min(threatRate, 0.10);  // max 10% influence

            var riskScore = anomalyRate * 0.5 + threatRate * 1.5;
            var index = 100 - (int)(riskScore * 100);
            SecurityIndex = Math.Clamp(index, 0, 100);

           try
{
    var history = EventRepository.GetEventsSince(DateTime.UtcNow.AddDays(-7));

    var now = DateTime.Now;
    var targetStart = now.AddHours(-24);

    bool TryTime(EventRecord e, out DateTime t) => DateTime.TryParse(e.Time, out t);

    // Baseline = everything older than last 24h (within 7 days)
    var baselineEvents = history
        .Where(e => TryTime(e, out var t) && t < targetStart)
        .ToList();

    // Target = last 24h
    var targetEvents = history
        .Where(e => TryTime(e, out var t) && t >= targetStart)
        .ToList();

    var baselineRows = FeatureBuilder.BuildPerUserHourlyFeatures(baselineEvents, lastHours: 7 * 24);
    var targetRows   = FeatureBuilder.BuildPerUserHourlyFeatures(targetEvents, lastHours: 24);

    Debug.WriteLine($"[ML] history={history.Count}, baselineEvents={baselineEvents.Count}, targetEvents={targetEvents.Count}");
    Debug.WriteLine($"[ML] baselineRows={baselineRows.Count}, targetRows={targetRows.Count}");

    var scored = AnomalyModel.TrainBaselineScoreTarget(baselineRows, targetRows);

    MlAnomalyCount = scored.Count(a => a.IsAnomaly);

    TopAnomalousUsers.Clear();
    foreach (var a in scored.Take(3))
        TopAnomalousUsers.Add($"{a.User}  (score: {a.Score:0.000})");
}
catch (Exception ex)
{
    Debug.WriteLine("[ML] " + ex.Message);
    MlAnomalyCount = 0;
}




        }




        private void LoadRecentAnomalies()
        {
            RecentAnomalies.Clear();

            var events = EventContext.GetCurrentEvents()
                .Where(e =>
                    e.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase) ||
                    e.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase) ||
                    e.Severity.Contains("Failure", StringComparison.OrdinalIgnoreCase))
                .Take(10);

            foreach (var ev in events)
            {
                RecentAnomalies.Add(new AnomalyItem
                {
                    Time = ev.Time,
                    Type = ev.Type,
                    Severity = ev.Severity,
                    Score = 80, // placeholder score for now
                    User = ev.User,
                    Description = ev.Details
                });
            }
        }

        // ===== Time filter buttons =====
        private void TimeFilterButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton btn)
                return;

            // Set global time range
            if (btn == AllTimeButton)
                EventContext.SetTimeRange(TimeRange.All);
            else if (btn == Last24hButton)
                EventContext.SetTimeRange(TimeRange.Last24Hours);
            else if (btn == Last1hButton)
                EventContext.SetTimeRange(TimeRange.Last1Hour);

            // Toggle visual state
            AllTimeButton.IsChecked = btn == AllTimeButton;
            Last24hButton.IsChecked = btn == Last24hButton;
            Last1hButton.IsChecked = btn == Last1hButton;

            // Recompute dashboard based on new filter
            InitFromEvents();
            LoadRecentAnomalies();
        }


        // Refresh button in the UI
        // ===== Manual refresh button =====
        private async void RefreshButton_Click(object? sender, RoutedEventArgs e)
        {
            await DoRefresh();
        }


        // ===== Timer tick (near real-time refresh) =====
        //private void Timer_Tick(object? sender, EventArgs e)
        //{
        //    DoRefresh();
        //}

        private async Task DoRefresh()
        {
            // Show status while refreshing
            AppStatus = "Refreshing...";
            LastLogUpdateText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Heavy work: reload logs on background thread
            await Task.Run(() =>
            {
                EventSource.Reload();
            });

            // Back on UI thread: recompute metrics & anomalies from new data
            InitFromEvents();
            LoadRecentAnomalies();

            AppStatus = "Running";
        }




        public class AnomalyItem
        {
            public string Time { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Severity { get; set; } = string.Empty;
            public int Score { get; set; }
            public string User { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }

        private void TextBlock_ActualThemeVariantChanged(object? sender, EventArgs e)
        {
        }
    }
}
