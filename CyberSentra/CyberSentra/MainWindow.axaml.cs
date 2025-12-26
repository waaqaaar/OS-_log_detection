using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Collections.Generic;

namespace CyberSentra
{
    public partial class MainWindow : Window
    {
        private readonly List<Button> _navButtons = new();

        public MainWindow()
        {
            InitializeComponent();
            InitNavButtons();

            MainContent.Content = new DashboardView();
            SetActive(DashboardButton);
            // Drag window from custom title bar area
            TitleBarDragArea.PointerPressed += TitleBarDragArea_PointerPressed;
        }
        private void TitleBarDragArea_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        }

        private void Minimize_Click(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestore_Click(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void Close_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
        private void DashboardButton_Click(object? sender, RoutedEventArgs e)
        {
            MainContent.Content = new DashboardView();
            SetActive(DashboardButton);
        }

        private void EventsButton_Click(object? sender, RoutedEventArgs e)
        {
            MainContent.Content = new EventsView();
            SetActive(EventsButton);
        }

        private void ThreatsButton_Click(object? sender, RoutedEventArgs e)
        {
            MainContent.Content = new ThreatsView();
            SetActive(ThreatsButton);
        }

        private void AdUsersButton_Click(object? sender, RoutedEventArgs e)
        {
            MainContent.Content = new AdUsersView();
            SetActive(AdUsersButton);
        }

        private void ThreatHistoryButton_Click(object? sender, RoutedEventArgs e)
        {
            MainContent.Content = new HistoricalThreatsView();
            SetActive(ThreatHistoryButton);
        }

        private void MLHistoryButton_Click(object? sender, RoutedEventArgs e)
        {
            MainContent.Content = new MlAnomalyHistoryView();
            SetActive(MLHistoryButton);
        }

        private void MlAnomaliesButton_Click(object? sender, RoutedEventArgs e)
        {
            MainContent.Content = new MlAnomaliesView();
            SetActive(MlAnomaliesButton);
        }

        private void SettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            MainContent.Content = new SettingsView();
            SetActive(SettingsButton);
        }

        private void InitNavButtons()
        {
            _navButtons.Clear();
            _navButtons.AddRange(new[]
            {
                DashboardButton,
                EventsButton,
                ThreatsButton,
                MlAnomaliesButton,
                AdUsersButton,
                ThreatHistoryButton,
                MLHistoryButton,
                SettingsButton,
            });
        }

        private void SetActive(Button active)
        {
            foreach (var b in _navButtons)
                b.Classes.Remove("active");

            active.Classes.Add("active");
        }
    }
}
