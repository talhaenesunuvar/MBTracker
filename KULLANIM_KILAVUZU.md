# İnternet Sayacı - Kullanma Kılavuzu

## 1. Başlatma

`InternetSayaci.exe` dosyasına çift tıkla. Ekranın sağ alt köşesinde, saatin hemen üstünde küçük koyu bir gösterge belirir. Ayrıca saatin yanındaki tepsi alanına (gerekirse `^` okuna bas) mavi oklu bir simge gelir.

Uygulama aynı anda yalnızca bir kez çalışır; ikinci kez açmaya çalışırsan bir şey olmaz.

## 2. Göstergeyi okumak

```
↓ 1,2 MB/sn        ↑ 34 KB/sn
Bugün 340 MB       Dönem 4,20 GB
```

| Alan | Anlamı |
|---|---|
| `↓` | Şu anki indirme hızı |
| `↑` | Şu anki yükleme hızı |
| Bugün | Gece yarısından beri harcanan toplam veri (indirme + yükleme) |
| Dönem | Kotanın yenilendiği günden beri harcanan toplam veri |

Kota girdiysen sağ altta `4,20 GB / 20 GB` şeklinde görünür ve göstergenin alt kenarında bir doluluk çubuğu çıkar. Çubuk %80'den sonra kırmızıya döner.

**İndirme hızının rengi:**

| Renk | Anlamı |
|---|---|
| Beyaz | Kayda değer bir indirme yok |
| Yeşil | 50 KB/sn üstü |
| Sarı | 1 MB/sn üstü, dikkat |
| Kırmızı | Son 1 dakikada uyarı eşiği aşıldı |

## 3. Fare ile kullanım

- **Sürükle:** Göstergeyi istediğin yere taşı. Konum hatırlanır.
- **Tek tık:** Uygulama bazında kullanım listesini açar.
- **Sağ tık:** Menüyü açar (aşağıda).
- **Tepsi simgesine çift tık:** Göstergeyi gizler / geri getirir. Tam ekran video veya oyunda gösterge rahatsız ederse bunu kullan.

## 4. Kim harcıyor? (uygulama listesi)

Göstergeye tıklayınca açılır.

- **Son 1 dk:** O uygulamanın son bir dakikada harcadığı veri. Liste buna göre sıralıdır, yani en üstteki şu an en çok harcayandır.
- **Bugün:** O uygulamanın bugünkü toplamı.

"Sistem / Windows" satırı, Windows'un belirli bir uygulamaya bağlayamadığı trafiği gösterir (sistem bileşenleri gibi).

Liste Windows'un kendi veri kullanımı kayıtlarından okunur, bu yüzden birkaç saniye geriden gelebilir.

## 5. Bildirimler

**Yoğun kullanım uyarısı:** Son 1 dakikada eşikten (varsayılan 50 MB) fazla veri harcanırsa bildirim gelir ve en çok harcayan uygulamanın adı yazar. Aynı uyarı 5 dakikada en fazla bir kez tekrarlanır. Bildirime tıklarsan uygulama listesi açılır.

**Kota uyarısı:** Aylık kota girdiysen dönem kullanımı %50, %80 ve %100'e ulaştığında birer kez uyarır.

Bildirim gelmiyorsa Windows'ta "Rahatsız etmeyin" modunun kapalı olduğunu kontrol et (Ayarlar → Sistem → Bildirimler).

## 6. Sağ tık menüsü

| Seçenek | Ne yapar |
|---|---|
| Uygulama bazında kullanım | Uygulama listesini açar |
| Göstergeyi gizle / göster | Göstergeyi saklar, sayım arka planda devam eder |
| Ayarlar... | Kota ve uyarı ayarları |
| Windows açılışında başlat | İşaretliyse bilgisayar açılınca kendiliğinden başlar |
| Çıkış | Uygulamayı kapatır, sayım durur |

## 7. Ayarlar

| Ayar | Açıklama |
|---|---|
| Aylık kota (GB) | Tarifendeki internet miktarı. 0 yazarsan kota takibi kapanır. |
| Kotanın yenilendiği gün | Faturanın / paketin yenilendiği ayın günü (ör. 15). |
| 1 dakikada şu kadar MB harcanırsa uyar | Yoğun kullanım uyarısının eşiği. 0 yazarsan bu uyarı kapanır. |

Çok sık uyarı alıyorsan eşiği yükselt (ör. 100), hiçbir şeyi kaçırmak istemiyorsan düşür (ör. 20).

## 8. Bilgisayar açılınca kendiliğinden başlaması

Sağ tık → **Windows açılışında başlat** işaretliyse her oturum açılışında kendiliğinden başlar. Kapatmak için aynı seçeneğe tekrar tıkla.

**Önemli:** Bu ayar `InternetSayaci.exe` dosyasının o anki konumunu kaydeder. Klasörü taşır veya yeniden adlandırırsan açılışta başlamaz. Taşıdıktan sonra uygulamayı yeni yerinden aç, seçeneğin işaretini kaldırıp yeniden koy.

## 9. Bilmen gerekenler

- **Sadece bu bilgisayarı sayar.** Telefonun kendi harcadığı veri dahil değildir; operatörün gösterdiği toplam daha yüksek çıkar.
- **Uygulama kapalıyken sayamaz.** Kapalı olduğu sürede harcanan veri toplamlara eklenmez.
- **Hangi ağda olduğuna bakmaz.** Başka bir Wi-Fi'ye bağlanırsan o kullanım da toplamlara eklenir.
- **İndirmeyi durdurmaz, sadece haber verir.** Durdurmak için listede gördüğün uygulamayı kendin kapatman gerekir.

## 10. Habersiz indirmeleri azaltmak

Telefon hotspot'unu Windows'ta **tarifeli bağlantı** olarak işaretle:

Ayarlar → Ağ ve İnternet → Wi-Fi → bağlı olduğun ağın özellikleri → **Tarifeli bağlantı**'yı aç.

Böylece Windows Update, OneDrive ve Microsoft Store büyük indirmeleri kendiliğinden yapmaz.

## 11. Sorun giderme

| Sorun | Çözüm |
|---|---|
| Gösterge görünmüyor | Tepsi simgesine çift tıkla. Simge de yoksa uygulama çalışmıyordur, exe'yi yeniden aç. |
| Gösterge görev çubuğunun arkasında kalıyor | Bir iki saniye bekle, kendini öne alır. |
| Uygulama listesi boş | Birkaç saniye bekle; liste ilk açılışta gecikebilir. |
| Sayaçları sıfırlamak istiyorum | Uygulamadan çık, `%APPDATA%\InternetSayaci\kullanim.txt` dosyasını sil, yeniden aç. |
| Tüm ayarları sıfırlamak istiyorum | Uygulamadan çık, `%APPDATA%\InternetSayaci` klasörünü sil. |

`%APPDATA%\InternetSayaci` klasörünü açmak için: `Win + R` → bu yolu yapıştır → Enter.

## 12. Kaldırma

1. Sağ tık → **Windows açılışında başlat** işaretini kaldır.
2. Sağ tık → **Çıkış**.
3. Uygulama klasörünü ve `%APPDATA%\InternetSayaci` klasörünü sil.
