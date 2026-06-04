// ClickFX — 动效系统：接口、动画状态、缓动函数、所有效果实现、效果注册表

using System;
using System.Collections.Generic;
using System.Drawing;
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
