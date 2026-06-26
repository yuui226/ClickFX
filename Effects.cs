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
    public int Margin;       // 该动画所需脏矩形半边距(像素,含缩放);0=用默认。供超宽效果(如长文字)上报
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

    public static float EaseInOutCubic(float t)
    {
        if (t < 0.5f) return 4f * t * t * t;
        float u = -2f * t + 2f;
        return 1f - u * u * u * 0.5f;
    }
}

// 各效果的随机参数通过 EffectData 每动画只计算一次（见 AnimationState.EffectData），
// 而非每帧 new Random(seed)。需要时仍创建临时 Random 实例来生成初始值，
// 但生成的结果会缓存，不会跨帧推进 Random 序列。

// ==================== 效果：线条爆发 ====================

class LineBurstEffect : IClickEffect
{
    public string Name { get { return "线条爆发"; } }
    public int Duration { get { return 500; } }

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

        float progress = Math.Min(1f, anim.Age / (float)Duration);
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
            int glowA = Math.Min(255, (int)(a * glowIntensity));
            _glowPen.Color = Color.FromArgb(glowA, baseColor);
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
    const float MaxDist = 36f;
    const float BaseSize = 2.8f;
    const float SizeJitter = 1.8f;
    const float CurveStrength = 6f;

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
            // 全程单段平滑减速地向外飞出，单调不回头(去掉中途停顿再加速的突兀)
            float moveT = Easing.EaseOutQuad(localT);
            float r = dist * moveT;
            // 侧向弧线用 smoothstep(两端速度为 0)，与径向不同曲线 → 平滑弧形且末端不甩动
            float curveT = localT * localT * (3f - 2f * localT);
            float curveAmount = s.Curve * anim.Scale * curveT;

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

            float deltaRot = s.RotSpeed * localT * (float)Math.PI / 180f;

            int a = (int)(255 * alpha);

            _pen.Width = 1.5f * sizeScale;
            _pen.Color = Color.FromArgb((int)(255 * Math.Min(1f, alpha * 1.2f)), baseColor);
            DrawStar(g, px, py, sz, _pen, s.CosDirs, s.SinDirs, deltaRot);

