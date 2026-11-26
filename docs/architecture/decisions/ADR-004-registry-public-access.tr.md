# ADR-004 — Registry Public Erişim

| Alan | Değer |
| --- | --- |
| Durum | **Kabul edildi (Accepted)** |
| Tarih | 26.11.2026 |
| Karar verenler | SmartEnum bakımcıları |
| Kapsam | SmartEnum Core public API |
| İlişkili | [ADR-003](ADR-003-smart-enum-registry-validation.tr.md) |
| İngilizce | [ADR-004-registry-public-access.md](ADR-004-registry-public-access.md) |

Bu kayıt, uygulanmış bir kararı belgeler. Üretim kodunu değiştirmez.

## Bağlam

ADR-003, `SmartEnum<TEnum, TValue>.Registry` üzerinden erişilen doğrulanmış bir `SmartEnumRegistry<TEnum, TValue>` kabul etti. Initialization, `LazyThreadSafetyMode.ExecutionAndPublication` kullanan bir `Lazy<T>` fabrikasıdır. Doğrulama, yayınlamadan önce çalışır. Başarısız initialization aynı exception'ı önbelleğe alır ve kısmi bir registry yayınlamaz.

Bu uygulamadan sonra aynı fabrikaya iki public yol vardı:

- `SmartEnum<TEnum, TValue>.Registry`
- `SmartEnumRegistry<TEnum, TValue>.Instance`

`Registry` amaçlanan sözleşmeydi. `Instance`, aynı lazy değere giden ikinci bir public static girişti. Çağıranlar belgelenmiş giriş noktasını atlayabilirdi.

Milestone 1.3 incelemesi bir C# kısıtını da kaydetti: public bir üye daha az erişilebilir bir tür döndüremez (CS0050). `Registry`, `SmartEnumRegistry<TEnum, TValue>` döndürdüğü için bu dönüş türü değişmeden registry türü `internal` yapılamaz.

## Karar

Kabul edildi — Seçenek A:

- `SmartEnum<TEnum, TValue>.Registry`, tek resmi public Registry giriş noktasıdır.
- `SmartEnumRegistry<TEnum, TValue>` public kalır, çünkü public `Registry` özelliğinin dönüş türüdür.
- `SmartEnumRegistry<TEnum, TValue>.Instance` internal'dır.
- Registry initialization, `LazyThreadSafetyMode.ExecutionAndPublication` ile `Lazy<T>` olarak kalır.
- Exception önbelleği ve doğrulama davranışı değişmez.

`Items`, public registry türünün public üyesi olarak kalır. Keşif, tekillik kuralları ve validation exception türü değişmez.

## Değerlendirilen seçenekler

### Seçenek A — Tek public giriş; registry türü public kalır

`Instance` internal yapılır. `SmartEnum<TEnum, TValue>.Registry` public kalır. `SmartEnumRegistry<TEnum, TValue>`, dönüş türü olarak public kalır. Lazy initialization, exception önbelleği ve doğrulama ADR-003'teki gibi kalır.

### Seçenek B — Registry türünü internal yapmak

`SmartEnumRegistry<TEnum, TValue>` internal yapılır. Bu, registry implementasyonunun teknik olarak mümkün olduğunda internal olması tercihine uyar.

`Registry` bu türü döndürdüğü sürece bu seçenek kullanılamaz. CS0050, public bir özelliğin daha az erişilebilir bir tür döndürmesini yasaklar. Seçenek B, farklı bir public dönüş türü gerektirir. Örnek: yalnızca `IReadOnlyList<TEnum>` açmak ve adlandırılmış registry türünü public yüzeyden çıkarmak.

## Seçenek A'nın seçilme nedeni

Seçenek A, ADR-003 sözleşmesini yeniden açmadan ikinci public girişi kaldırır.

- Çağıranlar adlandırılmış, doğrulanmış registry nesnesini ve `Items` koleksiyonunu korur.
- `Registry` dönüş türü değişmez. Mevcut `Domain.Registry` çağrıları kaynak uyumlu kalır.
- `Instance`, lazy fabrikanın implementasyon ayrıntısıdır. Üretim kodundaki tek çağıran, SmartEnum assembly'si içindeki `Registry`'dir.
- Doğrulama, thread güvenliği ve önbelleğe alınan hatalar ADR-003'ün tanımladığı gibi kalır.
- Seçenek B, `SmartEnumRegistry<TEnum, TValue>` türünü ancak public dönüş türünü değiştirerek gizler. Bu, erişim probleminden daha büyük bir API kırılmasıdır.

