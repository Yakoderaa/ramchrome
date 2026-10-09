using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;

namespace RAMChrome;

public sealed class MainForm : Form
{
    const string RepoOwner = "Yakoderaa";
    const string Repo = "ramchrome";
    const string CurrentVersion = "1.3.0";
    readonly Label chromeRam = new(), chromeProcesses = new(), status = new(), saved = new();
    readonly Button optimize = new(), update = new(), automaticButton = new();
    readonly CheckBox startWithWindows = new();
    readonly Label automaticState = new();
    readonly ComboBox interval = new();
    readonly ProgressBar progress = new();
    readonly NotifyIcon tray = new();
    readonly System.Windows.Forms.Timer statsTimer = new() { Interval = 2500 };
    readonly System.Windows.Forms.Timer autoTimer = new() { Interval = 120000 };
    bool optimizing, allowExit, autoEnabled;
    DateTime? lastAutomaticRun;

    public MainForm()
    {
        Text = $"RAMChrome {CurrentVersion}";
        Width = 600; Height = 500; StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(13,17,23); ForeColor = Color.FromArgb(240,246,252);
        Controls.Add(new Label { Text="RAMChrome", Font=new Font("Segoe UI",22,FontStyle.Bold), AutoSize=true, Location=new Point(28,24) });
        Controls.Add(new Label { Text="Optimizador de memoria para Google Chrome", ForeColor=Color.FromArgb(139,148,158), AutoSize=true, Location=new Point(31,64) });
        AddMetric("RAM de Chrome", chromeRam, 110); AddMetric("Procesos de Chrome", chromeProcesses, 155);
        optimize.Text="Optimizar ahora"; optimize.Location=new Point(30,210); optimize.Size=new Size(235,45); optimize.Click+=(_,_)=>Optimize(false); Controls.Add(optimize);
        update.Text="Buscar actualización"; update.Location=new Point(315,210); update.Size=new Size(235,45); update.Click+=async(_,_)=>await CheckForUpdateAsync(true); Controls.Add(update);
        saved.Text=""; saved.AutoSize=true; saved.ForeColor=Color.FromArgb(63,185,80); saved.Location=new Point(30,265); Controls.Add(saved);
        automaticButton.Location=new Point(30,310); automaticButton.Size=new Size(235,40); automaticButton.Click+=(_,_)=>ToggleAutomaticMode(); Controls.Add(automaticButton);
        automaticState.AutoSize=true; automaticState.Location=new Point(285,320); automaticState.ForeColor=Color.FromArgb(63,185,80); Controls.Add(automaticState);
        interval.Location=new Point(30,360); interval.Size=new Size(150,28); interval.DropDownStyle=ComboBoxStyle.DropDownList; interval.Items.AddRange(new object[]{"1 minuto","2 minutos","5 minutos","10 minutos"});
        interval.SelectedIndex=LoadSavedInterval(); autoTimer.Interval=GetIntervalMilliseconds(); interval.SelectedIndexChanged+=(_,_)=>{autoTimer.Interval=GetIntervalMilliseconds();SaveAutoSettings();UpdateAutomaticDisplay();}; Controls.Add(interval);
        startWithWindows.Text="Iniciar con Windows y abrir minimizado en la bandeja"; startWithWindows.AutoSize=true; startWithWindows.Location=new Point(30,400);
        startWithWindows.Checked=IsStartupEnabled(); startWithWindows.CheckedChanged+=(_,_)=>SetStartup(startWithWindows.Checked); Controls.Add(startWithWindows);
        progress.Location=new Point(30,435); progress.Size=new Size(520,18); progress.Visible=false; Controls.Add(progress);
        status.Text="Preparado."; status.ForeColor=Color.FromArgb(139,148,158); status.AutoSize=true; status.Location=new Point(30,465); Controls.Add(status);
        var menu=new ContextMenuStrip(); menu.Items.Add("Abrir RAMChrome",null,(_,_)=>RestoreFromTray()); menu.Items.Add("Optimizar ahora",null,(_,_)=>Optimize(false)); menu.Items.Add("Salir",null,(_,_)=>ExitApplication());
        tray.Icon=SystemIcons.Application; tray.Text="RAMChrome"; tray.ContextMenuStrip=menu; tray.DoubleClick+=(_,_)=>RestoreFromTray(); tray.Visible=true;
        autoEnabled=LoadSavedAutoEnabled(); UpdateAutomaticDisplay(); autoTimer.Tick+=(_,_)=>Optimize(true); autoTimer.Enabled=autoEnabled;
        statsTimer.Tick+=(_,_)=>RefreshStats(); statsTimer.Start(); RefreshStats();
        Shown+=async(_,_)=>{if(Environment.GetCommandLineArgs().Contains("--tray"))HideToTray(); await CheckForUpdateAsync(false);};
    }