            _brush.Color = Color.FromArgb(a, Color.White);
            float dotR = sz * 0.15f;
            g.FillEllipse(_brush, px - dotR, py - dotR, dotR * 2, dotR * 2);
        }
    }

    // 使用预计算方向向量绘制星星，deltaRot 为相对于 InitRot 的旋转增量（弧度）
    static void DrawStar(Graphics g, float cx, float cy, float radius, Pen pen,
        float[] cosDirs, float[] sinDirs, float deltaRot)
    {
        float cosR = (float)Math.Cos(deltaRot);
        float sinR = (float)Math.Sin(deltaRot);
        float inner = radius * 0.35f;
        for (int i = 0; i < StarPoints; i++)
        {
            int j = (i + 1) % StarPoints;
            float oc0 = cosDirs[i * 2], os0 = sinDirs[i * 2];
            float oc1 = cosDirs[i * 2 + 1], os1 = sinDirs[i * 2 + 1];
            float x1 = cx + (oc0 * cosR - os0 * sinR) * radius;
            float y1 = cy + (oc0 * sinR + os0 * cosR) * radius;
            float x2 = cx + (oc1 * cosR - os1 * sinR) * inner;
            float y2 = cy + (oc1 * sinR + os1 * cosR) * inner;
            g.DrawLine(pen, x1, y1, x2, y2);

            float nc0 = cosDirs[j * 2], ns0 = sinDirs[j * 2];
            float x4 = cx + (nc0 * cosR - ns0 * sinR) * radius;
            float y4 = cy + (nc0 * sinR + ns0 * cosR) * radius;
            g.DrawLine(pen, x2, y2, x4, y4);
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
        for (int i = 0; i < data.PetalCount; i++)
        {
            float angle = baseRotRad + i * angleStep;
            float pcx = cx + (float)Math.Cos(angle) * dist;
            float pcy = cy + (float)Math.Sin(angle) * dist;

            GraphicsState state = g.Save();
            g.TranslateTransform(pcx, pcy);
            g.RotateTransform(angle * 180f / (float)Math.PI);

            g.FillEllipse(_petalBrush, -hl, -hw, hl * 2, hw * 2);
            g.Restore(state);
        }
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
            float baseAngle = data.InitAngles[i];
            float spiralDelta = data.Directions[i] * data.SpiralSpeeds[i];
            float maxR = MaxRadius * scale;
            for (int trail = 3; trail >= 0; trail--)
            {
                float trailT = Math.Max(0f, t - trail * 0.04f);
                float trailAngle = baseAngle + spiralDelta * trailT;
                float trailRadius = maxR * Easing.EaseOutQuad(trailT);

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
        // 拖尾坐标缓存（每动画分配一次，避免每帧 GC）
        public float[] TrailPx;
        public float[] TrailPy;
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
                TrailPx = new float[TrailSegments + 1],
                TrailPy = new float[TrailSegments + 1],
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

        // ---- 多段渐隐拖尾（预计算 Bezier 点，避免每段重复求值） ----
        float segStep = TrailDuration / TrailSegments;
        float[] trailPx = data.TrailPx;
        float[] trailPy = data.TrailPy;
        for (int i = 0; i <= TrailSegments; i++)
        {
            float st = moveT - i * segStep;
            if (st < 0f) st = 0f;
            trailPx[i] = cx + Bezier(data.P0X, data.P1X, data.P2X, st) * scale;
            trailPy[i] = cy + Bezier(data.P0Y, data.P1Y, data.P2Y, st) * scale;
        }
        for (int i = 0; i < TrailSegments; i++)
        {
            float segT = moveT - i * segStep;
            if (segT < 0f) continue;

            float segAlpha = 1f - (float)i / TrailSegments;
            int a = (int)(255 * alpha * segAlpha * segAlpha);
            if (a <= 0) continue;

            float width = (2.5f - 1.8f * (float)i / TrailSegments) * scale;
            _trailPen.Color = Color.FromArgb(a, baseColor);
            _trailPen.Width = width;
            g.DrawLine(_trailPen, trailPx[i], trailPy[i], trailPx[i + 1], trailPy[i + 1]);
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
        public float[] SparkCos, SparkSin, SparkLen; // 击中点向外迸射的火花
    }

    Pen _contourPen = new Pen(Color.Black, 7f); // 深色描边，浅色背景下提供对比
    GraphicsPath _contourPath = new GraphicsPath(); // 整条一次性描边，避免折点叠暗成点
    Pen _glowPen = new Pen(Color.Black, 3f);
    Pen _corePen = new Pen(Color.Black, 1.5f);
    Pen _branchPen = new Pen(Color.Black, 1f);
    SolidBrush _flashBrush = new SolidBrush(Color.Black);

    public LightningEffect()
    {
        // 圆角端点/连接，避免逐段 DrawLine 在折点处留下缺口，使闪电连成一条流光
        _contourPen.StartCap = _contourPen.EndCap = LineCap.Round;
        _glowPen.StartCap = _glowPen.EndCap = LineCap.Round;
        _corePen.StartCap = _corePen.EndCap = LineCap.Round;
        _branchPen.StartCap = _branchPen.EndCap = LineCap.Round;
        _contourPen.LineJoin = _glowPen.LineJoin = _corePen.LineJoin = _branchPen.LineJoin = LineJoin.Round;
    }

    public void Cleanup()
    {
        _contourPen.Dispose();
        _contourPath.Dispose();
        _glowPen.Dispose();
        _corePen.Dispose();
        _branchPen.Dispose();
        _flashBrush.Dispose();
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

            // 击中点火花：随机方向 + 长度，落地瞬间向外迸射
            int sparkN = 6 + rng.Next(4); // 6~9 条
            data.SparkCos = new float[sparkN];
            data.SparkSin = new float[sparkN];
            data.SparkLen = new float[sparkN];
            for (int s = 0; s < sparkN; s++)
            {
                float sa = (float)(rng.NextDouble() * Math.PI * 2);
                data.SparkCos[s] = (float)Math.Cos(sa);
                data.SparkSin[s] = (float)Math.Sin(sa);
                data.SparkLen[s] = 0.6f + (float)(rng.NextDouble() * 0.8f);
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

        // 击中后多次回击闪烁：30%~65% 区间做逐渐衰减的强弱脉冲
        float flicker = 1f;
        if (t >= 0.30f && t < 0.65f)
        {
            float ft = (t - 0.30f) / 0.35f;
            float wave = (float)Math.Sin(ft * Math.PI * 6f); // 3 次回击
            flicker = 1f + 0.55f * wave * (1f - ft);          // 越往后越弱
            if (flicker < 0.45f) flicker = 0.45f;
        }

        // 炽热核心：基色向白偏移，外圈彩色辉光、内芯近白，更像真实电弧
        int hotR = (int)(baseColor.R + (255 - baseColor.R) * 0.72f);
        int hotG = (int)(baseColor.G + (255 - baseColor.G) * 0.72f);
        int hotB = (int)(baseColor.B + (255 - baseColor.B) * 0.72f);

        int a = (int)(255 * alpha * flicker);
        int glowA = (int)(80 * alpha * flicker);
        int contourA = (int)(120 * alpha);   // 深色描边不随回击闪烁，保持稳定对比
        if (a > 255) a = 255;
        if (glowA > 255) glowA = 255;
        if (contourA > 255) contourA = 255;

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

        // 深色描边：整条主干一次性 stroke（单条路径，折点不会因半透明叠加而变暗成点）
        _contourPath.Reset();
        {
            float pmx = cx + data.MainX[0] * scale;
            float pmy = cy + data.MainY[0] * scale;
            for (int i = 0; i < segCount; i++)
            {
                float segFar = (float)i / segCount;
                if (segFar >= revealEased) break;
                float segNear = (float)(i + 1) / segCount;
                float nx = cx + data.MainX[i + 1] * scale;
                float ny = cy + data.MainY[i + 1] * scale;
                float ex = nx, ey = ny;
                if (segNear > revealEased)
                {
                    float sp = (revealEased - segFar) / (segNear - segFar);
                    ex = pmx + (nx - pmx) * sp;
                    ey = pmy + (ny - pmy) * sp;
                }
                _contourPath.AddLine(pmx, pmy, ex, ey);
                pmx = nx; pmy = ny;
            }
        }
        _contourPen.Color = Color.FromArgb(contourA, 18, 18, 26);
        _contourPen.Width = 2.3f * scale;
        g.DrawPath(_contourPen, _contourPath);

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
            _corePen.Color = Color.FromArgb(a, hotR, hotG, hotB);
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
                    float bDenom = bSegNear - bSegFar;
                    if (bDenom <= 0f) continue;
                    float bp = (branchRevealEased - bSegFar) / bDenom;
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

        // 击中点：落地瞬间迸射火花 + 一个短促的高亮命中点
        if (t >= 0.26f && t < 0.60f)
        {
            float it = (t - 0.26f) / 0.34f;        // 0~1 迸射进度
            float spread = Easing.EaseOutQuad(it);
            float fade = 1f - it;
            float dist = (4f + 13f * spread) * scale;

            // 火花从命中点向外飞射、逐渐拉长变细并淡出
            float sw = (0.4f + 1.3f * fade) * scale;
            int sa = (int)(235 * alpha * fade * flicker);
            if (sa > 255) sa = 255;
            for (int s = 0; s < data.SparkLen.Length; s++)
            {
                float reach = dist * data.SparkLen[s];
                float x1 = cx + data.SparkCos[s] * reach * 0.5f;
                float y1 = cy + data.SparkSin[s] * reach * 0.5f;
                float x2 = cx + data.SparkCos[s] * reach;
                float y2 = cy + data.SparkSin[s] * reach;
                _corePen.Color = Color.FromArgb(sa, hotR, hotG, hotB);
                _corePen.Width = sw;
                g.DrawLine(_corePen, x1, y1, x2, y2);
            }

            // 命中点的高亮，极快收缩消失（锚定“击中这里”）
            float hit = (1f - Math.Min(1f, it / 0.4f));
            if (hit > 0.01f)
            {
                float r = (4.5f * hit + 1f) * scale;
                int ha = (int)(220 * alpha * hit * flicker);
                if (ha > 255) ha = 255;
                _flashBrush.Color = Color.FromArgb(ha, hotR, hotG, hotB);
                g.FillEllipse(_flashBrush, cx - r, cy - r, r * 2f, r * 2f);
            }
        }
    }
}

// ==================== 效果：爱心 ====================

class HeartEffect : IClickEffect
{
    public string Name { get { return "爱心"; } }
    public int Duration { get { return 600; } }

    const int Segments = 24;       // 爱心多边形采样点数
    const int MinHearts = 3;       // 3~4 个(减少重叠)
    const float MaxDist = 24f;     // 向外弹出距离(加大，让爱心分得更开)
    const float RiseDist = 16f;    // 额外上浮距离
    const float BaseSize = 7f;     // 爱心基础半径
    const float SizeJitter = 2f;

    // 单位爱心多边形(以原点为中心，尖端朝下，外接半径约 1)，静态预计算一次
    static readonly float[] HeartX = new float[Segments];
    static readonly float[] HeartY = new float[Segments];

    static HeartEffect()
    {
        var mx = new float[Segments];
        var my = new float[Segments];
        float maxExt = 0f;
        for (int i = 0; i < Segments; i++)
        {
            double a = i * (Math.PI * 2) / Segments;
            double x = 16.0 * Math.Pow(Math.Sin(a), 3);
            double y = 13.0 * Math.Cos(a) - 5.0 * Math.Cos(2 * a)
                     - 2.0 * Math.Cos(3 * a) - Math.Cos(4 * a);
            mx[i] = (float)x;
            my[i] = -(float)y; // 数学 y 向上 → 屏幕 y 向下，使尖端朝下
            float ext = (float)Math.Sqrt(x * x + y * y);
            if (ext > maxExt) maxExt = ext;
        }
        for (int i = 0; i < Segments; i++)
        {
            HeartX[i] = mx[i] / maxExt;
            HeartY[i] = my[i] / maxExt;
        }
    }

    class Particle
    {
        public float DirX, DirY;   // 弹出方向(偏上)
        public float Dist;         // 弹出距离系数
        public float Size;
        public float Bri;
        public float InitRot;      // 初始倾斜(弧度)
        public float WobbleAmp;    // 摇摆幅度
        public float WobbleSpeed;
        public float Delay;
    }

    class Data { public Particle[] Hearts; }

    SolidBrush _brush = new SolidBrush(Color.Black);
    Pen _edgePen = new Pen(Color.Black, 1f);
    readonly PointF[] _pts = new PointF[Segments]; // 复用顶点缓冲(仅 UI 线程，串行)

    public void Cleanup()
    {
        _brush.Dispose();
        _edgePen.Dispose();
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
            var rng = new Random(unchecked(anim.RandomSeed ^ 0x48454152));
            int count = MinHearts + rng.Next(2); // 3~4
            var hearts = new Particle[count];
            // 方向均匀分布在上方扇区 [-165°, -15°]，每个爱心占一个扇格内随机，
            // 避免方向扎堆导致重叠(屏幕 y 向下，向上即 -y)
            const double arcStartDeg = -165.0;
            const double arcSpanDeg = 150.0;
            double stepDeg = arcSpanDeg / count;
            for (int i = 0; i < count; i++)
            {
                double deg = arcStartDeg + stepDeg * (i + 0.15 + rng.NextDouble() * 0.7);
                double ang = deg * Math.PI / 180.0;
                hearts[i] = new Particle
                {
                    DirX = (float)Math.Cos(ang),
                    DirY = (float)Math.Sin(ang),
                    Dist = 0.75f + (float)(rng.NextDouble() * 0.5f),
                    Size = BaseSize + (float)(rng.NextDouble() * SizeJitter),
                    Bri = 0.8f + (float)(rng.NextDouble() * 0.2f),
                    InitRot = (float)((rng.NextDouble() - 0.5) * 0.5),
                    WobbleAmp = 0.15f + (float)(rng.NextDouble() * 0.2f),
                    WobbleSpeed = 1.5f + (float)(rng.NextDouble() * 1.5f),
                    Delay = (float)(rng.NextDouble() * 0.1f),
                };
            }
            data = new Data { Hearts = hearts };
            anim.EffectData = data;
        }

        for (int i = 0; i < data.Hearts.Length; i++)
        {
            var h = data.Hearts[i];

            float lt = (t - h.Delay) / (1f - h.Delay);
            if (lt <= 0f || lt >= 1f) continue;

            // 向外弹出 + 越往后越快地上浮
            float outward = Easing.EaseOutQuad(lt) * MaxDist * h.Dist * scale;
            float rise = Easing.EaseInQuad(lt) * RiseDist * scale;
            float px = cx + h.DirX * outward;
            float py = cy + h.DirY * outward - rise;

            // 出现弹跳 + 后段缩小
            float pop = Easing.EaseOutBack(Math.Min(1f, lt * 3.5f));
            float shrink = lt < 0.7f ? 1f : 1f - (lt - 0.7f) / 0.3f;
            float sz = h.Size * scale * pop * shrink;
            if (sz < 0.4f) continue;

            // 淡入淡出
            float fadeIn = Math.Min(1f, lt * 5f);
            float fadeOut = 1f - Easing.EaseInQuad(Math.Max(0f, lt - 0.5f) / 0.5f);
            float alpha = fadeIn * fadeOut;
            if (alpha <= 0.01f) continue;

            // 摇摆旋转
            float rot = h.InitRot + h.WobbleAmp
                      * (float)Math.Sin(lt * h.WobbleSpeed * Math.PI * 2);
            float cosR = (float)Math.Cos(rot);
            float sinR = (float)Math.Sin(rot);

            int a = (int)(255 * alpha);
            _brush.Color = Color.FromArgb(a,
                Math.Min(255, (int)(baseColor.R * h.Bri)),
                Math.Min(255, (int)(baseColor.G * h.Bri)),
                Math.Min(255, (int)(baseColor.B * h.Bri)));

            for (int j = 0; j < Segments; j++)
            {
                float ux = HeartX[j] * sz;
                float uy = HeartY[j] * sz;
                _pts[j].X = ux * cosR - uy * sinR + px;
                _pts[j].Y = ux * sinR + uy * cosR + py;
            }

            g.FillPolygon(_brush, _pts);

            // 边缘淡白高光
            int edgeA = (int)(a * 0.3f);
            if (edgeA > 0)
            {
                _edgePen.Color = Color.FromArgb(edgeA, Color.White);
                _edgePen.Width = 0.8f * scale;
                g.DrawPolygon(_edgePen, _pts);
            }
        }
    }
}

// ==================== 效果：雪花 ====================

class SnowflakeEffect : IClickEffect
{
    public string Name { get { return "雪花"; } }
    public int Duration { get { return 700; } }

    const int Arms = 6;            // 六角
    const float BranchDeg = 32f;   // 分叉角度
    const int MinFlakes = 3;       // 3~5 片
    const float SpreadX = 18f;     // 初始水平散布(避免起始重叠)
    const float SpreadY = 16f;     // 初始纵向散布(避免从同一水平线起落)
    const float FallDist = 46f;    // 下落距离
    const float BaseSize = 4f;     // 雪花基础半径(小)
    const float SizeJitter = 4f;   // 尺寸抖动，使每片明显不同(4~8)

    // 分叉沿臂的位置与长度
    static readonly float[] BranchFrac = { 0.5f, 0.78f };
    static readonly float[] BranchLen = { 0.34f, 0.22f };

    // 单位雪花的所有线段(旋转 0 时,相对花心),静态预计算一次
    static readonly float[] SX0, SY0, SX1, SY1;
    static readonly int SegCount;

    static SnowflakeEffect()
    {
        int per = 1 + BranchFrac.Length * 2; // 每臂:1 主干 + 每个分叉点左右各一
        SegCount = Arms * per;
        SX0 = new float[SegCount];
        SY0 = new float[SegCount];
        SX1 = new float[SegCount];
        SY1 = new float[SegCount];

        int idx = 0;
        float branchRad = BranchDeg * (float)Math.PI / 180f;
        for (int k = 0; k < Arms; k++)
        {
            float armAng = k * (float)(Math.PI * 2) / Arms;
            float ca = (float)Math.Cos(armAng);
            float sa = (float)Math.Sin(armAng);

            // 主干:花心 → 臂尖
            SX0[idx] = 0; SY0[idx] = 0; SX1[idx] = ca; SY1[idx] = sa; idx++;

            for (int f = 0; f < BranchFrac.Length; f++)
            {
                float bx = ca * BranchFrac[f];
                float by = sa * BranchFrac[f];
                float len = BranchLen[f];
                float lca = (float)Math.Cos(armAng + branchRad);
                float lsa = (float)Math.Sin(armAng + branchRad);
                float rca = (float)Math.Cos(armAng - branchRad);
                float rsa = (float)Math.Sin(armAng - branchRad);
                // 左分叉
                SX0[idx] = bx; SY0[idx] = by; SX1[idx] = bx + lca * len; SY1[idx] = by + lsa * len; idx++;
                // 右分叉
                SX0[idx] = bx; SY0[idx] = by; SX1[idx] = bx + rca * len; SY1[idx] = by + rsa * len; idx++;
            }
        }
    }

    class Particle
    {
        public float InitX, InitY; // 初始偏移
        public float FallFactor;   // 下落速度系数
        public float SwayAmp;      // 左右飘摆幅度
        public float SwayFreq;
        public float SwayPhase;
        public float Size;
        public float Bri;
        public float InitRot;
        public float RotSpeed;     // 缓慢自转(弧度/全程)
        public float Delay;
    }

    class Data { public Particle[] Flakes; }

    Pen _pen = new Pen(Color.Black, 1.2f);
    SolidBrush _dot = new SolidBrush(Color.Black);

    public void Cleanup()
    {
        _pen.Dispose();
        _dot.Dispose();
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
            var rng = new Random(unchecked(anim.RandomSeed ^ 0x534E4F57));
            int count = MinFlakes + rng.Next(3); // 3~5
            var flakes = new Particle[count];
            // 水平方向均匀散布，避免起始位置重叠
            for (int i = 0; i < count; i++)
            {
                float spreadT = (i + 0.2f + (float)rng.NextDouble() * 0.6f) / count; // 0~1
                flakes[i] = new Particle
                {
                    InitX = (spreadT * 2f - 1f) * SpreadX,
                    InitY = (float)((rng.NextDouble() - 0.5) * 2.0) * SpreadY,
                    FallFactor = 0.8f + (float)(rng.NextDouble() * 0.5f),
                    SwayAmp = 3f + (float)(rng.NextDouble() * 2.5f),
                    SwayFreq = 0.5f + (float)(rng.NextDouble() * 0.5f),
                    SwayPhase = (float)(rng.NextDouble() * Math.PI * 2),
                    Size = BaseSize + (float)(rng.NextDouble() * SizeJitter),
                    Bri = 0.8f + (float)(rng.NextDouble() * 0.2f),
                    InitRot = (float)(rng.NextDouble() * Math.PI * 2),
                    RotSpeed = (float)((rng.NextDouble() - 0.5) * 1.2),
                    Delay = (float)(rng.NextDouble() * 0.12f),
                };
            }
            data = new Data { Flakes = flakes };
            anim.EffectData = data;
        }

        for (int i = 0; i < data.Flakes.Length; i++)
        {
            var fl = data.Flakes[i];

            float lt = (t - fl.Delay) / (1f - fl.Delay);
            if (lt <= 0f || lt >= 1f) continue;

            // 匀速下落 + 左右飘摆(飘飘然)
            float fall = lt * FallDist * fl.FallFactor * scale;
            float sway = fl.SwayAmp * (float)Math.Sin(lt * fl.SwayFreq * Math.PI * 2 + fl.SwayPhase) * scale;
            float px = cx + fl.InitX * scale + sway;
            float py = cy + fl.InitY * scale + fall;

            // 出现弹入 + 后段(40%)逐渐变小
            float pop = Easing.EaseOutBack(Math.Min(1f, lt * 4f));
            float shrink = lt < 0.6f ? 1f : 1f - (lt - 0.6f) / 0.4f;
            float sz = fl.Size * scale * pop * shrink;
            if (sz < 0.5f) continue;

            // 淡入 + 后段淡出(与缩小同步，飘着变小消失)
            float fadeIn = Math.Min(1f, lt * 6f);
            float fadeOut = 1f - Easing.EaseInQuad(Math.Max(0f, lt - 0.6f) / 0.4f);
            float alpha = fadeIn * fadeOut;
            if (alpha <= 0.01f) continue;

            float rot = fl.InitRot + fl.RotSpeed * lt;
            float cosR = (float)Math.Cos(rot);
            float sinR = (float)Math.Sin(rot);

            int a = (int)(255 * alpha);
            _pen.Color = Color.FromArgb(a,
                Math.Min(255, (int)(baseColor.R * fl.Bri)),
                Math.Min(255, (int)(baseColor.G * fl.Bri)),
                Math.Min(255, (int)(baseColor.B * fl.Bri)));
            _pen.Width = Math.Max(0.6f, sz * 0.13f);

            for (int s = 0; s < SegCount; s++)
            {
                float x0 = (SX0[s] * cosR - SY0[s] * sinR) * sz + px;
                float y0 = (SX0[s] * sinR + SY0[s] * cosR) * sz + py;
                float x1 = (SX1[s] * cosR - SY1[s] * sinR) * sz + px;
                float y1 = (SX1[s] * sinR + SY1[s] * cosR) * sz + py;
                g.DrawLine(_pen, x0, y0, x1, y1);
            }

            // 花心小亮点
            float dotR = sz * 0.16f;
            if (dotR >= 0.4f)
            {
                _dot.Color = Color.FromArgb(a, Color.White);
                g.FillEllipse(_dot, px - dotR, py - dotR, dotR * 2, dotR * 2);
            }
        }
    }
}

// ==================== 效果：彩屑 ====================

class ConfettiEffect : IClickEffect
{
    public string Name { get { return "彩屑"; } }
    public int Duration { get { return 850; } }

    const int MinPieces = 8;       // 8~12 片
    const float MinSpeed = 58f;    // 初速度(向上弹出)
    const float SpeedJitter = 52f;
    const float PieceW = 2.6f;     // 矩形半宽
    const float PieceH = 1.6f;     // 矩形半高
    const float FadeStart = 0.62f; // 开始淡出的时间点
    const float HueSpread = 55f;   // 每片相对基色的色相偏移范围(±度)

    class Piece
    {
        public float Vx, Vy;             // 初速度分量
        public float Drag;               // 空气阻力系数 k
        public float TermVy;             // 竖直终端速度(向下为正)
        public float SwayAmp, SwayPhase, SwayFreq; // 下落时左右飘动
        public float W, H;               // 半宽/半高
        public int R, G, B;              // 预计算的纸屑颜色
        public float InitRot, RotSpeed;  // 平面旋转
        public float FlipPhase, FlipSpeed; // 翻面(宽度缩放)
    }

    class Data { public Piece[] Pieces; }

    SolidBrush _brush = new SolidBrush(Color.Black);
    readonly PointF[] _quad = new PointF[4]; // 复用四角缓冲(仅 UI 线程，串行)

    public void Cleanup()
    {
        _brush.Dispose();
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
            // 基色转 HSV，按片做色相/明度抖动，生成五彩纸屑(仍锚定用户所选颜色)
            float baseH, baseS, baseV;
            RgbToHsv(baseColor.R, baseColor.G, baseColor.B, out baseH, out baseS, out baseV);

            var rng = new Random(unchecked(anim.RandomSeed ^ 0x434F4E46));
            int count = MinPieces + rng.Next(5); // 8~12
            var pieces = new Piece[count];
            for (int i = 0; i < count; i++)
            {
                // 向上扇区弹出 [-160°, -20°](屏幕 y 向下，向上即 -y)，之后空气阻力 + 飘落
                double ang = (-160.0 + rng.NextDouble() * 140.0) * Math.PI / 180.0;
                float speed = MinSpeed + (float)(rng.NextDouble() * SpeedJitter);

                // 每片独立色相偏移 + 饱和度/明度抖动
                float h = baseH + (float)((rng.NextDouble() - 0.5) * 2.0 * HueSpread);
                if (h < 0f) h += 360f; else if (h >= 360f) h -= 360f;
                float s = Clamp01(baseS * (0.85f + (float)(rng.NextDouble() * 0.3f)));
                float v = Clamp01(baseV * (0.78f + (float)(rng.NextDouble() * 0.32f)));
                int r, gg, b;
                HsvToRgb(h, s, v, out r, out gg, out b);

                pieces[i] = new Piece
                {
                    Vx = (float)Math.Cos(ang) * speed,
                    Vy = (float)Math.Sin(ang) * speed,
                    Drag = 2.4f + (float)(rng.NextDouble() * 1.3f),
                    TermVy = 70f + (float)(rng.NextDouble() * 70f),
                    SwayAmp = 4f + (float)(rng.NextDouble() * 7f),
                    SwayPhase = (float)(rng.NextDouble() * Math.PI * 2),
                    SwayFreq = 4f + (float)(rng.NextDouble() * 5f),
                    W = PieceW * (0.7f + (float)(rng.NextDouble() * 0.6f)),
                    H = PieceH * (0.7f + (float)(rng.NextDouble() * 0.6f)),
                    R = r, G = gg, B = b,
                    InitRot = (float)(rng.NextDouble() * Math.PI * 2),
                    RotSpeed = (float)((rng.NextDouble() - 0.5) * 14.0),
                    FlipPhase = (float)(rng.NextDouble() * Math.PI * 2),
                    FlipSpeed = 1.5f + (float)(rng.NextDouble() * 2.5f),
                };
            }
            data = new Data { Pieces = pieces };
            anim.EffectData = data;
        }

        float time = t * Duration / 1000f; // 秒

        for (int i = 0; i < data.Pieces.Length; i++)
        {
            var p = data.Pieces[i];

            // 空气阻力模型:水平速度衰减为 0，竖直趋于终端速度，纸屑“飘”而非“砸”
            float ex = (float)Math.Exp(-p.Drag * time);
            float inv = (1f - ex) / p.Drag;
            float dispX = p.Vx * inv;
            float dispY = p.TermVy * time + (p.Vy - p.TermVy) * inv;
            // 飘动随阻力生效逐渐显现(初段直线弹出，后段左右摇摆)
            float sway = p.SwayAmp * (1f - ex) * (float)Math.Sin(p.SwayPhase + p.SwayFreq * time);

            float px = cx + (dispX + sway) * scale;
            float py = cy + dispY * scale;

            // 淡入(防硬边) + 后段淡出
            float alpha = Math.Min(1f, t * 10f);
            if (t > FadeStart) alpha *= 1f - (t - FadeStart) / (1f - FadeStart);
            if (alpha <= 0.01f) continue;

            // 翻滚:平面旋转 + 宽度缩放模拟翻面(纸屑转到侧面变窄)
            float rot = p.InitRot + p.RotSpeed * t;
            float flip = (float)Math.Abs(Math.Cos(p.FlipPhase + p.FlipSpeed * t * Math.PI * 2));
            float hw = p.W * (0.2f + 0.8f * flip) * scale;
            float hh = p.H * scale;
            if (hw < 0.3f) hw = 0.3f;

            float cosR = (float)Math.Cos(rot);
            float sinR = (float)Math.Sin(rot);

            int a = (int)(255 * alpha);
            _brush.Color = Color.FromArgb(a, p.R, p.G, p.B);

            // 四角(±hw, ±hh)旋转 + 平移
            float xw = hw * cosR, yw = hw * sinR;
            float xh = hh * sinR, yh = hh * cosR;
            _quad[0].X = px - xw + xh; _quad[0].Y = py - yw - yh;
            _quad[1].X = px + xw + xh; _quad[1].Y = py + yw - yh;
            _quad[2].X = px + xw - xh; _quad[2].Y = py + yw + yh;
            _quad[3].X = px - xw - xh; _quad[3].Y = py - yw + yh;

            g.FillPolygon(_brush, _quad);
        }
    }

    static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

    static void RgbToHsv(int r, int g, int b, out float h, out float s, out float v)
    {
        float rf = r / 255f, gf = g / 255f, bf = b / 255f;
        float max = Math.Max(rf, Math.Max(gf, bf));
        float min = Math.Min(rf, Math.Min(gf, bf));
        float d = max - min;
        v = max;
        s = max <= 0f ? 0f : d / max;
        if (d <= 0f) { h = 0f; return; }
        if (max == rf) h = 60f * (((gf - bf) / d) % 6f);
        else if (max == gf) h = 60f * (((bf - rf) / d) + 2f);
        else h = 60f * (((rf - gf) / d) + 4f);
        if (h < 0f) h += 360f;
    }

    static void HsvToRgb(float h, float s, float v, out int r, out int g, out int b)
    {
        float c = v * s;
        float x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
        float m = v - c;
        float rf, gf, bf;
        if (h < 60f) { rf = c; gf = x; bf = 0f; }
        else if (h < 120f) { rf = x; gf = c; bf = 0f; }
        else if (h < 180f) { rf = 0f; gf = c; bf = x; }
        else if (h < 240f) { rf = 0f; gf = x; bf = c; }
        else if (h < 300f) { rf = x; gf = 0f; bf = c; }
        else { rf = c; gf = 0f; bf = x; }
        r = (int)((rf + m) * 255f + 0.5f);
        g = (int)((gf + m) * 255f + 0.5f);
        b = (int)((bf + m) * 255f + 0.5f);
        if (r > 255) r = 255; if (g > 255) g = 255; if (b > 255) b = 255;
    }
}

