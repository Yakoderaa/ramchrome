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
    const string CurrentVersion = "1.2.0";

    readonly Label chromeRam = new();
    readonly Label chromeProcesses = new();
    readonly Label status = new();
    readonly Button optimize = new();
    readonly Button update = new();
    readonly Label saved = new();
    readonly CheckBox autoOptimize = new();
    readonly CheckBox startWithWindows = new();
    readonly ComboBox interval = new();
    readonly NotifyIcon tray = new();
    readonly System.Windows.Forms.Timer autoTimer = new() { Interval = 120000 };
    bool optimizing;
    readonly ProgressBar progress = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 2500 };

    public MainForm()
    {
        Text = $"RAMChrome {CurrentVersion}";
        Width = 600; Height = 500;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(13,17,23);
        ForeColor = Color.FromArgb(240,246,252);

        var title = new Label { Text="RAMChrome", Font=new Font("Segoe UI",22,FontStyle.Bold), AutoSize=true, Location=new Point(28,24) };
        Controls.Add(title);
        var subtitle = new Label { Text="Optimizador ligero de memoria para Google Chrome", ForeColor=Color.FromArgb(139,148,158), AutoSize=true, Location=new Point(31,64) };
        Controls.Add(subtitle);

        AddMetric("RAM de Chrome", chromeRam, 110);
        AddMetric("Procesos de Chrome", chromeProcesses, 155);

        optimize.Text="Optimizar Chrome"; optimize.Location=new Point(30,210); optimize.Size=new Size(235,45); optimize.Click+=(_,_)=>Optimize(); Controls.Add(optimize);
        saved.Text=""; saved.AutoSize=true; saved.ForeColor=Color.FromArgb(63,185,80); saved.Location=new Point(30,265); Controls.Add(saved);
        update.Text="Buscar actualización"; update.Location=new Point(285,210); update.Size=new Size(235,45); update.Click+=async(_,_)=>await CheckForUpdateAsync(true); Controls.Add(update);

        progress.Location=new Point(30,300); progress.Size=new Size(490,18); progress.Visible=false; Controls.Add(progress);
        status.Text="Comprobando versión al iniciar…"; status.ForeColor=Color.FromArgb(139,148,158); status.AutoSize=true; status.Location=new Point(30,340); Controls.Add(status);

        timer.Tick+=(_,_)=>RefreshStats(); timer.Start(); RefreshStats();
        Shown+=async(_,_)=>await CheckForUpdateAsync(false);
    }

    void AddMetric(string label, Label value, int y)
    {
        Controls.Add(new Label { Text=label, AutoSize=true, Location=new Point(30,y), ForeColor=Color.FromArgb(139,148,158) });
        value.Text="—"; value.AutoSize=true; value.Location=new Point(285,y); value.Font=new Font("Segoe UI",11,FontStyle.Bold); Controls.Add(value);
    }

    void RefreshStats()
    {
        var processes=Process.GetProcessesByName("chrome");
        long bytes=0;
        foreach(var p in processes){ try{bytes+=p.WorkingSet64;}catch{} p.Dispose(); }
        chromeProcesses.Text=processes.Length.ToString();
        chromeRam.Text=$"{bytes/1024d/1024d/1024d:0.00} GB";
    }

    void Optimize()
    {
        optimize.Enabled = false;
        try
        {
            long before = GetChromeWorkingSet();
            int trimmed = 0;
            foreach (var p in Process.GetProcessesByName("chrome"))
            {
                try
                {
                    if (p.HasExited) continue;
                    if (EmptyWorkingSet(p.Handle)) trimmed++;
                }
                catch { }
                finally { p.Dispose(); }
            }

            Thread.Sleep(350);
            long after = GetChromeWorkingSet();
            long savedBytes = Math.Max(0, before - after);
            saved.Text = $"Liberados del conjunto de trabajo: {FormatBytes(savedBytes)} · {trimmed} procesos";
            status.Text = (isAutomatic ? "Optimización automática completada." : "Optimización completada.") + " Chrome sigue abierto y las pestañas no se cerraron.";
            RefreshStats();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "RAMChrome", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { optimize.Enabled = true; optimizing = false; }
    }

    long GetChromeWorkingSet()
    {
        long total = 0;
        foreach (var p in Process.GetProcessesByName("chrome"))
        {
            try { total += p.WorkingSet64; } catch { }
            finally { p.Dispose(); }
        }
        return total;
    }

    static string FormatBytes(long bytes)
    {
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:0} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024d / 1024d:0.0} MB";
        return $"{bytes / 1024d / 1024d / 1024d:0.00} GB";
    }

    [System.Runtime.InteropServices.DllImport("psapi.dll", SetLastError = true)]
    static extern bool EmptyWorkingSet(IntPtr hProcess);

    bool IsStartupEnabled()\n    {\n        try { using var key=Registry.CurrentUser.OpenSubKey(@"Software\\Microsoft\\Windows\\CurrentVersion\\Run",false); return key?.GetValue("RAMChrome") is string value && value.Contains("--tray",StringComparison.OrdinalIgnoreCase); } catch { return false; }\n    }\n\n    void SetStartup(bool enabled)\n    {\n        try { using var key=Registry.CurrentUser.OpenSubKey(@"Software\\Microsoft\\Windows\\CurrentVersion\\Run",true) ?? Registry.CurrentUser.CreateSubKey(@"Software\\Microsoft\\Windows\\CurrentVersion\\Run"); if(enabled) key.SetValue("RAMChrome", $"\\\"{Application.ExecutablePath}\\\" --tray"); else key.DeleteValue("RAMChrome",false); status.Text=enabled?"Se iniciará con Windows minimizado en la bandeja.":"Inicio automático desactivado."; }\n        catch(Exception ex) { MessageBox.Show("No se pudo cambiar el inicio automático: "+ex.Message,"RAMChrome",MessageBoxButtons.OK,MessageBoxIcon.Error); startWithWindows.Checked=!enabled; }\n    }\n\n    void HideToTray() { ShowInTaskbar=false; WindowState=FormWindowState.Minimized; Hide(); }\n    void RestoreFromTray() { Show(); ShowInTaskbar=true; WindowState=FormWindowState.Normal; Activate(); }\n    void ExitApplication() { tray.Visible=false; autoTimer.Stop(); timer.Stop(); Application.Exit(); }\n    protected override void OnResize(EventArgs e) { base.OnResize(e); if(WindowState==FormWindowState.Minimized) HideToTray(); }\n    protected override void OnFormClosing(FormClosingEventArgs e) { if(e.CloseReason==CloseReason.UserClosing) { e.Cancel=true; HideToTray(); } else base.OnFormClosing(e); }\n\n    async Task CheckForUpdateAsync(bool manual)
    {
        update.Enabled=false;
        try
        {
            using var client=new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RAMChrome",CurrentVersion));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            var json=await client.GetStringAsync($"https://api.github.com/repos/{RepoOwner}/{Repo}/releases/latest");
            var release=JsonSerializer.Deserialize<GitHubRelease>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
            if(release is null||string.IsNullOrWhiteSpace(release.TagName)) throw new InvalidOperationException("GitHub no devolvió una release válida.");

            var remote=Version.Parse(NormalizeVersion(release.TagName));
            var local=Version.Parse(CurrentVersion);
            if(remote<=local){status.Text=manual?"Ya tenés la última versión.":$"Versión {CurrentVersion} instalada.";return;}

            var asset=release.Assets?.FirstOrDefault(a=>a.Name.Equals("RAMChrome.zip",StringComparison.OrdinalIgnoreCase));
            var hashAsset=release.Assets?.FirstOrDefault(a=>a.Name.Equals("RAMChrome.zip.sha256",StringComparison.OrdinalIgnoreCase));
            if(asset is null||hashAsset is null) throw new InvalidOperationException("La release no contiene los archivos de actualización esperados.");

            if(MessageBox.Show($"Hay una nueva versión: {release.TagName}.\n\n¿Descargar e instalar ahora?","Actualización disponible",MessageBoxButtons.YesNo,MessageBoxIcon.Information)!=DialogResult.Yes)return;

            progress.Visible=true; progress.Style=ProgressBarStyle.Marquee; status.Text=$"Descargando {release.TagName}…";
            var zip=Path.Combine(Path.GetTempPath(),$"RAMChrome-{remote}.zip");
            await DownloadAsync(client,asset.BrowserDownloadUrl,zip);

            status.Text="Verificando integridad…";
            var expected=(await client.GetStringAsync(hashAsset.BrowserDownloadUrl)).Trim().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
            var actual=Convert.ToHexString(await SHA256.HashDataAsync(File.OpenRead(zip))).ToLowerInvariant();
            if(!string.Equals(expected,actual,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("El SHA-256 no coincide. La actualización fue cancelada.");

            var updater=Path.Combine(AppContext.BaseDirectory,"RAMChrome.Updater.exe");
            if(!File.Exists(updater))throw new FileNotFoundException("No se encontró RAMChrome.Updater.exe.");

            Process.Start(new ProcessStartInfo{FileName=updater,UseShellExecute=true,Arguments=$"--pid {Environment.ProcessId} --zip \"{zip}\" --target \"{AppContext.BaseDirectory}\""});
            status.Text="Actualización preparada. Cerrando RAMChrome…"; Application.Exit();
        }
        catch(Exception ex)
        {
            status.Text="No se pudo comprobar/instalar la actualización.";
            if(manual)MessageBox.Show(ex.Message,"RAMChrome",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
        finally{progress.Visible=false;update.Enabled=true;}
    }

    static async Task DownloadAsync(HttpClient client,string url,string path)
    {
        using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead); response.EnsureSuccessStatusCode();
        await using var input=await response.Content.ReadAsStreamAsync(); await using var output=File.Create(path); await input.CopyToAsync(output);
    }

    static string NormalizeVersion(string value)=>value.Trim().TrimStart('v','V');

    sealed class GitHubRelease{public string? TagName{get;set;}public List<Asset>? Assets{get;set;}}
    sealed class Asset{public string Name{get;set;}="";public string BrowserDownloadUrl{get;set;}="";}
}