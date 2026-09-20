# ⚡ NetPulse — Windows Tarmoq Diagnostikasi va Optimizatori

<p align="center">
  <b>Zamonaviy, tezkor va professional Windows tarmoq tahlilchisi hamda 1-tugmali tezlik optimizatori.</b>
  <br />
  <i>100 Mbps Wi-Fi ulanishida real tezlik 5-6 Mbps ga tushib qolishi, yuqori ping va paket yo'qotishlarini bir zumda bartaraf eting.</i>
</p>

---

## 🎯 Asosiy Imkoniyatlar

- **⚡ 1-Tugmali To'liq Optimizatsiya:**
  - **TCP Window Auto-Tuning:** Windows tomonidan sun'iy cheklangan TCP qabul qilish oynasini to'liq kenglikka ochadi (`autotuninglevel=normal`).
  - **Network Throttling Index:** Windows Multimedia tizimining tarmoq paketlarini cheklashini butunlay o'chiradi (`0xFFFFFFFF`).
  - **Wi-Fi Adapter Quvvat Tejash:** Adapterning uyqu rejimiga ketib qolishi va ping sakrashlarini to'xtatadi (`Maximum Performance`).
  - **DNS Keshini Tozalash:** Eski va xato kesh yozuvlarini xavfsiz tozalaydi.

- **🔍 9 Bosqichli Chuqur Diagnostika:**
  1. **Wi-Fi Signal Sifati:** Signal kuchi (%), ulanish turi (2.4 GHz / 5 GHz / 6 GHz) va standart (Wi-Fi 4/5/6/7).
  2. **Quvvat Rejimi:** Adapter energiya tejash holati tekshiruvi.
  3. **TCP Auto-Tuning Holati:** Tizim darajasidagi o'tkazuvchanlik darajasi.
  4. **Network Throttling:** Windows reestridagi cheklov indeksi.
  5. **DNS Serverlar va Kechikish:** DNS serverlar tezligi va domenlarni aniqlash vaqti.
  6. **Winsock & IP Stack:** Tarmoq protokollari staki butunligi.
  7. **Kanal Interferensiyasi:** Qo'shni Wi-Fi tarmoqlari bilan to'qnashuv tahlili.
  8. **Tarmoq Drayveri:** Adapter drayveri versiyasi va chiqarilgan sanasi.
  9. **Real Tezlik Testi:** Link tezligi va real yuklab olish tezligi o'rtasidagi tafovut diagnostikasi.

- **🚀 Mustaqil Tezlik Testi (Speed Test):**
  - Cloudflare CDN tarmog'i orqali multi-stream yuklab olish.
  - Silliq Exponential Moving Average (EMA) o'lchov mexanizmi.
  - Haqiqiy ping kechikishi, yuklangan umumiy hajm va eng yuqori (Peak) tezlik ko'rsatkichlari.

- **🌐 100% Ko'p Tilli Interfeys (Multilingual):**
  - O'zbek tili (Lotin)
  - English
  - Русский

- **🛡️ Smart UAC Elevation:**
  - Skriptlarni xavfsiz Administrator huquqida bajarishda Windows UAC darchasida PowerShell emas, balki to'g'ridan-to'g'ri **NetPulse** nomi ko'rsatiladi.

---

## 💻 Tizim Talablari

- **Operatsion tizim:** Windows 10 (1809+) yoki Windows 11 (x64)
- **Talab qilinadigan dasturlar:** Hech narsa talab qilinmaydi! Reliz varianti **Self-Contained Single-File** bo'lib, .NET runtime dastur ichiga to'liq joylashtirilgan.

---

## 🛠️ Loyihani Qurish va Reliz Yig'ish (Building)

### 1. Ishlab chiquvchi rejimida ishga tushirish (Debug):
```powershell
dotnet build
dotnet run --project NetPulse
```

### 2. Yakuniy Reliz Yig'ish (1-bosishda bitta mustaqil `.exe`):
Loyihaning asosiy papkasida joylashgan `build-release.ps1` skriptini ishga tushiring:
```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1
```
Natijada `dist/` papkasida mustaqil, siqilgan va barcha kerakli kutubxonalarga ega bo'lgan tayyor **`NetPulse-v1.0.0-win-x64.exe`** hosil bo'ladi.

---

## 🚀 CI/CD va Reliz Chiqarish Qoidalari (GitHub Actions)

Loyiha GitHub Actions orqali avtomatlashtirilgan:

1. **`main` branchga push yoki merge qilinganda:**
   - Faqatgina **Build & Validate** bosqichi ishlaydi. Kod .NET 10 da xatosiz yig'ilishi tekshiriladi.
   - Hech qanday Release yoki Inno Setup paketi yaratilmaydi (keraksiz relizlar hosil bo'lmaydi).

2. **Faqatgina versiya tegi (Tag) qo'yilganda (masalan `v1.0.1`):**
   - **Release** bosqichi ishga tushadi.
   - Ham Inno Setup o'rnatuvchisi (**`NetPulse-Setup-v...exe`**), ham dasturning o'zi (**`NetPulse-v...-Portable.exe`**) alohida `.exe` holatida yig'iladi va GitHub Releases sahifasida e'lon qilinadi.

**Yangi reliz chiqarish buyrug'i:**
```bash
git tag v1.0.1
git push origin v1.0.1
```

---

## 📂 Loyiha Strukturasi

```
NetPulse/
├── Assets/
│   └── app.ico                 # Ko'p o'lchamli ilova belgisi (16-256px)
├── Core/
│   ├── AdminHelper.cs          # UAC va jarayonlarni boshqarish
│   ├── AppLogger.cs            # Shartli nosozliklarni qayd qilish
│   ├── DiagnosticEngine.cs     # Diagnostika tekshiruvlarini muvofiqlashtiruvchi
│   ├── NetworkHelper.cs        # WMI, Netsh va soket tarmoq yordamchilari
│   └── RepairEngine.cs         # 1-tugmali optimizatsiya va tiklash mexanizmi
├── Diagnostics/                # 9 ta mustaqil diagnostika modullari
├── Localization/               # O'zbek, Rus va Ingliz tili lug'atlari
├── Models/                     # DTO va tarmoq modellari
├── ViewModels/                 # MVVM ViewModel va boshqaruv logikasi
├── Converters/                 # WPF XAML qiymat konvertorlari
├── App.xaml / MainWindow.xaml  # WPF interfeys va resurslar
├── build-release.ps1           # Relizni avtomatik yig'uvchi skript
└── .gitignore                  # Git uchun e'tiborsiz qoldiriladigan fayllar
```

---

## 📄 Litsenziya

MIT License. Erkin foydalanish, o'zgartirish va tarqatish mumkin.
