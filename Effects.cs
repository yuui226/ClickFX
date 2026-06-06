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
    public string EffectName;
    public IClickEffect CachedEffect;
    public object EffectData; // 各效果缓存的预计算数据，每动画只算一次
    public float Scale = 1f; // 效果大小缩放系数
    Color _cachedColor;
    bool _colorCached;

    public Color GetColor(ColorConfig config)
    {
        if (!_colorCached)
        {
            try { _cachedColor = ColorTranslator.FromHtml(config.Primary); }
            catch { _cachedColor = Color.White; }
            _colorCached = true;
        }
        return _cachedColor;
    }
}

// ==================== 缓动函数 ====================

static class Easing
{
    public static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float tm1 = t - 1f;
        float tm1_2 = tm1 * tm1;
        return 1f + c3 * tm1_2 * tm1 + c1 * tm1_2;
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

// 各效果的随机参数通过 EffectData 每动画只计算一次（见 AnimationState.EffectData），
// 而非每帧 new Random(seed)。需要时仍创建临时 Random 实例来生成初始值，
// 但生成的结果会缓存，不会跨帧推进 Random 序列。

// ==================== 效果：线条爆发 ====================

class LineBurstEffect : IClickEffect
{
    public string Name { get { return "线条爆发"; } }
    public int Duration { get { return 600; } }

    const float EraseDistance = 13f;
    static readonly float[] LineLengths = { 10f, 11f, 10f };
    static readonly float[] LineDelays = { 0f, 0.0375f, 0.075f };
    static readonly float[] DirX, DirY;
    static readonly float PI = (float)Math.PI;

    static LineBurstEffect()
    {
        float[] angles = { 258f, 222f, 187f };
        DirX = new float[3];
        DirY = new float[3];
        for (int i = 0; i < 3; i++)
        {
            float rad = angles[i] * PI / 180f;
            DirX[i] = (float)Math.Cos(rad);
            DirY[i] = (float)Math.Sin(rad);
        }
    }

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
        float scale = anim.Scale;

        float progress = Math.Min(1f, anim.Age / 500f);
        if (progress >= 1f) return;

        Color baseColor = anim.GetColor(color);
        float glowIntensity = color.GlowIntensity;
        float erase = EraseDistance * scale;

