# KnitOut Clone

Hyper-casual puzzle: Örgüleri sökerek makaralara sarın ve alanı temizleyin.

## 🛠 Teknoloji Yığını

- **Zenject (DI):** Manager ve Controller yönetimi.
- **UniTask:** Tüm asenkron animasyonlar ve oyun akışı (Coroutine YOK).
- **DOTween Pro:** Makara dönüşü ve UI animasyonları.
- **Dreamteck Splines:** Dinamik ip ve sökülme efekti.

## 🎮 Oyun Mekaniği ve Akış

### 1. Grid ve Fabric Yapısı
- **FabricGridManager:** Üst kısımdaki örgü yapısını `StitchNode` (x,y) verisi olarak tutar.
- **SpoolArea:** Alt kısımdaki makara yerleştirme alanı. (Zaten yapıldı).

### 2. Sökülme Mantığı (Unraveling)
1. Oyuncu veya sistem makarayı slota yerleştirir.
2. Makara rengi ile üstündeki sütunun en altındaki ilmek (Lead Stitch) rengi karşılaştırılır.
3. **Eşleşme varsa:**
   - Makara ile ilmek arasında `Spline` oluşturulur.
   - Makara dönmeye başlar (`DOTween`).
   - İlmekler sırayla `UniTask` gecikmesiyle grid'den silinir ve partikül efektine dönüşür.
   - Makara doluluk oranı artar (Scale veya BlendShape).
4. **Engel:** Eğer sırada farklı renk varsa işlem durur.

### 3. Win / Fail
- **Win:** `FabricGridManager` içindeki aktif node sayısı 0 olduğunda.
- **Fail:** Tüm makara slotları dolu (`SpoolArea`) ve yapılacak hamle kalmadığında.

## Sınıf Mimarisi (Özet)

- `FabricManager` (Zenject Single): Örgü datasını yönetir.
- `SpoolController`: Makaranın kendisi. `WindingTask()` metodunu barındırır.
- `RopeController`: Dreamteck Spline'ı yönetir, ucu hareket ettirir.

## Kurallar

- **Async:** `Winding` işlemi `async UniTask` olarak yazılmalı, `await` ile bitmesi beklenmelidir.
- **Pooling:** Sökülen ilmeklerin efektleri için Object Pooling kullanılmalıdır.