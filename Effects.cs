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
}

// ==================== 动画状态 ====================

class AnimationState
{
    public Point Position;
    public int Age;
    public MouseButtons Button;
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

    public void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds)
    {
        int cx = anim.Position.X - screenBounds.X;
        int cy = anim.Position.Y - screenBounds.Y;
        float t = anim.Age / (float)Duration;

        Color baseColor;
        try { baseColor = ColorTranslator.FromHtml(color.Primary); }
        catch { baseColor = Color.White; }

        Random rng = MakeRng(anim.Position.X, anim.Position.Y);
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

    public void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds)
    {
        int cx = anim.Position.X - screenBounds.X;
        int cy = anim.Position.Y - screenBounds.Y;
        float t = anim.Age / (float)Duration;

        Color baseColor;
        try { baseColor = ColorTranslator.FromHtml(color.Primary); }
        catch { baseColor = Color.White; }

        Random rng = MakeRng(anim.Position.X, anim.Position.Y);

        // 中心闪光：前 120ms 亮后消失
        if (anim.Age < 120)
        {
            float flashT = anim.Age / 120f;
            float flashAlpha = (flashT < 0.3f) ? flashT / 0.3f : 1f - Easing.EaseInQuad((flashT - 0.3f) / 0.7f);
            if (flashAlpha > 0f)
            {
                int fa = (int)(255 * flashAlpha);
                _brush.Color = Color.FromArgb(fa, Color.White);
                float fr = 4f * (1f - flashT * 0.5f);
                g.FillEllipse(_brush, cx - fr, cy - fr, fr * 2, fr * 2);
            }
        }

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
    public int Duration { get { return 350; } }

    const float MaxRadius = 14f;
    const float InnerRatio = 0.5f;

    Pen _rayPen = new Pen(Color.Black, 2f);
    SolidBrush _brush = new SolidBrush(Color.Black);

    static Random MakeRng(int x, int y)
    {
        return new Random(unchecked(x * 1299709 ^ y * 7457));
    }

    public void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds)
    {
        int cx = anim.Position.X - screenBounds.X;
        int cy = anim.Position.Y - screenBounds.Y;
        float t = anim.Age / (float)Duration;

        Color baseColor;
        try { baseColor = ColorTranslator.FromHtml(color.Primary); }
        catch { baseColor = Color.White; }

        Random rng = MakeRng(anim.Position.X, anim.Position.Y);
        int rayCount = 6 + rng.Next(5);
        float initRot = (float)(rng.NextDouble() * Math.PI * 2);
        float rotDir = (rng.Next(2) == 0) ? 1f : -1f;
        float bri = 0.85f + (float)(rng.NextDouble() * 0.15f);

        // 膨胀 + 缩小 + 混合淡出
        float expand = Easing.EaseOutBack(Math.Min(1f, t * 3f));
        float shrink = 1f - Easing.EaseInQuad(t);
        float radius = MaxRadius * expand * shrink;
        float alpha = shrink;
        float rotExtra = rotDir * 30f * t;
        if (alpha <= 0f || radius < 0.5f) return;

        int a = (int)(255 * alpha * bri);
        int glowA = (int)(255 * alpha * 0.25f);
        float rotRad = initRot + rotExtra * (float)Math.PI / 180f;
        float stroke = 2f * shrink;

        // 光晕
        DrawStar(g, cx, cy, radius * 1.5f, rotRad, rayCount, Color.FromArgb(glowA, baseColor), stroke * 1.2f);

        // 主体
        DrawStar(g, cx, cy, radius, rotRad, rayCount, Color.FromArgb(a, baseColor), stroke);

        // 中心亮点
        float dotAlpha = (t < 0.3f) ? t / 0.3f : shrink;
        if (dotAlpha > 0f)
        {
            int da = (int)(255 * dotAlpha);
            _brush.Color = Color.FromArgb(da, Color.White);
            float dr = 2.5f * shrink;
            g.FillEllipse(_brush, cx - dr, cy - dr, dr * 2, dr * 2);
        }
    }

    void DrawStar(Graphics g, float cx, float cy, float radius, float rotation, int rayCount, Color color, float stroke)
    {
        _rayPen.Width = stroke;
        _rayPen.Color = color;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = rotation + i * (float)(Math.PI * 2) / rayCount;
            float len = (i % 2 == 0) ? radius : radius * InnerRatio;
            float ex = cx + (float)Math.Cos(angle) * len;
            float ey = cy + (float)Math.Sin(angle) * len;
            g.DrawLine(_rayPen, cx, cy, ex, ey);
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
    SolidBrush _dotBrush = new SolidBrush(Color.White);

    static Random MakeRng(int x, int y)
    {
        return new Random(unchecked(x * 6271 ^ y * 4153));
    }

    public void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds)
    {
        int cx = anim.Position.X - screenBounds.X;
        int cy = anim.Position.Y - screenBounds.Y;
        float t = anim.Age / (float)Duration;

        Color baseColor;
        try { baseColor = ColorTranslator.FromHtml(color.Primary); }
        catch { baseColor = Color.White; }

        Random rng = MakeRng(anim.Position.X, anim.Position.Y);
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

        // 中心亮点
        _dotBrush.Color = Color.FromArgb(a, Color.White);
        float dr = 2f * shrink;
        g.FillEllipse(_dotBrush, cx - dr, cy - dr, dr * 2, dr * 2);
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
}
