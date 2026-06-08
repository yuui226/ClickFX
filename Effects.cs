// ClickFX — 动效系统：接口、动画状态、缓动函数、所有效果实现、效果注册表

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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

class AnimationState : IDisposable
{
    public Point Position;
    public int Age;
    public int Duration;
    public MouseButtons Button;
    public int RandomSeed;
    public IClickEffect CachedEffect;
    public object EffectData; // 各效果缓存的预计算数据，每动画只算一次
    public float Scale = 1f; // 效果大小缩放系数
    Color _cachedColor;
    bool _colorCached;
    // 仅在 UI 线程（OnPaint → Draw）调用，无需加锁
    static readonly Random _colorRng = new Random();

    public Color GetColor(ColorConfig config)
    {
        if (!_colorCached)
        {
            if (config.RandomColor)
            {
                _cachedColor = RandomHSVColor();
            }
            else
            {
                try { _cachedColor = ColorTranslator.FromHtml(config.Primary); }
                catch { _cachedColor = Color.White; }
            }
            _colorCached = true;
        }
        return _cachedColor;
    }

    static Color RandomHSVColor()
    {
        double h = _colorRng.NextDouble() * 360.0;
        double s = 0.7 + _colorRng.NextDouble() * 0.3;  // 0.7 ~ 1.0
        double v = 0.8 + _colorRng.NextDouble() * 0.2;  // 0.8 ~ 1.0
        int hi = (int)(h / 60) % 6;
        double f = h / 60 - Math.Floor(h / 60);
        double p = v * (1 - s);
        double q = v * (1 - f * s);
        double t = v * (1 - (1 - f) * s);
        double r, g, b;
        switch (hi)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }
        return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }

    public void Dispose()
    {
        var disposable = EffectData as IDisposable;
        if (disposable != null)
        {
            disposable.Dispose();
        }
        EffectData = null;
    }
}

// ==================== 缓动函数 ====================

static class Easing
{
    public static readonly float PI = (float)Math.PI;

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

