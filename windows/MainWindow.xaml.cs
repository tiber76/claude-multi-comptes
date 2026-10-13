using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ClaudeMultiComptes.Windows;

public sealed class ClaudeProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#D97757";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? LinkedDataDirectoryPath { get; set; }
    [JsonIgnore] public bool IsLinked => !string.IsNullOrWhiteSpace(LinkedDataDirectoryPath);
    [JsonIgnore] public string DataDirectory => LinkedDataDirectoryPath ?? Path.Combine(ProfileStore.ProfilesDirectory, Id.ToString("D"));
}

public sealed record ExistingDataDirectory(string Path, string Title);

public static class ProfileStore
{
    public static readonly string SupportDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude Multi Comptes");
    public static readonly string ProfilesDirectory = Path.Combine(SupportDirectory, "Profiles");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static string? LoadError { get; private set; }
    public static ObservableCollection<ClaudeProfile> Load()
    {
        Directory.CreateDirectory(ProfilesDirectory);
        var index = Path.Combine(SupportDirectory, "profiles.json");
        if (!File.Exists(index)) return [];
        try { return new(JsonSerializer.Deserialize<List<ClaudeProfile>>(File.ReadAllText(index), JsonOptions) ?? []); }
        catch (Exception ex) { LoadError = ex.Message; return []; }
    }
    public static void Save(IEnumerable<ClaudeProfile> profiles)
    {
        Directory.CreateDirectory(SupportDirectory);
        var path = Path.Combine(SupportDirectory, "profiles.json");
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(profiles, JsonOptions));
        File.Move(temp, path, true);
    }
}

public partial class MainWindow : Window
{
    private static readonly string[] ProfileColors = ["#D97757", "#527A69", "#5574A6", "#9470A8", "#C18443", "#68737D"];
    private readonly ObservableCollection<ClaudeProfile> _profiles = ProfileStore.Load();
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private string? _claudeExecutable;
    private string? _claudeCodeCommand;
    private HashSet<Guid> _running = [];
    private static readonly HttpClient Http = new();

    public MainWindow()
    {
        InitializeComponent();
        _claudeExecutable = FindClaudeExecutable();
        _claudeCodeCommand = FindClaudeCodeCommand();
        CodeUpdateButton.IsEnabled = _claudeCodeCommand is not null;
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Claude-Multi-Comptes-Windows/0.1");
        _refreshTimer.Tick += (_, _) => RefreshRunning();
        _refreshTimer.Start();
        RenderProfiles();
        UpdateClaudeStatus();
        if (ProfileStore.LoadError is not null)
            MessageBox.Show(this, "La liste des comptes n’a pas pu être lue. Le fichier n’a pas été remplacé. Détail : " + ProfileStore.LoadError, "Profils Claude", MessageBoxButton.OK, MessageBoxImage.Warning);
        _ = RefreshVersionsAsync();
    }