// ==================== 效果：流萤 ====================

class FireflyEffect : IClickEffect
{
    public string Name { get { return "流萤"; } }
    public int Duration { get { return 520; } }

    const int MinDots = 5;        // 5~6 颗
    const float FadeOut = 0.58f;  // 开始整体淡出的时间点

    class Dot
    {
        public float StartAngle, StartRadius;
        public float Spin;             // 向心收拢时的旋转量(优雅弧线)
        public float CoreR;            // 核心半径
        public float TwPhase, TwSpeed; // 萤火明灭
    }

    class Data { public Dot[] Dots; }

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
        float scale = anim.Scale;

        Color baseColor = anim.GetColor(color);
        int hotR = (int)(baseColor.R + (255 - baseColor.R) * 0.6f);
        int hotG = (int)(baseColor.G + (255 - baseColor.G) * 0.6f);
        int hotB = (int)(baseColor.B + (255 - baseColor.B) * 0.6f);

        var data = anim.EffectData as Data;
        if (data == null)
        {
            var rng = new Random(unchecked(anim.RandomSeed ^ 0x46495245));
            int count = MinDots + rng.Next(2); // 5~6
            var dots = new Dot[count];
            float a0 = (float)(rng.NextDouble() * Math.PI * 2);
            for (int i = 0; i < count; i++)
            {
                // 均匀铺开 + 轻微抖动，收拢时整齐又不呆板
                dots[i] = new Dot
                {
                    StartAngle = a0 + (float)(i * Math.PI * 2 / count + (rng.NextDouble() - 0.5) * 0.5),
                    StartRadius = 17f + (float)(rng.NextDouble() * 9f),
                    Spin = (float)((0.6 + rng.NextDouble() * 0.6) * (rng.Next(2) == 0 ? 1 : -1)),
                    CoreR = 1.1f + (float)(rng.NextDouble() * 0.8f),
                    TwPhase = (float)(rng.NextDouble() * Math.PI * 2),
                    TwSpeed = 6f + (float)(rng.NextDouble() * 5f),
                };
            }
            data = new Data { Dots = dots };
            anim.EffectData = data;
        }

