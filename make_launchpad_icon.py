import os

from PIL import Image, ImageDraw

OUT = r"D:\550c-project\desktop-hub\DesktopHub\launchpad.ico"
S = 256
AMBER = (232, 176, 74, 255)
DIM = (150, 118, 60, 255)
DARK = (10, 10, 12, 255)

img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# rounded dark plate
d.rounded_rectangle([12, 12, S - 12, S - 12], radius=48, fill=DARK, outline=AMBER, width=7)
# inner hairline
d.rounded_rectangle([30, 30, S - 30, S - 30], radius=36, outline=(232, 176, 74, 70), width=2)

# 3x3 grid of tiles = "launchpad"
cell = 34
gap = 12
total = cell * 3 + gap * 2
x0 = (S - total) // 2
y0 = (S - total) // 2 + 6
for r in range(3):
    for c in range(3):
        x = x0 + c * (cell + gap)
        y = y0 + r * (cell + gap)
        fill = AMBER if (r, c) in ((0, 0), (0, 2), (2, 0), (1, 1)) else (232, 176, 74, 60)
        d.rounded_rectangle([x, y, x + cell, y + cell], radius=6,
                            fill=(232, 176, 74, 200) if (r, c) == (1, 1) else (0, 0, 0, 0),
                            outline=AMBER if (r, c) in ((0, 0), (0, 2), (2, 0), (2, 2)) else DIM,
                            width=3)

img.save(OUT, sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
print("saved", OUT, img.size)
img.resize((256, 256)).save(OUT.replace(".ico", ".png"))