    void AddMetric(string label, Label value, int y) {
        Controls.Add(new Label { Text=label, AutoSize=true, Location=new Point(30,y), ForeColor=Color.FromArgb(139,148,158) });
        value.Text="—"; value.AutoSize=true; value.Location=new Point(285,y); value.Font=new Font("Segoe UI",11,FontStyle.Bold); Controls.Add(value);
    }

    void RefreshStats() {
        var processes=Process.GetProcessesByName("chrome"); long bytes=0;
        foreach(var p in processes){try{bytes+=p.WorkingSet64;}catch{}finally{p.Dispose();}}
        chromeProcesses.Text=processes.Length.ToString(); chromeRam.Text=$"{bytes/1024d/1024d/1024d:0.00} GB";
    }

    void Optimize(bool automatic) {
        if(optimizing) return;
        optimizing=true; optimize.Enabled=false;
        try {
            long before=GetChromeWorkingSet(); int trimmed=0;
            foreach(var p in Process.GetProcessesByName("chrome")) {
                try { if(!p.HasExited && EmptyWorkingSet(p.Handle)) trimmed++; }
                catch {}
                finally { p.Dispose(); }
            }
            Thread.Sleep(250); long after=GetChromeWorkingSet(); long freed=Math.Max(0,before-after);
            saved.Text=$"Liberados del conjunto de trabajo: {FormatBytes(freed)} · {trimmed} procesos";
            if(automatic) { lastAutomaticRun=DateTime.Now; status.Text="Prueba/ejecución automática completada correctamente. Chrome sigue abierto."; UpdateAutomaticDisplay(); }
            else status.Text="Optimización manual completada. Chrome sigue abierto.";
            RefreshStats();
        } catch(Exception ex) { status.Text="No se pudo completar la optimización."; if(automatic) { automaticState.Text="AUTOMÁTICO ACTIVO · error"; automaticState.ForeColor=Color.FromArgb(248,81,73); } if(!automatic)MessageBox.Show(ex.Message,"RAMChrome",MessageBoxButtons.OK,MessageBoxIcon.Error); }
        finally { optimize.Enabled=true; optimizing=false; }
    }