        // 整体淡入(防硬边) + 后段淡出
        float globalA = Math.Min(1f, t * 6f);
        if (t > FadeOut) globalA *= 1f - (t - FadeOut) / (1f - FadeOut);
        if (globalA <= 0.01f) return;

        float prog = Easing.EaseInOutCubic(t); // 向心收拢进度(中段最快，首尾舒缓)

        for (int i = 0; i < data.Dots.Length; i++)
        {
            var d = data.Dots[i];

            float ang = d.StartAngle + d.Spin * prog;
            float radius = d.StartRadius * (1f - prog) * scale;
            float px = cx + (float)Math.Cos(ang) * radius;
            float py = cy + (float)Math.Sin(ang) * radius;

            // 萤火明灭
            float tw = 0.7f + 0.3f * (float)Math.Sin(d.TwPhase + d.TwSpeed * t);
            float aDot = globalA * tw;

            float cr = d.CoreR * scale;

            // 外层柔光
            float gr = cr * 3.2f;
            _brush.Color = Color.FromArgb((int)(65 * aDot), baseColor);
            g.FillEllipse(_brush, px - gr, py - gr, gr * 2f, gr * 2f);
            // 中层
            float mr = cr * 1.8f;
            _brush.Color = Color.FromArgb((int)(115 * aDot), baseColor);
            g.FillEllipse(_brush, px - mr, py - mr, mr * 2f, mr * 2f);
            // 核心(近白)
            _brush.Color = Color.FromArgb((int)(235 * aDot), hotR, hotG, hotB);
            g.FillEllipse(_brush, px - cr, py - cr, cr * 2f, cr * 2f);
        }

