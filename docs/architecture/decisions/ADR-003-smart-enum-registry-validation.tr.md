# ADR-003 — SmartEnum Registry Doğrulama ve Tekillik (Uniqueness)

| Alan | Değer |
| --- | --- |
| Durum | **Kabul edildi** |
| Tarih | 2026-11-26 |
| Karar verenler | SmartEnum bakımcıları |
| İlişkili | [ADR-002](ADR-002-instance-discovery.tr.md), `docs/design/SOURCE_GENERATOR.tr.md` |
| İngilizce | [ADR-003-smart-enum-registry-validation.md](ADR-003-smart-enum-registry-validation.md) |

## Bağlam

ADR-002, `public static readonly` Smart Enum örneklerinin artımlı Source Generator ve `ISmartEnumGenerated<TSelf>.GetInstances()` ile derleme zamanında keşfini kabul etti.

Keşif tek başına kullanılabilir bir katalog garanti etmez:

- Alan türünde hiç geçerli örnek olmayabilir.
- İki örnek aynı `Name` veya `Value` değerini paylaşabilir.
- Üretilen span’ler güvensiz başlatma senaryolarında teorik olarak null girdi içerebilir.
- Bazı `Name`/`Value` ifadeleri derleme zamanında değerlendirilemez.

Tüketicilerin, ya tamamen doğrulanmış ya da hiç gözlemlenemeyen bir **Registry**’ye ihtiyacı vardır.

## Karar

### Kabul edildi — çift katmanlı doğrulama

1. **Derleme zamanı (Generator)**, değerler güvenilir biçimde sabit katlanabiliyorsa tekillik ve boşluk doğrular.
2. **Çalışma zamanı (Core Registry)**, Generator sabitleri doğrulamış olsa bile, Registry başlatılırken keşfedilen örnek kümesini her zaman yeniden doğrular.

### Kabul edildi — Generator diagnostics (Error şiddeti)

| ID | Anlam |
| --- | --- |
| SMARTENUM003 | Yinelenen `Name` (Ordinal, büyük/küçük harf duyarlı) — derleme zamanında değerlendirilebildiğinde |
| SMARTENUM004 | Yinelenen `Value` — derleme zamanında değerlendirilebildiğinde |
| SMARTENUM005 | Alan türünde geçerli Smart Enum örneği yok |

### Kabul edildi — derleme zamanı değerlendirme kuralları

- Yalnızca Roslyn `SemanticModel.GetConstantValue` (ve eşdeğer sabit katlama) kullanılır.
- Keyfi runtime ifadeleri **uydurulmaz / çalıştırılmaz**.
- `Name` veya `Value` güvenilir değerlendirilemiyorsa, o çift için tekillik diagnostics’i **atlanır**; kontrol Runtime’a bırakılır.
- Derleme zamanı ad karşılaştırması: **Ordinal, case-sensitive**.
- Mümkün olduğunda her iki çakışan bildirim de raporlanır (birincil + additional location).

### Kabul edildi — Runtime Registry doğrulama

Her Registry başlatmasında doğrulanır:

1. Boş örnek koleksiyonu → başarısız.
2. Null örnek girdileri → başarısız.
3. Yinelenen `Name` (`StringComparer.Ordinal`) → başarısız.
4. Yinelenen `Value` (`EqualityComparer<TValue>.Default`) → başarısız.

Kurallar:

- Doğrulama, Registry kamuya açılmadan **önce** tamamlanır.
- Başarısız başlatma, kısmen kurulmuş bir Registry’yi gözlemlenebilir bırakmamalıdır.
- Başlatma **thread-safe** olmalıdır.
- Reflection yok, `ModuleInitializer` yok, türler arası global registry yok, Core runtime paket bağımlılığı yok.

### Kabul edildi — API şekli (ekleyici)

- `SmartEnum<TEnum, TValue>` ve `ISmartEnumGenerated<TSelf>` korunur.
- Doğrulanmış `SmartEnumRegistry<TEnum, TValue>`, `SmartEnum<TEnum, TValue>.Registry` üzerinden açılır.
- Keşif Source Generator + static abstract dispatch (`TEnum.GetInstances()`) olarak kalır.

## Sonuçlar — olumlu

- Sabit bildirimler için build zamanında fail-fast tekillik.
- Sabit olmayan ifadeler ve elle yazılmış `ISmartEnumGenerated` stub’ları için runtime güvenlik ağı.
- Boş alanlar için net Error diagnostics.
- Thread-safe, hep-ya-hiç Registry yayını.

## Sonuçlar — olumsuz / ödünleşimler

- Bazı yinelenmeler yalnızca runtime’da yakalanır (sabit olmayan ifadeler).
- Runtime doğrulama, kapalı alan türü başına ilk Registry erişiminde tek seferlik maliyet ekler.
- Generator, false positive üretmeden her `TValue` ifade şeklini kapsayamaz.

## Değerlendirilen alternatifler

| Alternatif | Sonuç |
| --- | --- |
| Yalnızca derleme zamanı | Reddedildi — runtime hesaplanan ad/değerler için eksik |
| Yalnızca runtime | Reddedildi — sabit yinelenmeler için erken CI geri bildirimi kaybolur |
| Global mutable registry | Reddedildi — ADR-002 ile çelişir |
| Reflection tarama | Reddedildi — ADR-002 ile çelişir |

## Güvenlik etkileri

- Deterministik Ordinal ad karşılaştırması, kültüre bağlı eşitlik sürprizlerini önler.
- Reflection olmaması, keşif/doğrulama yollarında trimmer/AOT uyum yüzeyini küçültür.

## Performans etkileri

- Generator kontrolleri, keşfedilen örnek alanlarıyla orantılıdır.
- Runtime doğrulama, thread-safe lazy başlatma altında tür başına bir kez O(n)’dir.

## Geçici (henüz ürün politikası olarak kabul edilmedi)

- Doğrulanmış enumeration ötesinde public lookup yardımcıları (`FromName` / `FromValue`).
- Ayrı bir doğrulama exception’ı ötesindeki exception hiyerarşisi.
- Boş alan için boş `GetInstances()` gövdesinin hâlâ üretilip üretilmeyeceği (uygulama SMARTENUM005 bildirirken üretebilir; derleme yine fail eder).

## Referanslar

- ADR-002 Örnek Keşfi
- Milestone 1.2 uygulama notları (yayınlandığında PR açıklaması)
