# İnternet Sayacı

Telefon interneti (hotspot) gibi kotalı bağlantılar için küçük bir Windows göstergesi. Bilgisayarın ne kadar veri harcadığını ekranın köşesinde canlı gösterir ve beklenmedik indirmelerde bildirim gönderir.

## Özellikler

- Anlık indirme / yükleme hızı, bugünkü toplam ve dönem toplamı
- 1 dakikada belirlenen eşikten (varsayılan 50 MB) fazla veri harcanırsa bildirim, en çok harcayan uygulamanın adıyla
- Göstergeye tıklayınca uygulama bazında kullanım listesi
- Aylık kota takibi: %50, %80 ve %100'de uyarı
- İsteğe bağlı olarak Windows açılışında başlatma

Yönetici izni gerektirmez. Sadece bu bilgisayarın kullanımını sayar; telefonun kendi harcadığı veri dahil değildir.

## Derleme

Ek kurulum gerekmez, Windows ile gelen C# derleyicisi kullanılır:

```
build.cmd
```

Çıktı: `InternetSayaci.exe`

## Kullanım

- Göstergeyi sürükleyerek taşı, tıklayarak uygulama listesini aç.
- Sağ tık: ayarlar, gizle/göster, açılışta başlat, çıkış.
- Veriler `%APPDATA%\InternetSayaci` klasöründe tutulur.

## İpucu

Hotspot bağlantısını Windows'ta **tarifeli bağlantı** olarak işaretlemek (Ayarlar → Ağ ve İnternet → Wi-Fi → ağ özellikleri) arka plan indirmelerinin çoğunu baştan engeller.