        // 收拢中心：随萤火聚拢渐亮的一点柔光，末段随整体淡出消散
        float centerI = prog * prog * globalA;
        if (centerI > 0.01f)
        {
            float r2 = (3f + 5f * prog) * scale;
            _brush.Color = Color.FromArgb((int)(85 * centerI), baseColor);
            g.FillEllipse(_brush, cx - r2, cy - r2, r2 * 2f, r2 * 2f);

            float r1 = (1.5f + 2f * prog) * scale;
            _brush.Color = Color.FromArgb((int)(200 * centerI), hotR, hotG, hotB);
            g.FillEllipse(_brush, cx - r1, cy - r1, r1 * 2f, r1 * 2f);
        }
    }
}

// ==================== 效果：恭喜发财 ====================

class FireworkEffect : IClickEffect
{
    public string Name { get { return "恭喜发财"; } }
    public int Duration { get { return 1100; } }

    const float FontPx = 22f;      // 基础字号(像素)，再乘 anim.Scale
    const float RiseEnd = 0.30f;   // 升空阶段占比，之后炸开
    const float MaxTiltDeg = 16f;  // 文字倾斜(±)，仅轻微歪头
    const float GravFall = 10f;    // 炸开后火星轻微下坠(围绕文字，不远离)
    const int TrailDots = 5;       // 每颗火星的彗尾采样点数

    class Particle { public float Cos, Sin, Speed, Bri, TwPhase, TwSpeed; }

    class Data
    {
        public float Dx, RiseH;    // 炸点相对点击处的偏移(未缩放)
        public float TiltDeg, SizeMul, AlphaMul; // 文字倾斜/大小/透明度随机
        public Color TextColor;    // 随机配色下与火花不同的文字色
        public Particle[] Parts;
        public string Word;        // 本次随机:「恭喜」或「发财」
        public float Tw, Th;       // 文本尺寸,首次测量后缓存
    }

    Font _font;
    StringFormat _fmt;
    SolidBrush _brush = new SolidBrush(Color.Black);
    Pen _ringPen = new Pen(Color.Black, 1.5f); // 起手点击反馈的小环

    public FireworkEffect()
    {
        // 楷体；缺失时 GDI 会回退到默认字体
        _font = new Font("KaiTi", FontPx, FontStyle.Bold, GraphicsUnit.Pixel);
        _fmt = StringFormat.GenericTypographic;
    }

