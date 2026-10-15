# ============================================================================
#  🖼️  021Cars — PROQRAM İKONU YARADAN SKRİPT ✓✓✓
# ----------------------------------------------------------------------------
#  İSTİFADƏ:   cd Docs\Qurasdirma ; py Ikon-Yarat.py
#              (və ya:  py Ikon-Yarat.py C:\yol\yeni_logo.png)
#
#  NƏ EDİR:
#    ①  Resources\021Cars.png  (mənbə şəkil ✓) oxunur
#    ②  256×256-a böyüdülür ✓ (kiçik şəkil olsa da Windows «böyük ikon»
#        rejimində keyfiyyətli göstərsin ✓)
#    ③  Resources\021Cars.ico  yaradılır — 7 ölçü ✓✓✓
#        16 · 24 · 32 · 48 · 64 · 128 · 256
#
#  ⚠ VACİB: PNG-ı dəyişdikdən sonra MÜTLƏQ bu skripti işlədin ✓ →
#     sonra:  Docs\Qurasdirma\Installer\Yig-Installer.ps1  (yenidən yığ ✓)
#
#  🔗 İkon hər iki .exe-ə .csproj → <ApplicationIcon> vasitəsilə yazılır ✓:
#     · EnterpriseAeroStudio.csproj  → Resources\021Cars.ico
#     · 021Cars_Installer.csproj     → ..\..\..\Resources\021Cars.ico
# ============================================================================

import os
import sys

from PIL import Image

# Kök qovluq = bu fayldan 2 səviyyə yuxarı (Docs\Qurasdirma → Autocode ✓)
KOK = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MENBE = os.path.join(KOK, "Resources", "021Cars.png")
HEDEF = os.path.join(KOK, "Resources", "021Cars.ico")

# Mənbə şəkil arqumentlə verilə bilər ✓ (boş = Resources\021Cars.png ✓)
if len(sys.argv) > 1:
    MENBE = sys.argv[1]

OLCULER = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]

if not os.path.exists(MENBE):
    print("XETA: menbe sekil tapilmadi - " + MENBE)
    sys.exit(1)

sekil = Image.open(MENBE).convert("RGBA")
print("Menbe : " + MENBE + "  (" + str(sekil.size[0]) + "x" + str(sekil.size[1]) + ")")

# 256-dan kiçikdirsə → LANCZOS ilə böyüdülür (əks halda Pillow 256 ölçüsünü atlayır ✗)
kopya = sekil
if sekil.size[0] < 256 or sekil.size[1] < 256:
    kopya = sekil.resize((256, 256), Image.LANCZOS)
    print("Sekil 256x256-a boyuduldu (LANCZOS)")

kopya.save(HEDEF, sizes=OLCULER)

yoxlama = Image.open(HEDEF)
print("Hedef : " + HEDEF)
print("Olculer: " + ", ".join(str(s[0]) for s in sorted(yoxlama.ico.sizes())))
print("OK - ikon hazirdir (indi Yig-Installer.ps1 isledin)")
