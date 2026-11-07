using System.Diagnostics;
using fernien.Core;
using fernien.Providers.Adb;
using fernien.Transfer;
using fernien.UI;

namespace fernien;

public partial class MainForm : Form
{
    private const string AppTitle = "Fernien — Dosya Aktarımı";
    private const string ThisPcTitle = "Bu Bilgisayar";
    private const string RemoteDragFormat = "Fernien.RemotePaths";
    private const int QueueColumnProgress = 3;

    /// <summary>Kayıtlı aktarım yöntemleri. Yeni bir yöntem (Wi‑Fi vb.) buraya eklenir.</summary>
    private readonly List<IDeviceProvider> _providers = [];
    private readonly HashSet<IDeviceProvider> _activeProviders = [];
    private readonly Dictionary<string, IDeviceSession> _sessions = [];
    private readonly TransferQueue _queue = new();
    private readonly Dictionary<TransferJob, ListViewItem> _jobRows = [];
    private readonly EntryComparer _localSorter = new();
    private readonly EntryComparer _phoneSorter = new();

    private IDeviceSession? _session;
    private string _localPath = "";
    private string _phonePath = "";
    private string? _initError;
    private CancellationTokenSource? _phoneListCts;
    private int _deviceRefreshQueued;
    private bool _suppressDeviceSelection;
    private bool _closing;

    public MainForm()
    {
        InitializeComponent();
        Text = AppTitle;

        lvLocal.SmallImageList = ShellIcons.SmallImages;
        lvPhone.SmallImageList = ShellIcons.SmallImages;
        lvLocal.ListViewItemSorter = _localSorter;
        lvPhone.ListViewItemSorter = _phoneSorter;

        cmbConflict.Items.AddRange(["Yeniden adlandır", "Üzerine yaz", "Atla"]);
        cmbConflict.SelectedIndex = 0;

        _providers.Add(new AdbDeviceProvider());
        // İleride: _providers.Add(new WifiDeviceProvider());

        _queue.JobAdded += (_, job) => RunOnUi(() => AddJobRow(job));
        _queue.JobUpdated += (_, job) => RunOnUi(() => OnJobUpdated(job));

        BuildContextMenus();
    }

    private ConflictPolicy SelectedPolicy => cmbConflict.SelectedIndex switch
    {
        1 => ConflictPolicy.Overwrite,
        2 => ConflictPolicy.Skip,
        _ => ConflictPolicy.Rename
    };

    #region Yaşam döngüsü

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        NavigateLocal(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        ActiveControl = lvLocal;
        ShowPhoneOverlay("Bağlantı hazırlanıyor…");
        UpdateCommandStates();

        await InitializeProvidersAsync();
        await RefreshDevicesAsync();
    }