    public void Cleanup()
    {
        if (_font != null) { _font.Dispose(); _font = null; }
        if (_fmt != null) { _fmt.Dispose(); _fmt = null; }
        _brush.Dispose();
        _ringPen.Dispose();
    }

    public void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds)
    {
        int cx = anim.Position.X - screenBounds.X;
        int cy = anim.Position.Y - screenBounds.Y;
        float scale = anim.Scale;
        Color baseColor = anim.GetColor(color);
        // 火星头部偏白，更像烟花
        int hotR = (int)(baseColor.R + (255 - baseColor.R) * 0.35f);
        int hotG = (int)(baseColor.G + (255 - baseColor.G) * 0.35f);
        int hotB = (int)(baseColor.B + (255 - baseColor.B) * 0.35f);

        var data = anim.EffectData as Data;
        if (data == null)
        {
            var rng = new Random(unchecked(anim.RandomSeed ^ 0x46414341));
            int n = 24 + rng.Next(9); // 24~32 颗火星
            var parts = new Particle[n];
            for (int i = 0; i < n; i++)
            {
                double ang = rng.NextDouble() * Math.PI * 2;
                parts[i] = new Particle
                {
                    Cos = (float)Math.Cos(ang),
                    Sin = (float)Math.Sin(ang),
                    Speed = 16f + (float)(rng.NextDouble() * 22f), // 扩散半径(围绕文字)
                    Bri = 0.7f + (float)(rng.NextDouble() * 0.3f),
                    TwPhase = (float)(rng.NextDouble() * Math.PI * 2),
                    TwSpeed = 8f + (float)(rng.NextDouble() * 9f),  // 闪烁
                };
            }
            data = new Data
            {
                Dx = (float)((rng.NextDouble() - 0.5) * 28.0),    // 升空时的水平随机
                RiseH = 40f + (float)(rng.NextDouble() * 28f),    // 升空高度(40~68)
                TiltDeg = (float)((rng.NextDouble() - 0.5) * 2.0 * MaxTiltDeg),
                SizeMul = 0.8f + (float)(rng.NextDouble() * 0.6f),   // 字号随机(0.8~1.4)
                AlphaMul = 0.55f + (float)(rng.NextDouble() * 0.45f), // 透明度随机(0.55~1.0)
                // 随机配色时，文字取一个与火花明显不同的色相；否则与火花同色
                TextColor = color.RandomColor ? RandomDistinctColor(rng, baseColor) : baseColor,
                Parts = parts,
                Word = rng.Next(2) == 0 ? "恭喜" : "发财",
                Tw = -1f,
            };
            anim.EffectData = data;
        }

        float t = anim.Age / (float)Duration;

        float bx = cx + data.Dx * scale;          // 炸点
        float by = cy - data.RiseH * scale;

        // 起手点击反馈：点击处一圈快速扩散淡出的小环 + 亮芯，先让人感知“点在这里”
        float ct = t / 0.16f; // 前 16% 时间(约 176ms)
        if (ct < 1f)
        {
            float ce = Easing.EaseOutQuad(ct);
            float cFade = 1f - ce;
            float rr = (3f + 9f * ce) * scale;
            _ringPen.Width = 1.5f * scale * cFade + 0.3f;
            _ringPen.Color = Color.FromArgb((int)(200 * cFade), baseColor);
            g.DrawEllipse(_ringPen, cx - rr, cy - rr, rr * 2f, rr * 2f);

            float dr = 2.2f * scale * cFade;
            if (dr > 0.3f)
            {
                _brush.Color = Color.FromArgb((int)(220 * cFade), hotR, hotG, hotB);
                g.FillEllipse(_brush, cx - dr, cy - dr, dr * 2f, dr * 2f);
            }
        }

        if (t < RiseEnd)
        {
            // —— 升空：光点从点击处加速上升、末端减速到炸点，带一截淡尾 ——
            float rt = t / RiseEnd;
            float re = Easing.EaseOutQuad(rt);
            float px = cx + (bx - cx) * re;
            float py = cy + (by - cy) * re;

            for (int k = 4; k >= 1; k--)
            {
                float tp = re - k * 0.1f;
                if (tp <= 0f) continue;
                float txp = cx + (bx - cx) * tp;
                float typ = cy + (by - cy) * tp;
                float tr = (1.4f - k * 0.18f) * scale;
                _brush.Color = Color.FromArgb((int)(70 * (1f - k * 0.2f)), baseColor);
                g.FillEllipse(_brush, txp - tr, typ - tr, tr * 2f, tr * 2f);
            }

            float gr = 3f * scale;
            _brush.Color = Color.FromArgb(90, baseColor);
            g.FillEllipse(_brush, px - gr, py - gr, gr * 2f, gr * 2f);
            float hr = 1.6f * scale;
            _brush.Color = Color.FromArgb(235, hotR, hotG, hotB);
            g.FillEllipse(_brush, px - hr, py - hr, hr * 2f, hr * 2f);
            return;
        }

        // —— 炸开 ——
        float bt = (t - RiseEnd) / (1f - RiseEnd); // 0~1
        float spread = Easing.EaseOutQuad(bt);     // 火星扩散(先快后慢)
        float pFade = 1f - bt * bt;                // 火星淡出(后段才明显减弱，整体更亮更持久)

        // 起爆闪光
        if (bt < 0.22f)
        {
            float fl = 1f - bt / 0.22f;
            float r2 = (4f + 14f * (1f - fl)) * scale;
            _brush.Color = Color.FromArgb((int)(150 * fl * fl), baseColor);
            g.FillEllipse(_brush, bx - r2, by - r2, r2 * 2f, r2 * 2f);
            float r1 = (2f + 5f * (1f - fl)) * scale;
            _brush.Color = Color.FromArgb((int)(220 * fl), hotR, hotG, hotB);
            g.FillEllipse(_brush, bx - r1, by - r1, r1 * 2f, r1 * 2f);
        }

        // 火星：径向飞散 + 轻微下坠，沿轨迹采样若干点成柔和彗尾(头亮尾淡、随重力自然弯曲)
        for (int i = 0; i < data.Parts.Length; i++)
        {
            var p = data.Parts[i];
            // 闪烁，让整片火星像真实烟花一样明灭跳动
            float tw = 0.6f + 0.4f * (float)Math.Sin(p.TwPhase + p.TwSpeed * bt);
            float fade = pFade * p.Bri * tw;
            if (fade <= 0.02f) continue;

            float ps = p.Speed * scale; // 每颗的扩散尺度，外提一次
            for (int j = 0; j < TrailDots; j++)
            {
                // 亮度沿彗尾单调递减：先算便宜的 alpha，过暗就直接收尾，省去坐标运算
                float falloff = 1f - (float)j / TrailDots; // 头部=1，向尾部递减
                int aa = (int)((j == 0 ? 255 : 210) * fade * falloff);
                if (aa <= 1) break;
                float sbt = bt - j * 0.05f;
                if (sbt < 0f) break;

                float sp = Easing.EaseOutQuad(sbt);
                float d = ps * sp;
                float x = bx + p.Cos * d;
                float y = by + p.Sin * d + GravFall * sbt * sbt * scale;
                float r = (0.4f + 1.3f * falloff) * scale;

                if (j == 0) _brush.Color = Color.FromArgb(aa, hotR, hotG, hotB); // 头点近白
                else _brush.Color = Color.FromArgb(aa, baseColor);
                g.FillEllipse(_brush, x - r, y - r, r * 2f, r * 2f);
            }
        }

        // 文字：炸开瞬间弹出「恭喜」或「发财」，随烟花一起淡出
        if (data.Tw < 0f)
        {
            SizeF sz = g.MeasureString(data.Word, _font, new PointF(0f, 0f), _fmt);
            data.Tw = sz.Width;
            data.Th = sz.Height;
        }
        float pop = Easing.EaseOutBack(Math.Min(1f, bt / 0.22f)); // 弹出
        float ta = Math.Min(1f, bt / 0.12f);                       // 快速浮现
        if (bt > 0.6f) ta *= 1f - (bt - 0.6f) / 0.4f;              // 后段淡出
        if (ta > 0.01f)
        {
            _brush.Color = Color.FromArgb((int)(255 * ta * data.AlphaMul), data.TextColor);
            GraphicsState st = g.Save();
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            g.TranslateTransform(bx, by);
            g.RotateTransform(data.TiltDeg);
            float s = scale * data.SizeMul * (0.5f + 0.5f * pop);
            g.ScaleTransform(s, s);
            g.DrawString(data.Word, _font, _brush, -data.Tw / 2f, -data.Th / 2f, _fmt);
            g.Restore(st);
        }
    }

    // 生成一个与 from 色相明显不同(偏移 110~250°)的鲜艳随机色
    static Color RandomDistinctColor(Random rng, Color from)
    {
        float h = Hue(from) + 110f + (float)(rng.NextDouble() * 140.0);
        if (h >= 360f) h -= 360f;
        float s = 0.75f + (float)(rng.NextDouble() * 0.25f);
        float v = 0.85f + (float)(rng.NextDouble() * 0.15f);
        float c = v * s;
        float x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
        float m = v - c;
        float r, g, b;
        if (h < 60f) { r = c; g = x; b = 0f; }
        else if (h < 120f) { r = x; g = c; b = 0f; }
        else if (h < 180f) { r = 0f; g = c; b = x; }
        else if (h < 240f) { r = 0f; g = x; b = c; }
        else if (h < 300f) { r = x; g = 0f; b = c; }
        else { r = c; g = 0f; b = x; }
        return Color.FromArgb(
            (int)((r + m) * 255f + 0.5f),
            (int)((g + m) * 255f + 0.5f),
            (int)((b + m) * 255f + 0.5f));
    }

    static float Hue(Color col)
    {
        float r = col.R / 255f, g = col.G / 255f, b = col.B / 255f;
        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));
        float d = max - min;
        if (d <= 0f) return 0f;
        float h;
        if (max == r) h = 60f * (((g - b) / d) % 6f);
        else if (max == g) h = 60f * (((b - r) / d) + 2f);
        else h = 60f * (((r - g) / d) + 4f);
        if (h < 0f) h += 360f;
        return h;
    }
}

