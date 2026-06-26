// ClickFX — 配置系统：数据模型、持久化、配置 UI

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

// ==================== 配置模型 ====================

class AppConfig
{
    public int Version { get; set; }
    public string LeftEffect { get; set; }
    public string RightEffect { get; set; }
    public ColorConfig LeftClick { get; set; }
    public ColorConfig RightClick { get; set; }
    public string InfoText { get; set; }
    public string InfoUrl { get; set; }
    public string RandomTexts { get; set; } // 「随机文字」效果的词库，每行一句
    public bool AutoStart { get; set; }
    public int HotkeyModifiers { get; set; }
    public int HotkeyKey { get; set; }
    public float EffectScale { get; set; }
    public string TriggerMode { get; set; }

    public AppConfig()
    {
        Version = 1;
        LeftEffect = "线条爆发";
        RightEffect = "线条爆发";
        LeftClick = new ColorConfig("#508CFF");
        RightClick = new ColorConfig("#FF6060");
        InfoText = "";
        InfoUrl = "";
        RandomTexts = "财源滚滚\n加油!\n心想事成\n好运连连\n大吉大利\n万事如意\n步步高升\n福星高照";
        AutoStart = false;
        HotkeyModifiers = 0;
        HotkeyKey = 0;
        EffectScale = 1.0f;
        TriggerMode = "Up";
    }
}

class ColorConfig
{
    public string Primary { get; set; }
    public float GlowIntensity { get; set; }
    public bool RandomColor { get; set; }

    public ColorConfig()
    {
        Primary = "#508CFF";
        GlowIntensity = 0.3f;
        RandomColor = false;
    }

    public ColorConfig(string primary) : this()
    {
        Primary = primary;
    }
}

// ==================== 配置管理 ====================