    static LineBurstEffect()
    {
        float[] angles = { 258f, 222f, 187f };
        DirX = new float[3];
        DirY = new float[3];
        for (int i = 0; i < 3; i++)
        {
            float rad = angles[i] * Easing.PI / 180f;
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
            _linePen.Width = 3f * scale;
            _glowPen.Color = Color.FromArgb((int)(a * glowIntensity), baseColor);
            _glowPen.Width = 1.5f * scale;

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
    const int StarPoints = 4;
    const float MaxDist = 28f;
    const float BaseSize = 2.8f;
    const float SizeJitter = 1.8f;
    const float CurveStrength = 8f;

    class StarData
    {
        public float Delay, Life, Dist, Size, Bri;
        public float Curve, TwinkleSpeed, InitRot, RotSpeed;
        public float CosAngle, SinAngle, CosPerp, SinPerp;
        public float[] CosDirs, SinDirs; // 预计算方向向量，避免每帧 trig
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
                // 预计算星星顶点方向向量，避免 Draw 中每帧 trig 调用
                float[] cosDirs = new float[StarPoints * 2];
                float[] sinDirs = new float[StarPoints * 2];
                for (int j = 0; j < StarPoints; j++)
                {
                    float a1 = angle + j * (float)(Math.PI * 2) / StarPoints;
                    float a2 = a1 + (float)Math.PI / StarPoints;
                    cosDirs[j * 2] = (float)Math.Cos(a1);
                    sinDirs[j * 2] = (float)Math.Sin(a1);
                    cosDirs[j * 2 + 1] = (float)Math.Cos(a2);
                    sinDirs[j * 2 + 1] = (float)Math.Sin(a2);
                }
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
                    CosDirs = cosDirs,
                    SinDirs = sinDirs,
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
            DrawStar(g, px, py, sz, _pen, s.CosDirs, s.SinDirs);

            _brush.Color = Color.FromArgb(a, Color.White);
            float dotR = sz * 0.15f;
            g.FillEllipse(_brush, px - dotR, py - dotR, dotR * 2, dotR * 2);
        }
    }

    // 使用预计算方向向量绘制星星，避免每帧 trig 调用
    static void DrawStar(Graphics g, float cx, float cy, float radius, Pen pen,
        float[] cosDirs, float[] sinDirs)
    {
        float inner = radius * 0.35f;
        for (int i = 0; i < StarPoints; i++)
        {
            int j = (i + 1) % StarPoints;
            float x1 = cx + cosDirs[i * 2] * radius;
            float y1 = cy + sinDirs[i * 2] * radius;
            float x2 = cx + cosDirs[i * 2 + 1] * inner;
            float y2 = cy + sinDirs[i * 2 + 1] * inner;
            g.DrawLine(pen, x1, y1, x2, y2);

            float x3 = x2;
            float y3 = y2;
            float x4 = cx + cosDirs[j * 2] * radius;
            float y4 = cy + sinDirs[j * 2] * radius;
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

        _petalBrush.Color = Color.FromArgb(a, baseColor);
        GraphicsState state = g.Save();
        for (int i = 0; i < data.PetalCount; i++)
        {
            float angle = baseRotRad + i * angleStep;
            float pcx = cx + (float)Math.Cos(angle) * dist;
            float pcy = cy + (float)Math.Sin(angle) * dist;

            g.Transform.Reset();
            g.TranslateTransform(pcx, pcy);
            g.RotateTransform(angle * 180f / (float)Math.PI);

            g.FillEllipse(_petalBrush, -hl, -hw, hl * 2, hw * 2);
        }
        g.Restore(state);
    }
}

// ==================== 效果：漩涡 ====================

class VortexEffect : IClickEffect
{
    public string Name { get { return "漩涡"; } }
    public int Duration { get { return 400; } }

    const int ParticleCount = 6;
    const float MaxRadius = 22f;

    class Data
    {
        public float[] InitAngles;
        public float[] SpiralSpeeds;
        public float[] Sizes;
        public float[] Bris;
        public float[] Directions;
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
            var rng = new Random(unchecked(anim.RandomSeed ^ 31415));
            data = new Data
            {
                InitAngles = new float[ParticleCount],
                SpiralSpeeds = new float[ParticleCount],
                Sizes = new float[ParticleCount],
                Bris = new float[ParticleCount],
                Directions = new float[ParticleCount],
            };
            for (int i = 0; i < ParticleCount; i++)
            {
                data.InitAngles[i] = (float)(rng.NextDouble() * Math.PI * 2);
                data.SpiralSpeeds[i] = 4f + (float)(rng.NextDouble() * 5f);
                data.Sizes[i] = 2f + (float)(rng.NextDouble() * 2f);
                data.Bris[i] = 0.7f + (float)(rng.NextDouble() * 0.3f);
                data.Directions[i] = (rng.Next(2) == 0) ? 1f : -1f;
            }
            anim.EffectData = data;
        }

        float alpha = 1f - Easing.EaseInQuad(t);
        if (alpha <= 0f) return;

        float scale = anim.Scale;
        float radius = MaxRadius * scale * Easing.EaseOutQuad(t);
        float sizeFade = 1f - Easing.EaseInQuad(t); // 所有粒子共享，提到外层

        for (int i = 0; i < ParticleCount; i++)
        {
            float bri = data.Bris[i];
            float baseSize = data.Sizes[i] * scale;

            // 绘制拖尾（3 个历史位置）
            for (int trail = 3; trail >= 0; trail--)
            {
                float trailT = Math.Max(0f, t - trail * 0.04f);
                float trailAngle = data.InitAngles[i] + data.Directions[i] * data.SpiralSpeeds[i] * trailT;
                float trailRadius = MaxRadius * scale * Easing.EaseOutQuad(trailT);

                float px = cx + (float)Math.Cos(trailAngle) * trailRadius;
                float py = cy + (float)Math.Sin(trailAngle) * trailRadius;

                float trailAlpha = alpha * (1f - trail * 0.25f);
                float size = baseSize * (1f - trail * 0.15f) * sizeFade;

                int a = (int)(255 * trailAlpha * bri);
                _brush.Color = Color.FromArgb(a,
                    Math.Min(255, (int)(baseColor.R * bri)),
                    Math.Min(255, (int)(baseColor.G * bri)),
                    Math.Min(255, (int)(baseColor.B * bri)));
                g.FillEllipse(_brush, px - size, py - size, size * 2, size * 2);
            }
        }
    }
}

// ==================== 效果：碎片 ====================

class FragmentEffect : IClickEffect
{
    public string Name { get { return "碎片"; } }
    public int Duration { get { return 600; } }

    const int BigFragCount = 6;    // 大碎片
    const int SmallFragCount = 12; // 小碎屑
    const float Gravity = 180f;    // 重力加速度

    class Data
    {
        // 大碎片
        public float[] BigVx, BigVy, BigRotSpeed, BigSize, BigBri;
        public int[] BigVerts;
        public float[][] BigShapeX, BigShapeY;
        public PointF[][] BigPts;    // 每碎片独立的复用顶点数组
        // 小碎屑
        public float[] SmVx, SmVy, SmSize, SmBri;
        public float GravityX, GravityY;
    }

    SolidBrush _brush = new SolidBrush(Color.Black);
    Pen _edgePen = new Pen(Color.Black, 1f);

    public void Cleanup()
    {
        _brush.Dispose();
        _edgePen.Dispose();
    }

    // 生成不规则多边形顶点（以原点为中心，外接圆半径约 r）
    static void MakeIrregularPoly(Random rng, int verts, float r,
                                  out float[] xs, out float[] ys)
    {
        xs = new float[verts];
        ys = new float[verts];
        for (int i = 0; i < verts; i++)
        {
            float angle = i * Easing.PI * 2f / verts + (float)(rng.NextDouble() - 0.5) * 0.6f;
            float rad = r * (0.65f + (float)(rng.NextDouble()) * 0.45f);
            xs[i] = (float)Math.Cos(angle) * rad;
            ys[i] = (float)Math.Sin(angle) * rad;
        }
    }

