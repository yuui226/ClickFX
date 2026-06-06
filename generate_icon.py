"""Generate ClickFX icon — 3 line burst effect with transparent background."""

import math
from PIL import Image, ImageDraw

# Line burst parameters (from LineBurstEffect)
ANGLES_DEG = [258, 222, 187]  # degrees
ERASE_DIST = 13
LINE_LENGTHS = [10, 11, 10]
LINE_WIDTH = 3
GLOW_WIDTH = 5
GLOW_ALPHA = 80

# Icon rendering settings
SIZE = 256
CENTER_X = SIZE // 2 + 40  # shift right
CENTER_Y = SIZE // 2 + 40  # shift down
SCALE = 5.5  # scale up for icon visibility

# Colors
LINE_COLOR = (80, 140, 255)       # blue, same as tray icon default
GLOW_COLOR = (80, 140, 255)


def draw_line_burst(img):
    """Draw 3 burst lines with glow on a transparent image."""
    draw = ImageDraw.Draw(img, 'RGBA')

    for i in range(3):
        angle_rad = math.radians(ANGLES_DEG[i])
        dx = math.cos(angle_rad)
        dy = math.sin(angle_rad)

        start_dist = ERASE_DIST * SCALE
        end_dist = (ERASE_DIST + LINE_LENGTHS[i] * 1.0) * SCALE

        sx = CENTER_X + dx * start_dist
        sy = CENTER_Y + dy * start_dist
        ex = CENTER_X + dx * end_dist
        ey = CENTER_Y + dy * end_dist

        # Glow (wider, semi-transparent)
        draw.line(
            [(sx, sy), (ex, ey)],
            fill=(*GLOW_COLOR, GLOW_ALPHA),
            width=int(GLOW_WIDTH * SCALE),
            joint='curve',
        )
        # Main line
        draw.line(
            [(sx, sy), (ex, ey)],
            fill=(*LINE_COLOR, 255),
            width=int(LINE_WIDTH * SCALE),
            joint='curve',
        )


def make_icon():
    """Generate multi-size ICO file."""
    # Generate at large size then resize
    big = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
    draw_line_burst(big)

    sizes = [256, 128, 64, 48, 32, 16]
    icons = []
    for s in sizes:
        if s == SIZE:
            icons.append(big.copy())
        else:
            icons.append(big.resize((s, s), Image.LANCZOS))

    icons[0].save(
        'icon.ico',
        format='ICO',
        sizes=[(s, s) for s in sizes],
        append_images=icons[1:],
    )
    print(f"Saved icon.ico with sizes: {sizes}")

    # Also save a PNG preview
    big.save('icon_preview.png', 'PNG')
    print("Saved icon_preview.png")


if __name__ == '__main__':
    make_icon()