    void ToggleAutomaticMode() {
        autoEnabled=!autoEnabled;
        autoTimer.Enabled=autoEnabled;
        SaveAutoSettings();
        UpdateAutomaticDisplay();
        if(autoEnabled) {
            status.Text="Modo automático activado. Ejecutando una prueba ahora…";
            Optimize(true);
        } else {
            status.Text="Modo automático desactivado; no se harán optimizaciones periódicas.";
        }
    }
    int GetIntervalMilliseconds() => interval.SelectedIndex switch { 0=>60000, 1=>120000, 2=>300000, _=>600000 };
    void UpdateAutomaticDisplay() {
        automaticButton.Text=autoEnabled?"Desactivar automático":"Establecer automático";
        automaticState.ForeColor=autoEnabled?Color.FromArgb(63,185,80):Color.FromArgb(139,148,158);
        automaticState.Text=autoEnabled
            ? $"AUTOMÁTICO ACTIVO · cada {new[]{1,2,5,10}[Math.Clamp(interval.SelectedIndex,0,3)]} min"+(lastAutomaticRun.HasValue?$" · última: {lastAutomaticRun.Value:HH:mm:ss}":" · esperando primera ejecución")
            : "AUTOMÁTICO DESACTIVADO";
        tray.Text=autoEnabled?"RAMChrome · automático activo":"RAMChrome · automático desactivado";
    }
    bool LoadSavedAutoEnabled() {
        try { using var key=Registry.CurrentUser.OpenSubKey(@"Software\RAMChrome",false); return key?.GetValue("AutoOptimize") is int value ? value==1 : true; }
        catch { return true; }
    }
    int LoadSavedInterval() {
        try { using var key=Registry.CurrentUser.OpenSubKey(@"Software\RAMChrome",false); int value=Convert.ToInt32(key?.GetValue("IntervalIndex")??1); return Math.Clamp(value,0,3); }
        catch { return 1; }
    }
    void SaveAutoSettings() {
        try { using var key=Registry.CurrentUser.CreateSubKey(@"Software\RAMChrome"); key.SetValue("AutoOptimize",autoEnabled?1:0,RegistryValueKind.DWord); key.SetValue("IntervalIndex",Math.Clamp(interval.SelectedIndex,0,3),RegistryValueKind.DWord); }
        catch { status.Text="No se pudieron guardar los ajustes automáticos; se mantienen solo durante esta sesión."; }
    }

    long GetChromeWorkingSet() {
        long total=0; foreach(var p in Process.GetProcessesByName("chrome")){try{total+=p.WorkingSet64;}catch{}finally{p.Dispose();}} return total;
    }
    static string FormatBytes(long bytes) {
        if(bytes<1024*1024)return $"{bytes/1024d:0} KB";
        if(bytes<1024L*1024*1024)return $"{bytes/1024d/1024d:0.0} MB";
        return $"{bytes/1024d/1024d/1024d:0.00} GB";
    }
    [System.Runtime.InteropServices.DllImport("psapi.dll", SetLastError=true)]
    static extern bool EmptyWorkingSet(IntPtr hProcess);

    bool IsStartupEnabled() {
        try { using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",false); return key?.GetValue("RAMChrome") is string value && value.Contains("--tray",StringComparison.OrdinalIgnoreCase); } catch { return false; }
    }
    void SetStartup(bool enabled) {
        try {
            using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true) ?? Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if(enabled)key.SetValue("RAMChrome",$"\"{Application.ExecutablePath}\" --tray"); else key.DeleteValue("RAMChrome",false);
            status.Text=enabled?"Se iniciará con Windows minimizado en la bandeja.":"Inicio automático desactivado.";
        } catch(Exception ex) { MessageBox.Show("No se pudo cambiar el inicio automático: "+ex.Message,"RAMChrome",MessageBoxButtons.OK,MessageBoxIcon.Error); startWithWindows.Checked=!enabled; }
    }
    void HideToTray() {
        if(!Visible) return;
        ShowInTaskbar=false; WindowState=FormWindowState.Minimized; Hide();
        tray.BalloonTipTitle="RAMChrome sigue funcionando";
        tray.BalloonTipText=autoEnabled?"La ventana se ocultó en la bandeja. La optimización automática está activa.":"La ventana se ocultó en la bandeja. La optimización automática está desactivada.";
        tray.ShowBalloonTip(1800);
    }
    void RestoreFromTray() { Show(); ShowInTaskbar=true; WindowState=FormWindowState.Normal; Activate(); }
    void ExitApplication() { allowExit=true; tray.Visible=false; autoTimer.Stop(); statsTimer.Stop(); Close(); }
    protected override void OnResize(EventArgs e) { base.OnResize(e); if(WindowState==FormWindowState.Minimized)HideToTray(); }
    protected override void OnFormClosing(FormClosingEventArgs e) {
        if(!allowExit && e.CloseReason==CloseReason.UserClosing) { e.Cancel=true; HideToTray(); }
        else { tray.Visible=false; autoTimer.Stop(); statsTimer.Stop(); base.OnFormClosing(e); }
    }