        for (int i = 0; i < 3; i++)
        {
            float lineLen = LineLengths[i] * scale;
            float lineProgress = Math.Max(0f, Math.Min(1f,
                (progress - LineDelays[i]) / (1f - LineDelays[i])));
            if (lineProgress <= 0f) continue;

            float dirX = DirX[i];
            float dirY = DirY[i];

            float alpha;
            float startDist, endDist;

            if (lineProgress <= 0.4f)
            {
                float expandPhase = lineProgress / 0.4f;
                float expand = Easing.EaseOutBack(expandPhase);

                startDist = erase;
                endDist = erase + lineLen * expand;
                alpha = 1f;
            }
            else
            {
                float disappearPhase = (lineProgress - 0.4f) / 0.15f;
                if (disappearPhase > 1f) disappearPhase = 1f;
                float shrink = 1f - Easing.EaseInQuad(disappearPhase);

                startDist = erase + lineLen * (1f - shrink);
                endDist = erase + lineLen * (1f + 0.4f * disappearPhase);
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

    class Data
    {
        public int RingCount;
        public float MaxRadius;
        public float Stagger;
        public float StrokeBase;
    }

    Pen _pen = new Pen(Color.Black, 2f);

    public void Cleanup()
    {
        _pen.Dispose();
    }

    public void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds)
    {
        int cx = anim.Position.X - screenBounds.X;
        int cy = anim.Position.Y - screenBounds.Y;
        float t = anim.Age / (float)Duration;

        Color baseColor = anim.GetColor(color);

        var data = anim.EffectData as Data;
        if (data == null)
        {
            var rng = new Random(unchecked(anim.RandomSeed ^ 92837));
            data = new Data
            {
                RingCount = 2 + rng.Next(3),
                MaxRadius = 16f + (float)(rng.NextDouble() * 8f),
                Stagger = 0.08f + (float)(rng.NextDouble() * 0.08f),
                StrokeBase = 2f + (float)(rng.NextDouble()),
            };
            anim.EffectData = data;
        }

        for (int i = 0; i < data.RingCount; i++)
        {
            float delay = i * data.Stagger;
            float ringT = Math.Max(0f, (t - delay) / (1f - delay));
            if (ringT <= 0f) continue;

            float expand = Easing.EaseOutQuad(ringT);
            float fade = 1f - Easing.EaseInQuad(ringT);
            float radius = data.MaxRadius * anim.Scale * expand * fade;
            if (radius < 0.5f || fade <= 0f) continue;

            int a = (int)(255 * fade);
            _pen.Color = Color.FromArgb(a, baseColor);
            _pen.Width = data.StrokeBase * fade;

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

    class Data
    {
        public float[] CosAngles;
        public float[] SinAngles;
        public float[] BaseSizes;
        public float[] Bris;
    }

    SolidBrush _brush = new SolidBrush(Color.Black);

    public void Cleanup()
    {
        _brush.Dispose();
    }

    public void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds)
    {
        int cx = anim.Position.X - screenBounds.X;
        int cy = anim.Position.Y - screenBounds.Y;
        float t = anim.Age / (float)Duration;

        Color baseColor = anim.GetColor(color);

        var data = anim.EffectData as Data;
        if (data == null)
        {
            var rng = new Random(unchecked(anim.RandomSeed ^ 73856093));
            data = new Data
            {
                CosAngles = new float[DotCount],
                SinAngles = new float[DotCount],
                BaseSizes = new float[DotCount],
                Bris = new float[DotCount],
            };
            for (int i = 0; i < DotCount; i++)
            {
                float angle = (float)(rng.NextDouble() * Math.PI * 2);
                data.CosAngles[i] = (float)Math.Cos(angle);
                data.SinAngles[i] = (float)Math.Sin(angle);
                data.BaseSizes[i] = 2f + (float)(rng.NextDouble() * 1.5f);
                data.Bris[i] = 0.85f + (float)(rng.NextDouble() * 0.15f);
            }
            anim.EffectData = data;
        }

        float shrink = 1f - Easing.EaseInQuad(t);
        if (shrink <= 0f) return;

        float dist = MaxDist * anim.Scale * Easing.EaseOutQuad(t);

        for (int i = 0; i < DotCount; i++)
        {
            float size = data.BaseSizes[i] * anim.Scale * shrink;
            float bri = data.Bris[i];

            float px = cx + data.CosAngles[i] * dist;
            float py = cy + data.SinAngles[i] * dist;

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

    class StarData
    {
        public float Delay, Life, Dist, Size, Bri;
        public float Curve, TwinkleSpeed, InitRot, RotSpeed;
        public float CosAngle, SinAngle, CosPerp, SinPerp;
    }

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

        Color baseColor = anim.GetColor(color);

        var stars = anim.EffectData as StarData[];
        if (stars == null)
        {
            var rng = new Random(unchecked(anim.RandomSeed ^ 1299709));
            stars = new StarData[StarCount];
            for (int i = 0; i < StarCount; i++)
            {
                float angle = (float)(rng.NextDouble() * Math.PI * 2);
                float perp = angle + (float)Math.PI / 2f;
                stars[i] = new StarData
                {
                    CosAngle = (float)Math.Cos(angle),
                    SinAngle = (float)Math.Sin(angle),
                    CosPerp = (float)Math.Cos(perp),
                    SinPerp = (float)Math.Sin(perp),
                    Delay = 0.05f + (float)(rng.NextDouble() * 0.25f),
                    Life = 0.5f + (float)(rng.NextDouble() * 0.45f),
                    Dist = MaxDist * (0.6f + (float)(rng.NextDouble() * 0.4f)),
                    Size = BaseSize + (float)(rng.NextDouble() * SizeJitter),
                    Bri = 0.75f + (float)(rng.NextDouble() * 0.25f),
                    Curve = ((rng.Next(2) == 0) ? 1f : -1f) * CurveStrength * (0.5f + (float)(rng.NextDouble())),
                    TwinkleSpeed = 0.8f + (float)(rng.NextDouble() * 0.7f),
                    InitRot = (float)(rng.NextDouble() * Math.PI * 2),
                    RotSpeed = ((rng.Next(2) == 0) ? 1f : -1f) * (80f + (float)(rng.NextDouble() * 120f)),
                };
            }
            anim.EffectData = stars;
        }

        for (int i = 0; i < StarCount; i++)
        {
            var s = stars[i];

            float localT = (t - s.Delay) / s.Life;
            if (localT <= 0f || localT >= 1f) continue;

            float dist = s.Dist * anim.Scale;
            float moveT = Easing.EaseOutQuad(Math.Min(1f, localT * 2.5f));
            float drift = Easing.EaseInQuad(Math.Max(0f, localT - 0.4f) / 0.6f) * dist * 0.4f;
            float r = dist * moveT + drift;

            float curveAmount = s.Curve * Easing.EaseInQuad(localT);

            float px = cx + s.CosAngle * r + s.CosPerp * curveAmount;
            float py = cy + s.SinAngle * r + s.SinPerp * curveAmount;

            float fadeIn = Math.Min(1f, localT * 6f);
            float fadeOut = 1f - Easing.EaseInQuad(Math.Max(0f, localT - 0.5f) / 0.5f);
            float twinkle = 0.7f + 0.3f * (float)Math.Sin(localT * s.TwinkleSpeed * Math.PI * 2);
            float alpha = fadeIn * fadeOut * twinkle * s.Bri;
            if (alpha <= 0.01f) continue;

            float sizeScale = Easing.EaseOutBack(Math.Min(1f, localT * 4f))
                            * (1f - Easing.EaseInQuad(Math.Max(0f, localT - 0.6f) / 0.4f) * 0.6f);
            float sz = s.Size * anim.Scale * sizeScale;
            if (sz < 0.3f) continue;

            float rot = s.InitRot + s.RotSpeed * localT * (float)Math.PI / 180f;

            int a = (int)(255 * alpha);

            _pen.Width = 1.5f * sizeScale;
            _pen.Color = Color.FromArgb((int)(255 * Math.Min(1f, alpha * 1.2f)), baseColor);
            DrawTinyStar(g, px, py, sz, rot, 4, _pen);

            _brush.Color = Color.FromArgb(a, Color.White);
            float dotR = sz * 0.15f;
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

    class Data
    {
        public int PetalCount;
        public float InitRot;
        public float RotDir;
        public float SizeVar;
    }

    SolidBrush _petalBrush = new SolidBrush(Color.Black);

    public void Cleanup()
    {
        _petalBrush.Dispose();
    }

    public void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds)
    {
        int cx = anim.Position.X - screenBounds.X;
        int cy = anim.Position.Y - screenBounds.Y;
        float t = anim.Age / (float)Duration;

        Color baseColor = anim.GetColor(color);

        var data = anim.EffectData as Data;
        if (data == null)
        {
            var rng = new Random(unchecked(anim.RandomSeed ^ 6271));
            data = new Data
            {
                PetalCount = 3 + rng.Next(3),
                InitRot = (float)(rng.NextDouble() * 360),
                RotDir = (rng.Next(2) == 0) ? 1f : -1f,
                SizeVar = 0.8f + (float)(rng.NextDouble() * 0.4f),
            };
            anim.EffectData = data;
        }

        float effectScale = anim.Scale;
        float expand = Easing.EaseOutBack(Math.Min(1f, t * 3f));
        float shrink = 1f - Easing.EaseInQuad(t);
        float dist = MaxDist * effectScale * expand * shrink;
        float scale = shrink;
        float alpha = shrink;
        float rotExtra = data.RotDir * 25f * t;
        if (alpha <= 0f) return;

        int a = (int)(255 * alpha);
        float baseRotRad = (data.InitRot + rotExtra) * (float)Math.PI / 180f;
        float angleStep = (float)(Math.PI * 2) / data.PetalCount;
        float hl = PetalLength * effectScale * scale * data.SizeVar;
        float hw = PetalWidth * effectScale * scale * data.SizeVar;

        for (int i = 0; i < data.PetalCount; i++)
        {
            float angle = baseRotRad + i * angleStep;
            float pcx = cx + (float)Math.Cos(angle) * dist;
            float pcy = cy + (float)Math.Sin(angle) * dist;

            GraphicsState state = g.Save();
            g.TranslateTransform(pcx, pcy);
            g.RotateTransform(angle * 180f / (float)Math.PI);

            _petalBrush.Color = Color.FromArgb(a, baseColor);
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

    public static IClickEffect GetFirst()
    {
        foreach (var kv in _effects)
            return kv.Value;
        return null;
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