    private async Task InitializeProvidersAsync()
    {
        foreach (var provider in _providers.Where(p => !_activeProviders.Contains(p)))
        {
            try
            {
                SetStatus($"{provider.Name} başlatılıyor…");
                await provider.InitializeAsync();
                provider.DevicesChanged += Provider_DevicesChanged;
                _activeProviders.Add(provider);
                _initError = null;
                SetStatus("Hazır");
            }
            catch (Exception ex)
            {
                _initError = $"{provider.Name} başlatılamadı.\n\n{ex.Message}";
                SetStatus($"{provider.Name} başlatılamadı.");
            }
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        var pending = _jobRows.Keys.Where(j => !j.IsFinished).ToList();
        if (pending.Count > 0 &&
            MessageBox.Show(this, "Devam eden aktarımlar var. Çıkarsanız iptal edilecek.\n\nÇıkılsın mı?", AppTitle,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        _closing = true;
        timerUi.Stop();
        foreach (var job in pending) _queue.Cancel(job);
        _queue.Dispose();
        foreach (var provider in _providers)
        {
            provider.DevicesChanged -= Provider_DevicesChanged;
            provider.Dispose();
        }
        foreach (var session in _sessions.Values) session.Dispose();
        _sessions.Clear();
    }

    private bool RunOnUi(Action action)
    {
        if (_closing || IsDisposed || !IsHandleCreated) return false;
        try
        {
            BeginInvoke(action);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    #endregion

    #region Cihazlar

    private void Provider_DevicesChanged(object? sender, EventArgs e)
    {
        // Art arda gelen bildirimleri tek yenilemede birleştir.
        if (Interlocked.Exchange(ref _deviceRefreshQueued, 1) == 1) return;
        bool queued = RunOnUi(async () =>
        {
            await Task.Delay(250);
            Interlocked.Exchange(ref _deviceRefreshQueued, 0);
            await RefreshDevicesAsync();
        });
        if (!queued) Interlocked.Exchange(ref _deviceRefreshQueued, 0);
    }

    private async Task RefreshDevicesAsync()
    {
        var devices = new List<DeviceInfo>();
        string? error = null;
        foreach (var provider in _providers.Where(_activeProviders.Contains))
        {
            try { devices.AddRange(await provider.GetDevicesAsync()); }
            catch (Exception ex) { error = ex.Message; }
        }
        if (_closing || IsDisposed) return;

        // Artık hazır olmayan cihazların oturumlarını kapat.
        foreach (string key in _sessions.Keys.ToList())
        {
            if (devices.Any(d => d.Key == key && d.State == DeviceState.Ready)) continue;
            var session = _sessions[key];
            _sessions.Remove(key);
            if (session == _session) _session = null;
            session.Dispose();
        }

        string? previousKey = (cmbDevices.SelectedItem as DeviceItem)?.Device.Key ?? _session?.Device.Key;
        _suppressDeviceSelection = true;
        try
        {
            cmbDevices.Items.Clear();
            foreach (var device in devices) cmbDevices.Items.Add(new DeviceItem(device));

            int index = devices.FindIndex(d => d.Key == previousKey);
            if (index < 0) index = devices.FindIndex(d => d.State == DeviceState.Ready);
            if (index < 0 && devices.Count > 0) index = 0;
            cmbDevices.SelectedIndex = index;
        }
        finally
        {
            _suppressDeviceSelection = false;
        }

        if (error is not null && devices.Count == 0) SetStatus("Cihazlar alınamadı: " + error);
        await ApplySelectedDeviceAsync();
    }

    private async void cmbDevices_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressDeviceSelection) return;
        await ApplySelectedDeviceAsync();
    }

    private async Task ApplySelectedDeviceAsync()
    {
        var device = (cmbDevices.SelectedItem as DeviceItem)?.Device;
        if (device is null || device.State != DeviceState.Ready)
        {
            _session = null;
            lblStorage.Text = "";
            lblPhoneTitle.Text = "TELEFON";
            ShowPhoneOverlay(_activeProviders.Count == 0 && _initError is not null
                ? _initError
                : (device?.Provider ?? _providers[0]).GetConnectionHelp(device));
            UpdateCommandStates();
            return;
        }

        if (_session is not null && _session.Device.Key == device.Key && !lblPhoneOverlay.Visible) return;

        if (!_sessions.TryGetValue(device.Key, out var session))
        {
            session = device.Provider.OpenSession(device);
            _sessions[device.Key] = session;
        }
        _session = session;
        lblPhoneTitle.Text = $"TELEFON  ·  {device.Name}";
        BuildPhonePlaces(session);
        HidePhoneOverlay();
        UpdateCommandStates();
        SetStatus($"{device.Name} bağlandı ({device.Provider.Name}).");

        await NavigatePhoneAsync(session.HomePath);
        await UpdateStorageAsync();
    }

    private async Task UpdateStorageAsync()
    {
        var session = _session;
        if (session is null) return;
        try
        {
            var storage = await session.GetStorageInfoAsync();
            if (session != _session) return;
            lblStorage.Text = storage is null ? "" : $"Telefon: {Fmt.Size(storage.FreeBytes)} boş / {Fmt.Size(storage.TotalBytes)}";
        }
        catch
        {
            lblStorage.Text = "";
        }
    }

    private async void btnRefreshDevices_Click(object? sender, EventArgs e)
    {
        await InitializeProvidersAsync();
        await RefreshDevicesAsync();
        if (_session is not null) await RefreshPhoneAsync();
    }

    private void btnHelp_Click(object? sender, EventArgs e)
    {
        string help = string.Join("\n\n", _providers.Select(p => $"[{p.Name}]\n{p.GetConnectionHelp(null)}"));
        MessageBox.Show(this, help, "Bağlantı yardımı", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowPhoneOverlay(string text)
    {
        _phoneListCts?.Cancel();
        lblPhoneOverlay.Text = text;
        lblPhoneOverlay.Visible = true;
        lvPhone.Visible = false;
        lvPhone.Items.Clear();
        txtPhonePath.Text = "";
        txtPhonePath.Enabled = false;
        ddPhonePlaces.DropDownItems.Clear();
    }

    private void HidePhoneOverlay()
    {
        lblPhoneOverlay.Visible = false;
        lvPhone.Visible = true;
        txtPhonePath.Enabled = true;
    }

    private void UpdateCommandStates()
    {
        bool connected = _session is not null;
        foreach (ToolStripItem item in toolStripPhone.Items) item.Enabled = connected;
        btnSendToPhone.Enabled = connected;
    }

    private sealed class DeviceItem(DeviceInfo device)
    {
        public DeviceInfo Device { get; } = device;

        public override string ToString() => Device.State switch
        {
            DeviceState.Ready => $"{Device.Name}  ·  {Device.Transport}",
            DeviceState.Unauthorized => $"{Device.Name}  ·  izin bekleniyor",
            DeviceState.Offline => $"{Device.Name}  ·  çevrimdışı",
            DeviceState.Connecting => $"{Device.Name}  ·  bağlanıyor",
            _ => $"{Device.Name}  ·  bilinmiyor"
        };
    }

    #endregion

    #region Bilgisayar paneli

    private void NavigateLocal(string path)
    {
        var items = new List<ListViewItem>();
        try
        {
            if (string.IsNullOrEmpty(path))
            {
                path = "";
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    string label;
                    try { label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? ShellIcons.GetDriveTypeName(drive.Name) : drive.VolumeLabel; }
                    catch { label = "Disk"; }

                    var tag = new EntryTag($"{label} ({drive.Name.TrimEnd('\\')})", drive.Name, true, drive.TotalSize, null, IsDrive: true);
                    var item = new ListViewItem(tag.Name, ShellIcons.GetDriveIconKey(drive.Name)) { Tag = tag };
                    item.SubItems.Add($"{Fmt.Size(drive.AvailableFreeSpace)} boş");
                    item.SubItems.Add(ShellIcons.GetDriveTypeName(drive.Name));
                    item.SubItems.Add($"Toplam {Fmt.Size(drive.TotalSize)}");
                    items.Add(item);
                }
            }
            else
            {
                var directory = new DirectoryInfo(Environment.ExpandEnvironmentVariables(path));
                if (!directory.Exists) throw new DirectoryNotFoundException($"Klasör bulunamadı: {path}");
                var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
                foreach (var info in directory.EnumerateFileSystemInfos("*", options))
                {
                    bool isDirectory = info is DirectoryInfo;
                    long size = info is FileInfo file ? file.Length : 0;
                    items.Add(CreateEntryItem(new EntryTag(info.Name, info.FullName, isDirectory, size, info.LastWriteTime)));
                }
                path = directory.FullName;
            }
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            txtLocalPath.Text = DisplayLocalPath(_localPath);
            return;
        }

        _localPath = path;
        txtLocalPath.Text = DisplayLocalPath(path);
        lvLocal.BeginUpdate();
        lvLocal.Items.Clear();
        lvLocal.Items.AddRange(items.ToArray());
        lvLocal.EndUpdate();
    }

    private static string DisplayLocalPath(string path) => string.IsNullOrEmpty(path) ? ThisPcTitle : path;

    private static ListViewItem CreateEntryItem(EntryTag tag)
    {
        var item = new ListViewItem(tag.Name, tag.IsDirectory ? ShellIcons.FolderKey : ShellIcons.GetFileIconKey(tag.Name)) { Tag = tag };
        item.SubItems.Add(tag.IsDirectory ? "" : Fmt.Size(tag.Size));
        item.SubItems.Add(tag.IsDirectory ? ShellIcons.FolderTypeName : ShellIcons.GetFileTypeName(tag.Name));
        item.SubItems.Add(tag.Modified?.ToString("g") ?? "");
        return item;
    }

    private void LocalUp()
    {
        if (string.IsNullOrEmpty(_localPath)) return;
        NavigateLocal(Directory.GetParent(_localPath)?.FullName ?? "");
    }

    private void btnLocalUp_Click(object? sender, EventArgs e) => LocalUp();

    private void btnLocalRefresh_Click(object? sender, EventArgs e) => NavigateLocal(_localPath);

    private void txtLocalPath_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return;
        e.SuppressKeyPress = true;
        string text = txtLocalPath.Text.Trim().Trim('"');
        NavigateLocal(text.Equals(ThisPcTitle, StringComparison.CurrentCultureIgnoreCase) ? "" : text);
    }

    private void ddLocalPlaces_DropDownOpening(object? sender, EventArgs e)
    {
        ddLocalPlaces.DropDownItems.Clear();
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        (string Title, string Path)[] places =
        [
            ("Masaüstü", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
            ("Belgeler", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            ("İndirilenler", Path.Combine(profile, "Downloads")),
            ("Resimler", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)),
            ("Videolar", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)),
            ("Müzik", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic))
        ];
        foreach (var (title, path) in places)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) continue;
            ddLocalPlaces.DropDownItems.Add(title, ShellIcons.SmallImages.Images[ShellIcons.FolderKey], (_, _) => NavigateLocal(path));
        }
        ddLocalPlaces.DropDownItems.Add(new ToolStripSeparator());
        ddLocalPlaces.DropDownItems.Add(ThisPcTitle, null, (_, _) => NavigateLocal(""));
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            string root = drive.Name;
            ddLocalPlaces.DropDownItems.Add(root.TrimEnd('\\'), ShellIcons.SmallImages.Images[ShellIcons.GetDriveIconKey(root)], (_, _) => NavigateLocal(root));
        }
    }

    private void btnLocalNewFolder_Click(object? sender, EventArgs e) => CreateLocalFolder();

    private void CreateLocalFolder()
    {
        if (string.IsNullOrEmpty(_localPath))
        {
            ShowInfo("Önce bir klasör açın.");
            return;
        }
        string? name = InputDialog.Prompt(this, "Yeni klasör", "Klasör adı:", "Yeni klasör");
        if (name is null) return;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            ShowError("Klasör adı geçersiz karakterler içeriyor.");
            return;
        }
        string target = Path.Combine(_localPath, name);
        if (Directory.Exists(target) || File.Exists(target))
        {
            ShowError("Bu adda bir öğe zaten var.");
            return;
        }
        try
        {
            Directory.CreateDirectory(target);
            NavigateLocal(_localPath);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void lvLocal_ItemActivate(object? sender, EventArgs e)
    {
        if (lvLocal.SelectedItems.Count == 0 || lvLocal.SelectedItems[0].Tag is not EntryTag tag) return;
        if (tag.IsDirectory) NavigateLocal(tag.FullPath);
        else OpenWithShell(tag.FullPath);
    }

    private void lvLocal_ColumnClick(object? sender, ColumnClickEventArgs e)
    {
        _localSorter.Toggle(e.Column);
        lvLocal.Sort();
    }

    private void lvLocal_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.F5: NavigateLocal(_localPath); break;
            case Keys.Back: LocalUp(); break;
            case Keys.A when e.Control: SelectAll(lvLocal); break;
            default: return;
        }
        e.Handled = true;
    }

    private List<string> SelectedLocalPaths() =>
        lvLocal.SelectedItems.Cast<ListViewItem>()
            .Select(i => i.Tag).OfType<EntryTag>()
            .Where(t => !t.IsDrive)
            .Select(t => t.FullPath)
            .ToList();

    private void btnSendToPhone_Click(object? sender, EventArgs e) => SendToPhone(SelectedLocalPaths());

    #endregion

    #region Telefon paneli

    private async Task NavigatePhoneAsync(string path)
    {
        var session = _session;
        if (session is null) return;
        path = RemotePath.Normalize(path);

        _phoneListCts?.Cancel();
        var cts = new CancellationTokenSource();
        _phoneListCts = cts;
        UseWaitCursor = true;
        SetStatus($"Okunuyor: {path}");
        try
        {
            var entries = await session.ListAsync(path, cts.Token);
            if (cts.IsCancellationRequested || session != _session) return;

            _phonePath = path;
            txtPhonePath.Text = path;
            var items = entries
                .Select(entry => CreateEntryItem(new EntryTag(entry.Name, entry.FullPath, entry.IsDirectory, entry.Size,
                    entry.ModifiedUtc.Year > 1970 ? entry.ModifiedUtc.ToLocalTime() : null)))
                .ToArray();
            lvPhone.BeginUpdate();
            lvPhone.Items.Clear();
            lvPhone.Items.AddRange(items);
            lvPhone.EndUpdate();
            SetStatus($"{path}  —  {entries.Count} öğe");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (session != _session) return;
            txtPhonePath.Text = _phonePath;
            SetStatus(ex.Message);
            if (ex is not DeviceException { IsConnectionLost: true }) ShowError(ex.Message);
        }
        finally
        {
            if (cts == _phoneListCts) UseWaitCursor = false;
        }
    }

    private Task RefreshPhoneAsync() => _session is null ? Task.CompletedTask : NavigatePhoneAsync(_phonePath);

    private Task PhoneUpAsync() =>
        _session is null || _phonePath == "/" ? Task.CompletedTask : NavigatePhoneAsync(RemotePath.GetParent(_phonePath));

    private void BuildPhonePlaces(IDeviceSession session)
    {
        ddPhonePlaces.DropDownItems.Clear();
        foreach (var shortcut in session.Shortcuts)
        {
            string path = shortcut.Path;
            ddPhonePlaces.DropDownItems.Add(shortcut.Title, ShellIcons.SmallImages.Images[ShellIcons.FolderKey],
                async (_, _) => await NavigatePhoneAsync(path));
        }
    }

    private async void btnPhoneUp_Click(object? sender, EventArgs e) => await PhoneUpAsync();

    private async void btnPhoneRefresh_Click(object? sender, EventArgs e) => await RefreshPhoneAsync();

    private async void txtPhonePath_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return;
        e.SuppressKeyPress = true;
        await NavigatePhoneAsync(txtPhonePath.Text);
    }

    private async void lvPhone_ItemActivate(object? sender, EventArgs e) => await ActivatePhoneSelectionAsync();

    private async Task ActivatePhoneSelectionAsync()
    {
        if (_session is null || lvPhone.SelectedItems.Count == 0 || lvPhone.SelectedItems[0].Tag is not EntryTag tag) return;
        if (tag.IsDirectory)
        {
            await NavigatePhoneAsync(tag.FullPath);
            return;
        }

        // Dosyayı geçici klasöre indirip varsayılan uygulamayla aç.
        string tempDirectory = Path.Combine(Path.GetTempPath(), "fernien");
        Directory.CreateDirectory(tempDirectory);
        _queue.Enqueue(_session, TransferDirection.ToComputer, [tag.FullPath], tempDirectory, ConflictPolicy.Overwrite, openWhenDone: true);
    }

    private void lvPhone_ColumnClick(object? sender, ColumnClickEventArgs e)
    {
        _phoneSorter.Toggle(e.Column);
        lvPhone.Sort();
    }

    private async void lvPhone_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.F5: e.Handled = true; await RefreshPhoneAsync(); break;
            case Keys.Back: e.Handled = true; await PhoneUpAsync(); break;
            case Keys.Delete: e.Handled = true; await DeletePhoneSelectionAsync(); break;
            case Keys.F2: e.Handled = true; await RenamePhoneSelectionAsync(); break;
            case Keys.A when e.Control: e.Handled = true; SelectAll(lvPhone); break;
        }
    }

    private List<EntryTag> SelectedPhoneEntries() =>
        lvPhone.SelectedItems.Cast<ListViewItem>().Select(i => i.Tag).OfType<EntryTag>().ToList();

    private void btnReceiveToPc_Click(object? sender, EventArgs e) =>
        ReceiveToPc(SelectedPhoneEntries().Select(t => t.FullPath).ToList());

    private async void btnPhoneNewFolder_Click(object? sender, EventArgs e) => await CreatePhoneFolderAsync();

    private async void btnPhoneRename_Click(object? sender, EventArgs e) => await RenamePhoneSelectionAsync();

    private async void btnPhoneDelete_Click(object? sender, EventArgs e) => await DeletePhoneSelectionAsync();

    private async Task CreatePhoneFolderAsync()
    {
        var session = _session;
        if (session is null) return;
        string? name = InputDialog.Prompt(this, "Yeni klasör", "Telefonda oluşturulacak klasörün adı:", "Yeni klasör");
        if (name is null || !IsValidRemoteName(name)) return;
        try
        {
            string target = RemotePath.Combine(_phonePath, name);
            if (await session.StatAsync(target) is not null)
            {
                ShowError("Bu adda bir öğe zaten var.");
                return;
            }
            await session.CreateDirectoriesAsync([target]);
            await RefreshPhoneAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async Task RenamePhoneSelectionAsync()
    {
        var session = _session;
        var selected = SelectedPhoneEntries();
        if (session is null || selected.Count != 1) return;
        var entry = selected[0];

        string? name = InputDialog.Prompt(this, "Yeniden adlandır", "Yeni ad:", entry.Name);
        if (name is null || name == entry.Name || !IsValidRemoteName(name)) return;
        try
        {
            await session.RenameAsync(entry.FullPath, RemotePath.Combine(RemotePath.GetParent(entry.FullPath), name));
            await RefreshPhoneAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async Task DeletePhoneSelectionAsync()
    {
        var session = _session;
        var selected = SelectedPhoneEntries();
        if (session is null || selected.Count == 0) return;

        string what = selected.Count == 1 ? $"\"{selected[0].Name}\"" : $"{selected.Count} öğe";
        if (MessageBox.Show(this, $"{what} telefondan kalıcı olarak silinecek (geri dönüşüm kutusu yoktur).\n\nDevam edilsin mi?",
                "Silme onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        try
        {
            foreach (var entry in selected)
            {
                SetStatus($"Siliniyor: {entry.Name}");
                await session.DeleteAsync(entry.FullPath);
            }
            SetStatus($"{what} silindi.");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        await RefreshPhoneAsync();
        await UpdateStorageAsync();
    }

    private bool IsValidRemoteName(string name)
    {
        if (name.Contains('/') || name is "." or "..")
        {
            ShowError("Ad \"/\" karakteri içeremez.");
            return false;
        }
        return true;
    }

    #endregion

    #region Aktarım başlatma ve sürükle-bırak

    private void SendToPhone(IReadOnlyList<string> localPaths)
    {
        if (_session is null)
        {
            ShowInfo("Önce telefonu bağlayın.");
            return;
        }
        if (localPaths.Count == 0)
        {
            ShowInfo("Telefona göndermek için bilgisayar panelinden dosya veya klasör seçin\nya da Gezgin'den telefon paneline sürükleyin.");
            return;
        }
        var job = _queue.Enqueue(_session, TransferDirection.ToDevice, localPaths, _phonePath, SelectedPolicy);
        SetStatus($"Kuyruğa eklendi: {job.DisplayName} → {_phonePath}");
    }

    private void ReceiveToPc(IReadOnlyList<string> remotePaths)
    {
        if (_session is null)
        {
            ShowInfo("Önce telefonu bağlayın.");
            return;
        }
        if (remotePaths.Count == 0)
        {
            ShowInfo("Bilgisayara almak için telefon panelinden dosya veya klasör seçin.");
            return;
        }
        if (string.IsNullOrEmpty(_localPath))
        {
            ShowInfo("Önce bilgisayar panelinde hedef klasörü açın.");
            return;
        }
        var job = _queue.Enqueue(_session, TransferDirection.ToComputer, remotePaths, _localPath, SelectedPolicy);
        SetStatus($"Kuyruğa eklendi: {job.DisplayName} → {_localPath}");
    }

    private void lvLocal_ItemDrag(object? sender, ItemDragEventArgs e)
    {
        var paths = SelectedLocalPaths();
        if (paths.Count == 0) return;
        lvLocal.DoDragDrop(new DataObject(DataFormats.FileDrop, paths.ToArray()), DragDropEffects.Copy);
    }

    private void lvPhone_ItemDrag(object? sender, ItemDragEventArgs e)
    {
        var paths = SelectedPhoneEntries().Select(t => t.FullPath).ToList();
        if (paths.Count == 0) return;
        var data = new DataObject();
        data.SetData(RemoteDragFormat, string.Join("\n", paths));
        lvPhone.DoDragDrop(data, DragDropEffects.Copy);
    }

    private void lvPhone_DragEnter(object? sender, DragEventArgs e)
    {
        bool accept = _session is not null
                      && e.Data?.GetDataPresent(DataFormats.FileDrop) == true
                      && e.Data.GetDataPresent(RemoteDragFormat) != true;
        e.Effect = accept ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void lvPhone_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths) SendToPhone(paths);
    }

    private void lvLocal_DragEnter(object? sender, DragEventArgs e)
    {
        bool accept = _session is not null && !string.IsNullOrEmpty(_localPath) && e.Data?.GetDataPresent(RemoteDragFormat) == true;
        e.Effect = accept ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void lvLocal_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(RemoteDragFormat) is string text)
            ReceiveToPc(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    #endregion

    #region Aktarım kuyruğu

    private void AddJobRow(TransferJob job)
    {
        var row = new ListViewItem(Enumerable.Repeat("", 7).ToArray()) { Tag = job };
        _jobRows[job] = row;
        lvQueue.Items.Add(row);
        UpdateJobRow(row, job);
        row.EnsureVisible();
    }

    private void OnJobUpdated(TransferJob job)
    {
        if (_jobRows.TryGetValue(job, out var row)) UpdateJobRow(row, job);
        if (!job.IsFinished) return;

        SetStatus($"{job.DisplayName}: {StatusText(job)}");

        if (job.Status == TransferStatus.Completed && job.OpenWhenDone && job.FirstOutputPath is { } output && File.Exists(output))
        {
            OpenWithShell(output);
            return;
        }

        if (job.Direction == TransferDirection.ToDevice && job.Session == _session)
        {
            if (RemotePath.AreEqual(job.DestinationDirectory, _phonePath)) _ = RefreshPhoneAsync();
            _ = UpdateStorageAsync();
        }
        else if (job.Direction == TransferDirection.ToComputer &&
                 string.Equals(job.DestinationDirectory.TrimEnd('\\'), _localPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            NavigateLocal(_localPath);
        }

        if (WindowState == FormWindowState.Minimized || !ContainsFocus) FlashTaskbar();
    }

    private void timerUi_Tick(object? sender, EventArgs e)
    {
        double totalSpeed = 0;
        long done = 0, total = 0;
        foreach (var (job, row) in _jobRows)
        {
            if (!job.IsActive) continue;
            if (job.Status == TransferStatus.Running)
            {
                job.SampleSpeed();
                totalSpeed += job.BytesPerSecond;
                done += job.TransferredBytes;
                total += job.TotalBytes;
            }
            UpdateJobRow(row, job);
        }

        lblSpeed.Text = totalSpeed > 0 ? "  " + Fmt.Speed(totalSpeed) : "";
        string title = total > 0 ? $"%{Math.Clamp(done * 100 / total, 0, 100)} · {AppTitle}" : AppTitle;
        if (Text != title) Text = title;
    }

    private static void UpdateJobRow(ListViewItem row, TransferJob job)
    {
        SetCell(row, 0, job.DisplayName);
        SetCell(row, 1, job.Direction == TransferDirection.ToDevice ? "PC → Telefon" : "Telefon → PC");
        SetCell(row, 2, StatusText(job));
        SetCell(row, QueueColumnProgress, job.Status == TransferStatus.Queued ? "" : $"%{ProgressFraction(job) * 100:0}");
        SetCell(row, 4, job.Status switch
        {
            TransferStatus.Running => Fmt.Speed(job.BytesPerSecond),
            TransferStatus.Completed or TransferStatus.CompletedWithErrors => Fmt.Speed(job.AverageBytesPerSecond),
            _ => ""
        });
        SetCell(row, 5, job.EstimatedRemaining is { } remaining ? Fmt.Duration(remaining) : "");
        SetCell(row, 6, job.Status is TransferStatus.Queued or TransferStatus.Preparing && job.TotalBytes == 0
            ? ""
            : $"{Fmt.Size(job.TransferredBytes)} / {Fmt.Size(job.TotalBytes)}");

        string tooltip = job.Message ?? job.CurrentFile ?? string.Join("\n", job.Sources.Take(10));
        if (row.ToolTipText != tooltip) row.ToolTipText = tooltip;
    }

    private static void SetCell(ListViewItem row, int index, string text)
    {
        if (row.SubItems[index].Text != text) row.SubItems[index].Text = text;
    }

    private static double ProgressFraction(TransferJob job)
    {
        long total = job.TotalBytes;
        if (total > 0) return Math.Clamp((double)job.TransferredBytes / total, 0, 1);
        return job.Status is TransferStatus.Completed or TransferStatus.CompletedWithErrors ? 1 : 0;
    }

    private static string StatusText(TransferJob job) => job.Status switch
    {
        TransferStatus.Queued => "Sırada",
        TransferStatus.Preparing => job.CurrentFile ?? "Hazırlanıyor…",
        TransferStatus.Running => job.TotalFiles > 1
            ? $"Aktarılıyor ({job.CompletedFiles + job.FailedFiles + 1}/{job.TotalFiles})"
            : "Aktarılıyor",
        TransferStatus.Completed => job.SkippedFiles > 0
            ? $"Tamamlandı ({job.CompletedFiles} dosya, {job.SkippedFiles} atlandı)"
            : $"Tamamlandı ({job.CompletedFiles} dosya)",
        TransferStatus.CompletedWithErrors => $"{job.FailedFiles} dosya başarısız, {job.CompletedFiles} tamam",
        TransferStatus.Failed => "Hata: " + job.Message,
        TransferStatus.Canceled => "İptal edildi",
        _ => ""
    };

    private void btnCancelTransfer_Click(object? sender, EventArgs e)
    {
        var jobs = lvQueue.SelectedItems.Cast<ListViewItem>().Select(i => i.Tag).OfType<TransferJob>().ToList();
        if (jobs.Count == 0) jobs = _jobRows.Keys.Where(j => j.IsActive).ToList();
        foreach (var job in jobs) _queue.Cancel(job);
    }

    private void btnClearFinished_Click(object? sender, EventArgs e)
    {
        foreach (var (job, row) in _jobRows.Where(p => p.Key.IsFinished).ToList())
        {
            lvQueue.Items.Remove(row);
            _jobRows.Remove(job);
        }
    }

    private void lvQueue_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e) => e.DrawDefault = true;

    private void lvQueue_DrawItem(object? sender, DrawListViewItemEventArgs e)
    {
        // Hücreler DrawSubItem içinde çizilir.
    }

    private void lvQueue_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.ColumnIndex != QueueColumnProgress || e.Item?.Tag is not TransferJob job)
        {
            e.DrawDefault = true;
            return;
        }

        var graphics = e.Graphics;
        using (var background = new SolidBrush(e.Item.Selected ? SystemColors.Highlight : lvQueue.BackColor))
            graphics.FillRectangle(background, e.Bounds);

        var bar = Rectangle.Inflate(e.Bounds, -4, -3);
        if (bar.Width <= 4 || bar.Height <= 4) return;

        Color fill = job.Status switch
        {
            TransferStatus.Failed => Color.FromArgb(214, 69, 65),
            TransferStatus.CompletedWithErrors => Color.FromArgb(230, 162, 60),
            TransferStatus.Canceled => Color.FromArgb(170, 170, 170),
            _ => Color.FromArgb(6, 176, 37)
        };

        using (var track = new SolidBrush(Color.FromArgb(232, 232, 232)))
            graphics.FillRectangle(track, bar);

        int width = (int)Math.Round(bar.Width * ProgressFraction(job));
        if (width > 0)
        {
            using var brush = new SolidBrush(fill);
            graphics.FillRectangle(brush, bar.X, bar.Y, width, bar.Height);
        }

        using (var border = new Pen(Color.FromArgb(190, 190, 190)))
            graphics.DrawRectangle(border, bar.X, bar.Y, bar.Width - 1, bar.Height - 1);

        TextRenderer.DrawText(graphics, e.SubItem?.Text ?? "", lvQueue.Font, bar, Color.Black,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    #endregion

    #region Sağ tık menüleri

    private void BuildContextMenus()
    {
        var phoneMenu = new ContextMenuStrip(components);
        var phoneOpen = MenuItem("Aç", "Enter", async () => await ActivatePhoneSelectionAsync());
        var phoneReceive = MenuItem("Bilgisayara al", "", () => ReceiveToPc(SelectedPhoneEntries().Select(t => t.FullPath).ToList()));
        var phoneRename = MenuItem("Yeniden adlandır", "F2", async () => await RenamePhoneSelectionAsync());
        var phoneDelete = MenuItem("Sil", "Del", async () => await DeletePhoneSelectionAsync());
        phoneMenu.Items.AddRange(new ToolStripItem[]
        {
            phoneOpen, phoneReceive, new ToolStripSeparator(), phoneRename, phoneDelete, new ToolStripSeparator(),
            MenuItem("Yeni klasör", "", async () => await CreatePhoneFolderAsync()),
            MenuItem("Yenile", "F5", async () => await RefreshPhoneAsync())
        });
        phoneMenu.Opening += (_, e) =>
        {
            if (_session is null)
            {
                e.Cancel = true;
                return;
            }
            int count = lvPhone.SelectedItems.Count;
            phoneOpen.Enabled = count == 1;
            phoneReceive.Enabled = count > 0;
            phoneRename.Enabled = count == 1;
            phoneDelete.Enabled = count > 0;
        };
        lvPhone.ContextMenuStrip = phoneMenu;

        var localMenu = new ContextMenuStrip(components);
        var localOpen = MenuItem("Aç", "Enter", () => lvLocal_ItemActivate(lvLocal, EventArgs.Empty));
        var localSend = MenuItem("Telefona gönder", "", () => SendToPhone(SelectedLocalPaths()));
        var localReveal = MenuItem("Gezgin'de göster", "", () =>
        {
            if (lvLocal.SelectedItems.Count > 0 && lvLocal.SelectedItems[0].Tag is EntryTag tag)
                Process.Start("explorer.exe", $"/select,\"{tag.FullPath}\"");
        });
        localMenu.Items.AddRange(new ToolStripItem[]
        {
            localOpen, localSend, localReveal, new ToolStripSeparator(),
            MenuItem("Yeni klasör", "", CreateLocalFolder),
            MenuItem("Yenile", "F5", () => NavigateLocal(_localPath))
        });
        localMenu.Opening += (_, _) =>
        {
            int count = lvLocal.SelectedItems.Count;
            localOpen.Enabled = count == 1;
            localSend.Enabled = count > 0 && _session is not null;
            localReveal.Enabled = count == 1 && !string.IsNullOrEmpty(_localPath);
        };
        lvLocal.ContextMenuStrip = localMenu;
    }

    private static ToolStripMenuItem MenuItem(string text, string shortcut, Action onClick)
    {
        var item = new ToolStripMenuItem(text) { ShortcutKeyDisplayString = shortcut };
        item.Click += (_, _) => onClick();
        return item;
    }

    #endregion

    #region Yardımcılar

    private static void SelectAll(ListView listView)
    {
        listView.BeginUpdate();
        foreach (ListViewItem item in listView.Items) item.Selected = true;
        listView.EndUpdate();
    }

    private void OpenWithShell(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void SetStatus(string text) => lblStatus.Text = text;

    private void ShowError(string message) =>
        MessageBox.Show(this, message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

    private void ShowInfo(string message) =>
        MessageBox.Show(this, message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void FlashTaskbar()
    {
        try { NativeMethods.FlashWindow(Handle, true); } catch { /* önemsiz */ }
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool FlashWindow(IntPtr hwnd, bool bInvert);
    }

    #endregion
}