// ==================== 随机文字词库 ====================
// 用户在设置里配置的词库(每行一句)，启动与配置变更时由 OverlayManager 刷新。
// 仅在 UI 线程读写(设置对话框 + OnPaint 同线程)，数组引用整体替换，读取无需加锁。

static class TextPool
{
    static string[] _phrases = new string[0];
    const int MaxLineLen = 30; // 单行最长字数，防止有人贴一整段把文字撑得比屏幕还宽

    // 把多行原文净化成词库：去空行、去首尾空白、超长单行截断
    public static void Load(string raw)
    {
        if (string.IsNullOrEmpty(raw)) { _phrases = new string[0]; return; }
        var list = new List<string>();
        var lines = raw.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        foreach (var line in lines)
        {
            var s = line.Trim();
            if (s.Length == 0) continue;
            if (s.Length > MaxLineLen) s = s.Substring(0, MaxLineLen);
            list.Add(s);
        }
        _phrases = list.ToArray();
    }

    // 按种子稳定选一句(一次动画内不变)；词库为空返回 null
    public static string Pick(int seed)
    {
        var arr = _phrases;
        if (arr.Length == 0) return null;
        int idx = (int)((uint)seed % (uint)arr.Length);
        return arr[idx];
    }
}

// ==================== 效果：随机文字 ====================
// 复用「恭喜发财」的升空→炸开→文字弹出动画；文字随机取自用户词库(TextPool)，
// 烟花扩散范围(火星数量与速度)与文字长度正相关。

class RandomTextEffect : IClickEffect
{
    public string Name { get { return "随机文字"; } }
    public int Duration { get { return 1100; } }

    const float FontPx = 22f;
    const float RiseEnd = 0.30f;
    const float MaxTiltDeg = 12f;
    const float GravFall = 10f;
    const int TrailDots = 5;

    class Particle { public float Cos, Sin, Speed, Bri, TwPhase, TwSpeed; }

    class Data
    {
        public float Dx, RiseH;
        public float TiltDeg, SizeMul, AlphaMul;
        public Color TextColor;
        public Particle[] Parts;
        public string Word;        // 本次随机选中的文字(词库为空时为 "")
        public float Tw, Th;       // 文本尺寸，首次测量后缓存
    }

    Font _font;
    StringFormat _fmt;
    SolidBrush _brush = new SolidBrush(Color.Black);
    Pen _ringPen = new Pen(Color.Black, 1.5f);

    public RandomTextEffect()
    {
        _font = new Font("KaiTi", FontPx, FontStyle.Bold, GraphicsUnit.Pixel);
        _fmt = StringFormat.GenericTypographic;
    }

    public void Cleanup()
    {
        if (_font != null) { _font.Dispose(); _font = null; }
        if (_fmt != null) { _fmt.Dispose(); _fmt = null; }
        _brush.Dispose();
        _ringPen.Dispose();
    }