static class ConfigManager
{
    static readonly string ConfigDir = InitConfigDir();
    static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    static string InitConfigDir()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClickFX");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return dir;
    }

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return new AppConfig();
            var json = File.ReadAllText(ConfigPath, Encoding.UTF8);
            return ParseConfig(json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[ClickFX] Load config failed: " + ex.Message);
            return new AppConfig();
        }
    }

    public static void Save(AppConfig config)
    {
        try
        {
            File.WriteAllText(ConfigPath, SerializeConfig(config), Encoding.UTF8);
        }
        catch (Exception ex) { Debug.WriteLine("[ClickFX] Save config failed: " + ex.Message); }
    }

    const string AutoStartKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string AppName = "ClickFX";

    public static void SetAutoStart(bool enable)
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(AutoStartKey, true))
            {
                if (key == null) return;
                if (enable)
                {
                    key.SetValue(AppName, "\"" + Application.ExecutablePath + "\"");
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
        }
        catch (Exception ex) { Debug.WriteLine("[ClickFX] SetAutoStart failed: " + ex.Message); }
    }

    // ==================== 手动 JSON 序列化 ====================

    static string SerializeConfig(AppConfig c)
    {
        return "{\n"
            + "  \"Version\": " + c.Version + ",\n"
            + "  \"LeftEffect\": " + Quote(c.LeftEffect) + ",\n"
            + "  \"RightEffect\": " + Quote(c.RightEffect) + ",\n"
            + "  \"LeftClick\": " + SerializeColor(c.LeftClick) + ",\n"
            + "  \"RightClick\": " + SerializeColor(c.RightClick) + ",\n"
            + "  \"InfoText\": " + Quote(c.InfoText ?? "") + ",\n"
            + "  \"InfoUrl\": " + Quote(c.InfoUrl ?? "") + ",\n"
            + "  \"RandomTexts\": " + Quote(c.RandomTexts ?? "") + ",\n"
            + "  \"AutoStart\": " + (c.AutoStart ? "true" : "false") + ",\n"
            + "  \"HotkeyModifiers\": " + c.HotkeyModifiers + ",\n"
            + "  \"HotkeyKey\": " + c.HotkeyKey + ",\n"
            + "  \"EffectScale\": " + c.EffectScale.ToString("0.0###",
                System.Globalization.CultureInfo.InvariantCulture) + ",\n"
            + "  \"TriggerMode\": " + Quote(c.TriggerMode ?? "Up") + "\n"
            + "}";
    }

    static string SerializeColor(ColorConfig c)
    {
        return "{ \"Primary\": " + Quote(c.Primary)
            + ", \"GlowIntensity\": " + c.GlowIntensity.ToString("0.0####",
                System.Globalization.CultureInfo.InvariantCulture)
            + ", \"RandomColor\": " + (c.RandomColor ? "true" : "false") + " }";
    }

    static string Quote(string s)
    {
        return "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
    }

    // ==================== 手动 JSON 解析 ====================

    static AppConfig ParseConfig(string json)
    {
        var c = new AppConfig();
        var d = ParseJsonToDict(json);

        int ver;
        if (d.ContainsKey("Version") && int.TryParse(d["Version"], out ver))
            c.Version = ver;

        // 版本迁移：按版本号依次升级配置结构
        // if (ver < 2) { /* v1 → v2 迁移逻辑 */ }
        // if (ver < 3) { /* v2 → v3 迁移逻辑 */ }
        if (d.ContainsKey("LeftEffect"))
            c.LeftEffect = d["LeftEffect"];
        if (d.ContainsKey("RightEffect"))
            c.RightEffect = d["RightEffect"];
        if (d.ContainsKey("LeftClick"))
            c.LeftClick = ParseColor(d["LeftClick"]);
        if (d.ContainsKey("RightClick"))
            c.RightClick = ParseColor(d["RightClick"]);
        if (d.ContainsKey("InfoText"))
            c.InfoText = d["InfoText"];
        if (d.ContainsKey("InfoUrl"))
            c.InfoUrl = d["InfoUrl"];
        if (d.ContainsKey("RandomTexts"))
            c.RandomTexts = d["RandomTexts"];
        if (d.ContainsKey("AutoStart"))
            c.AutoStart = d["AutoStart"].ToLower() == "true";
        int n;
        if (d.ContainsKey("HotkeyModifiers") && int.TryParse(d["HotkeyModifiers"], out n))
            c.HotkeyModifiers = n;
        if (d.ContainsKey("HotkeyKey") && int.TryParse(d["HotkeyKey"], out n))
            c.HotkeyKey = n;
        float f;
        if (d.ContainsKey("EffectScale") && float.TryParse(d["EffectScale"],
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out f))
            c.EffectScale = Math.Max(0.3f, Math.Min(3.0f, f));
        if (d.ContainsKey("TriggerMode"))
            c.TriggerMode = d["TriggerMode"];

        return c;
    }

    static ColorConfig ParseColor(string json)
    {
        var c = new ColorConfig();
        var d = ParseJsonToDict(json);
        if (d.ContainsKey("Primary")) c.Primary = d["Primary"];
        float f;
        if (d.ContainsKey("GlowIntensity") && float.TryParse(d["GlowIntensity"],
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out f))
            c.GlowIntensity = Math.Max(0f, Math.Min(1f, f));
        if (d.ContainsKey("RandomColor"))
            c.RandomColor = d["RandomColor"].ToLower() == "true";
        return c;
    }

    static Dictionary<string, string> ParseJsonToDict(string json)
    {
        var dict = new Dictionary<string, string>();
        json = json.Trim();
        if (json.StartsWith("{")) json = json.Substring(1);
        if (json.EndsWith("}")) json = json.Substring(0, json.Length - 1);

        int i = 0;
        while (i < json.Length)
        {
            // skip whitespace
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length) break;

            // read key
            var key = ReadJsonString(json, ref i);
            if (key == null) break;

            // skip :
            while (i < json.Length && (json[i] == ':' || char.IsWhiteSpace(json[i]))) i++;

            // read value
            string value;
            if (i < json.Length && json[i] == '"')
            {
                value = ReadJsonString(json, ref i);
            }
            else if (i < json.Length && json[i] == '{')
            {
                // 嵌套对象：跟踪花括号深度
                int depth = 0;
                int start = i;
                while (i < json.Length)
                {
                    if (json[i] == '{') depth++;
                    else if (json[i] == '}') { depth--; if (depth == 0) { i++; break; } }
                    else if (json[i] == '"') { i++; while (i < json.Length && json[i] != '"') { if (json[i] == '\\' && i + 1 < json.Length) i += 2; else i++; } }
                    i++;
                }
                value = json.Substring(start, i - start).Trim();
            }
            else
            {
                int start = i;
                while (i < json.Length && json[i] != ',' && json[i] != '}') i++;
                value = json.Substring(start, i - start).Trim();
            }

            dict[key] = value ?? "";

            // skip comma
            while (i < json.Length && (json[i] == ',' || char.IsWhiteSpace(json[i]))) i++;
        }
        return dict;
    }

    static string ReadJsonString(string json, ref int i)
    {
        if (i >= json.Length || json[i] != '"') return null;
        i++; // skip opening "
        var sb = new StringBuilder();
        while (i < json.Length)
        {
            if (json[i] == '\\' && i + 1 < json.Length)
            {
                char c = json[i + 1];
                switch (c)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    default: sb.Append(c); break;
                }
                i += 2;
            }
            else if (json[i] == '"')
            {
                i++; // skip closing "
                return sb.ToString();
            }
            else
            {
                sb.Append(json[i]);
                i++;
            }
        }
        return sb.ToString();
    }
}

