// ClickFX — 动效系统：接口、动画状态、缓动函数、所有效果实现、效果注册表

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ==================== 接口 ====================

interface IClickEffect
{
    string Name { get; }
    int Duration { get; }
    void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds);
    void Cleanup();
}

// ==================== 动画状态 ====================

class AnimationState
{
    public Point Position;
    public int Age;
    public int Duration;
    public MouseButtons Button;
    public int RandomSeed;
}

// ==================== 缓动函数 ====================

static class Easing
{
    public static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * (float)Math.Pow(t - 1, 3) + c1 * (float)Math.Pow(t - 1, 2);
    }

    public static float EaseInQuad(float t)
    {
        return t * t;
    }

    public static float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }
}

// ==================== 效果：线条爆发 ====================

class LineBurstEffect : IClickEffect
{
    public string Name { get { return "线条爆发"; } }
    public int Duration { get { return 600; } }

    const float EraseDistance = 13f;
    static readonly float[] LineLengths = { 10f, 11f, 10f };
    static readonly float[] LineAngles = { 258f, 222f, 187f };
    static readonly float[] LineDelays = { 0f, 0.0375f, 0.075f };

    Pen _linePen = new Pen(Color.Black, 3f);
    Pen _glowPen = new Pen(Color.Black, 1.5f);

    public void Cleanup()
    {
        _linePen.Dispose();
        _glowPen.Dispose();
    }

    public void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds)
    {
        int cx = anim.Position.X - screenBounds.X;
        int cy = anim.Position.Y - screenBounds.Y;

        float progress = Math.Min(1f, anim.Age / 500f);
        if (progress >= 1f) return;

        Color baseColor;
        try { baseColor = ColorTranslator.FromHtml(color.Primary); }
        catch { baseColor = Color.White; }
        float glowIntensity = color.GlowIntensity;

        for (int i = 0; i < 3; i++)
        {
            float lineProgress = Math.Max(0f, Math.Min(1f,
                (progress - LineDelays[i]) / (1f - LineDelays[i])));
            if (lineProgress <= 0f) continue;

            float angleRad = LineAngles[i] * (float)Math.PI / 180f;
            float dirX = (float)Math.Cos(angleRad);
            float dirY = (float)Math.Sin(angleRad);

            float alpha;
            float startDist, endDist;

            if (lineProgress <= 0.4f)
            {
                float expandPhase = lineProgress / 0.4f;
                float expand = Easing.EaseOutBack(expandPhase);

                startDist = EraseDistance;
                endDist = EraseDistance + LineLengths[i] * expand;
                alpha = 1f;
            }
            else
            {
                float disappearPhase = (lineProgress - 0.4f) / 0.15f;
                if (disappearPhase > 1f) disappearPhase = 1f;
                float shrink = 1f - Easing.EaseInQuad(disappearPhase);

                startDist = EraseDistance + LineLengths[i] * (1f - shrink);
                endDist = EraseDistance + LineLengths[i] * (1f + 0.4f * disappearPhase);
                alpha = shrink;
            }

            if (alpha <= 0f) continue;

            int a = (int)(255 * alpha);
            _linePen.Color = Color.FromArgb(a, baseColor);
            _glowPen.Color = Color.FromArgb((int)(a * glowIntensity), baseColor);

            float sx = cx + dirX * startDist;
            float sy = cy + dirY * startDist;
            float ex = cx + dirX * endDist;
            float ey = cy + dirY * endDist;

            g.DrawLine(_linePen, sx, sy, ex, ey);
            g.DrawLine(_glowPen, sx, sy, ex, ey);
        }
    }
}

// ==================== 效果：水波纹 ====================

class RippleEffect : IClickEffect
{
    public string Name { get { return "水波纹"; } }
    public int Duration { get { return 350; } }

    Pen _pen = new Pen(Color.Black, 2f);

    static Random MakeRng(int x, int y)
    {
        return new Random(unchecked(x * 92837 ^ y * 4177));
    }

    public void Cleanup()
    {
        _pen.Dispose();
    }

