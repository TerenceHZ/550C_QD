import os

from PIL import Image, ImageDraw

OUT = r"C:\Users\Public\Pictures\550c-icons"
os.makedirs(OUT, exist_ok=True)

AMBER = (232, 176, 74, 255)
DIM = (150, 118, 60, 255)
S = 256


def new_canvas():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


def save(img, name):
    path = os.path.join(OUT, name)
    img.save(path, sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    print("wrote", path)


# ---------- PC (monitor + stand) ----------
img = new_canvas()
d = ImageDraw.Draw(img)
# monitor frame
d.rounded_rectangle([30, 38, 226, 168], radius=10, outline=AMBER, width=10)
# screen inner
d.rectangle([52, 58, 204, 148], outline=DIM, width=3)
# scan lines inside
for y in range(68, 148, 16):
    d.line([(52, y), (204, y)], fill=(232, 176, 74, 70), width=2)
# stand
d.line([(118, 168), (118, 196)], fill=AMBER, width=10)
d.line([(138, 168), (138, 196)], fill=AMBER, width=10)
d.rounded_rectangle([84, 196, 172, 214], radius=4, outline=AMBER, width=8)
# LED
d.ellipse([190, 154, 200, 164], fill=AMBER)
save(img, "pc-550c.ico")

# ---------- Recycle bin (empty) ----------
img = new_canvas()
d = ImageDraw.Draw(img)
# lid
d.rounded_rectangle([64, 42, 192, 66], radius=6, outline=AMBER, width=8)
# handle
d.rounded_rectangle([106, 28, 150, 42], radius=4, outline=DIM, width=6)
# body (slightly tapered)
d.polygon([(76, 74), (180, 74), (168, 220), (88, 220)], outline=AMBER, width=8)
# ribs
for x in (108, 128, 148):
    d.line([(x, 92), (x, 202)], fill=DIM, width=4)
save(img, "recycle-empty-550c.ico")

# ---------- Recycle bin (full) ----------
img = new_canvas()
d = ImageDraw.Draw(img)
d.rounded_rectangle([64, 42, 192, 66], radius=6, outline=AMBER, width=8)
d.rounded_rectangle([106, 28, 150, 42], radius=4, outline=DIM, width=6)
d.polygon([(76, 74), (180, 74), (168, 220), (88, 220)], outline=AMBER, width=8)
for x in (108, 128, 148):
    d.line([(x, 92), (x, 202)], fill=DIM, width=4)
# paper sticking out
d.line([(96, 74), (86, 40)], fill=AMBER, width=6)
d.line([(120, 74), (126, 32)], fill=AMBER, width=6)
d.line([(150, 74), (158, 44)], fill=AMBER, width=6)
save(img, "recycle-full-550c.ico")

print("done")
