# Fernien — Telefon ⇄ Bilgisayar Dosya Aktarımı

USB kablo üzerinden Android telefon ile Windows bilgisayar arasında hızlı, iki yönlü dosya aktarımı yapan
C# WinForms (.NET 10) masaüstü uygulaması.

## Özellikler

- **İki panelli gezgin:** solda bilgisayar, sağda telefon (Windows simgeleri, sıralama, doğal ad sıralaması).
- **İki yönlü aktarım:** "Telefona gönder →" / "← Bilgisayara al" düğmeleri, sağ tık menüleri veya sürükle-bırak
  (Windows Gezgini'nden telefon paneline de sürüklenebilir).
- **Klasör aktarımı:** alt klasörler ve boş klasörler dahil.
- **Anlık cihaz algılama:** telefon takılınca/çıkarılınca liste kendiliğinden güncellenir.
- **Aktarım kuyruğu:** ilerleme çubuğu, anlık hız, kalan süre, iptal. Yarım kalan dosya temizlenir.
- **Çakışma politikası:** aynı adda dosya varsa *Yeniden adlandır* (`ad (1).jpg`), *Üzerine yaz* veya *Atla*.
- Telefonda yeni klasör, yeniden adlandırma (F2), silme (Del), boş alan gösterimi.
- Telefondaki dosyaya çift tıklayınca geçici klasöre indirip varsayılan uygulamayla açar.
- Değiştirilme tarihleri korunur.

## Gereksinimler

1. **adb.exe** (Google *SDK Platform-Tools*). Uygulama sırasıyla şuralara bakar:
   uygulama klasöründeki `platform-tools\adb.exe`, `ANDROID_HOME` / `ANDROID_SDK_ROOT`,
   `%LOCALAPPDATA%\Android\Sdk\platform-tools`, son olarak `PATH`.
2. Telefonda **USB hata ayıklama** açık olmalı:
   Ayarlar › Telefon hakkında › *Yapım numarası*na 7 kez dokunun → Geliştirici seçenekleri › *USB hata ayıklama*.
   İlk bağlantıda telefonda çıkan izin penceresini onaylayın.
3. Bazı markalar (ör. Samsung) için üreticinin Windows USB sürücüsü gerekebilir.

## Tek exe olarak yayınlama

```
dotnet publish -p:PublishProfile=TekExe
```

Çıktı: `publish\fernien.exe` (yaklaşık 54 MB). Visual Studio'da: projeye sağ tık › Yayımla › **TekExe** profili.

Exe tamamen bağımsızdır:
- .NET çalışma zamanı içine gömülüdür; hedef bilgisayarda .NET kurmaya gerek yoktur.
- ADB (`ThirdParty\platform-tools\`: adb.exe, AdbWinApi.dll, AdbWinUsbApi.dll, libwinpthread-1.dll, NOTICE.txt)
  exe'ye gömülüdür. Sistemde adb bulunamazsa ilk çalıştırmada `%LOCALAPPDATA%\Fernien\platform-tools\<sürüm>\`
  altına çıkarılır. Sistemde (ör. Android Studio ile) adb varsa sürüm çakışmasını önlemek için önce o kullanılır.
- ADB'yi güncellemek için `ThirdParty\platform-tools\` içindeki dosyaları yenileriyle değiştirip yeniden yayınlayın.

> **Lisans notu:** Gömülü dosyalar Google'ın Android SDK Platform-Tools paketinden alınmıştır ve
> Android SDK lisans koşullarına tabidir (bkz. `NOTICE.txt`). Exe'yi başkalarına dağıtmadan önce bu
> koşulları kontrol edin.

## Mimari

```
Core/                  Yöntemden bağımsız arayüzler
  Abstractions.cs        IDeviceProvider (aktarım yöntemi), IDeviceSession (dosya işlemleri), DeviceInfo, RemoteEntry
  RemotePath.cs          POSIX yol yardımcıları
Providers/Adb/         USB kablo yöntemi (ADB)
  AdbSocket.cs           ADB sunucusuna (127.0.0.1:5037) bağlantı ve istek çerçeveleme
  AdbClient.cs           host:devices-l, features, transport, shell, track-devices
  AdbSyncService.cs      sync protokolü: STAT/STA2, LIST/LIS2, SEND, RECV (64 KB parçalar)
  AdbServer.cs           adb.exe bulma ve sunucuyu başlatma
  AdbDeviceProvider.cs   cihaz listesi, anlık izleme, bağlantı yardım metinleri
  AdbDeviceSession.cs    havuzlanmış sync bağlantıları, yükleme/indirme, mkdir/rm/mv, depolama bilgisi
Transfer/              Kuyruk (yalnızca IDeviceSession kullanır)
  TransferJob.cs         iş durumu, ilerleme, hız/kalan süre
  TransferQueue.cs       planlama (klasör tarama, çakışma çözümü), sıralı yürütme, iptal
UI/                    Yardımcı arayüz bileşenleri (simgeler, sıralama, giriş penceresi)
MainForm.cs            Ana pencere
```

ADB için harici bir NuGet paketi kullanılmaz; ADB sunucusuyla doğrudan protokol düzeyinde konuşulur.
Böylece aktarım tek bir TCP akışı üzerinden 64 KB'lık parçalarla yapılır, bayt düzeyinde ilerleme alınır ve
çok sayıda küçük dosyada bağlantı tekrar kullanılır. Cihaz destekliyorsa 4 GB üzeri dosya boyutları için
v2 komutları (`LIS2`/`STA2`) kullanılır.

## Yeni bir aktarım yöntemi eklemek (ör. Wi‑Fi)

1. `IDeviceProvider` ve `IDeviceSession` arayüzlerini uygulayan yeni bir sınıf yazın (`Providers/Wifi/...`).
2. `MainForm` yapıcısında `_providers.Add(new WifiDeviceProvider());` satırını ekleyin.

Kuyruk, çakışma yönetimi, ilerleme ve arayüz değişmeden yeni yöntemle çalışır; cihazlar aynı listede görünür.
Not: ADB zaten kablosuz hata ayıklamayı da destekler; `adb pair` / `adb connect` ile eşlenen bir telefon
listede "Ağ" olarak görünür ve kablo gibi kullanılabilir.

## Lisans

Copyright (C) 2026 Fernien katkıda bulunanları

Bu program özgür yazılımdır: Free Software Foundation tarafından yayımlanan **GNU Genel Kamu Lisansı
sürüm 3** (GPLv3) koşulları altında yeniden dağıtabilir ve/veya değiştirebilirsiniz. Program yararlı olması
umuduyla, ancak **HİÇBİR GARANTİ OLMADAN** dağıtılmaktadır. Ayrıntılar için [LICENSE](LICENSE) dosyasına bakın.

Exe'ye gömülü ADB dosyaları bu projenin parçası değildir; Android Open Source Project'e aittir ve kendi
lisansları (Apache 2.0 vb.) geçerlidir. Bkz. `ThirdParty/platform-tools/NOTICE.txt`.

## Bilinen sınırlamalar

- Yalnızca Android. iPhone farklı bir protokol (AFC/libimobiledevice) gerektirir; ayrı bir sağlayıcı olarak eklenebilir.
- Telefon panelinden Windows Gezgini'ne doğrudan sürükleme desteklenmez (bilgisayar paneline sürükleyin).
