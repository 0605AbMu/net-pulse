# NetPulse

NetPulse — Windows operatsion tizimi uchun zamonaviy tarmoq diagnostikasi, tezlikni tahlil qilish va optimallashtirish vositasi (WPF, .NET 10).

## Imkoniyatlari

- **Tarmoq monitoringi**: Wi-Fi signal kuchi, ulanish turi, IP, shlyuz va DNS sozlamalari.
- **Avtomatik diagnostika**:
  - Wi-Fi kanal to'qnashuvlari (Channel Interference)
  - DNS kechikishi va javob tezligi
  - Wi-Fi adapter drayveri holati
  - Winsock katalogi yaxlitligi
  - TCP Auto-Tuning holati
  - Windows Network Throttling tahlili
  - Quvvat tejash (Power Management) tufayli tezlik tushishini aniqlash
  - Speed Test (yuklab olish tezligi va kechikish)
- **1-bosqichli avtomatik tuzatish (One-Click Repair)**:
  - DNS keshini tozalash (Flush DNS)
  - Winsock & TCP/IP stackini qayta tiklash
  - Tarmoq adapterini qayta yuklash
  - TCP sozlamalarini optimallashtirish
- **Ko'p tillilik**: O'zbekcha, Ruscha va Inglizcha interfeys.

---

## O'rnatish va Yangilash (Setup & Update)

Loyihada **Inno Setup** asosidagi o'rnatuvchi (`NetPulse-Setup.exe`) mavjud.

### 1. Yangi o'rnatish (Clean Setup)
`NetPulse-Setup.exe` faylini ishga tushiring. U dasturni kerakli papkaga (`Program Files/NetPulse`) o'rnatadi va ish stoli hamda bosh menyuga yorliqlar yaratadi.

### 2. Yangilash (Update)
Yangi versiya chiqqanda:
- Yangi `NetPulse-Setup.exe` ni ishga tushirishning o'zi kifoya.
- Inno Setup o'rnatilgan eski versiyani avtomatik aniqlaydi (`AppId` orqali).
- Ishlayotgan dasturni yangilashdan oldin xavfsiz yopadi (`CloseApplications=yes`).
- Barcha eski fayllarni yangi versiya fayllari bilan almashtiradi (`ignoreversion`).
- Foydalanuvchi sozlamalari saqlanib qoladi.

**Fonsiz yangilash (Silent update):**
```powershell
NetPulse-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```

---

## Mahalliy yig'ish (Local Build)

Mahalliy kompyuterda o'rnatuvchini yaratish uchun:

```powershell
.\installer\build.ps1 -Version "1.0.0"
```
*Eslatma: Inno Setup 6 kompyuteringizda o'rnatilgan bo'lishi kerak (`choco install innosetup -y`).*

---

## CI/CD Avtomatlashtirish (GitHub Actions)

Loyiha GitHub Actions orqali avtomatik yig'iladi va tarqatiladi:
- `main` branchga push qilinganda avtomatik tekshiruv va installer yig'iladi.
- Yangi versiya tegi qo'yilganda (masalan, `v1.0.1`) yoki manual trigger qilinganda:
  - .NET 10 ilova `win-x64` self-contained formatda yig'iladi.
  - Inno Setup orqali `NetPulse-Setup.exe` yaratiladi.
  - GitHub Releases sahifasida avtomatik yangi versiya (Release) e'lon qilinadi.