    async Task CheckForUpdateAsync(bool manual) {
        update.Enabled=false;
        try {
            using var client=new HttpClient(); client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RAMChrome",CurrentVersion)); client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            var json=await client.GetStringAsync($"https://api.github.com/repos/{RepoOwner}/{Repo}/releases/latest");
            var release=JsonSerializer.Deserialize<GitHubRelease>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
            if(release is null||string.IsNullOrWhiteSpace(release.TagName))throw new InvalidOperationException("GitHub no devolvió una release válida.");
            var remote=Version.Parse(NormalizeVersion(release.TagName)); var local=Version.Parse(CurrentVersion);
            if(remote<=local){status.Text=manual?"Ya tenés la última versión.":$"Versión {CurrentVersion} instalada.";return;}
            var asset=release.Assets?.FirstOrDefault(a=>a.Name.Equals("RAMChrome.zip",StringComparison.OrdinalIgnoreCase));
            var hashAsset=release.Assets?.FirstOrDefault(a=>a.Name.Equals("RAMChrome.zip.sha256",StringComparison.OrdinalIgnoreCase));
            if(asset is null||hashAsset is null)throw new InvalidOperationException("La release no contiene los archivos de actualización esperados.");
            if(MessageBox.Show($"Hay una nueva versión: {release.TagName}.\n\n¿Descargar e instalar ahora?","Actualización disponible",MessageBoxButtons.YesNo,MessageBoxIcon.Information)!=DialogResult.Yes)return;
            progress.Visible=true; progress.Style=ProgressBarStyle.Marquee; status.Text=$"Descargando {release.TagName}…";
            var zip=Path.Combine(Path.GetTempPath(),$"RAMChrome-{remote}.zip"); await DownloadAsync(client,asset.BrowserDownloadUrl,zip);
            status.Text="Verificando integridad…";
            string expected=(await client.GetStringAsync(hashAsset.BrowserDownloadUrl)).Trim().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
            string actual; await using(var stream=File.OpenRead(zip))actual=Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
            if(!string.Equals(expected,actual,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("El SHA-256 no coincide. La actualización fue cancelada.");
            var updater=Path.Combine(AppContext.BaseDirectory,"RAMChrome.Updater.exe"); if(!File.Exists(updater))throw new FileNotFoundException("No se encontró RAMChrome.Updater.exe.");
            Process.Start(new ProcessStartInfo{FileName=updater,UseShellExecute=true,Arguments=$"--pid {Environment.ProcessId} --zip \"{zip}\" --target \"{AppContext.BaseDirectory}\""});
            status.Text="Actualización preparada. Cerrando RAMChrome…"; allowExit=true; Application.Exit();
        } catch(Exception ex) { status.Text="No se pudo comprobar/instalar la actualización."; if(manual)MessageBox.Show(ex.Message,"RAMChrome",MessageBoxButtons.OK,MessageBoxIcon.Error); }
        finally { progress.Visible=false; update.Enabled=true; }
    }
    static async Task DownloadAsync(HttpClient client,string url,string path) {
        using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead); response.EnsureSuccessStatusCode();
        await using var input=await response.Content.ReadAsStreamAsync(); await using var output=File.Create(path); await input.CopyToAsync(output);
    }
    static string NormalizeVersion(string value)=>value.Trim().TrimStart('v','V');
    sealed class GitHubRelease { public string? TagName{get;set;} public List<Asset>? Assets{get;set;} }
    sealed class Asset { public string Name{get;set;}=""; public string BrowserDownloadUrl{get;set;}=""; }
}