    public void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds)
    {
        int cx = anim.Position.X - screenBounds.X;
        int cy = anim.Position.Y - screenBounds.Y;
        float t = anim.Age / (float)Duration;

        Color baseColor;
        try { baseColor = ColorTranslator.FromHtml(color.Primary); }
        catch { baseColor = Color.White; }

        Random rng = new Random(unchecked(anim.RandomSeed ^ 92837));
        int ringCount = 2 + rng.Next(3);
        float maxRadius = 16f + (float)(rng.NextDouble() * 8f);
        float stagger = 0.08f + (float)(rng.NextDouble() * 0.08f);
        float strokeBase = 2f + (float)(rng.NextDouble());

        for (int i = 0; i < ringCount; i++)
        {
            float delay = i * stagger;
            float ringT = Math.Max(0f, (t - delay) / (1f - delay));
            if (ringT <= 0f) continue;

            float expand = Easing.EaseOutQuad(ringT);
            float fade = 1f - Easing.EaseInQuad(ringT);
            float radius = maxRadius * expand * fade;
            if (radius < 0.5f || fade <= 0f) continue;

            int a = (int)(255 * fade);
            _pen.Color = Color.FromArgb(a, baseColor);
            _pen.Width = strokeBase * fade;

            g.DrawEllipse(_pen, cx - radius, cy - radius, radius * 2, radius * 2);
        }
    }
}

// ==================== 效果：火花 ====================

class SparkEffect : IClickEffect
{
    public string Name { get { return "火花"; } }
    public int Duration { get { return 350; } }

    const int DotCount = 8;
    const float MaxDist = 18f;

    SolidBrush _brush = new SolidBrush(Color.Black);

    static Random MakeRng(int x, int y)
    {
        return new Random(unchecked(x * 73856093 ^ y * 19349663));
    }

    public void Cleanup()
    {
        _brush.Dispose();
    }

    public void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds)
    {
        int cx = anim.Position.X - screenBounds.X;
        int cy = anim.Position.Y - screenBounds.Y;
        float t = anim.Age / (float)Duration;

        Color baseColor;
        try { baseColor = ColorTranslator.FromHtml(color.Primary); }
        catch { baseColor = Color.White; }

        Random rng = new Random(unchecked(anim.RandomSeed ^ 73856093));

        // 粒子：快速弹出，平滑缩小消失
        float shrink = 1f - Easing.EaseInQuad(t);
        if (shrink <= 0f) return;

        for (int i = 0; i < DotCount; i++)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2);
            float dist = MaxDist * Easing.EaseOutQuad(t);
            float size = (2f + (float)(rng.NextDouble() * 1.5f)) * shrink;
            float bri = 0.85f + (float)(rng.NextDouble() * 0.15f);

            float px = cx + (float)Math.Cos(angle) * dist;
            float py = cy + (float)Math.Sin(angle) * dist;

            int a = (int)(255 * shrink * bri);
            _brush.Color = Color.FromArgb(a,
                Math.Min(255, (int)(baseColor.R * bri)),
                Math.Min(255, (int)(baseColor.G * bri)),
                Math.Min(255, (int)(baseColor.B * bri)));
            g.FillEllipse(_brush, px - size, py - size, size * 2, size * 2);
        }
    }
}

// ==================== 效果：星光 ====================

class StarEffect : IClickEffect
{
    public string Name { get { return "星光"; } }
    public int Duration { get { return 500; } }

    const int StarCount = 7;
    const float MaxDist = 28f;
    const float BaseSize = 2.8f;
    const float SizeJitter = 1.8f;
    const float CurveStrength = 8f;

    Pen _pen = new Pen(Color.Black, 1.2f);
    SolidBrush _brush = new SolidBrush(Color.Black);

    public void Cleanup()
    {
        _pen.Dispose();
        _brush.Dispose();
    }