    public void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds)
    {
        int cx = anim.Position.X - screenBounds.X;
        int cy = anim.Position.Y - screenBounds.Y;
        float t = anim.Age / (float)Duration;
        float scale = anim.Scale;

        Color baseColor = anim.GetColor(color);

        var data = anim.EffectData as Data;
        if (data == null)
        {
            var rng = new Random(unchecked(anim.RandomSeed ^ 2718));

            float gravAngle = Easing.PI * 0.5f + (float)((rng.NextDouble() - 0.5) * 0.5f);

            float gCos = (float)Math.Cos(gravAngle);
            float gSin = (float)Math.Sin(gravAngle);

            data = new Data
            {
                BigVx = new float[BigFragCount],
                BigVy = new float[BigFragCount],
                BigRotSpeed = new float[BigFragCount],
                BigSize = new float[BigFragCount],
                BigBri = new float[BigFragCount],
                BigVerts = new int[BigFragCount],
                BigShapeX = new float[BigFragCount][],
                BigShapeY = new float[BigFragCount][],
                BigPts = new PointF[BigFragCount][],
                SmVx = new float[SmallFragCount],
                SmVy = new float[SmallFragCount],
                SmSize = new float[SmallFragCount],
                SmBri = new float[SmallFragCount],
                GravityX = gCos,
                GravityY = gSin,
            };

            // 大碎片：预计算速度分量，避免每帧 Cos/Sin
            for (int i = 0; i < BigFragCount; i++)
            {
                float angle = (float)(rng.NextDouble() * Easing.PI * 2) + (float)(rng.NextDouble() - 0.5) * 0.4f;
                float speed = 30f + (float)(rng.NextDouble() * 70f);
                data.BigVx[i] = (float)Math.Cos(angle) * speed;
                data.BigVy[i] = (float)Math.Sin(angle) * speed;
                data.BigRotSpeed[i] = (float)((rng.NextDouble() - 0.5) * 600f);
                data.BigSize[i] = 4f + (float)(rng.NextDouble() * 4f);
                data.BigBri[i] = 0.75f + (float)(rng.NextDouble() * 0.25f);
                data.BigVerts[i] = 3 + rng.Next(3);
                MakeIrregularPoly(rng, data.BigVerts[i], 1f,
                    out data.BigShapeX[i], out data.BigShapeY[i]);
                data.BigPts[i] = new PointF[data.BigVerts[i]];
            }

            // 小碎屑：同样预计算
            for (int i = 0; i < SmallFragCount; i++)
            {
                float angle = (float)(rng.NextDouble() * Easing.PI * 2);
                float speed = 50f + (float)(rng.NextDouble() * 80f);
                data.SmVx[i] = (float)Math.Cos(angle) * speed;
                data.SmVy[i] = (float)Math.Sin(angle) * speed;
                data.SmSize[i] = 1.5f + (float)(rng.NextDouble() * 2f);
                data.SmBri[i] = 0.6f + (float)(rng.NextDouble() * 0.4f);
            }

            anim.EffectData = data;
        }

        // ---- 命中闪光 + 冲击波 ----
        if (t < 0.15f)
        {
            float flashT = t / 0.15f;
            // 核心闪光
            float flashSize = 10f * scale * flashT;
            int flashA = (int)(220 * (1f - flashT));
            _brush.Color = Color.FromArgb(flashA, baseColor);
            g.FillEllipse(_brush, cx - flashSize, cy - flashSize, flashSize * 2, flashSize * 2);
            // 白色高光核心
            float coreSize = 5f * scale * (1f - flashT);
            int coreA = (int)(180 * (1f - flashT));
            _brush.Color = Color.FromArgb(coreA, Color.White);
            g.FillEllipse(_brush, cx - coreSize, cy - coreSize, coreSize * 2, coreSize * 2);
            // 冲击波环
            float ringSize = 18f * scale * flashT;
            float ringAlpha = (1f - flashT) * 0.6f;
            _edgePen.Color = Color.FromArgb((int)(255 * ringAlpha), baseColor);
            _edgePen.Width = 2.5f * scale * (1f - flashT);
            g.DrawEllipse(_edgePen, cx - ringSize, cy - ringSize, ringSize * 2, ringSize * 2);
        }

        // ---- 碎片阶段 ----
        float fragT = Math.Max(0f, (t - 0.08f) / 0.92f);
        if (fragT <= 0f) return;

        float fragAlpha = 1f - Easing.EaseInQuad(fragT);
        if (fragAlpha <= 0f) return;

        float time = fragT * Duration / 1000f;

        // 预计算重力偏移（所有碎片共享）
        float gravOffX = 0.5f * data.GravityX * Gravity * time * time * scale;
        float gravOffY = 0.5f * data.GravityY * Gravity * time * time * scale;

        // ---- 大碎片 ----
        for (int i = 0; i < BigFragCount; i++)
        {
            float bri = data.BigBri[i];
            float px = cx + data.BigVx[i] * time * scale + gravOffX;
            float py = cy + data.BigVy[i] * time * scale + gravOffY;

            float rotRad = data.BigRotSpeed[i] * time * (Easing.PI / 180f);
            float sizeFade = fragT < 0.6f ? 1f : 1f - (fragT - 0.6f) / 0.4f;
            float sz = data.BigSize[i] * scale * sizeFade;
            if (sz < 0.5f) continue;

            int a = (int)(255 * fragAlpha * bri);
            _brush.Color = Color.FromArgb(a,
                Math.Min(255, (int)(baseColor.R * bri)),
                Math.Min(255, (int)(baseColor.G * bri)),
                Math.Min(255, (int)(baseColor.B * bri)));

            float rotCos = (float)Math.Cos(rotRad);
            float rotSin = (float)Math.Sin(rotRad);
            float[] sx = data.BigShapeX[i];
            float[] sy = data.BigShapeY[i];
            int vCount = sx.Length;

            PointF[] pts = data.BigPts[i];

            for (int j = 0; j < vCount; j++)
            {
                pts[j].X = sx[j] * sz * rotCos - sy[j] * sz * rotSin + px;
                pts[j].Y = sx[j] * sz * rotSin + sy[j] * sz * rotCos + py;
            }

            g.FillPolygon(_brush, pts);

            // 边缘高光
            int edgeA = (int)(a * 0.35f);
            _edgePen.Color = Color.FromArgb(edgeA, Color.White);
            _edgePen.Width = 0.8f * scale;
            g.DrawPolygon(_edgePen, pts);
        }

        // ---- 小碎屑 ----
        for (int i = 0; i < SmallFragCount; i++)
        {
            float bri = data.SmBri[i];
            float smAlpha = fragT < 0.4f ? 1f : 1f - (fragT - 0.4f) / 0.6f;
            if (smAlpha <= 0f) continue;

            float px = cx + data.SmVx[i] * time * scale + gravOffX;
            float py = cy + data.SmVy[i] * time * scale + gravOffY;

            float sz = data.SmSize[i] * scale * smAlpha;
            if (sz < 0.3f) continue;

            int a = (int)(255 * smAlpha * fragAlpha * bri);
            _brush.Color = Color.FromArgb(a,
                Math.Min(255, (int)(baseColor.R * bri)),
                Math.Min(255, (int)(baseColor.G * bri)),
                Math.Min(255, (int)(baseColor.B * bri)));

            g.FillEllipse(_brush, px - sz, py - sz, sz * 2, sz * 2);
        }
    }
}

