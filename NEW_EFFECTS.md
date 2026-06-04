# 新动效设计

> 每个动效都有精确的数值参数，可直接映射到代码实现。
> 目标：5 个新动效，视觉上与现有"线条爆发""水波纹"互不雷同，且彼此风格各异。

---

## 1. 火花（Spark）

**概念：** 模拟金属撞击迸射的火花，小粒子从点击点向外喷射，受重力影响抛物线下落，带短拖尾，亮度随机闪烁。

**视觉细节：**
- **粒子数量：** 左键 12 个，右键 8 个（右键更大更亮，粒子更少更有重量感）
- **发射：** 从点击点向四周均匀随机发射，初始速度 80~200 px/s，角度均匀分布（0°~360°）
- **运动：** 每帧应用重力加速度（+400 px/s²），轨迹为抛物线
- **拖尾：** 每个粒子绘制 3~4 段连线，长度为当前速度的 0.03 倍，前段亮后段暗
- **颜色：** 基色由配置决定，粒子亮度在 60%~100% 间随机闪烁（每帧独立随机）
- **大小：** 粒子半径 1.5~2.5px，随机
- **生命周期：** 700ms，最后 200ms 整体线性淡出
- **缓动：** 初始喷射用 `EaseOutQuad` 加速，之后匀速 + 重力

**渲染伪代码：**
```
每个粒子:
  pos += velocity * dt
  velocity.y += gravity * dt
  alpha = (age < 500) ? 1.0 : 1.0 - (age - 500) / 200
  brightness = 0.6 + random() * 0.4
  color = baseColor * brightness * alpha
  draw_circle(pos, radius, color)
  draw_trail(pos - velocity * 0.03, pos, color * 0.5)
```

---

## 2. 星光（Star）

**概念：** 点击处闪现一颗小型星芒，快速膨胀后消散。像相机快门闪光或卡通"叮！"的效果。

**视觉细节：**
- **星芒形状：** 4 角星（十字 + 对角线），共 8 条射线，长短交替（长:短 = 2:1）
- **膨胀阶段（0~200ms）：**
  - 星芒半径从 0 → 25px，`EaseOutBack` 缓动（略微过冲再回弹）
  - 透明度保持 100%
- **消散阶段（200~500ms）：**
  - 星芒半径保持 25px
  - 透明度从 100% → 0%，`EaseInQuad` 缓动（先慢后快消失）
  - 整体旋转 45°（从正十字转到斜十字）
- **中心光点：** 始终在圆心绘制一个半径 3px 的白色高亮圆点，比星芒晚 100ms 开始消散
- **发光层：** 在星芒下方叠加一层同形状但大 1.5 倍、透明度为 30% 的模糊光晕
- **颜色：** 长射线用配置基色，短射线用白色，中心光点白色

**渲染伪代码：**
```
if age < 200:
  radius = 25 * EaseOutBack(age / 200)
  alpha = 1.0
  rotation = 0
else:
  radius = 25
  alpha = 1.0 - EaseInQuad((age - 200) / 300)
  rotation = 45 * ((age - 200) / 300)

// 光晕
draw_star(cx, cy, radius * 1.5, 8, rotation, baseColor * 0.3 * alpha)
// 主体
draw_star(cx, cy, radius, 8, rotation, baseColor * alpha, white * alpha for short rays)
// 中心
draw_circle(cx, cy, 3, white * clamp((age - 100) / 100, 0, 1) * alpha)
```

---

## 3. 磁力线（Magnetic）

**概念：** 模拟磁场线从远处汇聚到点击点，先快速聚拢，到达中心后闪烁一下消散。适合表达"吸引""聚焦"的感觉。

**视觉细节：**
- **线条数量：** 6 条，均匀分布在点击点周围（每 60° 一条）
- **起始位置：** 每条线从距离中心 40px 处开始，方向指向中心
- **线形：** 每条线是三阶贝塞尔曲线，控制点使线条呈微弧形（弧度约 15°），模拟真实磁力线的弯曲感
- **汇聚阶段（0~350ms）：**
  - 线条从起始端向中心延伸，`EaseInQuad` 缓动（先慢后快，模拟加速靠近）
  - 线条长度从 0 → 35px
  - 透明度 100%
- **到达 + 闪烁（350~450ms）：**
  - 线条到达中心，透明度瞬间跳到 100% 再快速衰减
  - 中心出现一个半径 5px 的亮点，脉冲一次（半径 5→8→5）
- **消散（450~600ms）：**
  - 线条从末端开始缩短（像被吸走），同时透明度下降
  - 中心亮点同步淡出
- **颜色：** 线条和中心光点均使用配置基色

**渲染伪代码：**
```
if age < 350:
  t = age / 350
  length = 35 * EaseInQuad(t)
  alpha = 1.0
else if age < 450:
  length = 35
  alpha = 1.0 - (age - 350) / 100
  center_pulse = 5 + 3 * sin((age - 350) / 100 * PI)
  draw_circle(cx, cy, center_pulse, baseColor)
else:
  length = 35 * (1 - (age - 450) / 150)
  alpha = 0.3 * (1 - (age - 450) / 150)

for i in 0..5:
  angle = i * 60°
  start = point_at(cx, cy, angle, 40)
  end = point_at(cx, cy, angle, 40 - length)
  control1 = rotate_towards(start, end, 15°) * 0.3
  control2 = rotate_towards(start, end, -15°) * 0.7
  draw_bezier(start, control1, control2, end, baseColor * alpha, stroke=2)
```