    public void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds)
    {
        int cx = anim.Position.X - screenBounds.X;
        int cy = anim.Position.Y - screenBounds.Y;
        float t = anim.Age / (float)Duration;

        Color baseColor;
        try { baseColor = ColorTranslator.FromHtml(color.Primary); }
        catch { baseColor = Color.White; }

        // 每次点击用不同种子 → 轨迹随机
        Random rng = new Random(unchecked(anim.RandomSeed ^ 1299709));

        // ── 星星粒子 ──
        for (int i = 0; i < StarCount; i++)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2);
            float delay = 0.05f + (float)(rng.NextDouble() * 0.25f);
            float life = 0.5f + (float)(rng.NextDouble() * 0.45f);
            float dist = MaxDist * (0.6f + (float)(rng.NextDouble() * 0.4f));
            float size = BaseSize + (float)(rng.NextDouble() * SizeJitter);
            float bri = 0.75f + (float)(rng.NextDouble() * 0.25f);
            float curve = ((rng.Next(2) == 0) ? 1f : -1f) * CurveStrength * (0.5f + (float)(rng.NextDouble()));
            float twinkleSpeed = 0.8f + (float)(rng.NextDouble() * 0.7f);
            float initRot = (float)(rng.NextDouble() * Math.PI * 2);
            float rotSpeed = ((rng.Next(2) == 0) ? 1f : -1f) * (80f + (float)(rng.NextDouble() * 120f));

            // 粒子局部时间
            float localT = (t - delay) / life;
            if (localT <= 0f || localT >= 1f) continue;

            // 运动：弹出 + 缓慢滑行
            float moveT = Easing.EaseOutQuad(Math.Min(1f, localT * 2.5f));
            float drift = Easing.EaseInQuad(Math.Max(0f, localT - 0.4f) / 0.6f) * dist * 0.4f;
            float r = dist * moveT + drift;

            // 轨迹弯曲（垂直方向偏移）
            float perpAngle = angle + (float)Math.PI / 2f;
            float curveAmount = curve * Easing.EaseInQuad(localT);

            float px = cx + (float)Math.Cos(angle) * r + (float)Math.Cos(perpAngle) * curveAmount;
            float py = cy + (float)Math.Sin(angle) * r + (float)Math.Sin(perpAngle) * curveAmount;

            // 透明度：淡入 + 缓慢闪烁 + 淡出
            float fadeIn = Math.Min(1f, localT * 6f);
            float fadeOut = 1f - Easing.EaseInQuad(Math.Max(0f, localT - 0.5f) / 0.5f);
            float twinkle = 0.7f + 0.3f * (float)Math.Sin(localT * twinkleSpeed * Math.PI * 2);
            float alpha = fadeIn * fadeOut * twinkle * bri;
            if (alpha <= 0.01f) continue;

            // 大小：弹出后缓慢缩小
            float sizeScale = Easing.EaseOutBack(Math.Min(1f, localT * 4f))
                            * (1f - Easing.EaseInQuad(Math.Max(0f, localT - 0.6f) / 0.4f) * 0.6f);
            float s = size * sizeScale;
            if (s < 0.3f) continue;

            // 旋转
            float rot = initRot + rotSpeed * localT * (float)Math.PI / 180f;

            int a = (int)(255 * alpha);

            // 彩色星形主体
            _pen.Width = 1.5f * sizeScale;
            _pen.Color = Color.FromArgb((int)(255 * Math.Min(1f, alpha * 1.2f)), baseColor);
            DrawTinyStar(g, px, py, s, rot, 4, _pen);

            // 中心高光点
            _brush.Color = Color.FromArgb(a, Color.White);
            float dotR = s * 0.15f;
            g.FillEllipse(_brush, px - dotR, py - dotR, dotR * 2, dotR * 2);
        }
    }

    void DrawTinyStar(Graphics g, float cx, float cy, float radius, float rotation, int points, Pen pen)
    {
        float inner = radius * 0.35f;
        for (int i = 0; i < points; i++)
        {
            float a1 = rotation + i * (float)(Math.PI * 2) / points;
            float a2 = a1 + (float)Math.PI / points;
            float x1 = cx + (float)Math.Cos(a1) * radius;
            float y1 = cy + (float)Math.Sin(a1) * radius;
            float x2 = cx + (float)Math.Cos(a2) * inner;
            float y2 = cy + (float)Math.Sin(a2) * inner;
            g.DrawLine(pen, x1, y1, x2, y2);

            float a3 = a2;
            float a4 = rotation + (i + 1) * (float)(Math.PI * 2) / points;
            float x3 = cx + (float)Math.Cos(a3) * inner;
            float y3 = cy + (float)Math.Sin(a3) * inner;
            float x4 = cx + (float)Math.Cos(a4) * radius;
            float y4 = cy + (float)Math.Sin(a4) * radius;
            g.DrawLine(pen, x3, y3, x4, y4);
        }
    }
}