// ==================== 效果：流星 ====================

class MeteorEffect : IClickEffect
{
    public string Name { get { return "流星"; } }
    public int Duration { get { return 400; } }

    const float StartDist = 65f;
    const int TrailSegments = 12;
    const float TrailDuration = 0.2f;  // 拖尾覆盖的时间跨度
    const int SparkCount = 5;

    class Data
    {
        // 轨迹控制点（贝塞尔）
        public float P0X, P0Y;  // 起点
        public float P1X, P1Y;  // 控制点
        public float P2X, P2Y;  // 终点（点击处）
        // 头部
        public float HeadSize;
        // 尾部粒子（预计算 cos/sin，避免每帧 trig）
        public float[] SparkCos;
        public float[] SparkSin;
        public float[] SparkSize;
    }

    Pen _trailPen = new Pen(Color.Black, 2f);
    Pen _glowPen = new Pen(Color.Black, 4f);
    SolidBrush _headBrush = new SolidBrush(Color.Black);
    SolidBrush _sparkBrush = new SolidBrush(Color.Black);

    public void Cleanup()
    {
        _trailPen.Dispose();
        _glowPen.Dispose();
        _headBrush.Dispose();
        _sparkBrush.Dispose();
    }

    // 贝塞尔插值
    static float Bezier(float p0, float p1, float p2, float t)
    {
        float om = 1f - t;
        return om * om * p0 + 2f * om * t * p1 + t * t * p2;
    }