    private static string? FindClaudeExecutable()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new List<string>
        {
            Path.Combine(local, "Programs", "Claude", "Claude.exe"),
            Path.Combine(local, "AnthropicClaude", "Claude.exe"),
            Path.Combine(local, "AnthropicClaude", "claude.exe")
        };
        var appx = RunPowerShell("$p = Get-AppxPackage -Name Claude -ErrorAction SilentlyContinue | Sort-Object Version -Descending | Select-Object -First 1; if ($p) { $p.InstallLocation }");
        if (!string.IsNullOrWhiteSpace(appx))
            foreach (var exe in new[] { "app\\Claude.exe", "app\\claude.exe", "Claude.exe", "claude.exe" }) candidates.Add(Path.Combine(appx.Trim(), exe));
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindClaudeCodeCommand()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(user, ".local", "bin", "claude.exe"),
            Path.Combine(user, ".local", "bin", "claude"),
            Path.Combine(appData, "npm", "claude.cmd"),
            Path.Combine(appData, "npm", "claude.exe")
        };
        var found = candidates.FirstOrDefault(File.Exists);
        if (found is not null) return found;
        try
        {
            using var process = Process.Start(new ProcessStartInfo("where.exe", "claude") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
            var paths = process?.StandardOutput.ReadToEnd().Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries) ?? [];
            process?.WaitForExit(3000);
            return paths.FirstOrDefault(path => File.Exists(path) && !path.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase));
        }
        catch { return null; }
    }

    private static string RunPowerShell(string command)
    {
        try
        {
            var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(command));
            using var process = Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -EncodedCommand " + encoded) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
            if (process is null) return "";
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return output.Trim();
        }
        catch { return ""; }
    }

    private void UpdateClaudeStatus()
    {
        ClaudeStatusText.Text = _claudeExecutable is null ? "Claude absent" : "Claude prêt";
        ClaudeStatusDot.Fill = _claudeExecutable is null ? Brushes.Orange : Brushes.LimeGreen;
    }

    private void Persist()
    {
        if (ProfileStore.LoadError is not null) { MessageBox.Show(this, "La liste des comptes n’a pas été lue correctement ; aucune modification ne sera enregistrée afin de protéger tes données.", "Profils Claude", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        try { ProfileStore.Save(_profiles); RenderProfiles(); }
        catch (Exception ex) { MessageBox.Show(this, "Impossible d’enregistrer la liste des comptes : " + ex.Message, "Profils Claude", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void RenderProfiles()
    {
        ProfileList.Children.Clear();
        if (_profiles.Count == 0)
        {
            ProfileList.Children.Add(new Border { Background = (Brush)FindResource("Panel"), BorderBrush = (Brush)FindResource("PanelBorder"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(20), Child = new TextBlock { Text = "Aucun compte pour le moment. Ajoute ton premier compte Claude.", Foreground = (Brush)FindResource("Muted"), HorizontalAlignment = HorizontalAlignment.Center } });
        }
        foreach (var profile in _profiles) ProfileList.Children.Add(BuildProfileCard(profile));
        OpenCountText.Text = $"{_running.Count} ouverts";
    }

    private Border BuildProfileCard(ClaudeProfile profile)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 9) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(12), Background = BrushFor(profile.Color), Child = new TextBlock { Text = "●", FontSize = 19, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        row.Children.Add(icon);
        var details = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 0, 0, 0) };
        details.Children.Add(new TextBlock { Text = profile.Name, FontSize = 14, FontWeight = FontWeights.SemiBold });
        details.Children.Add(new TextBlock { Text = (_running.Contains(profile.Id) ? "●  Ouvert" : "●  Prêt à ouvrir") + (profile.IsLinked ? " · Profil existant" : ""), FontSize = 12, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 4, 0, 0) });
        Grid.SetColumn(details, 1); row.Children.Add(details);

        var menu = new Button { Content = "•••", Padding = new Thickness(11, 7, 11, 7), Margin = new Thickness(3, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Options du compte" };
        var context = new ContextMenu();
        context.Items.Add(MenuItem("Renommer…", (_, _) => Rename(profile)));
        context.Items.Add(MenuItem(profile.IsLinked ? "Changer le profil de données…" : "Associer un profil existant…", (_, _) => AttachExisting(profile), _running.Contains(profile.Id)));
        if (profile.IsLinked) context.Items.Add(MenuItem("Détacher le profil existant", (_, _) => { if (Confirm("Détacher le profil", "Les données associées restent sur le disque.")) { profile.LinkedDataDirectoryPath = null; Persist(); } }, _running.Contains(profile.Id)));
        context.Items.Add(new Separator());
        context.Items.Add(MenuItem("Retirer de la liste…", (_, _) => Remove(profile), _running.Contains(profile.Id)));
        menu.ContextMenu = context;
        menu.Click += (_, _) => context.IsOpen = true;
        Grid.SetColumn(menu, 2); row.Children.Add(menu);

        var launch = new Button { Content = _running.Contains(profile.Id) ? "✓  Déjà ouvert" : "Ouvrir", MinWidth = 100, VerticalAlignment = VerticalAlignment.Center, IsEnabled = !_running.Contains(profile.Id) && _claudeExecutable is not null };
        launch.Click += (_, _) => Launch(profile);
        Grid.SetColumn(launch, 3); row.Children.Add(launch);
        return new Border { Background = (Brush)FindResource("Panel"), BorderBrush = (Brush)FindResource("PanelBorder"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 11, 14, 11), Child = row };
    }

    private static MenuItem MenuItem(string title, RoutedEventHandler action, bool disabled = false)
    {
        var item = new MenuItem { Header = title, IsEnabled = !disabled };
        item.Click += action;
        return item;
    }

    private static SolidColorBrush BrushFor(string hex)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        catch { return new SolidColorBrush(Color.FromRgb(217, 119, 87)); }
    }

    private void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        var name = AskText("Ajouter un compte", "Nom du compte :");
        if (string.IsNullOrWhiteSpace(name)) return;
        _profiles.Add(new ClaudeProfile { Name = name.Trim(), Color = ProfileColors[_profiles.Count % ProfileColors.Length] });
        Persist();
    }

    private void Rename(ClaudeProfile profile)
    {
        var name = AskText("Renommer le compte", "Nouveau nom :", profile.Name);
        if (!string.IsNullOrWhiteSpace(name)) { profile.Name = name.Trim(); Persist(); }
    }

    private void Remove(ClaudeProfile profile)
    {
        if (!Confirm("Retirer ce compte ?", "La fiche sera retirée. Le dossier et les données Claude restent sur ce PC.")) return;
        _profiles.Remove(profile);
        Persist();
    }

    private void AttachExisting(ClaudeProfile profile)
    {
        var candidates = FindExistingDataDirectories(profile);
        var picker = new OpenFolderDialog { Title = "Choisir le dossier de données du compte Claude", Multiselect = false };
        if (candidates.Count > 0)
        {
            var select = new Window { Title = "Profils Claude existants", Width = 600, Height = 390, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = (Brush)FindResource("Page"), Foreground = Brushes.White };
            var panel = new DockPanel { Margin = new Thickness(18) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var choose = new Button { Content = "Associer le dossier", Background = (Brush)FindResource("Accent"), Margin = new Thickness(0, 12, 8, 0) };
            var browse = new Button { Content = "Parcourir…", Margin = new Thickness(0, 12, 8, 0) };
            var cancel = new Button { Content = "Annuler", Margin = new Thickness(0, 12, 0, 0) };
            buttons.Children.Add(choose); buttons.Children.Add(browse); buttons.Children.Add(cancel); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
            var list = new ListBox { Background = (Brush)FindResource("Panel"), Foreground = Brushes.White, BorderBrush = (Brush)FindResource("PanelBorder"), DisplayMemberPath = nameof(ExistingDataDirectory.Title), ItemsSource = candidates };
            panel.Children.Add(list); select.Content = panel;
            choose.Click += (_, _) => { if (list.SelectedItem is ExistingDataDirectory item) { profile.LinkedDataDirectoryPath = item.Path; Persist(); select.DialogResult = true; } };
            browse.Click += (_, _) => { if (picker.ShowDialog(this) == true) { profile.LinkedDataDirectoryPath = picker.FolderName; Persist(); select.DialogResult = true; } };
            cancel.Click += (_, _) => select.DialogResult = false;
            select.ShowDialog();
        }
        else if (picker.ShowDialog(this) == true) { profile.LinkedDataDirectoryPath = picker.FolderName; Persist(); }
    }

    private List<ExistingDataDirectory> FindExistingDataDirectories(ClaudeProfile profile)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new List<string> { Path.Combine(appData, "Claude") };
        try { candidates.AddRange(Directory.GetDirectories(appData, "Claude-*")); } catch { }
        try
        {
            var packages = Path.Combine(local, "Packages");
            if (Directory.Exists(packages))
                foreach (var package in Directory.GetDirectories(packages, "Claude*")) candidates.Add(Path.Combine(package, "LocalCache", "Roaming", "Claude"));
        }
        catch { }
        try { candidates.AddRange(Directory.GetDirectories(ProfileStore.ProfilesDirectory)); } catch { }
        if (profile.IsLinked) candidates.Add(profile.LinkedDataDirectoryPath!);
        var linked = new HashSet<string>(_profilesForLinks(profile).Select(p => Normalize(p.LinkedDataDirectoryPath!)), StringComparer.OrdinalIgnoreCase);
        return candidates.Where(Directory.Exists).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(path => HasClaudeData(path) || string.Equals(Normalize(profile.LinkedDataDirectoryPath ?? ""), Normalize(path), StringComparison.OrdinalIgnoreCase))
            .Where(path => !linked.Contains(Normalize(path)))
            .Select(path => new ExistingDataDirectory(path, Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)) switch { "Claude" => "Claude classique", var name when name.StartsWith("Claude-") => "Claude · " + name[7..], _ => Path.GetFileName(Path.GetDirectoryName(path)) + " · " + Path.GetFileName(path) }))
            .ToList();
    }

    private IEnumerable<ClaudeProfile> _profilesForLinks(ClaudeProfile current) => _profiles.Where(p => p.Id != current.Id && p.IsLinked);
    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
    private static bool HasClaudeData(string path) => new[] { "Local State", "Preferences", "Local Storage", "IndexedDB", "Partitions" }.Any(p => File.Exists(Path.Combine(path, p)) || Directory.Exists(Path.Combine(path, p)));

    private void Launch(ClaudeProfile profile)
    {
        if (_claudeExecutable is null) { MessageBox.Show(this, "Claude Desktop est introuvable. Installe Claude, puis relance cette application.", "Claude Multi Comptes"); return; }
        RefreshRunning();
        if (_running.Contains(profile.Id)) return;
        try
        {
            var useDefault = IsDefaultClaudeDirectory(profile.DataDirectory);
            if (!useDefault) Directory.CreateDirectory(profile.DataDirectory);
            var start = new ProcessStartInfo(_claudeExecutable) { UseShellExecute = true };
            if (!useDefault) start.ArgumentList.Add("--user-data-dir=" + profile.DataDirectory);
            Process.Start(start);
            _ = Task.Delay(900).ContinueWith(_ => Dispatcher.Invoke(RefreshRunning));
        }
        catch (Exception ex) { MessageBox.Show(this, "Impossible de lancer Claude : " + ex.Message, "Claude Multi Comptes"); }
    }

    private static bool IsDefaultClaudeDirectory(string path)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var pathName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        return string.Equals(Normalize(path), Normalize(Path.Combine(appData, "Claude")), StringComparison.OrdinalIgnoreCase)
            || (pathName == "Claude" && path.Contains(Path.Combine("Packages", "Claude"), StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshRunning()
    {
        var output = RunPowerShell("Get-CimInstance Win32_Process -Filter \"name='Claude.exe'\" -ErrorAction SilentlyContinue | ForEach-Object { $_.CommandLine }");
        var commandLines = output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        var active = new HashSet<Guid>();
        foreach (var profile in _profiles)
        {
            var marker = "--user-data-dir=" + profile.DataDirectory;
            var isRunning = commandLines.Any(line => line.Contains(marker, StringComparison.OrdinalIgnoreCase))
                || (IsDefaultClaudeDirectory(profile.DataDirectory) && commandLines.Any(line => !line.Contains("--user-data-dir=", StringComparison.OrdinalIgnoreCase)));
            if (isRunning) active.Add(profile.Id);
        }
        if (!_running.SetEquals(active)) { _running = active; RenderProfiles(); }
    }

    private void DesktopUpdate_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://claude.com/download") { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Claude Multi Comptes"); }
    }

    private async void RefreshVersions_Click(object sender, RoutedEventArgs e) => await RefreshVersionsAsync();
    private async void CodeUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_claudeCodeCommand is null) { MessageBox.Show(this, "Claude Code est introuvable. Installe-le depuis la documentation officielle.", "Claude Multi Comptes"); return; }
        CodeUpdateButton.IsEnabled = false;
        try
        {
            var result = await RunCodeCommandAsync("update");
            MessageBox.Show(this, string.IsNullOrWhiteSpace(result) ? "Commande de mise à jour terminée." : result, "Mise à jour Claude Code");
            await RefreshVersionsAsync();
        }
        catch (Exception ex) { MessageBox.Show(this, "Impossible de mettre à jour Claude Code : " + ex.Message, "Claude Multi Comptes"); }
        finally { CodeUpdateButton.IsEnabled = true; }
    }

    private async Task RefreshVersionsAsync()
    {
        CodeVersionText.Text = "Claude Code installée : vérification…";
        try
        {
            if (_claudeCodeCommand is null) throw new FileNotFoundException("Claude Code introuvable.");
            var current = await RunCodeCommandAsync("--version");
            var latestJson = await Http.GetStringAsync("https://api.github.com/repos/anthropics/claude-code/releases/latest");
            using var document = JsonDocument.Parse(latestJson);
            var latest = document.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "inconnue";
            var installed = current.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.TrimStart('v') ?? "inconnue";
            CodeVersionText.Text = $"Claude Code installée  {installed}   ·   dernière publiée  {latest}   ·   {(IsNewer(latest, installed) ? "Mise à jour disponible" : "À jour")}";
        }
        catch { CodeVersionText.Text = "Claude Code non détectée ou vérification indisponible."; }
    }

    private static bool IsNewer(string candidate, string current) => Version.TryParse(candidate, out var latest) && Version.TryParse(current, out var installed) && latest > installed;

    private static async Task<string> RunCommandAsync(string file, IEnumerable<string> args)
    {
        var start = new ProcessStartInfo(file) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("La commande ne démarre pas.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = (await stdout) + Environment.NewLine + (await stderr);
        if (process.ExitCode != 0) throw new InvalidOperationException(output.Trim());
        return output.Trim();
    }

    private Task<string> RunCodeCommandAsync(string argument)
    {
        if (_claudeCodeCommand is null) throw new FileNotFoundException("Claude Code introuvable.");
        var commandPath = _claudeCodeCommand.Contains(' ') ? "\"" + _claudeCodeCommand + "\"" : _claudeCodeCommand;
        return RunCommandAsync("cmd.exe", ["/c", "call " + commandPath + " " + argument]);
    }

    private static string? AskText(string title, string label, string? initial = null)
    {
        var dialog = new Window { Title = title, Width = 390, Height = 170, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = new SolidColorBrush(Color.FromRgb(32, 34, 37)), Foreground = Brushes.White };
        var stack = new StackPanel { Margin = new Thickness(18) };
        stack.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 8) });
        var input = new TextBox { Text = initial ?? "" }; stack.Children.Add(input);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
        var ok = new Button { Content = "Valider", IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(200, 95, 69)), Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Annuler", IsCancel = true };
        ok.Click += (_, _) => dialog.DialogResult = true; buttons.Children.Add(ok); buttons.Children.Add(cancel); stack.Children.Add(buttons); dialog.Content = stack;
        return dialog.ShowDialog() == true ? input.Text : null;
    }

    private bool Confirm(string title, string body) => MessageBox.Show(this, body, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
}