// ==================== 效果：花瓣 ====================

class PetalEffect : IClickEffect
{
    public string Name { get { return "花瓣"; } }
    public int Duration { get { return 350; } }

    const float MaxDist = 14f;
    const float PetalLength = 8f;
    const float PetalWidth = 3.5f;

    SolidBrush _petalBrush = new SolidBrush(Color.Black);

    static Random MakeRng(int x, int y)
    {
        return new Random(unchecked(x * 6271 ^ y * 4153));
    }

    public void Cleanup()
    {
        _petalBrush.Dispose();
    }

    public void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds)
    {
        int cx = anim.Position.X - screenBounds.X;
        int cy = anim.Position.Y - screenBounds.Y;
        float t = anim.Age / (float)Duration;

        Color baseColor;
        try { baseColor = ColorTranslator.FromHtml(color.Primary); }
        catch { baseColor = Color.White; }

        Random rng = new Random(unchecked(anim.RandomSeed ^ 6271));
        int petalCount = 3 + rng.Next(3);
        float initRot = (float)(rng.NextDouble() * 360);
        float rotDir = (rng.Next(2) == 0) ? 1f : -1f;
        float sizeVar = 0.8f + (float)(rng.NextDouble() * 0.4f);

        // 膨胀 + 缩小 + 淡出同步进行
        float expand = Easing.EaseOutBack(Math.Min(1f, t * 3f));
        float shrink = 1f - Easing.EaseInQuad(t);
        float dist = MaxDist * expand * shrink;
        float scale = shrink;
        float alpha = shrink;
        float rotExtra = rotDir * 25f * t;
        if (alpha <= 0f) return;

        int a = (int)(255 * alpha);
        float baseRotRad = (initRot + rotExtra) * (float)Math.PI / 180f;

        for (int i = 0; i < petalCount; i++)
        {
            float angle = baseRotRad + i * (float)(Math.PI * 2) / petalCount;
            float pcx = cx + (float)Math.Cos(angle) * dist;
            float pcy = cy + (float)Math.Sin(angle) * dist;

            GraphicsState state = g.Save();
            g.TranslateTransform(pcx, pcy);
            g.RotateTransform(angle * 180f / (float)Math.PI);

            _petalBrush.Color = Color.FromArgb(a, baseColor);
            float hl = PetalLength * scale * sizeVar;
            float hw = PetalWidth * scale * sizeVar;
            g.FillEllipse(_petalBrush, -hl, -hw, hl * 2, hw * 2);

            g.Restore(state);
        }
    }
}

// ==================== 效果注册表 ====================

static class EffectRegistry
{
    static readonly Dictionary<string, IClickEffect> _effects = new Dictionary<string, IClickEffect>();

    public static void Register(IClickEffect effect)
    {
        _effects[effect.Name] = effect;
    }

    public static IClickEffect Get(string name)
    {
        IClickEffect effect;
        _effects.TryGetValue(name, out effect);
        return effect;
    }

    public static List<string> GetAllNames()
    {
        var names = new List<string>();
        foreach (var kv in _effects)
            names.Add(kv.Key);
        return names;
    }

    public static void Cleanup()
    {
        foreach (var kv in _effects)
            kv.Value.Cleanup();
        _effects.Clear();
    }
}