    public void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds)
    {
        int cx = anim.Position.X - screenBounds.X;
        int cy = anim.Position.Y - screenBounds.Y;
        float scale = anim.Scale;

        Color baseColor = anim.GetColor(color);

        var data = anim.EffectData as Data;
        if (data == null)
        {
            var rng = new Random(unchecked(anim.RandomSeed ^ 1618));
            float angle = (float)(rng.NextDouble() * Easing.PI * 2);
            float cosA = (float)Math.Cos(angle);
            float sinA = (float)Math.Sin(angle);
            float cosP = (float)Math.Cos(angle + Easing.PI / 2f);
            float sinP = (float)Math.Sin(angle + Easing.PI / 2f);

            float startX = cosA * StartDist;
            float startY = sinA * StartDist;

            // 控制点：在中点附近偏移，产生弧线
            float curve = (float)((rng.NextDouble() - 0.5) * 2f * 25f);
            float midX = startX * 0.5f + cosP * curve;
            float midY = startY * 0.5f + sinP * curve;

            data = new Data
            {
                P0X = startX, P0Y = startY,
                P1X = midX, P1Y = midY,
                P2X = 0, P2Y = 0,
                HeadSize = 2.5f + (float)(rng.NextDouble() * 1.5f),
                SparkCos = new float[SparkCount],
                SparkSin = new float[SparkCount],
                SparkSize = new float[SparkCount],
            };
            for (int i = 0; i < SparkCount; i++)
            {
                float sparkAngle = (float)(rng.NextDouble() * Easing.PI * 2);
                data.SparkCos[i] = (float)Math.Cos(sparkAngle);
                data.SparkSin[i] = (float)Math.Sin(sparkAngle);
                data.SparkSize[i] = 1f + (float)(rng.NextDouble() * 1.5f);
            }
            anim.EffectData = data;
        }

        float t = anim.Age / (float)Duration;
        float alpha = 1f - Easing.EaseInQuad(t);
        if (alpha <= 0f) return;

        // 飞行阶段 0~0.4，命中爆发 0.4~1.0
        float flyT = Math.Min(1f, t / 0.4f);
        float moveT = Easing.EaseInQuad(flyT);

        // 头部当前位置
        float headX = cx + Bezier(data.P0X, data.P1X, data.P2X, moveT) * scale;
        float headY = cy + Bezier(data.P0Y, data.P1Y, data.P2Y, moveT) * scale;

        // ---- 多段渐隐拖尾 ----
        for (int i = 0; i < TrailSegments; i++)
        {
            float segT = moveT - (float)i / TrailSegments * TrailDuration;
            if (segT < 0f) continue;

            float segAlpha = 1f - (float)i / TrailSegments;
            int a = (int)(255 * alpha * segAlpha * segAlpha);
            if (a <= 0) continue;

            float sx = cx + Bezier(data.P0X, data.P1X, data.P2X, segT) * scale;
            float sy = cy + Bezier(data.P0Y, data.P1Y, data.P2Y, segT) * scale;
            float ex = cx + Bezier(data.P0X, data.P1X, data.P2X,
                Math.Max(0f, segT - TrailDuration / TrailSegments)) * scale;
            float ey = cy + Bezier(data.P0Y, data.P1Y, data.P2Y,
                Math.Max(0f, segT - TrailDuration / TrailSegments)) * scale;

            float width = (2.5f - 1.8f * (float)i / TrailSegments) * scale;
            _trailPen.Color = Color.FromArgb(a, baseColor);
            _trailPen.Width = width;
            g.DrawLine(_trailPen, sx, sy, ex, ey);
        }

        // ---- 头部辉光 ----
        if (flyT < 1f)
        {
            float headA = alpha * (1f - flyT * 0.3f);
            int ha = (int)(255 * headA);
            float glowSize = data.HeadSize * 2.5f * scale;
            _glowPen.Color = Color.FromArgb((int)(ha * 0.4f), baseColor);
            _glowPen.Width = glowSize;
            g.DrawLine(_glowPen, headX, headY, headX + 0.1f, headY);

            float hs = data.HeadSize * scale;
            _headBrush.Color = Color.FromArgb(ha, Color.White);
            g.FillEllipse(_headBrush, headX - hs, headY - hs, hs * 2, hs * 2);
            _headBrush.Color = Color.FromArgb(ha, baseColor);
            float inner = hs * 0.6f;
            g.FillEllipse(_headBrush, headX - inner, headY - inner, inner * 2, inner * 2);
        }

        // ---- 命中爆发粒子 ----
        if (flyT >= 1f)
        {
            float burstT = (t - 0.4f) / 0.6f;
            float burstAlpha = 1f - Easing.EaseInQuad(burstT);
            if (burstAlpha > 0f)
            {
                int ba = (int)(255 * burstAlpha * alpha);
                float dist = 20f * scale * Easing.EaseOutQuad(burstT);

                for (int i = 0; i < SparkCount; i++)
                {
                    float spx = cx + data.SparkCos[i] * dist;
                    float spy = cy + data.SparkSin[i] * dist;
                    float sz = data.SparkSize[i] * scale * (1f - burstT);
                    if (sz < 0.3f) continue;

                    _sparkBrush.Color = Color.FromArgb(ba, baseColor);
                    g.FillEllipse(_sparkBrush, spx - sz, spy - sz, sz * 2, sz * 2);
                }

                // 命中闪光圈
                float ringSize = 8f * scale * burstT;
                float ringAlpha = burstAlpha * 0.5f;
                _glowPen.Color = Color.FromArgb((int)(255 * ringAlpha), baseColor);
                _glowPen.Width = 2f * scale * burstAlpha;
                g.DrawEllipse(_glowPen, cx - ringSize, cy - ringSize, ringSize * 2, ringSize * 2);
            }
        }
    }
}

// ==================== 效果：手指 ====================

class FingerEffect : IClickEffect
{
    public string Name { get { return "手指"; } }
    public int Duration { get { return 500; } }

    const float StartDist = 55f;
    const float EmojiSize = 192f;    // 渲染分辨率（高分辨率保证放大不糊）
    const float DisplayScale = 0.15f; // 显示缩放（与 EmojiSize 配合保持原始显示大小）

    class Data
    {
        public float RotationDeg;
    }

