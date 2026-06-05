// ClickFX — 配置系统：数据模型、持久化、配置 UI

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

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

    public AppConfig()
    {
        Version = 1;
        LeftEffect = "线条爆发";
        RightEffect = "线条爆发";
        LeftClick = new ColorConfig("#508CFF");
        RightClick = new ColorConfig("#FF6060");
        InfoText = "";
        InfoUrl = "";
    }
}

class ColorConfig
{
    public string Primary { get; set; }
    public float GlowIntensity { get; set; }

    public ColorConfig()
    {
        Primary = "#508CFF";
        GlowIntensity = 0.3f;
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
        catch
        {
            return new AppConfig();
        }
    }

    public static void Save(AppConfig config)
    {
        try
        {
            File.WriteAllText(ConfigPath, SerializeConfig(config), Encoding.UTF8);
        }
        catch { }
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
            + "  \"InfoUrl\": " + Quote(c.InfoUrl ?? "") + "\n"
            + "}";
    }

    static string SerializeColor(ColorConfig c)
    {
        return "{ \"Primary\": " + Quote(c.Primary)
            + ", \"GlowIntensity\": " + c.GlowIntensity.ToString("0.0####",
                System.Globalization.CultureInfo.InvariantCulture) + " }";
    }

    static string Quote(string s)
    {
        return "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\n", "\\n").Replace("\r", "\\r") + "\"";
    }

    // ==================== 手动 JSON 解析 ====================

    static AppConfig ParseConfig(string json)
    {
        var c = new AppConfig();
        var d = ParseJsonToDict(json);

        int ver;
        if (d.ContainsKey("Version") && int.TryParse(d["Version"], out ver))
            c.Version = ver;
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
            c.GlowIntensity = f;
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
                    else if (json[i] == '"') { i++; while (i < json.Length && json[i] != '"') { if (json[i] == '\\') i++; i++; } }
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

    ComboBox _leftEffectCombo, _rightEffectCombo;
    Panel _leftPreview, _rightPreview;
    TextBox _leftHex, _rightHex;
    Button _leftPick, _rightPick;
    Label _infoLabel;
    LinkLabel _projectLink;

    AppConfig _originalConfig;

    public ConfigForm(AppConfig config)
    {
        _originalConfig = config;
        Result = config;
        InitUI();
        LoadValues(config);
    }

    void InitUI()
    {
        Text = "ClickFX 设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(380, 380);

        int y = 15;

        // 左键动效
        Controls.Add(new Label { Text = "左键动效：", Location = new Point(15, y + 3), AutoSize = true });
        _leftEffectCombo = new ComboBox
        {
            Location = new Point(90, y),
            Size = new Size(180, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _leftEffectCombo.Items.AddRange(EffectRegistry.GetAllNames().ToArray());
        Controls.Add(_leftEffectCombo);
        y += 35;

        // 右键动效
        Controls.Add(new Label { Text = "右键动效：", Location = new Point(15, y + 3), AutoSize = true });
        _rightEffectCombo = new ComboBox
        {
            Location = new Point(90, y),
            Size = new Size(180, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _rightEffectCombo.Items.AddRange(EffectRegistry.GetAllNames().ToArray());
        Controls.Add(_rightEffectCombo);
        y += 40;

        // 左键颜色
        Controls.Add(new Label { Text = "左键颜色：", Location = new Point(15, y + 3), AutoSize = true });
        _leftPreview = new Panel { Location = new Point(90, y), Size = new Size(28, 22), BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(_leftPreview);
        _leftHex = new TextBox { Location = new Point(125, y), Size = new Size(100, 22) };
        Controls.Add(_leftHex);
        _leftPick = new Button { Text = "选择", Location = new Point(232, y - 1), Size = new Size(60, 24) };
        _leftPick.Click += (s, e) => PickColor(_leftHex, _leftPreview);
        Controls.Add(_leftPick);
        y += 38;

        // 右键颜色
        Controls.Add(new Label { Text = "右键颜色：", Location = new Point(15, y + 3), AutoSize = true });
        _rightPreview = new Panel { Location = new Point(90, y), Size = new Size(28, 22), BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(_rightPreview);
        _rightHex = new TextBox { Location = new Point(125, y), Size = new Size(100, 22) };
        Controls.Add(_rightHex);
        _rightPick = new Button { Text = "选择", Location = new Point(232, y - 1), Size = new Size(60, 24) };
        _rightPick.Click += (s, e) => PickColor(_rightHex, _rightPreview);
        Controls.Add(_rightPick);
        y += 38;

        // HEX 输入变化时更新预览
        _leftHex.TextChanged += (s, e) => UpdatePreview(_leftHex, _leftPreview);
        _rightHex.TextChanged += (s, e) => UpdatePreview(_rightHex, _rightPreview);
        _leftHex.Leave += (s, e) => NormalizeHex(_leftHex);
        _rightHex.Leave += (s, e) => NormalizeHex(_rightHex);

        // 恢复默认
        var resetBtn = new Button { Text = "恢复默认", Location = new Point(15, y), Size = new Size(80, 28) };
        resetBtn.Click += (s, e) => LoadValues(new AppConfig());
        Controls.Add(resetBtn);
        y += 40;

        // 关于信息
        Controls.Add(new Label { Text = "关于信息：", Location = new Point(15, y + 3), AutoSize = true });
        y += 25;
        _infoLabel = new Label
        {
            Location = new Point(15, y),
            Size = new Size(340, 60),
            BorderStyle = BorderStyle.Fixed3D,
            BackColor = SystemColors.ControlLightLight
        };
        Controls.Add(_infoLabel);
        y += 65;

        _projectLink = new LinkLabel
        {
            Text = "GitHub: https://github.com/yuui226/ClickFX",
            Location = new Point(15, y),
            Size = new Size(340, 18),
            AutoSize = false
        };
        _projectLink.LinkClicked += (s, e) =>
            System.Diagnostics.Process.Start("https://github.com/yuui226/ClickFX");
        Controls.Add(_projectLink);
        y += 25;

        // 确定 / 取消
        var okBtn = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(190, y), Size = new Size(80, 28) };
        okBtn.Click += (s, e) => ApplyValues();
        Controls.Add(okBtn);
        var cancelBtn = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(280, y), Size = new Size(80, 28) };
        Controls.Add(cancelBtn);

        AcceptButton = okBtn;
        CancelButton = cancelBtn;
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

        if (!string.IsNullOrEmpty(config.InfoText))
        {
            _infoLabel.Text = config.InfoText
                + (string.IsNullOrEmpty(config.InfoUrl) ? "" : "\n" + config.InfoUrl);
        }
        else
        {
            _infoLabel.Text = string.IsNullOrEmpty(config.InfoUrl)
                ? "" : config.InfoUrl;
        }
    }

    void ApplyValues()
    {
        Result = new AppConfig();
        Result.LeftEffect = (_leftEffectCombo.SelectedItem as string) ?? "线条爆发";
        Result.RightEffect = (_rightEffectCombo.SelectedItem as string) ?? "线条爆发";
        Result.LeftClick = new ColorConfig(NormalizeHexValue(_leftHex.Text));
        Result.LeftClick.GlowIntensity = _originalConfig.LeftClick.GlowIntensity;
        Result.RightClick = new ColorConfig(NormalizeHexValue(_rightHex.Text));
        Result.RightClick.GlowIntensity = _originalConfig.RightClick.GlowIntensity;
        Result.InfoText = _originalConfig.InfoText;
        Result.InfoUrl = _originalConfig.InfoUrl;
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