// ==================== 配置 UI ====================

class ConfigForm : Form
{
    public AppConfig Result { get; private set; }
    public Action<AppConfig> OnPreview; // 实时预览回调

    ComboBox _leftEffectCombo, _rightEffectCombo, _triggerModeCombo;
    Panel _leftPreview, _rightPreview;
    TextBox _leftHex, _rightHex;
    TextBox _randomTextBox;
    Label _randomLabel;
    Button _leftPick, _rightPick;
    CheckBox _leftRandomBtn, _rightRandomBtn;
    LinkLabel _projectLink;

    CheckBox _autoStartCheckBox;
    Label _scaleValueLabel;
    float _currentScale = 1.0f;
    Label _hotkeyDisplayLabel;
    Button _hotkeySetBtn, _hotkeyClearBtn;

    AppConfig _originalConfig;
    int _hotkeyModifiers, _hotkeyKey;
    bool _loading = true; // LoadValues 期间抑制预览回调

    public ConfigForm(AppConfig config)
    {
        _originalConfig = config;
        Result = config;
        InitUI();
        LoadValues(config);
        _loading = false;
        UpdateRandomTextVisibility();
    }

    const string RandomTextEffectName = "随机文字";
    const int RandomBlockH = 84;     // 「随机文字」标签 + 文本框占用的垂直高度
    const int FormShownHeight = 414; // 显示文本框时的窗口高度（= InitUI 的 ClientSize 高）
    bool _randomShown = true;        // 布局初始按"显示"构建
    Dictionary<Control, int> _belowTops; // "显示"布局下，文本框下方各控件的原始 Top

    // 仅当左键或右键选了「随机文字」效果时显示词库文本框；隐藏时把下方控件整体上移
    // 并收缩窗口高度，避免留下空白，显示时再还原。
    // 用记录的原始 Top 做绝对定位（而非相对增减），重复切换也不会累积漂移。
    void UpdateRandomTextVisibility()
    {
        bool show = (_leftEffectCombo.SelectedItem as string) == RandomTextEffectName
                 || (_rightEffectCombo.SelectedItem as string) == RandomTextEffectName;
        if (show == _randomShown) return;

        int off = show ? 0 : RandomBlockH; // 隐藏时下方控件整体上移 RandomBlockH
        SuspendLayout();
        foreach (var kv in _belowTops)
            kv.Key.Top = kv.Value - off;
        _randomLabel.Visible = show;
        _randomTextBox.Visible = show;
        ClientSize = new Size(ClientSize.Width, FormShownHeight - off);
        _randomShown = show;
        ResumeLayout();
    }