    System.Drawing.Imaging.ImageAttributes _imgAttrs;
    System.Drawing.Imaging.ColorMatrix _colorMatrix;
    Font _emojiFont;
    Bitmap _cachedEmojiBmp; // 所有动画共享，避免重复渲染

    public FingerEffect()
    {
        _emojiFont = new Font("Segoe UI Emoji", EmojiSize, GraphicsUnit.Pixel);
    }

    public void Cleanup()
    {
        if (_cachedEmojiBmp != null) { _cachedEmojiBmp.Dispose(); _cachedEmojiBmp = null; }
        if (_imgAttrs != null) { _imgAttrs.Dispose(); _imgAttrs = null; }
        if (_emojiFont != null) { _emojiFont.Dispose(); _emojiFont = null; }
        _colorMatrix = null;
    }

    Bitmap GetEmojiBitmap()
    {
        if (_cachedEmojiBmp != null) return _cachedEmojiBmp;
        string emoji = "👆";
        var measured = TextRenderer.MeasureText(emoji, _emojiFont);
        int w = measured.Width + 4;
        int h = measured.Height + 4;
        if (w < 8) w = 8;
        if (h < 8) h = 8;
        _cachedEmojiBmp = new Bitmap(w, h);
        using (var g = Graphics.FromImage(_cachedEmojiBmp))
        {
            g.Clear(Color.Transparent);
            TextRenderer.DrawText(g, emoji, _emojiFont, new Point(2, 2),
                Color.White, Color.Transparent);
        }
        return _cachedEmojiBmp;
    }

    public void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds)
    {
        int cx = anim.Position.X - screenBounds.X;
        int cy = anim.Position.Y - screenBounds.Y;
        float scale = anim.Scale;
        Color baseColor = anim.GetColor(color);

        var data = anim.EffectData as Data;
        if (data == null)
        {
            var rng = new Random(unchecked(anim.RandomSeed ^ 5937));
            float angle = (float)(rng.NextDouble() * Easing.PI * 2);
            data = new Data
            {
                RotationDeg = angle * 180f / Easing.PI + 90f,
            };
            anim.EffectData = data;
        }

        Bitmap emojiBmp = GetEmojiBitmap();

        float t = anim.Age / (float)Duration;
        float moveT = Math.Min(1f, t / 0.6f);
        float moveEased = Easing.EaseOutBack(moveT);
        // 从 50% 开始淡出，30% 时间内完成
        float fadeT = Math.Max(0f, (t - 0.5f) / 0.3f);
        float alpha = 1f - Easing.EaseInQuad(fadeT);
        if (alpha <= 0f) return;

        float drawW = emojiBmp.Width * scale * DisplayScale;
        float drawH = emojiBmp.Height * scale * DisplayScale;
        float tipOff = drawH * 0.45f;
        float rotRad = data.RotationDeg * Easing.PI / 180f;

        float tipDirX = (float)Math.Sin(rotRad);
        float tipDirY = -(float)Math.Cos(rotRad);

        // 右侧垂直方向偏移（相对手指朝向）
        float perpX = -tipDirY;
        float perpY = tipDirX;
        float endPx = cx - tipDirX * tipOff + perpX * 5f * scale;
        float endPy = cy - tipDirY * tipOff + perpY * 5f * scale;
        float startPx = endPx - tipDirX * StartDist * scale;
        float startPy = endPy - tipDirY * StartDist * scale;

        float px = startPx + (endPx - startPx) * moveEased;
        float py = startPy + (endPy - startPy) * moveEased;

        int a = (int)(255 * alpha);
        if (a <= 0 || emojiBmp == null) return;

        if (_imgAttrs == null) _imgAttrs = new System.Drawing.Imaging.ImageAttributes();
        if (_colorMatrix == null) _colorMatrix = new System.Drawing.Imaging.ColorMatrix();
        _colorMatrix[0, 0] = baseColor.R / 255f;
        _colorMatrix[1, 1] = baseColor.G / 255f;
        _colorMatrix[2, 2] = baseColor.B / 255f;
        _colorMatrix[3, 3] = a / 255f;
        _imgAttrs.SetColorMatrix(_colorMatrix,
            System.Drawing.Imaging.ColorMatrixFlag.Default,
            System.Drawing.Imaging.ColorAdjustType.Bitmap);

        GraphicsState state = g.Save();
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.TranslateTransform(px, py);
        g.RotateTransform(data.RotationDeg);

        int dw = (int)Math.Round(drawW);
        int dh = (int)Math.Round(drawH);
        var destRect = new Rectangle(-dw / 2, -dh / 2, dw, dh);
        g.DrawImage(emojiBmp, destRect,
            0, 0, emojiBmp.Width, emojiBmp.Height,
            GraphicsUnit.Pixel, _imgAttrs);

        g.Restore(state);
    }
}

// ==================== 效果：闪电 ====================

class LightningEffect : IClickEffect
{
    public string Name { get { return "闪电"; } }
    public int Duration { get { return 350; } }

    const int SubdivideLevels = 3;
    const float StartDist = 60f;
    const float OffsetBase = 18f;

