// EZ Video/Music Player - smart launcher (v1.2.1)
// Sits in front of EZPlayerCore.exe as EZPlayer.exe.
//  - Videos: one at a time (a new video replaces the one playing, in the same window)
//  - Songs: added to the playlist of the player that is already open
// Options: EZPlayer.exe --options
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("EZ Video/Music Player")]
[assembly: AssemblyProduct("EZ Video/Music Player")]
[assembly: AssemblyDescription("Play Everything. See the Music. Control the Sound.")]
[assembly: AssemblyVersion("1.2.1.0")]
[assembly: AssemblyFileVersion("1.2.1.0")]

static class Program
{
    const string Version = "1.2.1";
    const string OptKey = @"Software\EZVideoMusicPlayer\Launcher";

    static readonly HashSet<string> AudioExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        ".mp3",".wav",".flac",".m4a",".aac",".ogg",".oga",".opus",".wma",".aiff",".aif",".aifc",".alac",
        ".ape",".wv",".mka",".mid",".midi",".amr",".ac3",".dts",".m4b",".mpc",".tta",".au",".caf",".mp2",".dsf",".dff"
    };

    static string Dir { get { return Path.GetDirectoryName(Application.ExecutablePath); } }
    static string Core { get { return Path.Combine(Dir, "EZPlayerCore.exe"); } }

    [STAThread]
    static int Main(string[] args)
    {
        try { HealRegistry(); } catch { }

        if (args.Length > 0 && (args[0] == "--options" || args[0] == "/options"))
        {
            Application.EnableVisualStyles();
            Application.Run(new OptionsForm());
            return 0;
        }
        if (args.Length > 0 && args[0] == "--launcher-version")
        {
            Console.WriteLine(Version);
            return 0;
        }
        if (!File.Exists(Core))
        {
            MessageBox.Show("EZPlayerCore.exe is missing. Please reinstall EZ Video/Music Player.",
                "EZ Video/Music Player", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        // Command switches (--register-associations, --enqueue, --version ...) go straight to the player.
        bool hasSwitch = args.Any(a => a.StartsWith("--"));
        if (args.Length == 0 || hasSwitch)
        {
            if (args.Length == 0) EnsureVideoMode();
            return Run(args, args.Any(a => a == "--version" || a.StartsWith("--register") || a.StartsWith("--unregister") || a == "--smoke-test" || a == "--diagnose"));
        }

        bool songsToPlaylist = GetOpt("SongsToPlaylist", true);
        var audio = new List<string>();
        var other = new List<string>();
        foreach (var a in args)
        {
            if (songsToPlaylist && AudioExt.Contains(Path.GetExtension(a))) audio.Add(a); else other.Add(a);
        }

        using (var m = new Mutex(false, "EZVideoMusicPlayerLauncher"))
        {
            try { m.WaitOne(15000); } catch (AbandonedMutexException) { }
            try
            {
                if (other.Count > 0)
                {
                    if (!CoreRunning()) EnsureVideoMode();
                    Run(other.ToArray(), false);
                    if (audio.Count > 0) WaitForPlayer();
                }
                foreach (var song in audio)
                {
                    if (CoreRunning())
                    {
                        Run(new[] { "--enqueue", song }, true);
                    }
                    else
                    {
                        // No player open yet: open it with this song so it starts playing.
                        EnsureVideoMode();
                        Run(new[] { song }, false);
                        WaitForPlayer();
                    }
                }
            }
            finally { try { m.ReleaseMutex(); } catch { } }
        }
        return 0;
    }

    static int Run(string[] args, bool wait)
    {
        var psi = new ProcessStartInfo(Core, string.Join(" ", args.Select(Quote)));
        psi.UseShellExecute = false;
        psi.WorkingDirectory = Dir;
        var p = Process.Start(psi);
        if (wait && p != null) { p.WaitForExit(20000); try { return p.ExitCode; } catch { return 0; } }
        return 0;
    }

    static string Quote(string a)
    {
        if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return a;
        var sb = new StringBuilder("\"");
        int bs = 0;
        foreach (char c in a)
        {
            if (c == '\\') { bs++; continue; }
            if (c == '"') { sb.Append('\\', bs * 2 + 1); sb.Append('"'); bs = 0; continue; }
            sb.Append('\\', bs); bs = 0; sb.Append(c);
        }
        sb.Append('\\', bs * 2); sb.Append('"');
        return sb.ToString();
    }

    static bool CoreRunning()
    {
        try
        {
            foreach (var p in Process.GetProcessesByName("EZPlayerCore"))
            {
                try { if (!p.HasExited) return true; } catch { return true; }
            }
        }
        catch { }
        return false;
    }

    static void WaitForPlayer()
    {
        // Give the player time to open its window and listen for new songs.
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 10000)
        {
            foreach (var p in Process.GetProcessesByName("EZPlayerCore"))
            {
                try { p.Refresh(); if (p.MainWindowHandle != IntPtr.Zero) { Thread.Sleep(800); return; } } catch { }
            }
            Thread.Sleep(200);
        }
    }

    // ---------- "Videos one at a time" = the player's own single-window setting ----------
    static void EnsureVideoMode()
    {
        if (CoreRunning()) return;  // never touch settings while the player is open
        bool want = GetOpt("VideosOneAtATime", true);
        foreach (var f in SettingsFiles())
        {
            try
            {
                string s = File.ReadAllText(f);
                var rx = new Regex("(\"singleInstance\"\\s*:\\s*)(true|false)");
                if (!rx.IsMatch(s)) continue;
                string now = rx.Match(s).Groups[2].Value;
                string target = want ? "true" : "false";
                if (now == target) continue;
                string n = rx.Replace(s, "${1}" + target, 1);
                File.Copy(f, f + ".before-launcher", true);
                File.WriteAllText(f, n, new UTF8Encoding(false));
            }
            catch { }
        }
    }

    static IEnumerable<string> SettingsFiles()
    {
        var roots = new List<string>();
        string env = Environment.GetEnvironmentVariable("EZPLAYER_CONFIG_DIR");
        if (!string.IsNullOrEmpty(env)) roots.Add(env);
        roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        roots.Add(Dir);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in roots)
        {
            if (string.IsNullOrEmpty(r) || !Directory.Exists(r)) continue;
            var candidates = new List<string>();
            candidates.Add(Path.Combine(r, "settings.json"));
            try
            {
                foreach (var d in Directory.GetDirectories(r))
                {
                    string name = Path.GetFileName(d).ToLowerInvariant();
                    if (name.Contains("ez") && (name.Contains("player") || name.Contains("music")))
                    {
                        candidates.Add(Path.Combine(d, "settings.json"));
                        try { foreach (var d2 in Directory.GetDirectories(d)) candidates.Add(Path.Combine(d2, "settings.json")); } catch { }
                    }
                }
            }
            catch { }
            foreach (var c in candidates)
            {
                if (File.Exists(c) && seen.Add(c))
                {
                    string t = "";
                    try { t = File.ReadAllText(c); } catch { }
                    if (t.Contains("\"singleInstance\"")) yield return c;
                }
            }
        }
    }

    // ---------- keep Windows "Open with" pointing at this launcher ----------
    static void HealRegistry()
    {
        string me = Application.ExecutablePath;
        string[] keys = {
            @"Software\Classes\EZVideoMusicPlayer.Media\shell\open\command",
            @"Software\Classes\EZVideoMusicPlayer.Media\shell\enqueue\command",
            @"Software\Classes\Applications\EZPlayer.exe\shell\open\command",
            @"Software\Classes\Applications\EZPlayer.exe\shell\enqueue\command",
        };
        foreach (var k in keys) FixCommand(k, me);
        try
        {
            using (var sfa = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SystemFileAssociations"))
            {
                if (sfa != null)
                    foreach (var ext in sfa.GetSubKeyNames())
                        FixCommand(@"Software\Classes\SystemFileAssociations\" + ext + @"\shell\EZPlayerEnqueue\command", me);
            }
        }
        catch { }
    }

    static void FixCommand(string key, string me)
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(key, true))
            {
                if (k == null) return;
                var v = k.GetValue(null) as string;
                if (v != null && v.IndexOf("EZPlayerCore.exe", StringComparison.OrdinalIgnoreCase) >= 0)
                    k.SetValue(null, Regex.Replace(v, "EZPlayerCore\\.exe", "EZPlayer.exe", RegexOptions.IgnoreCase));
            }
        }
        catch { }
    }

    public static bool GetOpt(string name, bool def)
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(OptKey))
            {
                if (k == null) return def;
                var v = k.GetValue(name);
                if (v == null) return def;
                return Convert.ToInt32(v) != 0;
            }
        }
        catch { return def; }
    }

    public static void SetOpt(string name, bool val)
    {
        using (var k = Registry.CurrentUser.CreateSubKey(OptKey))
            k.SetValue(name, val ? 1 : 0, RegistryValueKind.DWord);
    }

    // ---------- the settings window ----------
    class OptionsForm : Form
    {
        CheckBox videos, songs;
        public OptionsForm()
        {
            Text = "EZ Player - How files open";
            Font = new Font("Segoe UI", 10.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 330);
            BackColor = Color.FromArgb(24, 22, 40);
            ForeColor = Color.White;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var title = new Label { Text = "How files open", Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 200, 215), AutoSize = true, Location = new Point(24, 18) };
            var sub = new Label { Text = "When you double-click a file while EZ Player is already open:",
                AutoSize = true, Location = new Point(26, 58), ForeColor = Color.FromArgb(200, 200, 220) };

            videos = new CheckBox { Text = "VIDEOS: one at a time\n(a new video replaces the one that's playing, in the same window)",
                AutoSize = false, Size = new Size(500, 56), Location = new Point(30, 92), Checked = GetOpt("VideosOneAtATime", true) };
            songs = new CheckBox { Text = "SONGS: add to the playlist\n(the new song lines up after the others - nothing stops)",
                AutoSize = false, Size = new Size(500, 56), Location = new Point(30, 154), Checked = GetOpt("SongsToPlaylist", true) };

            var note = new Label { Text = "Video changes take effect the next time you open the player.",
                AutoSize = true, Location = new Point(30, 220), ForeColor = Color.FromArgb(170, 170, 190), Font = new Font("Segoe UI", 9f) };

            var save = new Button { Text = "Save", Size = new Size(120, 40), Location = new Point(290, 262),
                BackColor = Color.FromArgb(91, 63, 224), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            save.FlatAppearance.BorderSize = 0;
            var cancel = new Button { Text = "Cancel", Size = new Size(110, 40), Location = new Point(420, 262),
                BackColor = Color.FromArgb(50, 48, 70), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cancel.FlatAppearance.BorderSize = 0;
            save.Click += (s, e) => {
                SetOpt("VideosOneAtATime", videos.Checked);
                SetOpt("SongsToPlaylist", songs.Checked);
                EnsureVideoMode();
                MessageBox.Show("Saved.", "EZ Player", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            };
            cancel.Click += (s, e) => Close();
            AcceptButton = save; CancelButton = cancel;
            Controls.AddRange(new Control[] { title, sub, videos, songs, note, save, cancel });
        }
    }
}
