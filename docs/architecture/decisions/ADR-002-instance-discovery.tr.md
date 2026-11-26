# ADR-002 — Derleme Zamanı Örnek Keşfi (Instance Discovery)

| Alan | Değer |
| --- | --- |
| Durum | **Kabul edildi** (Milestone 1 prototip kapsamı için) |
| Tarih | 2026-11-26 |
| Karar verenler | SmartEnum bakımcıları |
| İlişkili | `docs/design/SOURCE_GENERATOR.tr.md` |

## Bağlam

Smart Enum alan türleri, kapalı bir adlandırılmış örnek kümesini `public static readonly` alanlar olarak bildirir. Çekirdek API’ler (sonraki kilometre taşlarında lookup, enumeration, doğrulama yardımcıları) bu kümeyi güvenilir biçimde elde edebilmelidir.

Geleneksel yaklaşımlar:

1. Statik alanlar üzerinde çalışma zamanı reflection.
2. Elle kayıt (`ModuleInitializer`, statik kurucular, global registry).
3. Roslyn Source Generator ile derleme zamanı keşif.

Milestone 1, trimming ve Native AOT **hedefleriyle** uyumlu bir mimariyi doğrulamalı; ancak bu hedeflerin henüz kanıtlandığını iddia etmemelidir.

## Karar

**Kabul edildi:** Örnek keşfi **derleme zamanında**, artımlı bir Roslyn Source Generator (`SmartEnum.Generators`) ile yapılır. Generator, uygun her **partial** alan türünde teknik bir static-abstract arayüzün (`SmartEnum.Internal.ISmartEnumGenerated<TSelf>`) uygulamasını üretir.

**Kabul edildi:** Örnek keşfi için çalışma zamanı reflection **kullanılmaz**.

**Kabul edildi:** Keşif için `ModuleInitializer` tabanlı kayıt ve değiştirilebilir global registry **redd edilir**.

**Kabul edildi:** `SmartEnum<TEnum, TValue>` türetilen alan türleri `partial` olmalıdır.

**Kabul edildi:** Keşif sonuçları paylaşılan bir runtime registry üzerinden değil, **static abstract arayüz dağıtımı** (`TEnum.GetInstances()`) ile açığa çıkar.

## Sonuçlar — olumlu

- Keşif maliyeti derleme zamanına kayar; runtime enumeration statik bir dizi üzerinde span’dır.
- Keşif için reflection yüzeyi yoktur (trimming/AOT *niyeti* ile daha iyi hizalanma).
- Çapraz assembly mutable registry ve `ModuleInitializer` kaynaklı başlatma sırası sürprizleri yoktur.
- Geçersiz bildirimler için build’i düşüren diagnostics üretilebilir (partial olmayan tür, mutable instance alanı).

## Sonuçlar — olumsuz / ödünleşimler

- Alan türleri `partial` olmalıdır.
- Tüketiciler için generator paketleme ve analyzer bağlama doğru olmalıdır.
- `ISmartEnumGenerated<T>` **genel (public)** teknik bir yüzeydir (aşağıya bakın); `EditorBrowsable` bunu public API’den kaldırmaz.
- İç içe ve karmaşık generic alan şekilleri Milestone 1’de desteklenmiş **sayılmaz**.

## Public API notu — `EditorBrowsable`

`ISmartEnumGenerated<TSelf>`, IntelliSense gürültüsünü azaltmak için `[EditorBrowsable(EditorBrowsableState.Never)]` ile işaretlenir. Bu öznitelik arayüzü private yapmaz. Tür, Core assembly’nin public API’sinde kalır ve herhangi bir tüketici tarafından referans alınabilir. Bu görünürlük, Core generic kısıtları ve assembly’ler arası static abstract dispatch için gereklidir.

## Değerlendirilen alternatifler

| Alternatif | Sonuç |
| --- | --- |
| `public static readonly` alanlar üzerinde reflection | Reddedildi — trimming/AOT altında kırılgan; startup maliyeti; eksik örnekleri teşhis etmek zor |
| Elle `Register` / global dictionary | Reddedildi — mutable global durum; kaydı unutmak kolay |
| `ModuleInitializer` otomatik kayıt | Reddedildi — sıralama riskleri; muhakemesi zor; yine runtime registry |
| Source Generator + static abstract arayüz | **Kabul edildi** |

## Güvenlik etkileri

- Derleme zamanı keşif, runtime tip inceleme API’lerine bağımlılığı azaltır.
- Üretilen kod tam nitelikli tür adları kullanır; kullanıcıdan gelen dizeleri çalıştırmaz.
- Milestone 1’de kontrol edilen geçersiz şekiller, sessiz runtime boşlukları yerine derleyici diagnostics olarak görünür.

## Performans etkileri

- Generator işi artımlıdır ve değişen adaylarla orantılıdır.
- Runtime `GetInstances()`, statik dizi üzerinde `ReadOnlySpan<T>` döner (CS9203-güvenli desen).
- Çağrı başına reflection yoktur.

## AOT ve trimming etkileri

- Mimari AOT/trimming dostu olacak şekilde **seçilmiştir**.
- Milestone 1 Native AOT veya tam trim doğrulaması **iddia etmez**. Bunlar sonraki doğrulama aşamalarıdır.

## Geçici teknik seçimler (henüz ürün politikası olarak “Kabul edildi” değil)

- SMARTENUM001/002 dışındaki kesin diagnostic kimlik kataloğu.
- Nihai NuGet paketleme şekli (tek paket vs Core + Generators).
- `ISmartEnumGenerated<T>` uzun vadede `SmartEnum.Internal` içinde kalacak mı.
- Nested türler ve açık generic alanlar için destek matrisi.

## Referanslar

- `docs/design/SOURCE_GENERATOR.md`
- `docs/design/SOURCE_GENERATOR.tr.md`
- `ADR-002-instance-discovery.md`
