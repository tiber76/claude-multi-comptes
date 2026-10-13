using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace ClaudeMultiComptes.Linux;

public sealed class ClaudeProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#D97757";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? LinkedDataDirectoryPath { get; set; }
    public bool IsLinked => !string.IsNullOrWhiteSpace(LinkedDataDirectoryPath);
    public string DataDirectory => LinkedDataDirectoryPath ?? Path.Combine(ProfileStore.ProfilesDirectory, Id.ToString("D"));
}

public sealed record ExistingDataDirectory(string Path, string Title);

public static class ProfileStore
{
    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public static string ConfigRoot => Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } custom
        ? custom
        : Path.Combine(Home, ".config");
    public static string SupportDirectory => Path.Combine(ConfigRoot, "Claude Multi Comptes");
    public static string ProfilesDirectory => Path.Combine(SupportDirectory, "Profiles");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static string? LoadError { get; private set; }

    public static ObservableCollection<ClaudeProfile> Load()
    {
        Directory.CreateDirectory(ProfilesDirectory);
        var path = Path.Combine(SupportDirectory, "profiles.json");
        if (!File.Exists(path)) return [];
        try { return new(JsonSerializer.Deserialize<List<ClaudeProfile>>(File.ReadAllText(path), JsonOptions) ?? []); }
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
    private static readonly string[] Colors = ["#D97757", "#527A69", "#5574A6", "#9470A8", "#C18443", "#68737D"];
    private static readonly HttpClient Http = new();
    private readonly ObservableCollection<ClaudeProfile> _profiles = ProfileStore.Load();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly HashSet<Guid> _running = [];
    private string? _claudeExecutable;
    private string? _claudeCodeExecutable;
    private bool _busyUpdating;

    public MainWindow()
    {
        InitializeComponent();
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Claude-Multi-Comptes-Linux/0.1");
        _claudeExecutable = FindClaudeDesktop();
        _claudeCodeExecutable = FindClaudeCode();
        AddProfileButton.IsEnabled = _claudeExecutable is not null;
        CodeUpdateButton.IsEnabled = _claudeCodeExecutable is not null;
        UpdateClaudeStatus();
        RenderProfiles();
        _timer.Tick += (_, _) => RefreshRunning();
        _timer.Start();
        Opened += async (_, _) =>
        {
            if (ProfileStore.LoadError is not null)
                await ShowMessageAsync("Profils Claude", "La liste des comptes n’a pas pu être lue. Le fichier ne sera pas remplacé afin de protéger tes données.\n\n" + ProfileStore.LoadError);
            await RefreshVersionsAsync();
        };
    }

    private static string? FindClaudeDesktop()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            FindExecutable("claude-desktop"),
            "/usr/bin/claude-desktop", "/usr/local/bin/claude-desktop",
            "/opt/Claude/claude", "/opt/claude-desktop/claude",
            Path.Combine(home, ".local/bin/claude-desktop")
        };
        return candidates.Where(x => !string.IsNullOrWhiteSpace(x)).FirstOrDefault(x => File.Exists(x) && OperatingSystem.IsLinux() && (File.GetUnixFileMode(x!).HasFlag(UnixFileMode.UserExecute) || File.GetUnixFileMode(x!).HasFlag(UnixFileMode.GroupExecute) || File.GetUnixFileMode(x!).HasFlag(UnixFileMode.OtherExecute)));
    }

    private static string? FindExecutable(string name)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var path in paths)
        {
            var candidate = Path.Combine(path, name);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static string? FindClaudeCode()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var local = Path.Combine(home, ".local/bin/claude");
        return File.Exists(local) ? local : FindExecutable("claude");
    }

    private void UpdateClaudeStatus()
    {
        ClaudeStatusText.Text = _claudeExecutable is null ? "Claude absent" : "Claude prêt";
        ClaudeStatusDot.Fill = _claudeExecutable is null ? Brush.Parse("#E7A13D") : Brush.Parse("#48C774");
    }

    private void Persist()
    {
        if (ProfileStore.LoadError is not null)
        {
            _ = ShowMessageAsync("Profils Claude", "La liste des comptes n’a pas été lue correctement ; aucune modification ne sera enregistrée.");
            return;
        }
        try { ProfileStore.Save(_profiles); RenderProfiles(); }
        catch (Exception ex) { _ = ShowMessageAsync("Erreur d’enregistrement", ex.Message); }
    }

    private void RenderProfiles()
    {
        ProfileList.Children.Clear();
        if (_profiles.Count == 0)
            ProfileList.Children.Add(Card(new TextBlock { Text = "Aucun compte pour le moment. Ajoute ton premier compte Claude.", Foreground = Brush.Parse("#A2A5A8"), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center }));
        foreach (var profile in _profiles) ProfileList.Children.Add(ProfileCard(profile));
        OpenCountText.Text = $"{_running.Count} ouverts";
    }

    private static Border Card(Control content, Thickness? padding = null, Thickness? margin = null) => new()
    {
        Background = Brush.Parse("#202225"),
        BorderBrush = Brush.Parse("#2D3033"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(14),
        Padding = padding ?? new Thickness(15, 12),
        Margin = margin ?? new Thickness(0, 0, 0, 9),
        Child = content
    };

    private Control ProfileCard(ClaudeProfile profile)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("50,*,Auto,Auto") };
        var color = ParseBrush(profile.Color);
        var avatar = new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(12), Background = color, Child = new TextBlock { Text = "●", FontSize = 19, Foreground = Brushes.White, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center } };
        row.Children.Add(avatar);
        var info = new StackPanel { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Spacing = 4 };
        info.Children.Add(new TextBlock { Text = profile.Name, FontSize = 14, FontWeight = FontWeight.SemiBold });
        info.Children.Add(new TextBlock { Text = (_running.Contains(profile.Id) ? "●  Ouvert" : "●  Prêt à ouvrir") + (profile.IsLinked ? " · Profil existant" : ""), FontSize = 12, Foreground = Brush.Parse("#A2A5A8") });
        Grid.SetColumn(info, 1); row.Children.Add(info);

        var options = new Button { Content = "•••", Padding = new Thickness(11, 7), Margin = new Thickness(3, 0, 9, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        var menu = new ContextMenu();
        menu.Items.Add(Menu("Renommer…", (_, _) => _ = RenameAsync(profile)));
        menu.Items.Add(Menu(profile.IsLinked ? "Changer le profil de données…" : "Associer un profil existant…", (_, _) => _ = AttachExistingAsync(profile), _running.Contains(profile.Id)));
        if (profile.IsLinked) menu.Items.Add(Menu("Détacher le profil existant", (_, _) => _ = DetachAsync(profile), _running.Contains(profile.Id)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Menu("Retirer de la liste…", (_, _) => _ = RemoveAsync(profile), _running.Contains(profile.Id)));
        options.ContextMenu = menu;
        options.Click += (_, _) => menu.Open(options);
        Grid.SetColumn(options, 2); row.Children.Add(options);

        var launch = new Button { Content = _running.Contains(profile.Id) ? "✓  Déjà ouvert" : "Ouvrir", MinWidth = 95, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, IsEnabled = !_running.Contains(profile.Id) && _claudeExecutable is not null };
        launch.Click += (_, _) => Launch(profile);
        Grid.SetColumn(launch, 3); row.Children.Add(launch);
        return Card(row);
    }

    private static MenuItem Menu(string title, EventHandler<RoutedEventArgs> action, bool disabled = false)
    {
        var item = new MenuItem { Header = title, IsEnabled = !disabled };
        item.Click += action;
        return item;
    }

    private static SolidColorBrush ParseBrush(string color)
    {
        try { return Brush.Parse(color) as SolidColorBrush ?? new SolidColorBrush(Color.Parse(color)); }
        catch { return new SolidColorBrush(Color.Parse("#D97757")); }
    }

    private async void AddProfile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var name = await AskNameAsync("Ajouter un compte", "Nom du compte :");
        if (string.IsNullOrWhiteSpace(name)) return;
        _profiles.Add(new ClaudeProfile { Name = name.Trim(), Color = Colors[_profiles.Count % Colors.Length] });
        Persist();
    }

    private async Task RenameAsync(ClaudeProfile profile)
    {
        var name = await AskNameAsync("Renommer le compte", "Nouveau nom :", profile.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        profile.Name = name.Trim();
        Persist();
    }

    private async Task RemoveAsync(ClaudeProfile profile)
    {
        if (!await ConfirmAsync("Retirer ce compte ?", "La fiche sera retirée. Le dossier et les données Claude restent sur cet ordinateur.")) return;
        _profiles.Remove(profile);
        Persist();
    }

    private async Task DetachAsync(ClaudeProfile profile)
    {
        if (!await ConfirmAsync("Détacher le profil ?", "Les données associées restent à leur emplacement sur le disque.")) return;
        profile.LinkedDataDirectoryPath = null;
        Persist();
    }

    private async Task AttachExistingAsync(ClaudeProfile profile)
    {
        var candidates = FindExistingDataDirectories(profile);
        var dialog = new Window { Title = "Profils Claude existants", Width = 650, Height = 420, MinWidth = 520, MinHeight = 320, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush.Parse("#17191B"), Foreground = Brushes.White };
        var outer = new DockPanel { Margin = new Thickness(18) };
        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 12, 0, 0) };
        var choose = new Button { Content = "Associer le dossier", Background = Brush.Parse("#C85F45"), IsEnabled = candidates.Count > 0 };
        var browse = new Button { Content = "Parcourir…" };
        var cancel = new Button { Content = "Annuler" };
        buttons.Children.Add(choose); buttons.Children.Add(browse); buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom); outer.Children.Add(buttons);
        var list = new ListBox { Background = Brush.Parse("#202225"), Foreground = Brushes.White, BorderBrush = Brush.Parse("#2D3033") };
        foreach (var item in candidates)
            list.Items.Add(new ListBoxItem { Content = $"{item.Title}\n{item.Path}", Tag = item, Padding = new Thickness(10) });
        outer.Children.Add(list);
        dialog.Content = outer;
        choose.Click += (_, _) =>
        {
            if (list.SelectedItem is ListBoxItem { Tag: ExistingDataDirectory directory })
            {
                profile.LinkedDataDirectoryPath = directory.Path;
                Persist();
                dialog.Close(true);
            }
        };
        browse.Click += async (_, _) =>
        {
            var path = await PickFolderAsync("Choisir le dossier de données Claude");
            if (path is null) return;
            profile.LinkedDataDirectoryPath = path;
            Persist();
            dialog.Close(true);
        };
        cancel.Click += (_, _) => dialog.Close(false);
        await dialog.ShowDialog<bool?>(this);
    }

    private List<ExistingDataDirectory> FindExistingDataDirectories(ClaudeProfile current)
    {
        var root = Path.Combine(ProfileStore.ConfigRoot);
        var candidates = new List<string> { Path.Combine(root, "Claude") };
        try { candidates.AddRange(Directory.GetDirectories(root, "Claude-*")); } catch { }
        try { candidates.AddRange(Directory.GetDirectories(ProfileStore.ProfilesDirectory)); } catch { }
        if (current.IsLinked) candidates.Add(current.LinkedDataDirectoryPath!);
        var alreadyLinked = _profiles.Where(p => p.Id != current.Id && p.IsLinked).Select(p => Normalize(p.LinkedDataDirectoryPath!)).ToHashSet(StringComparer.Ordinal);
        return candidates.Where(Directory.Exists).Select(Path.GetFullPath).Distinct(StringComparer.Ordinal)
            .Where(path => HasClaudeData(path) || string.Equals(Normalize(current.LinkedDataDirectoryPath ?? ""), Normalize(path), StringComparison.Ordinal))
            .Where(path => !alreadyLinked.Contains(Normalize(path)))
            .Select(path => new ExistingDataDirectory(path, Path.GetFileName(path) == "Claude" ? "Claude classique" : Path.GetFileName(path).Replace("Claude-", "Claude · ")))
            .ToList();
    }

    private static bool HasClaudeData(string path) => new[] { "Local State", "Preferences", "Local Storage", "IndexedDB", "Partitions" }
        .Any(name => File.Exists(Path.Combine(path, name)) || Directory.Exists(Path.Combine(path, name)));

    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

    private async Task<string?> PickFolderAsync(string title)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null) return null;
        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    private void Launch(ClaudeProfile profile)
    {
        if (_claudeExecutable is null) return;
        RefreshRunning();
        if (_running.Contains(profile.Id)) return;
        try
        {
            var defaultData = IsDefaultDataDirectory(profile.DataDirectory);
            if (!defaultData) Directory.CreateDirectory(profile.DataDirectory);
            var start = new ProcessStartInfo(_claudeExecutable) { UseShellExecute = false };
            if (!defaultData) start.ArgumentList.Add("--user-data-dir=" + profile.DataDirectory);
            Process.Start(start);
            DispatcherTimer.RunOnce(RefreshRunning, TimeSpan.FromMilliseconds(900));
        }
        catch (Exception ex) { _ = ShowMessageAsync("Impossible de lancer Claude", ex.Message); }
    }

    private static bool IsDefaultDataDirectory(string path) => string.Equals(Normalize(path), Normalize(Path.Combine(ProfileStore.ConfigRoot, "Claude")), StringComparison.Ordinal);

    private void RefreshRunning()
    {
        var commandLines = GetProcessCommandLines();
        var desktop = _claudeExecutable is null ? "" : Path.GetFullPath(_claudeExecutable);
        var active = new HashSet<Guid>();
        foreach (var profile in _profiles)
        {
            var marker = "--user-data-dir=" + profile.DataDirectory;
            var running = commandLines.Any(line => line.Contains(marker, StringComparison.Ordinal))
                || (IsDefaultDataDirectory(profile.DataDirectory) && commandLines.Any(line => line.Contains("claude", StringComparison.OrdinalIgnoreCase) && (desktop.Length == 0 || line.Contains(Path.GetFileName(desktop), StringComparison.Ordinal)) && !line.Contains("--user-data-dir=", StringComparison.Ordinal)));
            if (running) active.Add(profile.Id);
        }
        if (!_running.SetEquals(active))
        {
            _running.Clear(); _running.UnionWith(active);
            RenderProfiles();
        }
    }

    private static List<string> GetProcessCommandLines()
    {
        var lines = new List<string>();
        try
        {
            foreach (var directory in Directory.EnumerateDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(directory), out _)) continue;
                try
                {
                    var args = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(directory, "cmdline"))).Split('\0', StringSplitOptions.RemoveEmptyEntries);
                    if (args.Length > 0) lines.Add(string.Join(' ', args));
                }
                catch { }
            }
        }
        catch { }
        return lines;
    }

    private async void DesktopUpdate_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("xdg-open", "https://support.anthropic.com/fr/articles/10065433-install-claude-desktop") { UseShellExecute = false }); }
        catch (Exception ex) { await ShowMessageAsync("Claude Desktop", ex.Message); }
    }

    private async void RefreshVersions_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await RefreshVersionsAsync();

    private async void CodeUpdate_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_claudeCodeExecutable is null) return;
        _busyUpdating = true; CodeUpdateButton.IsEnabled = false;
        try
        {
            var output = await RunCommandAsync(_claudeCodeExecutable, ["update"]);
            await ShowMessageAsync("Mise à jour Claude Code", string.IsNullOrWhiteSpace(output) ? "Commande de mise à jour terminée." : output);
            await RefreshVersionsAsync();
        }
        catch (Exception ex) { await ShowMessageAsync("Mise à jour Claude Code", ex.Message); }
        finally { _busyUpdating = false; CodeUpdateButton.IsEnabled = _claudeCodeExecutable is not null; }
    }

    private async Task RefreshVersionsAsync()
    {
        CodeVersionText.Text = "Claude Code installée : vérification…";
        try
        {
            if (_claudeCodeExecutable is null) throw new FileNotFoundException("Claude Code introuvable.");
            var currentText = await RunCommandAsync(_claudeCodeExecutable, ["--version"]);
            using var release = JsonDocument.Parse(await Http.GetStringAsync("https://api.github.com/repos/anthropics/claude-code/releases/latest"));
            var current = currentText.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.TrimStart('v') ?? "inconnue";
            var latest = release.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "inconnue";
            var update = Version.TryParse(latest, out var latestVersion) && Version.TryParse(current, out var currentVersion) && latestVersion > currentVersion;
            CodeVersionText.Text = $"Claude Code installée  {current}   ·   dernière publiée  {latest}   ·   {(update ? "Mise à jour disponible" : "À jour")}";
        }
        catch { CodeVersionText.Text = "Claude Code non détectée ou vérification indisponible."; }
        CodeUpdateButton.IsEnabled = _claudeCodeExecutable is not null && !_busyUpdating;
    }

    private static async Task<string> RunCommandAsync(string executable, IReadOnlyList<string> args)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("La commande n’a pas démarré.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = (await stdout) + Environment.NewLine + (await stderr);
        if (process.ExitCode != 0) throw new InvalidOperationException(output.Trim());
        return output.Trim();
    }

    private async Task<string?> AskNameAsync(string title, string label, string initial = "")
    {
        var dialog = Dialog(title, 390, 175);
        var stack = new StackPanel { Margin = new Thickness(18), Spacing = 10 };
        stack.Children.Add(new TextBlock { Text = label });
        var input = new TextBox { Text = initial };
        stack.Children.Add(input);
        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 8 };
        var ok = new Button { Content = "Valider", Background = Brush.Parse("#C85F45") };
        var cancel = new Button { Content = "Annuler" };
        buttons.Children.Add(ok); buttons.Children.Add(cancel); stack.Children.Add(buttons); dialog.Content = stack;
        ok.Click += (_, _) => dialog.Close(input.Text);
        cancel.Click += (_, _) => dialog.Close(null);
        return await dialog.ShowDialog<string?>(this);
    }

    private async Task<bool> ConfirmAsync(string title, string body) => await ShowChoiceAsync(title, body, "Continuer");

    private async Task<bool> ShowChoiceAsync(string title, string body, string confirm)
    {
        var dialog = Dialog(title, 440, 190);
        var stack = new StackPanel { Margin = new Thickness(18), Spacing = 15 };
        stack.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap });
        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 8 };
        var yes = new Button { Content = confirm, Background = Brush.Parse("#C85F45") };
        var no = new Button { Content = "Annuler" };
        buttons.Children.Add(yes); buttons.Children.Add(no); stack.Children.Add(buttons); dialog.Content = stack;
        yes.Click += (_, _) => dialog.Close(true); no.Click += (_, _) => dialog.Close(false);
        return await dialog.ShowDialog<bool>(this);
    }

    private async Task ShowMessageAsync(string title, string body)
    {
        var dialog = Dialog(title, 500, 220);
        var stack = new StackPanel { Margin = new Thickness(18), Spacing = 16 };
        stack.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap });
        var button = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Background = Brush.Parse("#C85F45") };
        button.Click += (_, _) => dialog.Close(); stack.Children.Add(button); dialog.Content = stack;
        await dialog.ShowDialog(this);
    }

    private Window Dialog(string title, double width, double height) => new()
    {
        Title = title,
        Width = width,
        Height = height,
        MinWidth = width,
        MinHeight = height,
        MaxWidth = width,
        MaxHeight = height,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Background = Brush.Parse("#202225"),
        Foreground = Brushes.White,
        CanResize = false
    };
}
