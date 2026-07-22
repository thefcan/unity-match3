# Unity Gaming Services Kurulumu (login + cloud save + leaderboard)

Oyun **local-first** çalışır: bu adımlar yapılmadan da her şey (menü, seviyeler,
ilerleme) sorunsuz çalışır — sadece bulut senkronu ve skor tablosu "offline"
kalır. Kod tarafı hazır; aşağıdaki adımlar tamamen dashboard/editör tıklamasıdır
ve **ücretsizdir, kredi kartı istemez**.

## 1. Paketlerin gelmesi

`Packages/manifest.json`'a UGS paketleri eklendi. Unity editörüne odaklandığında
otomatik iner (Play modunda olmadığından emin ol). İndikten sonra `Match3.Cloud`
asmdef'i kendiliğinden derlenir (paket yokken `UGS_PACKAGES` define'ı olmadığı
için tamamen derleme dışıdır).

## 2. UGS projesi oluştur ve bağla

1. Unity Editor → **Edit → Project Settings → Services**
2. Unity hesabınla giriş yap (Unity ID — ücretsiz)
3. **Create a Unity Project ID** → organizasyonunu seç → oluştur
   (veya var olan bir cloud projesine bağla)

## 3. Servisleri etkinleştir

[dashboard.unity3d.com](https://dashboard.unity3d.com) → projen:

- **Authentication** → Launch: sağlayıcı eklemeye gerek yok (anonim giriş
  varsayılan çalışır)
- **Cloud Save** → Launch (varsayılan ayarlar yeterli)
- **Leaderboards** → Launch → **Create leaderboard**:
  - ID: `time-attack-score` (birebir bu — CloudSync.LeaderboardId ile eşleşmeli)
  - Sort order: **Descending** (yüksek skor iyi)
  - Update type: **Keep best**
- **Cloud Code** → Launch

## 4. Cloud Code script'ini yayınla

İki yol:

**A) Editörden (önerilen):** Window → **Deployment** penceresi →
`Assets/CloudCode/submit_score.js` görünür → işaretle → **Deploy**.

**B) Dashboard'dan:** Cloud Code → Scripts → Create Script → adı `submit_score`,
parametreler `score: Numeric`, `duration: Numeric` → dosyanın içeriğini yapıştır
→ Publish.

> Script yayınlanana kadar oyun otomatik olarak doğrudan leaderboard yazımına
> düşer (hile koruması olmadan ama çalışır). Yayınlandıktan sonra tüm skorlar
> sunucu doğrulamasından geçer (`ScoreBounds` ile senkron: ≤400 puan/sn,
> ≥5 sn koşu).

## 5. Doğrulama

1. Play moduna gir → Settings panelinde **"Cloud: online (Player xxxxxx)"**
   görünmeli (ilk açılışta 1-2 sn sürebilir; "offline" kalırsa Console'a bak)
2. Bir seviye kazan → dashboard → Cloud Save → Data → oyuncu kaydında
   `progress` anahtarı görünmeli
3. Time Attack oyna → bitir → menüde **RANKS** → skorun listelenmeli
4. Çift cihaz/temiz kurulum testi: ilerleme dosyasını sil
   (`~/Library/Application Support/.../progress.sav`) → oyunu aç → bulut
   ilerlemesi geri gelmeli (max-yıldız birleştirme, asla kayıp yok)

## Mimari notlar

- `Match3.Cloud` asmdef'i `defineConstraints: [UGS_PACKAGES]` taşır — paketler
  yoksa assembly hiç derlenmez; oyun tarafı ona asla referans vermez
  (iletişim `CloudBridge` üzerinden tek yönlüdür)
- Senkron modeli: pull → `ProgressMerger.Merge` (seviye başına max yıldız,
  CRDT-tarzı, kayıpsız) → yerel değiştiyse değiştir → bulut gerideyse push.
  Her seviye kazanımı arka planda push eder. Hiçbir çağrı menüyü/oyunu bekletmez.
- Ücretsiz kota (Temmuz 2026): Auth/Leaderboards ~50k MAU'ya kadar, Cloud
  Save/Cloud Code cömert ücretsiz katman; kart kayıtlı olmadığı için kota aşımı
  FATURA değil SERVİS DURMASI demektir — oyun o durumda da local-first çalışır.
