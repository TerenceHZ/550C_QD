import os

from PIL import Image, ImageDraw

OUT = r"D:\550c-project\desktop-hub\assets"
os.makedirs(OUT, exist_ok=True)

S = 256
AMBER = (232, 176, 74, 255)
DIM = (150, 118, 60, 255)
FILL = (14, 12, 9, 255)


def new():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


# ---------- folder ----------
img = new()
d = ImageDraw.Draw(img)
# tab
d.polygon([(28, 62), (100, 62), (116, 82), (28, 82)], fill=FILL, outline=AMBER)
# body
d.polygon([(28, 82), (228, 82), (228, 196), (28, 196)], fill=FILL, outline=AMBER, width=6)
# inner line
d.line([(40, 176), (216, 176)], fill=DIM, width=3)
# little bars
for i in range(3):
    x = 44 + i * 18
    d.rectangle([x, 100, x + 12, 106], fill=AMBER)
img.save(os.path.join(OUT, "folder-550c.png"))

# ---------- generic document ----------
img = new()
d = ImageDraw.Draw(img)
d.polygon([(56, 32), (168, 32), (200, 64), (200, 224), (56, 224)], fill=FILL, outline=AMBER, width=6)
d.line([(168, 32), (168, 64)], fill=AMBER, width=5)
d.line([(168, 64), (200, 64)], fill=AMBER, width=5)
for i in range(4):
    y = 100 + i * 22
    d.line([(76, y), (180, y)], fill=DIM, width=3)
img.save(os.path.join(OUT, "doc-550c.png"))

print("saved folder + doc icons")