    // 从当前 UI 状态构建 AppConfig（不触发副作用）
    AppConfig BuildCurrentConfig()
    {
        var c = new AppConfig();
        c.LeftEffect = (_leftEffectCombo.SelectedItem as string) ?? "线条爆发";
        c.RightEffect = (_rightEffectCombo.SelectedItem as string) ?? "线条爆发";
        c.LeftClick = new ColorConfig(NormalizeHexValue(_leftHex.Text));
        c.LeftClick.GlowIntensity = _originalConfig.LeftClick.GlowIntensity;
        c.LeftClick.RandomColor = _leftRandomBtn.Checked;
        c.RightClick = new ColorConfig(NormalizeHexValue(_rightHex.Text));
        c.RightClick.GlowIntensity = _originalConfig.RightClick.GlowIntensity;
        c.RightClick.RandomColor = _rightRandomBtn.Checked;
        c.InfoText = _originalConfig.InfoText;
        c.InfoUrl = _originalConfig.InfoUrl;
        c.RandomTexts = (_randomTextBox.Text ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
        c.AutoStart = _autoStartCheckBox.Checked;
        c.HotkeyModifiers = _hotkeyModifiers;
        c.HotkeyKey = _hotkeyKey;
        c.EffectScale = _currentScale;
        c.TriggerMode = (_triggerModeCombo.SelectedIndex == 1) ? "Down" : "Up";
        return c;
    }

    void NotifyPreview()
    {
        if (_loading) return;
        if (OnPreview != null) OnPreview(BuildCurrentConfig());
    }

    void InitUI()
    {
        Text = "ClickFX 设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(344, 414);

        int y = 15;

        // 左键：标签 + 动效下拉 + 颜色选择
        Controls.Add(new Label { Text = "左键", Location = new Point(12, y + 4), AutoSize = true });
        _leftEffectCombo = new ComboBox
        {
            Location = new Point(48, y),
            Size = new Size(115, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _leftEffectCombo.Items.AddRange(EffectRegistry.GetAllNames().ToArray());
        _leftEffectCombo.SelectedIndexChanged += (s, e) => { NotifyPreview(); UpdateRandomTextVisibility(); };
        Controls.Add(_leftEffectCombo);
        _leftPreview = new Panel { Location = new Point(168, y), Size = new Size(20, 20), BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(_leftPreview);
        _leftHex = new TextBox { Location = new Point(194, y), Size = new Size(55, 25) };
        Controls.Add(_leftHex);
        _leftPick = new Button { Text = "选色", Location = new Point(250, y - 1), Size = new Size(40, 25) };
        _leftPick.Click += (s, e) => PickColor(_leftHex, _leftPreview);
        Controls.Add(_leftPick);
        _leftRandomBtn = new CheckBox { Text = "随机", Location = new Point(292, y - 1), Size = new Size(40, 25), Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter };
        _leftRandomBtn.CheckedChanged += (s, e) => ToggleRandom(_leftRandomBtn.Checked, _leftHex, _leftPick, _leftPreview);
        Controls.Add(_leftRandomBtn);
        y += 32;

        // 右键：标签 + 动效下拉 + 颜色选择
        Controls.Add(new Label { Text = "右键", Location = new Point(12, y + 4), AutoSize = true });
        _rightEffectCombo = new ComboBox
        {
            Location = new Point(48, y),
            Size = new Size(115, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _rightEffectCombo.Items.AddRange(EffectRegistry.GetAllNames().ToArray());
        _rightEffectCombo.SelectedIndexChanged += (s, e) => { NotifyPreview(); UpdateRandomTextVisibility(); };
        Controls.Add(_rightEffectCombo);
        _rightPreview = new Panel { Location = new Point(168, y), Size = new Size(20, 20), BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(_rightPreview);
        _rightHex = new TextBox { Location = new Point(194, y), Size = new Size(55, 25) };
        Controls.Add(_rightHex);
        _rightPick = new Button { Text = "选色", Location = new Point(250, y - 1), Size = new Size(40, 25) };
        _rightPick.Click += (s, e) => PickColor(_rightHex, _rightPreview);
        Controls.Add(_rightPick);
        _rightRandomBtn = new CheckBox { Text = "随机", Location = new Point(292, y - 1), Size = new Size(40, 25), Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter };
        _rightRandomBtn.CheckedChanged += (s, e) => ToggleRandom(_rightRandomBtn.Checked, _rightHex, _rightPick, _rightPreview);
        Controls.Add(_rightRandomBtn);
        y += 35;

        // HEX 输入变化时更新预览
        _leftHex.TextChanged += (s, e) => { UpdatePreview(_leftHex, _leftPreview); NotifyPreview(); };
        _rightHex.TextChanged += (s, e) => { UpdatePreview(_rightHex, _rightPreview); NotifyPreview(); };
        _leftHex.Leave += (s, e) => NormalizeHex(_leftHex);
        _rightHex.Leave += (s, e) => NormalizeHex(_rightHex);

        // 随机文字词库（仅当左/右键选择了「随机文字」效果时显示，每行一句，随机抽取弹出）
        _randomLabel = new Label { Text = "随机文字(每行一句)：", Location = new Point(12, y), AutoSize = true };
        Controls.Add(_randomLabel);
        y += 20;
        _randomTextBox = new TextBox
        {
            Multiline = true,            // 必须先于 Size 设置，否则高度会被单行模式钳成一行
            ScrollBars = ScrollBars.Vertical,
            WordWrap = false,
            AcceptsReturn = true,
            Location = new Point(12, y),
            Size = new Size(320, 56),
        };
        _randomTextBox.TextChanged += (s, e) => NotifyPreview();
        Controls.Add(_randomTextBox);
        y += 64;

        // 效果大小（+/− 按钮）
        Controls.Add(new Label { Text = "效果大小：", Location = new Point(12, y + 3), AutoSize = true });
        _scaleValueLabel = new Label
        {
            Text = "1.0x",
            Location = new Point(78, y + 3),
            AutoSize = true,
        };
        Controls.Add(_scaleValueLabel);
        var scaleMinusBtn = new Button { Text = "↓", Location = new Point(118, y - 3), Size = new Size(24, 24), TextAlign = ContentAlignment.MiddleCenter };
        scaleMinusBtn.Click += (s, e) => { _currentScale = Math.Max(0.3f, (float)Math.Round(_currentScale - 0.1f, 1)); _scaleValueLabel.Text = _currentScale.ToString("0.0") + "x"; NotifyPreview(); };
        Controls.Add(scaleMinusBtn);
        var scalePlusBtn = new Button { Text = "↑", Location = new Point(146, y - 3), Size = new Size(24, 24), TextAlign = ContentAlignment.MiddleCenter };
        scalePlusBtn.Click += (s, e) => { _currentScale = Math.Min(3.0f, (float)Math.Round(_currentScale + 0.1f, 1)); _scaleValueLabel.Text = _currentScale.ToString("0.0") + "x"; NotifyPreview(); };
        Controls.Add(scalePlusBtn);
        var resetBtn = new Button { Text = "恢复默认", Location = new Point(262, y - 3), Size = new Size(70, 24) };
        resetBtn.Click += (s, e) => ResetEffects();
        Controls.Add(resetBtn);
        y += 32;

        // 触发时机
        Controls.Add(new Label { Text = "触发时机：", Location = new Point(12, y + 3), AutoSize = true });
        _triggerModeCombo = new ComboBox
        {
            Location = new Point(80, y),
            Size = new Size(100, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _triggerModeCombo.Items.AddRange(new object[] { "抬起时", "按下时" });
        _triggerModeCombo.SelectedIndexChanged += (s, e) => NotifyPreview();
        Controls.Add(_triggerModeCombo);
        y += 32;

        // 显示快捷键
        Controls.Add(new Label { Text = "快捷键：", Location = new Point(12, y + 3), AutoSize = true });
        y += 24;
        Controls.Add(new Label { Text = "启用/暂停", Location = new Point(12, y + 3), AutoSize = true });
        _hotkeyDisplayLabel = new Label
        {
            Text = HotkeyToString(_hotkeyModifiers, _hotkeyKey),
            Location = new Point(80, y + 1),
            Size = new Size(148, 18),
            AutoSize = false,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = SystemColors.ControlLight,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(_hotkeyDisplayLabel);
        _hotkeySetBtn = new Button { Text = "设置", Location = new Point(232, y - 1), Size = new Size(50, 24) };
        _hotkeySetBtn.Click += (s, e) => SetHotkey();
        Controls.Add(_hotkeySetBtn);
        _hotkeyClearBtn = new Button { Text = "清除", Location = new Point(286, y - 1), Size = new Size(46, 24) };
        _hotkeyClearBtn.Click += (s, e) => { _hotkeyModifiers = 0; _hotkeyKey = 0; _hotkeyDisplayLabel.Text = ""; NotifyPreview(); };
        Controls.Add(_hotkeyClearBtn);
        y += 30;

        // 开机自启动
        _autoStartCheckBox = new CheckBox
        {
            Text = "开机自启动",
            Location = new Point(12, y),
            AutoSize = true
        };
        Controls.Add(_autoStartCheckBox);
        y += 28;

        // GitHub 链接
        _projectLink = new LinkLabel
        {
            Text = "GitHub: https://github.com/yuui226/ClickFX",
            Location = new Point(12, y),
            Size = new Size(310, 18),
            AutoSize = false
        };
        _projectLink.LinkClicked += (s, e) =>
            System.Diagnostics.Process.Start("https://github.com/yuui226/ClickFX");
        Controls.Add(_projectLink);
        y += 20;

        var donateLabel = new Label
        {
            Text = "欢迎在 GitHub 给项目点个 Star",
            Location = new Point(12, y),
            Size = new Size(310, 18),
            ForeColor = SystemColors.GrayText
        };
        Controls.Add(donateLabel);
        y += 28;

        // 确定（右下角）
        var okBtn = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(248, y), Size = new Size(80, 28) };
        okBtn.Click += (s, e) => ApplyValues();
        Controls.Add(okBtn);

        AcceptButton = okBtn;

        // 记录文本框下方所有控件的原始 Top（布局即按"显示"构建），供按需显隐时精确还原
        _belowTops = new Dictionary<Control, int>();
        foreach (Control c in Controls)
            if (c != _randomLabel && c != _randomTextBox && c.Top > _randomTextBox.Top)
                _belowTops[c] = c.Top;
    }

    // 恢复默认：仅重置动效、颜色、大小，不影响快捷键和开机自启动
    void ResetEffects()
    {
        var defaults = new AppConfig();
        if (_leftEffectCombo.Items.Contains(defaults.LeftEffect))
            _leftEffectCombo.SelectedItem = defaults.LeftEffect;
        else if (_leftEffectCombo.Items.Count > 0)
            _leftEffectCombo.SelectedIndex = 0;
        if (_rightEffectCombo.Items.Contains(defaults.RightEffect))
            _rightEffectCombo.SelectedItem = defaults.RightEffect;
        else if (_rightEffectCombo.Items.Count > 0)
            _rightEffectCombo.SelectedIndex = 0;
        _leftHex.Text = defaults.LeftClick.Primary;
        _rightHex.Text = defaults.RightClick.Primary;
        _leftRandomBtn.Checked = false;
        _rightRandomBtn.Checked = false;
        _currentScale = defaults.EffectScale;
        _scaleValueLabel.Text = _currentScale.ToString("0.0") + "x";
        _triggerModeCombo.SelectedIndex = 0;
    }

    void SetHotkey()
    {
        using (var dlg = new HotkeyDialog())
        {
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _hotkeyModifiers = dlg.Modifiers;
                _hotkeyKey = dlg.Key;
                _hotkeyDisplayLabel.Text = HotkeyToString(_hotkeyModifiers, _hotkeyKey);
            }
        }
    }

    internal static string HotkeyToString(int mods, int key)
    {
        if (mods == 0 && key == 0) return "";
        var parts = new List<string>();
        if ((mods & 0x0002) != 0) parts.Add("Ctrl");
        if ((mods & 0x0001) != 0) parts.Add("Alt");
        if ((mods & 0x0004) != 0) parts.Add("Shift");
        parts.Add(((Keys)key).ToString());
        return string.Join("+", parts);
    }

    void LoadValues(AppConfig config)
    {
        if (_leftEffectCombo.Items.Contains(config.LeftEffect))
            _leftEffectCombo.SelectedItem = config.LeftEffect;
        else if (_leftEffectCombo.Items.Count > 0)
            _leftEffectCombo.SelectedIndex = 0;

        if (_rightEffectCombo.Items.Contains(config.RightEffect))
            _rightEffectCombo.SelectedItem = config.RightEffect;
        else if (_rightEffectCombo.Items.Count > 0)
            _rightEffectCombo.SelectedIndex = 0;

        _leftHex.Text = config.LeftClick.Primary;
        _rightHex.Text = config.RightClick.Primary;
        // 文本框只认 \r\n 作为换行；配置里存的是 \n，加载时统一转成 \r\n 否则会挤成一行
        _randomTextBox.Text = (config.RandomTexts ?? "")
            .Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        _leftRandomBtn.Checked = config.LeftClick.RandomColor;
        _rightRandomBtn.Checked = config.RightClick.RandomColor;

        _autoStartCheckBox.Checked = config.AutoStart;
        _currentScale = Math.Max(0.3f, Math.Min(3.0f, config.EffectScale));
        _scaleValueLabel.Text = _currentScale.ToString("0.0") + "x";
        _hotkeyModifiers = config.HotkeyModifiers;
        _hotkeyKey = config.HotkeyKey;
        _hotkeyDisplayLabel.Text = HotkeyToString(_hotkeyModifiers, _hotkeyKey);
        _triggerModeCombo.SelectedIndex = (config.TriggerMode == "Down") ? 1 : 0;
    }

    void ApplyValues()
    {
        Result = BuildCurrentConfig();
        // 同步开机自启动注册表
        ConfigManager.SetAutoStart(Result.AutoStart);
    }

    void ToggleRandom(bool random, TextBox hexBox, Button pickBtn, Panel preview)
    {
        hexBox.Enabled = !random;
        pickBtn.Enabled = !random;
        preview.BackColor = random ? SystemColors.Control : (ParseHexColor(hexBox.Text) ?? SystemColors.Control);
        NotifyPreview();
    }

    void PickColor(TextBox hexBox, Panel preview)
    {
        using (var dlg = new ColorDialog { FullOpen = true })
        {
            var current = ParseHexColor(hexBox.Text);
            if (current.HasValue) dlg.Color = current.Value;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                hexBox.Text = ColorToHex(dlg.Color);
            }
        }
    }

    void UpdatePreview(TextBox hexBox, Panel preview)
    {
        var c = ParseHexColor(hexBox.Text);
        preview.BackColor = c ?? SystemColors.Control;
    }

    void NormalizeHex(TextBox hexBox)
    {
        hexBox.Text = NormalizeHexValue(hexBox.Text);
    }

    static string NormalizeHexValue(string hex)
    {
        hex = (hex ?? "").Trim();
        if (!hex.StartsWith("#")) hex = "#" + hex;
        if (hex.Length == 4) // #RGB -> #RRGGBB
            hex = "#" + hex[1] + hex[1] + hex[2] + hex[2] + hex[3] + hex[3];
        hex = hex.ToUpper();
        if (hex.Length != 7) return "#000000";
        // 验证 # 后的字符是否为合法十六进制
        for (int i = 1; i < 7; i++)
        {
            char c = hex[i];
            if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
                return "#000000";
        }
        return hex;
    }

    static Color? ParseHexColor(string hex)
    {
        try
        {
            hex = NormalizeHexValue(hex);
            return ColorTranslator.FromHtml(hex);
        }
        catch
        {
            return null;
        }
    }

    static string ColorToHex(Color c)
    {
        return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
    }
}

// ==================== 快捷键设置对话框 ====================

class HotkeyDialog : Form
{
    public int Modifiers { get; private set; }
    public int Key { get; private set; }
    Label _hintLabel;
    bool _captured;

    public HotkeyDialog()
    {
        Text = "设置快捷键";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(260, 100);
        KeyPreview = true;

        _hintLabel = new Label
        {
            Text = "请按下快捷键组合...\n（至少包含一个修饰键：Ctrl/Alt/Shift）",
            Location = new Point(12, 12),
            Size = new Size(236, 40),
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(_hintLabel);

        var cancelBtn = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(170, 66), Size = new Size(76, 26) };
        Controls.Add(cancelBtn);
        CancelButton = cancelBtn;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        e.SuppressKeyPress = true;

        if (!e.Control && !e.Alt && !e.Shift) return;

        // 只记录非修饰键，不立即关闭
        if (e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.Menu
            && e.KeyCode != Keys.ShiftKey && e.KeyCode != Keys.LWin
            && e.KeyCode != Keys.RWin)
        {
            int mods = 0;
            if (e.Alt) mods |= 0x0001;
            if (e.Control) mods |= 0x0002;
            if (e.Shift) mods |= 0x0004;

            Modifiers = mods;
            Key = (int)e.KeyCode;
            _captured = true;

            _hintLabel.Text = ConfigForm.HotkeyToString(mods, (int)e.KeyCode)
                + "\n松开按键确认...";
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        // 所有键松开后才确认
        if (_captured && !e.Control && !e.Alt && !e.Shift)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