    public void Draw(Graphics g, AnimationState anim, ColorConfig color, Rectangle screenBounds)
    {
        int cx = anim.Position.X - screenBounds.X;
        int cy = anim.Position.Y - screenBounds.Y;
        float scale = anim.Scale;
        Color baseColor = anim.GetColor(color);
        int hotR = (int)(baseColor.R + (255 - baseColor.R) * 0.35f);
        int hotG = (int)(baseColor.G + (255 - baseColor.G) * 0.35f);
        int hotB = (int)(baseColor.B + (255 - baseColor.B) * 0.35f);

        var data = anim.EffectData as Data;
        if (data == null)
        {
            var rng = new Random(unchecked(anim.RandomSeed ^ 0x52545854)); // 'RTXT'
            string word = TextPool.Pick(anim.RandomSeed) ?? "";
            int chars = word.Length;
            // 长度系数：2 字≈1.0，越长烟花越大(上限 2.4 倍)
            float lenK = Math.Max(0.8f, Math.Min(2.4f, chars * 0.5f));

            int n = 22 + rng.Next(8) + Math.Min(18, chars * 2); // 火星数量随长度增加
            var parts = new Particle[n];
            for (int i = 0; i < n; i++)
            {
                double ang = rng.NextDouble() * Math.PI * 2;
                parts[i] = new Particle
                {
                    Cos = (float)Math.Cos(ang),
                    Sin = (float)Math.Sin(ang),
                    Speed = (15f + (float)(rng.NextDouble() * 20f)) * lenK, // 扩散半径随长度放大
                    Bri = 0.7f + (float)(rng.NextDouble() * 0.3f),
                    TwPhase = (float)(rng.NextDouble() * Math.PI * 2),
                    TwSpeed = 8f + (float)(rng.NextDouble() * 9f),
                };
            }
            data = new Data
            {
                Dx = (float)((rng.NextDouble() - 0.5) * 28.0),
                RiseH = 40f + (float)(rng.NextDouble() * 28f),
                TiltDeg = (float)((rng.NextDouble() - 0.5) * 2.0 * MaxTiltDeg),
                SizeMul = 0.85f + (float)(rng.NextDouble() * 0.4f),
                AlphaMul = 0.7f + (float)(rng.NextDouble() * 0.3f),
                TextColor = color.RandomColor ? RandomDistinctColor(rng, baseColor) : baseColor,
                Parts = parts,
                Word = word,
                Tw = -1f,
            };
            anim.EffectData = data;

            // 上报脏矩形所需半边距：覆盖文字宽高、炸点抬升、火星扩散，避免长文字留残影。
            // 用字数估算文字半宽(charW 取偏大值更保险，宁可多清一点也不留影)。
            const float charW = FontPx * 1.15f;
            float halfTextW = chars * charW * data.SizeMul * scale * 0.5f;
            float halfTextH = FontPx * data.SizeMul * scale * 0.7f;
            float sparkReach = (35f * lenK + 14f) * scale;
            float reach = Math.Max(
                Math.Max(Math.Abs(data.Dx) * scale + halfTextW, data.RiseH * scale + halfTextH),
                sparkReach);
            anim.Margin = (int)(reach + 10f);
        }

        float t = anim.Age / (float)Duration;

        float bx = cx + data.Dx * scale;          // 炸点
        float by = cy - data.RiseH * scale;

        // 让炸点(烟花+文字一起)大致留在屏幕内：用字数粗估文本半宽夹一下，
        // 文字过长则放弃水平夹取(否则会把两侧都顶出去)
        int wordLen = data.Word.Length;
        float halfW = wordLen * FontPx * 0.6f * data.SizeMul * scale * 0.5f;
        float halfH = FontPx * data.SizeMul * scale * 0.6f;
        float W = screenBounds.Width, H = screenBounds.Height;
        if (halfW * 2f + 8f < W)
            bx = Math.Max(halfW + 4f, Math.Min(W - halfW - 4f, bx));
        if (halfH * 2f + 8f < H)
            by = Math.Max(halfH + 4f, Math.Min(H - halfH - 4f, by));

        // 起手点击反馈：点击处一圈快速扩散淡出的小环 + 亮芯
        float ct = t / 0.16f;
        if (ct < 1f)
        {
            float ce = Easing.EaseOutQuad(ct);
            float cFade = 1f - ce;
            float rr = (3f + 9f * ce) * scale;
            _ringPen.Width = 1.5f * scale * cFade + 0.3f;
            _ringPen.Color = Color.FromArgb((int)(200 * cFade), baseColor);
            g.DrawEllipse(_ringPen, cx - rr, cy - rr, rr * 2f, rr * 2f);

            float dr = 2.2f * scale * cFade;
            if (dr > 0.3f)
            {
                _brush.Color = Color.FromArgb((int)(220 * cFade), hotR, hotG, hotB);
                g.FillEllipse(_brush, cx - dr, cy - dr, dr * 2f, dr * 2f);
            }
        }

        if (t < RiseEnd)
        {
            // —— 升空：光点从点击处加速上升、末端减速到炸点，带一截淡尾 ——
            float rt = t / RiseEnd;
            float re = Easing.EaseOutQuad(rt);
            float px = cx + (bx - cx) * re;
            float py = cy + (by - cy) * re;

            for (int k = 4; k >= 1; k--)
            {
                float tp = re - k * 0.1f;
                if (tp <= 0f) continue;
                float txp = cx + (bx - cx) * tp;
                float typ = cy + (by - cy) * tp;
                float tr = (1.4f - k * 0.18f) * scale;
                _brush.Color = Color.FromArgb((int)(70 * (1f - k * 0.2f)), baseColor);
                g.FillEllipse(_brush, txp - tr, typ - tr, tr * 2f, tr * 2f);
            }

            float gr = 3f * scale;
            _brush.Color = Color.FromArgb(90, baseColor);
            g.FillEllipse(_brush, px - gr, py - gr, gr * 2f, gr * 2f);
            float hr = 1.6f * scale;
            _brush.Color = Color.FromArgb(235, hotR, hotG, hotB);
            g.FillEllipse(_brush, px - hr, py - hr, hr * 2f, hr * 2f);
            return;
        }

        // —— 炸开 ——
        float bt = (t - RiseEnd) / (1f - RiseEnd);
        float pFade = 1f - bt * bt;

        // 起爆闪光
        if (bt < 0.22f)
        {
            float fl = 1f - bt / 0.22f;
            float r2 = (4f + 14f * (1f - fl)) * scale;
            _brush.Color = Color.FromArgb((int)(150 * fl * fl), baseColor);
            g.FillEllipse(_brush, bx - r2, by - r2, r2 * 2f, r2 * 2f);
            float r1 = (2f + 5f * (1f - fl)) * scale;
            _brush.Color = Color.FromArgb((int)(220 * fl), hotR, hotG, hotB);
            g.FillEllipse(_brush, bx - r1, by - r1, r1 * 2f, r1 * 2f);
        }

        // 火星：径向飞散 + 轻微下坠，沿轨迹采样若干点成柔和彗尾
        for (int i = 0; i < data.Parts.Length; i++)
        {
            var p = data.Parts[i];
            float tw = 0.6f + 0.4f * (float)Math.Sin(p.TwPhase + p.TwSpeed * bt);
            float fade = pFade * p.Bri * tw;
            if (fade <= 0.02f) continue;

            float ps = p.Speed * scale;
            for (int j = 0; j < TrailDots; j++)
            {
                float falloff = 1f - (float)j / TrailDots;
                int aa = (int)((j == 0 ? 255 : 210) * fade * falloff);
                if (aa <= 1) break;
                float sbt = bt - j * 0.05f;
                if (sbt < 0f) break;

                float sp = Easing.EaseOutQuad(sbt);
                float d = ps * sp;
                float x = bx + p.Cos * d;
                float y = by + p.Sin * d + GravFall * sbt * sbt * scale;
                float r = (0.4f + 1.3f * falloff) * scale;

                if (j == 0) _brush.Color = Color.FromArgb(aa, hotR, hotG, hotB);
                else _brush.Color = Color.FromArgb(aa, baseColor);
                g.FillEllipse(_brush, x - r, y - r, r * 2f, r * 2f);
            }
        }

        // 文字：炸开瞬间弹出随机文字，随烟花一起淡出(词库为空则只放烟花)
        if (data.Word.Length > 0)
        {
            if (data.Tw < 0f)
            {
                SizeF sz = g.MeasureString(data.Word, _font, new PointF(0f, 0f), _fmt);
                data.Tw = sz.Width;
                data.Th = sz.Height;
            }
            float pop = Easing.EaseOutBack(Math.Min(1f, bt / 0.22f));
            float ta = Math.Min(1f, bt / 0.12f);
            if (bt > 0.6f) ta *= 1f - (bt - 0.6f) / 0.4f;
            if (ta > 0.01f)
            {
                _brush.Color = Color.FromArgb((int)(255 * ta * data.AlphaMul), data.TextColor);
                GraphicsState st = g.Save();
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g.TranslateTransform(bx, by);
                g.RotateTransform(data.TiltDeg);
                float s = scale * data.SizeMul * (0.5f + 0.5f * pop);
                g.ScaleTransform(s, s);
                g.DrawString(data.Word, _font, _brush, -data.Tw / 2f, -data.Th / 2f, _fmt);
                g.Restore(st);
            }
        }
    }

    // 生成一个与 from 色相明显不同(偏移 110~250°)的鲜艳随机色
    static Color RandomDistinctColor(Random rng, Color from)
    {
        float h = Hue(from) + 110f + (float)(rng.NextDouble() * 140.0);
        if (h >= 360f) h -= 360f;
        float s = 0.75f + (float)(rng.NextDouble() * 0.25f);
        float v = 0.85f + (float)(rng.NextDouble() * 0.15f);
        float c = v * s;
        float x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
        float m = v - c;
        float r, g, b;
        if (h < 60f) { r = c; g = x; b = 0f; }
        else if (h < 120f) { r = x; g = c; b = 0f; }
        else if (h < 180f) { r = 0f; g = c; b = x; }
        else if (h < 240f) { r = 0f; g = x; b = c; }
        else if (h < 300f) { r = x; g = 0f; b = c; }
        else { r = c; g = 0f; b = x; }
        return Color.FromArgb(
            (int)((r + m) * 255f + 0.5f),
            (int)((g + m) * 255f + 0.5f),
            (int)((b + m) * 255f + 0.5f));
    }

    static float Hue(Color col)
    {
        float r = col.R / 255f, g = col.G / 255f, b = col.B / 255f;
        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));
        float d = max - min;
        if (d <= 0f) return 0f;
        float h;
        if (max == r) h = 60f * (((g - b) / d) % 6f);
        else if (max == g) h = 60f * (((b - r) / d) + 2f);
        else h = 60f * (((r - g) / d) + 4f);
        if (h < 0f) h += 360f;
        return h;
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
        {
            try { kv.Value.Cleanup(); } catch { }
        }
        _effects.Clear();
    }
}
