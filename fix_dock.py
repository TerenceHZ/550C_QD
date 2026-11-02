import json
import os

base = r"D:\550c-project\desktop-hub\items"
names = [
    "Google Chrome.lnk",
    "Steam.lnk",
    "QQ.lnk",
    "微信.lnk",
    "Telegram.lnk",
    "抖音.lnk",
    "WPS Office.lnk",
    "QQ音乐.lnk",
]
items = [os.path.join(base, n) for n in names if os.path.exists(os.path.join(base, n))]
with open(r"D:\550c-project\desktop-hub\dock.json", "w", encoding="utf-8") as f:
    json.dump(items, f, ensure_ascii=False, indent=2)
print("written", len(items))
for i in items:
    print(" ", i)
