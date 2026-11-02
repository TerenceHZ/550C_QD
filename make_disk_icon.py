import os

from PIL import Image, ImageDraw

OUT = r"D:\550c-project\desktop-hub\assets"
os.makedirs(OUT, exist_ok=True)

S = 256
AMBER = (232, 176, 74, 255)
DIM = (150, 118, 60, 255)
FAINT = (232, 176, 74, 60)

img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# drive body
d.rounded_rectangle([26, 52, 230, 204], radius=12, fill=(10, 10, 12, 255),
                    outline=AMBER, width=7)
# top slot
d.rounded_rectangle([44, 74, 212, 96], radius=5, outline=DIM, width=3)
# disk platter hint
d.ellipse([104, 108, 152, 156], outline=DIM, width=3)
d.ellipse([124, 128, 132, 136], fill=AMBER)
# status ladder
for i in range(5):
    x = 52 + i * 12
    d.rectangle([x, 168, x + 7, 176], fill=AMBER if i < 3 else FAINT)
# activity led
d.ellipse([200, 168, 216, 184], fill=AMBER)
# feet
d.rectangle([54, 204, 78, 214], fill=AMBER)
d.rectangle([178, 204, 202, 214], fill=AMBER)

img.save(os.path.join(OUT, "disk-550c.png"))
print("saved", os.path.join(OUT, "disk-550c.png"))