---

## 4. 雷达（Radar）

**概念：** 从点击点发出一圈扇形扫描波，像雷达屏幕的扫描线，扫过 360° 后留下一圈淡痕再消散。

**视觉细节：**
- **扫描波：** 一个从中心向外扩散的圆环 + 圆环前方 60° 的扇形高亮区
- **扩散阶段（0~500ms）：**
  - 圆环半径从 0 → 40px，`EaseOutQuad` 缓动（先快后慢）
  - 圆环线宽 1.5px，透明度 40%（淡痕）
  - 扇形区域：圆环前方 60°，从圆心到圆环边缘，填充透明度 15% 的基色
  - 扇形前缘：一条从圆心出发的射线，线宽 2px，透明度 80%
- **旋转：** 扫描扇形每帧旋转 +6°（即 ~100ms 转一圈），持续旋转
- **消散阶段（500~700ms）：**
  - 圆环和扇形整体线性淡出
  - 圆环半径停止增长
- **尾迹：** 扇形后方 30° 区域内，圆环透明度从 40% 渐变到 0%，形成拖尾效果
- **颜色：** 全部使用配置基色

**渲染伪代码：**
```
progress = age / 500
radius = 40 * EaseOutQuad(min(progress, 1))
angle = (age / 1000) * 360  // 每秒转一圈

if age < 500:
  fade = 1.0
else:
  fade = 1.0 - (age - 500) / 200

// 淡痕圆环
draw_arc(cx, cy, radius, 0, 360, baseColor * 0.4 * fade, stroke=1.5)
// 扇形填充
draw_pie(cx, cy, radius, angle-30, angle+30, baseColor * 0.15 * fade)
// 前缘射线
draw_line(cx, cy, cx + cos(angle)*radius, cy + sin(angle)*radius, baseColor * 0.8 * fade, stroke=2)
```

---

## 5. 花瓣（Petal）

**概念：** 从点击点绽放一朵小型花瓣图案，3~5 片椭圆形花瓣向外展开，旋转散开后消散。优雅、有机的感觉。

**视觉细节：**
- **花瓣数量：** 4 片，均匀分布（每 90° 一片）
- **花瓣形状：** 椭圆，长轴 12px，短轴 5px，长轴指向径向方向
- **展开阶段（0~300ms）：**
  - 花瓣从圆心向外移动，距离 0 → 20px，`EaseOutBack` 缓动（略微过冲）
  - 花瓣同时旋转，每片旋转 +30°（从紧贴到展开）
  - 透明度 100%
  - 花瓣缩放从 0.3 → 1.0
- **保持阶段（300~400ms）：**
  - 花瓣位置和大小保持
  - 整体缓慢旋转 +10°
- **消散阶段（400~650ms）：**
  - 花瓣向外再移动 5px，同时缩小到 0.5x
  - 透明度从 100% → 0%，`EaseInQuad` 缓动
  - 每片花瓣独立旋转 +20°（散开感）
- **中心点：** 全程在圆心绘制半径 2px 的亮点，与花瓣同步消散
- **颜色：** 花瓣使用配置基色，中心点白色

**渲染伪代码：**
```
if age < 300:
  t = age / 300
  dist = 20 * EaseOutBack(t)
  scale = 0.3 + 0.7 * t
  alpha = 1.0
  base_rotation = 30 * t
else if age < 400:
  dist = 20
  scale = 1.0
  alpha = 1.0
  base_rotation = 30 + 10 * ((age - 300) / 100)
else:
  t = (age - 400) / 250
  dist = 20 + 5 * t
  scale = 1.0 - 0.5 * t
  alpha = 1.0 - EaseInQuad(t)
  base_rotation = 40 + 20 * t

for i in 0..3:
  angle = i * 90° + base_rotation
  cx = cx + cos(angle) * dist
  cy = cy + sin(angle) * dist
  draw_ellipse_rotated(cx, cy, 12 * scale, 5 * scale, angle, baseColor * alpha)

dot_alpha = (age < 400) ? 1.0 : 1.0 - EaseInQuad((age - 400) / 250)
draw_circle(cx, cy, 2, white * dot_alpha)
```

---

## 总览对比

| 效果 | 风格 | 持续时间 | 核心元素 | 视觉关键词 |
|------|------|----------|----------|------------|
| 火花 | 硬朗/工业 | 700ms | 抛物线粒子 + 拖尾 | 迸射、重力、闪烁 |
| 星光 | 闪耀/卡通 | 500ms | 四角星芒 + 中心光点 | 闪光、过冲、旋转 |
| 磁力线 | 科技/冷峻 | 600ms | 弧线汇聚 + 脉冲亮点 | 聚焦、弯曲、脉冲 |
| 雷达 | 科幻/电子 | 700ms | 扩散圆环 + 旋转扇形 | 扫描、扩散、拖尾 |
| 花瓣 | 优雅/有机 | 650ms | 椭圆花瓣 + 旋转展开 | 绽放、旋转、柔和 |
