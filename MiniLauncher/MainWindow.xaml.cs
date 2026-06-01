using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace MiniLauncher;

public partial class MainWindow : Window, IDisposable
{
    private const int HotkeyId = 19001;
    private const int WmHotkey = 0x0312;
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupRegistryName = "MiniLauncher";
    private const uint ModAlt = 0x0001;
    private const uint VkSpace = 0x20;

    private readonly ObservableCollection<PinnedItem> _pins = [];
    private readonly ObservableCollection<StoreItem> _storeItems = [];
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly string _configPath;
    private Forms.NotifyIcon? _trayIcon;
    private HwndSource? _source;
    private LauncherConfig _config = new();
    private bool _updatingUi;
    private bool _uiReady;
    private bool _isHiding;
    private string _currentView = "home";

    public MainWindow()
    {
        InitializeComponent();

        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MiniLauncher");
        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, "config.json");

        ItemsList.ItemsSource = _pins;
        StoreList.ItemsSource = _storeItems;
    }

    public void ShowLauncher()
    {
        if (!IsLoaded)
        {
            Show();
        }

        ApplyTheme();
        PositionLauncher();
        Show();
        WindowState = WindowState.Normal;
        Topmost = _config.AlwaysOnTop;
        Activate();
        AnimateShow();
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    public void Dispose()
    {
        SaveConfig();
        UnregisterHotkey();
        _trayIcon?.Dispose();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadConfig();
        ApplyTheme();
        _uiReady = true;
        ApplyViewMode();
        SetupTray();
        RegisterHotkey();
        ShowHome();
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        if (_config.AutoHideOnLostFocus && _currentView == "home")
        {
            HideLauncher();
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideLauncher();
            e.Handled = true;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ShowHome();
        RefreshPins();
    }

    private void SearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OpenSelectedOrFirst();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down && ItemsList.Items.Count > 0)
        {
            ItemsList.Focus();
            ItemsList.SelectedIndex = Math.Max(0, ItemsList.SelectedIndex);
            e.Handled = true;
        }
    }

    private void ItemsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsList.SelectedItem is PinnedItem item)
        {
            OpenPinnedItem(item);
        }
    }

    private void ItemsList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OpenSelectedOrFirst();
            e.Handled = true;
        }
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = HasSupportedDrop(e)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return;
        }

        var added = new List<string>();
        foreach (var path in (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop))
        {
            if (AddPin(path))
            {
                added.Add(Path.GetFullPath(path));
            }
        }

        SaveConfig();
        ShowHome();
        RefreshPins();
        if (added.Count > 0)
        {
            SelectPinnedPath(added[^1]);
            StatusText.Text = added.Count == 1
                ? $"Pinned: {MakeDisplayName(added[0])}"
                : $"Pinned {added.Count} items";
        }
        else
        {
            StatusText.Text = "Nothing new to pin";
        }
    }

    private void Shell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1 && e.OriginalSource is not System.Windows.Controls.TextBox)
        {
            DragMove();
        }
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e) => ShowHome();

    private void StoreButton_Click(object sender, RoutedEventArgs e)
    {
        _currentView = "store";
        ItemsList.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        StoreView.Visibility = Visibility.Visible;
        LoadStore();
        StatusText.Text = _config.StorePath.Length == 0 ? "Set a store path in Settings" : _config.StorePath;
        AnimatePanel(StoreView);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _currentView = "settings";
        ItemsList.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Collapsed;
        StoreView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Visible;
        _updatingUi = true;
        StorePathBox.Text = _config.StorePath;
        GridModeRadio.IsChecked = _config.ViewMode != "List";
        ListModeRadio.IsChecked = _config.ViewMode == "List";
        StartupCheckBox.IsChecked = IsStartupEnabled();
        TopmostCheckBox.IsChecked = _config.AlwaysOnTop;
        AutoHideCheckBox.IsChecked = _config.AutoHideOnLostFocus;
        CursorPositionCheckBox.IsChecked = _config.OpenNearCursor;
        _updatingUi = false;
        StatusText.Text = _configPath;
        AnimatePanel(SettingsView);
    }

    private void HideButton_Click(object sender, RoutedEventArgs e) => HideLauncher();

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
    }

    private void AddAppButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Add app or shortcut",
            Filter = "Apps and shortcuts|*.exe;*.lnk;*.bat;*.cmd|All files|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        foreach (var file in dialog.FileNames)
        {
            AddPin(file);
        }

        SaveConfig();
        ShowHome();
        RefreshPins();
    }

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choose folder to pin",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        AddPin(dialog.SelectedPath);
        SaveConfig();
        ShowHome();
        RefreshPins();
    }

    private void RefreshStore_Click(object sender, RoutedEventArgs e) => LoadStore();

    private void BrowseStorePath_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choose offline store folder",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_config.StorePath) ? _config.StorePath : string.Empty
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            _config.StorePath = dialog.SelectedPath;
            StorePathBox.Text = dialog.SelectedPath;
            SaveConfig();
            LoadStore();
        }
    }

    private void StorePathBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingUi)
        {
            return;
        }

        _config.StorePath = StorePathBox.Text.Trim();
        SaveConfig();
    }

    private void ViewMode_Checked(object sender, RoutedEventArgs e)
    {
        if (_updatingUi || !_uiReady)
        {
            return;
        }

        _config.ViewMode = ListModeRadio.IsChecked == true ? "List" : "Grid";
        ApplyViewMode();
        SaveConfig();
        RefreshPins();
    }

    private void StartupCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingUi || !_uiReady)
        {
            return;
        }

        SetStartupEnabled(StartupCheckBox.IsChecked == true);
        StatusText.Text = StartupCheckBox.IsChecked == true ? "Startup enabled" : "Startup disabled";
    }

    private void BehaviorCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingUi || !_uiReady)
        {
            return;
        }

        _config.AlwaysOnTop = TopmostCheckBox.IsChecked == true;
        _config.AutoHideOnLostFocus = AutoHideCheckBox.IsChecked == true;
        _config.OpenNearCursor = CursorPositionCheckBox.IsChecked == true;
        Topmost = _config.AlwaysOnTop;
        SaveConfig();
    }

    private void InstallStoreItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not StoreItem item)
        {
            return;
        }

        AddPin(item.Path, item.Name, item.IconPath, item.Keywords);
        SaveConfig();
        ShowHome();
        RefreshPins();
    }

    private void OpenItem_Click(object sender, RoutedEventArgs e)
    {
        if (ContextItem(sender) is PinnedItem item)
        {
            OpenPinnedItem(item);
        }
    }

    private void ToggleFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (ContextItem(sender) is not PinnedItem item)
        {
            return;
        }

        var configItem = _config.Pins.FirstOrDefault(pin => string.Equals(pin.Path, item.Path, StringComparison.OrdinalIgnoreCase));
        if (configItem is null)
        {
            return;
        }

        configItem.IsFavorite = !configItem.IsFavorite;
        SaveConfig();
        RefreshPins();
    }

    private void EditKeywords_Click(object sender, RoutedEventArgs e)
    {
        if (ContextItem(sender) is not PinnedItem item)
        {
            return;
        }

        var configItem = FindConfigItem(item.Path);
        if (configItem is null)
        {
            return;
        }

        var keywords = Microsoft.VisualBasic.Interaction.InputBox("Keywords for search", "Edit keywords", configItem.Keywords).Trim();
        configItem.Keywords = keywords;
        SaveConfig();
        RefreshPins();
    }

    private void ChangeIcon_Click(object sender, RoutedEventArgs e)
    {
        if (ContextItem(sender) is not PinnedItem item)
        {
            return;
        }

        var configItem = FindConfigItem(item.Path);
        if (configItem is null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose custom icon image",
            Filter = "Images and icons|*.png;*.jpg;*.jpeg;*.ico;*.bmp|All files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        configItem.IconPath = dialog.FileName;
        SaveConfig();
        RefreshPins();
    }

    private void RenameItem_Click(object sender, RoutedEventArgs e)
    {
        if (ContextItem(sender) is not PinnedItem item)
        {
            return;
        }

        var name = Microsoft.VisualBasic.Interaction.InputBox("Display name", "Rename", item.Name).Trim();
        if (name.Length == 0)
        {
            return;
        }

        var configItem = _config.Pins.FirstOrDefault(pin => string.Equals(pin.Path, item.Path, StringComparison.OrdinalIgnoreCase));
        if (configItem is not null)
        {
            configItem.Name = name;
        }

        item.Name = name;
        SaveConfig();
        RefreshPins();
    }

    private void MoveUpItem_Click(object sender, RoutedEventArgs e)
    {
        MovePinnedItem(sender, -1);
    }

    private void MoveDownItem_Click(object sender, RoutedEventArgs e)
    {
        MovePinnedItem(sender, 1);
    }

    private void OpenContainingFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ContextItem(sender) is not PinnedItem item)
        {
            return;
        }

        var folder = Directory.Exists(item.Path) ? item.Path : Path.GetDirectoryName(item.Path);
        if (folder is not null && Directory.Exists(folder))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
    }

    private void UnpinItem_Click(object sender, RoutedEventArgs e)
    {
        if (ContextItem(sender) is not PinnedItem item)
        {
            return;
        }

        _config.Pins.RemoveAll(pin => string.Equals(pin.Path, item.Path, StringComparison.OrdinalIgnoreCase));
        SaveConfig();
        RefreshPins();
    }

    private void OpenConfigButton_Click(object sender, RoutedEventArgs e)
    {
        SaveConfig();
        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{_configPath}\"") { UseShellExecute = true });
    }

    private void ExportConfigButton_Click(object sender, RoutedEventArgs e)
    {
        SaveConfig();
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Mini Launcher config",
            FileName = $"mini-launcher-config-{DateTime.Now:yyyyMMdd-HHmm}.json",
            Filter = "JSON config|*.json|All files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        File.Copy(_configPath, dialog.FileName, true);
        StatusText.Text = $"Exported: {dialog.FileName}";
    }

    private void ImportConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import Mini Launcher config",
            Filter = "JSON config|*.json|All files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var imported = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(dialog.FileName), _jsonOptions);
        if (imported is null)
        {
            StatusText.Text = "Import failed";
            return;
        }

        _config = imported;
        NormalizeConfig();
        SaveConfig();
        ApplyTheme();
        ApplyViewMode();
        ShowHome();
        StatusText.Text = $"Imported: {dialog.FileName}";
    }

    private void CleanMissingButton_Click(object sender, RoutedEventArgs e)
    {
        var before = _config.Pins.Count;
        _config.Pins.RemoveAll(pin => !File.Exists(pin.Path) && !Directory.Exists(pin.Path));
        var removed = before - _config.Pins.Count;
        SaveConfig();
        RefreshPins();
        StatusText.Text = removed == 0 ? "No missing pins found" : $"Removed {removed} missing pins";
    }

    private void LoadConfig()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                _config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(_configPath), _jsonOptions) ?? new LauncherConfig();
                NormalizeConfig();
            }
            catch
            {
                _config = new LauncherConfig();
            }
        }

        if (_config.Pins.Count == 0)
        {
            AddKnownDefaultPin(Environment.SpecialFolder.Desktop);
            AddKnownDefaultPin(Environment.SpecialFolder.MyDocuments);
            SaveConfig();
        }

        NormalizeConfig();

        RefreshPins();
    }

    private void SaveConfig()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        File.WriteAllText(_configPath, JsonSerializer.Serialize(_config, _jsonOptions));
    }

    private void RefreshPins()
    {
        if (_currentView != "home")
        {
            return;
        }

        var query = SearchBox.Text.Trim();
        var matches = _config.Pins
            .Where(pin => Matches(pin, query))
            .OrderByDescending(pin => pin.IsFavorite)
            .ThenBy(pin => pin.SortOrder)
            .ThenByDescending(pin => pin.LastOpenedUtc)
            .ThenBy(pin => pin.Name)
            .Select(PinnedItem.FromConfig)
            .ToList();

        _pins.Clear();
        foreach (var item in matches)
        {
            _pins.Add(item);
        }

        ResultCountText.Text = $"{_pins.Count}";
        EmptyState.Visibility = _pins.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ItemsList.Visibility = _pins.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        HeaderMetaText.Text = $"{_config.Pins.Count} pins | Alt + Space";
        StatusText.Text = query.Length == 0 ? _configPath : $"Search: {query}";
        AnimatePanel(ItemsList);
    }

    private void ShowHome()
    {
        _currentView = "home";
        StoreView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        RefreshPins();
        AnimatePanel(ItemsList);
    }

    private void LoadStore()
    {
        _storeItems.Clear();
        if (!Directory.Exists(_config.StorePath))
        {
            StoreHintText.Text = _config.StorePath.Length == 0 ? "Set a store path in Settings" : _config.StorePath;
            StatusText.Text = "Offline store folder not found";
            return;
        }

        foreach (var item in StoreReader.Load(_config.StorePath))
        {
            _storeItems.Add(item);
        }

        StatusText.Text = $"{_storeItems.Count} store items";
        StoreHintText.Text = _config.StorePath;
        AnimatePanel(StoreList);
    }

    private void AddKnownDefaultPin(Environment.SpecialFolder folder)
    {
        var path = Environment.GetFolderPath(folder);
        if (Directory.Exists(path))
        {
            AddPin(path);
        }
    }

    private bool AddPin(string path, string? name = null, string? iconPath = null, string? keywords = null)
    {
        if (!IsSupportedPinPath(path))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path);
        if (_config.Pins.Any(pin => string.Equals(pin.Path, fullPath, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        _config.Pins.Add(new PinnedItemConfig
        {
            Name = string.IsNullOrWhiteSpace(name) ? MakeDisplayName(fullPath) : name.Trim(),
            Path = fullPath,
            IconPath = iconPath ?? string.Empty,
            Keywords = keywords ?? string.Empty,
            SortOrder = _config.Pins.Count == 0 ? 0 : _config.Pins.Max(pin => pin.SortOrder) + 1
        });
        return true;
    }

    private static bool HasSupportedDrop(System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return false;
        }

        return ((string[])e.Data.GetData(System.Windows.DataFormats.FileDrop)).Any(IsSupportedPinPath);
    }

    private static bool IsSupportedPinPath(string path)
    {
        if (Directory.Exists(path))
        {
            return true;
        }

        if (!File.Exists(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase);
    }

    private void SelectPinnedPath(string path)
    {
        var item = _pins.FirstOrDefault(pin => string.Equals(pin.Path, path, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return;
        }

        ItemsList.SelectedItem = item;
        ItemsList.ScrollIntoView(item);
    }

    private static string MakeDisplayName(string path)
    {
        if (Directory.Exists(path))
        {
            return new DirectoryInfo(path).Name;
        }

        return Path.GetFileNameWithoutExtension(path);
    }

    private static bool Matches(PinnedItemConfig pin, string query)
    {
        if (query.Length == 0)
        {
            return true;
        }

        return pin.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || pin.Path.Contains(query, StringComparison.OrdinalIgnoreCase)
            || pin.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void OpenSelectedOrFirst()
    {
        var item = ItemsList.SelectedItem as PinnedItem ?? _pins.FirstOrDefault();
        if (item is not null)
        {
            OpenPinnedItem(item);
        }
    }

    private void OpenPinnedItem(PinnedItem item)
    {
        try
        {
            Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true });
            MarkOpened(item.Path);
            HideLauncher();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void MarkOpened(string path)
    {
        var configItem = _config.Pins.FirstOrDefault(pin => string.Equals(pin.Path, path, StringComparison.OrdinalIgnoreCase));
        if (configItem is null)
        {
            return;
        }

        configItem.LaunchCount++;
        configItem.LastOpenedUtc = DateTime.UtcNow;
        SaveConfig();
    }

    private PinnedItemConfig? FindConfigItem(string path)
    {
        return _config.Pins.FirstOrDefault(pin => string.Equals(pin.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    private void MovePinnedItem(object sender, int direction)
    {
        if (ContextItem(sender) is not PinnedItem item)
        {
            return;
        }

        NormalizeConfig();
        var ordered = _config.Pins.OrderBy(pin => pin.SortOrder).ToList();
        var index = ordered.FindIndex(pin => string.Equals(pin.Path, item.Path, StringComparison.OrdinalIgnoreCase));
        var swapIndex = index + direction;
        if (index < 0 || swapIndex < 0 || swapIndex >= ordered.Count)
        {
            return;
        }

        (ordered[index].SortOrder, ordered[swapIndex].SortOrder) = (ordered[swapIndex].SortOrder, ordered[index].SortOrder);
        SaveConfig();
        RefreshPins();
    }

    private PinnedItem? ContextItem(object sender)
    {
        if ((sender as FrameworkElement)?.DataContext is PinnedItem direct)
        {
            return direct;
        }

        if (sender is MenuItem menuItem
            && menuItem.Parent is ContextMenu menu
            && menu.PlacementTarget is FrameworkElement target
            && target.DataContext is PinnedItem placed)
        {
            return placed;
        }

        return null;
    }

    private void ApplyViewMode()
    {
        ItemsList.ItemTemplate = (DataTemplate)FindResource(_config.ViewMode == "List" ? "ListItemTemplate" : "GridItemTemplate");
        var panel = _config.ViewMode == "List"
            ? new FrameworkElementFactory(typeof(VirtualizingStackPanel))
            : new FrameworkElementFactory(typeof(WrapPanel));
        ItemsList.ItemsPanel = new ItemsPanelTemplate(panel);
    }

    private void ApplyTheme()
    {
        var dark = IsWindowsDarkMode();
        Resources["ShellBrush"] = new SolidColorBrush(dark ? System.Windows.Media.Color.FromArgb(242, 31, 35, 40) : System.Windows.Media.Color.FromArgb(242, 245, 247, 250));
        Resources["CardBrush"] = new SolidColorBrush(dark ? System.Windows.Media.Color.FromArgb(232, 42, 47, 54) : Colors.White);
        Resources["CardHoverBrush"] = new SolidColorBrush(dark ? System.Windows.Media.Color.FromRgb(53, 59, 68) : System.Windows.Media.Color.FromRgb(244, 248, 251));
        Resources["TextBrush"] = new SolidColorBrush(dark ? System.Windows.Media.Color.FromRgb(240, 244, 248) : System.Windows.Media.Color.FromRgb(21, 26, 31));
        Resources["MutedTextBrush"] = new SolidColorBrush(dark ? System.Windows.Media.Color.FromRgb(171, 181, 194) : System.Windows.Media.Color.FromRgb(93, 102, 114));
        Resources["LineBrush"] = new SolidColorBrush(dark ? System.Windows.Media.Color.FromArgb(54, 255, 255, 255) : System.Windows.Media.Color.FromArgb(31, 17, 24, 39));
        Resources["SoftAccentBrush"] = new SolidColorBrush(dark ? System.Windows.Media.Color.FromArgb(36, 96, 165, 250) : System.Windows.Media.Color.FromArgb(26, 37, 99, 235));
    }

    private static bool IsWindowsDarkMode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    private void PositionLauncher()
    {
        if (_config.OpenNearCursor)
        {
            PositionNearCursor();
            return;
        }

        CenterOnActiveScreen();
    }

    private void PositionNearCursor()
    {
        var cursor = Forms.Cursor.Position;
        var screen = Forms.Screen.FromPoint(cursor).WorkingArea;
        Left = Math.Clamp(cursor.X - Width / 2, screen.Left, screen.Right - Width);
        Top = Math.Clamp(cursor.Y - 90, screen.Top, screen.Bottom - Height);
    }

    private void CenterOnActiveScreen()
    {
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
        Left = screen.Left + (screen.Width - Width) / 2;
        Top = screen.Top + (screen.Height - Height) / 2;
    }

    private void NormalizeConfig()
    {
        for (var i = 0; i < _config.Pins.Count; i++)
        {
            if (_config.Pins[i].SortOrder < 0 || _config.Pins.Count(pin => pin.SortOrder == _config.Pins[i].SortOrder) > 1)
            {
                for (var j = 0; j < _config.Pins.Count; j++)
                {
                    _config.Pins[j].SortOrder = j;
                }

                break;
            }
        }
    }

    private static bool IsStartupEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(StartupRegistryPath);
            return key?.GetValue(StartupRegistryName) is string value && value.Contains(GetExecutablePath(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void SetStartupEnabled(bool enabled)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(StartupRegistryPath);
        if (enabled)
        {
            key.SetValue(StartupRegistryName, $"\"{GetExecutablePath()}\"");
        }
        else
        {
            key.DeleteValue(StartupRegistryName, false);
        }
    }

    private static string GetExecutablePath()
    {
        return Environment.ProcessPath
            ?? AppContext.BaseDirectory;
    }

    private void HideLauncher()
    {
        if (_isHiding)
        {
            return;
        }

        _isHiding = true;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(110))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) =>
        {
            Hide();
            _isHiding = false;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        };
        BeginAnimation(OpacityProperty, fade);
    }

    private void AnimateShow()
    {
        _isHiding = false;
        BeginAnimation(OpacityProperty, null);
        Opacity = 0;

        if (ShellRoot.RenderTransform is ScaleTransform scale)
        {
            scale.ScaleX = 0.96;
            scale.ScaleY = 0.96;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, EaseTo(1, 180, EasingMode.EaseOut));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, EaseTo(1, 180, EasingMode.EaseOut));
        }

        BeginAnimation(OpacityProperty, EaseTo(1, 145, EasingMode.EaseOut));
    }

    private void AnimatePanel(UIElement element)
    {
        if (!_uiReady || element.Visibility != Visibility.Visible)
        {
            return;
        }

        element.BeginAnimation(OpacityProperty, null);
        element.Opacity = 0.62;
        element.BeginAnimation(OpacityProperty, EaseTo(1, 140, EasingMode.EaseOut));
    }

    private static DoubleAnimation EaseTo(double to, int milliseconds, EasingMode mode)
    {
        return new DoubleAnimation(to, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new CubicEase { EasingMode = mode }
        };
    }

    private void SetupTray()
    {
        if (_trayIcon is not null)
        {
            return;
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => Dispatcher.Invoke(ShowLauncher));
        menu.Items.Add("Settings", null, (_, _) => Dispatcher.Invoke(() =>
        {
            ShowLauncher();
            SettingsButton_Click(this, new RoutedEventArgs());
        }));
        menu.Items.Add("Start with Windows", null, (_, _) => Dispatcher.Invoke(() =>
        {
            var next = !IsStartupEnabled();
            SetStartupEnabled(next);
            StatusText.Text = next ? "Startup enabled" : "Startup disabled";
        }));
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(() =>
        {
            SaveConfig();
            Dispose();
            System.Windows.Application.Current.Shutdown();
        }));

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "Mini Launcher",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowLauncher);
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
            {
                return System.Drawing.Icon.ExtractAssociatedIcon(processPath) ?? SystemIcons.Application;
            }
        }
        catch
        {
            // Fall back to the system app icon.
        }

        return SystemIcons.Application;
    }

    private void RegisterHotkey()
    {
        var helper = new WindowInteropHelper(this);
        _source = HwndSource.FromHwnd(helper.Handle);
        _source?.AddHook(HwndHook);
        RegisterHotKey(helper.Handle, HotkeyId, ModAlt, VkSpace);
    }

    private void UnregisterHotkey()
    {
        var helper = new WindowInteropHelper(this);
        UnregisterHotKey(helper.Handle, HotkeyId);
        _source?.RemoveHook(HwndHook);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            if (IsVisible)
            {
                HideLauncher();
            }
            else
            {
                ShowLauncher();
            }

            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

public sealed class LauncherConfig
{
    public List<PinnedItemConfig> Pins { get; set; } = [];
    public string StorePath { get; set; } = string.Empty;
    public string ViewMode { get; set; } = "Grid";
    public bool AlwaysOnTop { get; set; } = true;
    public bool AutoHideOnLostFocus { get; set; } = true;
    public bool OpenNearCursor { get; set; }
}

public sealed class PinnedItemConfig
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public int LaunchCount { get; set; }
    public DateTime? LastOpenedUtc { get; set; }
    public int SortOrder { get; set; }
}

public class PinnedItem : INotifyPropertyChanged
{
    private string _name = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public string Path { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public int LaunchCount { get; set; }
    public DateTime? LastOpenedUtc { get; set; }
    public int SortOrder { get; set; }
    public string Kind => Directory.Exists(Path) ? "Folder" : "App";
    public string FavoriteBadge => IsFavorite ? "*" : string.Empty;
    public string MetaText
    {
        get
        {
            if (LaunchCount == 0)
            {
                return "Not opened yet";
            }

            return LaunchCount == 1 ? "Opened 1 time" : $"Opened {LaunchCount} times";
        }
    }

    [JsonIgnore]
    public ImageSource IconImage => IconLoader.Load(Path, IconPath);

    public static PinnedItem FromConfig(PinnedItemConfig config)
    {
        return new PinnedItem
        {
            Name = config.Name,
            Path = config.Path,
            IconPath = config.IconPath,
            Keywords = config.Keywords,
            IsFavorite = config.IsFavorite,
            LaunchCount = config.LaunchCount,
            LastOpenedUtc = config.LastOpenedUtc,
            SortOrder = config.SortOrder
        };
    }
}

public sealed class StoreItem : PinnedItem
{
    public string Category { get; set; } = "Tools";
}

public static class StoreReader
{
    public static IEnumerable<StoreItem> Load(string storePath)
    {
        var rootConfig = Path.Combine(storePath, "config.json");
        if (File.Exists(rootConfig))
        {
            foreach (var item in ReadStoreFile(rootConfig, storePath))
            {
                yield return item;
            }
        }

        foreach (var config in Directory.EnumerateFiles(storePath, "config.json", SearchOption.AllDirectories)
                     .Where(path => !string.Equals(path, rootConfig, StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var item in ReadStoreFile(config, Path.GetDirectoryName(config)!))
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<StoreItem> ReadStoreFile(string configPath, string basePath)
    {
        using var stream = File.OpenRead(configPath);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        if (root.TryGetProperty("apps", out var apps) && apps.ValueKind == JsonValueKind.Array)
        {
            foreach (var app in apps.EnumerateArray())
            {
                var item = CreateItem(app, basePath);
                if (item is not null)
                {
                    yield return item;
                }
            }

            yield break;
        }

        var single = CreateItem(root, basePath);
        if (single is not null)
        {
            yield return single;
        }
    }

    private static StoreItem? CreateItem(JsonElement element, string basePath)
    {
        var path = ReadString(element, "path");
        if (path.Length == 0)
        {
            path = ReadString(element, "target");
        }

        if (path.Length == 0)
        {
            return null;
        }

        path = ResolvePath(basePath, path);
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return null;
        }

        var icon = ReadString(element, "icon");
        if (icon.Length > 0)
        {
            icon = ResolvePath(basePath, icon);
        }

        return new StoreItem
        {
            Name = ReadString(element, "name", MainWindowDisplayName(path)),
            Path = path,
            IconPath = icon,
            Keywords = ReadString(element, "keywords"),
            Category = ReadString(element, "category", "Tools")
        };
    }

    private static string ResolvePath(string basePath, string path)
    {
        return System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.GetFullPath(System.IO.Path.Combine(basePath, path));
    }

    private static string ReadString(JsonElement element, string name, string fallback = "")
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;
    }

    private static string MainWindowDisplayName(string path)
    {
        return Directory.Exists(path)
            ? new DirectoryInfo(path).Name
            : System.IO.Path.GetFileNameWithoutExtension(path);
    }
}

public static class IconLoader
{
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;

    public static ImageSource Load(string path, string iconPath)
    {
        if (File.Exists(iconPath))
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(iconPath);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                // Fall through to shell icon.
            }
        }

        var shellIcon = LoadShellIcon(path);
        if (shellIcon is not null)
        {
            return shellIcon;
        }

        return CreateFallbackIcon(Directory.Exists(path));
    }

    private static ImageSource? LoadShellIcon(string path)
    {
        var info = new ShFileInfo();
        var attributes = Directory.Exists(path) ? FileAttributeDirectory : FileAttributeNormal;
        var handle = SHGetFileInfo(path, attributes, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiIcon | ShgfiLargeIcon);
        if (handle == IntPtr.Zero || info.IconHandle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                info.IconHandle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(48, 48));
            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
        finally
        {
            DestroyIcon(info.IconHandle);
        }
    }

    private static ImageSource CreateFallbackIcon(bool isFolder)
    {
        var group = new DrawingGroup();
        if (isFolder)
        {
            group.Children.Add(new GeometryDrawing(
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)),
                null,
                new RectangleGeometry(new Rect(5, 12, 18, 8), 3, 3)));
            group.Children.Add(new GeometryDrawing(
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 191, 36)),
                null,
                new RectangleGeometry(new Rect(4, 17, 40, 25), 5, 5)));
            var folderImage = new DrawingImage(group);
            folderImage.Freeze();
            return folderImage;
        }

        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)),
            null,
            new RectangleGeometry(new Rect(0, 0, 48, 48), 10, 10)));
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr IconHandle;
        public int IconIndex;
        public uint Attributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string path,
        uint fileAttributes,
        ref ShFileInfo fileInfo,
        uint fileInfoSize,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr icon);
}