    class Data
    {
        public float[] MainX, MainY;
        public int BranchCount;
        public int[] BranchStart;
        public float[][] BranchX, BranchY;
    }

    Pen _glowPen = new Pen(Color.Black, 3f);
    Pen _corePen = new Pen(Color.Black, 1.5f);
    Pen _branchPen = new Pen(Color.Black, 1f);

    public void Cleanup()
    {
        _glowPen.Dispose();
        _corePen.Dispose();
        _branchPen.Dispose();
    }

    // 使用数组替代 List，避免内部扩容和 GC 压力
    static void Subdivide(Random rng, float x1, float y1, float x2, float y2,
                          float offsetX, float offsetY, int levels,
                          float[] xs, float[] ys, ref int curIdx, int maxIdx)
    {
        if (levels <= 0)
        {
            if (curIdx < maxIdx) { xs[curIdx] = x2; ys[curIdx] = y2; curIdx++; }
            return;
        }
        float mx = (x1 + x2) * 0.5f;
        float my = (y1 + y2) * 0.5f;
        float disp = (float)(rng.NextDouble() - 0.5) * 2f;
        mx += offsetX * disp;
        my += offsetY * disp;
        Subdivide(rng, x1, y1, mx, my, offsetX * 0.5f, offsetY * 0.5f, levels - 1, xs, ys, ref curIdx, maxIdx);
        Subdivide(rng, mx, my, x2, y2, offsetX * 0.5f, offsetY * 0.5f, levels - 1, xs, ys, ref curIdx, maxIdx);
    }