`Instance`'ın public bırakılması, bu karardan önceki durumdu. İki resmi giriş noktası desteklenen yüzeyi belirsiz kıldığı için reddedildi.

## Public API sonuçları

| Üye | Erişim |
| --- | --- |
| `SmartEnum<TEnum, TValue>.Registry` | public — tek resmi giriş |
| `SmartEnumRegistry<TEnum, TValue>` | public — `Registry` dönüş türü |
| `SmartEnumRegistry<TEnum, TValue>.Items` | public |
| `SmartEnumRegistry<TEnum, TValue>.Instance` | internal |
| `SmartEnumValidationException` | public — değişmedi |

Dış assembly'ler `Instance` adını kullanamaz. SmartEnum assembly'si içindeki kod kullanabilir. Non-public reflection internal özelliği görebilir. Bu desteklenen bir API değildir.

`SmartEnumRegistry<TEnum, TValue>` public kaldığı için bu türe sonra eklenen public üyeler public API olur.

## Uyumluluk etkileri

- `SmartEnum<TEnum, TValue>.Registry`, registry türü ve `Items` için kaynak uyumludur.
- Başka bir assembly'den `SmartEnumRegistry<TEnum, TValue>.Instance` doğrudan kullanımı kaynak uyumlu değildir (`CS0122`).
- Exception türleri, exception mesajları, lazy modu ve doğrulama kuralları değişmez.
- Yayınlanmış kararlı bir paket, `Instance`'ı desteklenen public üye olarak taahhüt etmemiştir. Desteklenen sözleşme `Registry`'dir.

## Sonuçlar ve ödünleşimler

### Olumlu

- Doğrulanmış registry'yi almanın belgelenmiş tek yolu vardır.
- ADR-003 davranışı durur: ya hep ya hiç yayınlama, önbelleğe alınan hata, Ordinal name kontrolü ve `EqualityComparer<TValue>.Default` ile value kontrolü.
- Public tür, `Items` ve daha sonra domain türü üzerinden dönecek lookup API'leri için kullanılabilir kalır.

### Ödünleşimler

- Registry türü, sonraki bilinçli bir dönüş türü değişikliği olmadan gizlenemez.
- `Instance`, SmartEnum assembly'si içinde çağrılabilir kalır. Yeni Core kodu `Registry` üzerinden gitmelidir.
- Tüketiciler public registry türüne referans tutabilir. Kapsülleme üye düzeyindedir (`Instance`), tür düzeyinde değildir.

## Test stratejisi

Testler public sözleşmeyi ve erişim sınırını gözler. Doğrulama semantiğini değiştirmez.

- Geçerli bir domain `Registry` üzerinden initialize olur.
- Tekrarlanan `Registry` erişimi aynı örneği döndürür.
- Paralel erişim tek bir registry örneği yayınlar.
- Dış test assembly'si public `Instance` özelliği bulamaz. Non-public static özellik assembly kapsamındadır (`IsAssembly`).
- Mevcut ADR-003 kontrolleri geçmeye devam eder: boş, null, yinelenen name, yinelenen value, önbelleğe alınan hata, değiştirilemez `Items` ve domain başına bağımsız registry.

## ADR-003 ile ilişki

ADR-003, bir registry'nin ne garanti ettiğini ve tüketicilerin ona `SmartEnum<TEnum, TValue>.Registry` ile ulaştığını kararlaştırır. `Instance`'ın public olmasını gerektirmez.

ADR-004 yalnızca lazy fabrikayı kimin çağırabileceğini daraltır. Şunları değiştirmez:

- derleme zamanı tanılamaları SMARTENUM003, SMARTENUM004 ve SMARTENUM005
- boş, null, yinelenen name ve yinelenen value için runtime kontrolleri
- `LazyThreadSafetyMode.ExecutionAndPublication`
- önbelleğe alınan initialization hataları
- public `Items` koleksiyonu

ADR-002 keşif kararı olarak kalır. ADR-005 (Lookup ve Equality) **Önerildi (Proposed)** durumundadır ve bu kararın dışındadır.

## Referanslar

- [ADR-003](ADR-003-smart-enum-registry-validation.tr.md) — Registry doğrulama ve tekillik
- `docs/design/REGISTRY.tr.md` — resmi giriş noktası için davranış notları
- C# erişilebilirliği: public bir üye daha az erişilebilir bir tür açamaz (CS0050)