    public void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds)
    {
        int cx = anim.Position.X - screenBounds.X;
        int cy = anim.Position.Y - screenBounds.Y;
        float scale = anim.Scale;

        Color baseColor = anim.GetColor(color);

        var data = anim.EffectData as Data;
        if (data == null)
        {
            var rng = new Random(unchecked(anim.RandomSeed ^ 29979));
            data = new Data();

            float angle = (float)(rng.NextDouble() * Math.PI * 2);
            float cosA = (float)Math.Cos(angle);
            float sinA = (float)Math.Sin(angle);
            float cosP = (float)Math.Cos(angle + Math.PI / 2f);
            float sinP = (float)Math.Sin(angle + Math.PI / 2f);

            // 主干：分形细分生成锯齿路径
            int mainCount = 1 << SubdivideLevels; // 8 段
            int mainTotal = mainCount + 1;
            data.MainX = new float[mainTotal];
            data.MainY = new float[mainTotal];
            data.MainX[0] = cosA * StartDist;
            data.MainY[0] = sinA * StartDist;
            int mainIdx = 1;
            Subdivide(rng,
                cosA * StartDist, sinA * StartDist, 0f, 0f,
                cosP * OffsetBase, sinP * OffsetBase,
                SubdivideLevels, data.MainX, data.MainY, ref mainIdx, mainTotal);

            // 分支：2-4 条，每条也用分形细分
            int mainSegCount = data.MainX.Length - 1;
            data.BranchCount = 2 + rng.Next(3);
            data.BranchStart = new int[data.BranchCount];
            data.BranchX = new float[data.BranchCount][];
            data.BranchY = new float[data.BranchCount][];

            for (int b = 0; b < data.BranchCount; b++)
            {
                int startIdx = 2 + rng.Next(mainSegCount - 2);
                data.BranchStart[b] = startIdx;

                // 分支方向：从主干方向偏转 30-80 度
                float branchAngle = angle + (float)((rng.NextDouble() - 0.5) * Math.PI * 0.8f);
                float bCos = (float)Math.Cos(branchAngle);
                float bSin = (float)Math.Sin(branchAngle);
                float bCosP = (float)Math.Cos(branchAngle + Math.PI / 2f);
                float bSinP = (float)Math.Sin(branchAngle + Math.PI / 2f);

                float branchLen = StartDist * (0.25f + (float)(rng.NextDouble() * 0.25f));
                int branchLevels = 2 + rng.Next(2); // 4 或 8 段

                int branchTotal = (1 << branchLevels) + 1;
                data.BranchX[b] = new float[branchTotal];
                data.BranchY[b] = new float[branchTotal];
                data.BranchX[b][0] = data.MainX[startIdx];
                data.BranchY[b][0] = data.MainY[startIdx];
                int bIdx = 1;
                Subdivide(rng,
                    data.MainX[startIdx], data.MainY[startIdx],
                    data.MainX[startIdx] + bCos * branchLen,
                    data.MainY[startIdx] + bSin * branchLen,
                    bCosP * OffsetBase * 0.5f, bSinP * OffsetBase * 0.5f,
                    branchLevels, data.BranchX[b], data.BranchY[b], ref bIdx, branchTotal);
            }

            anim.EffectData = data;
        }

        float t = anim.Age / (float)Duration;
        float alpha = 1f - Easing.EaseInQuad(t);
        if (alpha <= 0f) return;

        // 从远端到近端的渲染进度（前 35% 时间完成展开）
        float reveal = Math.Min(1f, t / 0.35f);
        float revealEased = Easing.EaseOutQuad(reveal);

        int segCount = data.MainX.Length - 1;

        // 击中后闪烁：35%~55% 区间做两次明暗脉冲
        float flicker = 1f;
        if (t >= 0.35f && t < 0.55f)
        {
            float ft = (t - 0.35f) / 0.2f;
            float wave = (float)Math.Sin(ft * Math.PI * 2f);
            flicker = 1f + 0.6f * wave;
        }

        int a = (int)(255 * alpha * flicker);
        int glowA = (int)(80 * alpha * flicker);
        if (a > 255) a = 255;
        if (glowA > 255) glowA = 255;

        // 计算当前闪电尖端位置（用于末端闪光）
        float tipX = 0, tipY = 0;
        {
            int tipSeg = (int)(revealEased * segCount);
            if (tipSeg >= segCount) tipSeg = segCount - 1;
            float segFrac = revealEased * segCount - tipSeg;
            float tx1 = cx + data.MainX[tipSeg] * scale;
            float ty1 = cy + data.MainY[tipSeg] * scale;
            float tx2 = cx + data.MainX[tipSeg + 1] * scale;
            float ty2 = cy + data.MainY[tipSeg + 1] * scale;
            tipX = tx1 + (tx2 - tx1) * segFrac;
            tipY = ty1 + (ty2 - ty1) * segFrac;
        }

        // 主干辉光 + 核心（逐段显现，远端粗近端细）
        for (int i = 0; i < segCount; i++)
        {
            float segFar = (float)i / segCount;
            float segNear = (float)(i + 1) / segCount;
            if (segFar >= revealEased) break;

            float x1 = cx + data.MainX[i] * scale;
            float y1 = cy + data.MainY[i] * scale;
            float x2 = cx + data.MainX[i + 1] * scale;
            float y2 = cy + data.MainY[i + 1] * scale;

            if (segNear > revealEased)
            {
                float segProgress = (revealEased - segFar) / (segNear - segFar);
                x2 = x1 + (x2 - x1) * segProgress;
                y2 = y1 + (y2 - y1) * segProgress;
            }

            float widthT = 1f - (float)i / segCount;
            float glowW = (1.5f + 2.5f * widthT) * scale;
            float coreW = (0.6f + 1.2f * widthT) * scale;

            _glowPen.Color = Color.FromArgb(glowA, baseColor);
            _glowPen.Width = glowW;
            _corePen.Color = Color.FromArgb(a, baseColor);
            _corePen.Width = coreW;

            g.DrawLine(_glowPen, x1, y1, x2, y2);
            g.DrawLine(_corePen, x1, y1, x2, y2);
        }

        // 分支（逐段展开）
        for (int b = 0; b < data.BranchCount; b++)
        {
            float branchRevealPos = (float)data.BranchStart[b] / segCount;
            if (branchRevealPos >= revealEased) continue;

            int branchLen = data.BranchX[b].Length - 1;
            float branchAlpha = Math.Min(1f, (revealEased - branchRevealPos) / 0.2f);
            float parentWidthT = 1f - branchRevealPos;

            // 分支在主干到达后的展开进度
            float branchReveal = Math.Min(1f, (revealEased - branchRevealPos) / (1f - branchRevealPos));
            float branchRevealEased = Easing.EaseOutQuad(branchReveal);

            for (int i = 0; i < branchLen; i++)
            {
                float bSegFar = (float)i / branchLen;
                float bSegNear = (float)(i + 1) / branchLen;
                if (bSegFar >= branchRevealEased) break;

                float x1 = cx + data.BranchX[b][i] * scale;
                float y1 = cy + data.BranchY[b][i] * scale;
                float x2 = cx + data.BranchX[b][i + 1] * scale;
                float y2 = cy + data.BranchY[b][i + 1] * scale;

                if (bSegNear > branchRevealEased)
                {
                    float bp = (branchRevealEased - bSegFar) / (bSegNear - bSegFar);
                    x2 = x1 + (x2 - x1) * bp;
                    y2 = y1 + (y2 - y1) * bp;
                }

                // 分支也从粗到细
                float bWidthT = 1f - (float)i / branchLen;
                _branchPen.Width = (0.4f + 0.6f * bWidthT) * parentWidthT * scale;
                _branchPen.Color = Color.FromArgb((int)(a * 0.7f * branchAlpha), baseColor);

                g.DrawLine(_branchPen, x1, y1, x2, y2);
            }
        }

        // 末端闪光（展开过程中，尖端带亮光）
        if (reveal < 1f)
        {
            float tipFade = 1f - reveal;
            float tipSize = (4f + 3f * tipFade) * scale;
            int tipA = (int)(200 * tipFade * alpha);
            _glowPen.Color = Color.FromArgb(tipA, baseColor);
            _glowPen.Width = tipSize;
            // 画一个小十字闪光
            float cs = 3f * scale * tipFade;
            g.DrawLine(_glowPen, tipX - cs, tipY, tipX + cs, tipY);
            g.DrawLine(_glowPen, tipX, tipY - cs, tipX, tipY + cs);
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
