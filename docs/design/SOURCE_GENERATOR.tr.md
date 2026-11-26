# SmartEnum Source Generator — Mimari (Milestone 1)

| Alan | Değer |
| --- | --- |
| Durum | Prototip uygulandı |
| Kapsam | Yalnızca derleme zamanı örnek keşfi |
| İlişkili ADR | [ADR-002](../architecture/decisions/ADR-002-instance-discovery.tr.md) |
| İngilizce karşılık | [SOURCE_GENERATOR.md](SOURCE_GENERATOR.md) |

Bu belge, Milestone 1’de doğrulanan Source Generator mimarisini açıklar. **Kabul edilen** kararları **geçici** teknik seçimlerden ayırır. Onaylanmamış ürün önerileri “Kabul edildi” olarak işaretlenmez.

## 1. Bağlam ve problem

Bir Smart Enum alan türü sonlu bir örnek kümesi bildirir:

```csharp
public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
{
    public static readonly OrderStatus Pending = new("Pending", 1);
    public static readonly OrderStatus Approved = new("Approved", 2);

    private OrderStatus(string name, int value) : base(name, value) { }
}
```

Sonraki Core API’lerin bu örnekleri şu yollara başvurmadan listelemesi gerekir:

- çalışma zamanı reflection,
- değiştirilebilir global registry,
- veya `ModuleInitializer` yan etkileri.

Milestone 1, derleme zamanı keşif + static abstract arayüz dağıtımının uçtan uca çalıştığını kanıtlar.

## 2. Kabul edilen mimari kararlar

| Karar | Durum |
| --- | --- |
| Artımlı Roslyn Source Generator kullanımı (`IIncrementalGenerator`) | **Kabul edildi** |
| Örneklerin derleme zamanında keşfi | **Kabul edildi** |
| Reflection tabanlı keşfin reddi | **Kabul edildi** |
| `ModuleInitializer` kaydının reddi | **Kabul edildi** |
| Keşif için global mutable registry reddi | **Kabul edildi** |
| `partial` alan bildirimlerinin zorunlu olması | **Kabul edildi** |
| `ISmartEnumGenerated<TSelf>` uygulamasının üretilmesi | **Kabul edildi** |
| Static abstract arayüz dağıtımı (`TEnum.GetInstances()`) | **Kabul edildi** |
| Generator projesinin Core’a `ProjectReference` vermemesi | **Kabul edildi** |
| Core’un sıfır dış runtime paket bağımlılığı | **Kabul edildi** |
| Milestone 1’de üst düzey, non-generic alan sınıflarına öncelik | **Kabul edildi** (kapsam sınırı) |

## 3. Neden derleme zamanı keşif seçildi

Derleme zamanı keşif:

- doğruluk kontrollerini build’e taşır,
- deterministik üretilmiş kaynak sağlar,
- runtime tip taramasından kaçınır,
- trimming/AOT *niyeti* ile hizalanır (AOT doğrulaması sonraki kilometre taşıdır).

## 4. Neden reflection reddedildi

Reflection ile alan tarama:

- trimmer/AOT altında kırılgandır,
- hataları runtime’a erteler,
- ölçülebilir startup maliyeti ekler,
- “eksik örnek” hatalarını, kapsamlı runtime testleri olmadan CI’da yakalamayı zorlaştırır.

## 5. Neden partial bildirimler zorunlu

Generator, ikinci bir `partial` tür bildirimi ekler; bu bildirim:

- üretilen arayüzü alan türüne ekler,
- `GetInstances()` uygular,
- namespace ve erişilebilirliği korur.

`partial` olmadan generator kullanıcı türünü yasal olarak genişletemez. Partial olmayan alanlar **SMARTENUM001** tanısını üretir.

## 6. Neden static abstract arayüz dağıtımı seçildi

Arayüzler üzerindeki static abstract üyeler (C# generic math stili) şunu mümkün kılar:

```csharp
static ReadOnlySpan<TEnum> GetAll<TEnum>()
    where TEnum : ISmartEnumGenerated<TEnum>
    => TEnum.GetInstances();
```

Faydalar:

- paylaşılan registry araması yok,
- dağıtım generic kısıt üzerinden çözülür,
- arayüz public olduğunda assembly’ler arası çalışır.

## 7. Neden ModuleInitializer reddedildi

`ModuleInitializer` kaydı genellikle assembly yüklenirken global bir tabloya yazar. Sorunlar:

- assembly’ler arası başlatma sırası,
- örneklerin ne zaman görünür olduğuna dair muhakeme zorluğu,
- yine bir runtime registry modeli,
- generator diagnostics’e göre daha zayıf build-zamanı doğrulama.

## 8. Core sözleşmesi (prototip)

```csharp
public abstract class SmartEnum<TEnum, TValue>
    where TEnum : SmartEnum<TEnum, TValue>, ISmartEnumGenerated<TEnum>
    where TValue : notnull
```

Teknik arayüz:

```csharp
namespace SmartEnum.Internal;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISmartEnumGenerated<TSelf>
    where TSelf : ISmartEnumGenerated<TSelf>
{
    static abstract ReadOnlySpan<TSelf> GetInstances();
}
```

### Public API sonucu

`EditorBrowsable` yalnızca IDE sunumunu etkiler. Arayüz **public API** olarak kalır. Çağıranlar ona referans verebilir. Bu, çapraz assembly static abstract kısıtları için bilinçli bir seçimdir.

## 9. Generator davranışı

Proje: `src/SmartEnum.Generators` (`netstandard2.0`, Roslyn bileşeni).

### Keşif

1. Sözdizimi predikatı: base listesi olan class/record.
2. Anlamsal filtre: taban tür kimliği `SmartEnum.SmartEnum<TEnum, TValue>` ve `TEnum` bildiren türdür (yalnızca string eşleştirme değil; namespace + metadata kimliği).
3. `partial` zorunlu (aksi halde SMARTENUM001).
4. Türü alan türüne eşit olan `public static readonly` alanları topla.
5. İlgisiz statik alanları yok say (`string`, `int` vb.).
6. `readonly` olmayan `public static` alan-türündeki alanlar için SMARTENUM002 bildir.

### Üretim

Doğrulanmış şekil (`ReadOnlySpan<T>` olarak doğrudan collection expression geçersizdir — CS9203). Üretilen kod statik dizi destek alanı kullanır:

```csharp
// <auto-generated/>
#nullable enable

namespace Demo;

public sealed partial class OrderStatus : global::SmartEnum.Internal.ISmartEnumGenerated<global::Demo.OrderStatus>
{
    private static readonly global::Demo.OrderStatus[] _smartEnumInstances = [Pending, Approved];

    static global::System.ReadOnlySpan<global::Demo.OrderStatus> global::SmartEnum.Internal.ISmartEnumGenerated<global::Demo.OrderStatus>.GetInstances()
        => _smartEnumInstances;
}
```

Üretilen kodun özellikleri:

- tam nitelikli tür adları,
- korunmuş namespace,
- korunmuş erişilebilirlik / `sealed`,
- `#nullable enable`,
- deterministik sıra (geçerli alanların bildirim sırası),
- reflection yok, `ModuleInitializer` yok, global durum yok.

## 10. Avantajlar ve dezavantajlar

### Avantajlar

- Build-zamanı doğrulama.
- Basit runtime yolu.
- Net proje sınırı (Generator ↛ Core).
- Snapshot ile test edilebilir çıktı.

### Dezavantajlar

- `partial` zorunluluğu.
- Public teknik arayüz.
- NuGet tüketicileri için paketleme karmaşıklığı (geçici).
- Milestone 1’de sınırlı alan şekilleri.

## 11. Değerlendirilen alternatifler

ADR-002’ye bakın. Özet: reflection, elle registry ve `ModuleInitializer` reddedilerek generator + static abstract dispatch kabul edildi.

## 12. Güvenlik etkileri

- Keşfedilen üye adlarının runtime’da serbest biçimli değerlendirilmesi yok; derleme zamanı sembolleri kullanılır.
- Diagnostics, kontrol edilen geçersiz şekillerde sessizce eksik katalog riskini azaltır.
- Üretilen tanımlayıcılar derleyici sembollerinden gelir; serbest biçimli kullanıcı girdi akışlarından değil.

## 13. Performans etkileri

- Artımlı generator hattı, mümkün olduğunda tam derleme yeniden taramalarından kaçınır.
- Runtime keşif, statik dizi / span’e O(1) erişimdir.
- Reflection önbelleği yoktur.

## 14. AOT ve trimming etkileri

**Kabul edilen niyet:** reflection keşfinden kaçınarak AOT/trim dostu kalmak.

**Milestone 1’de iddia edilmeyenler:** Native AOT publish başarısı, trim uyarılarının sıfır olması veya linker yapılandırmasının tamamı. Bunlar ayrı doğrulama kilometre taşları gerektirir.

## 15. Generator sınırlamaları (Milestone 1)

- Üst düzey, non-generic alanlar doğrulanmış yoldur.
- Nested türler / açık generic’ler desteklenmiş sayılmaz.
- Lookup/equality/serialization üretimi yoktur.
- SMARTENUM001/002 dışındaki analyzer önerileri yoktur.
- Generator, entegrasyon testlerinde Analyzer `ProjectReference` olarak bağlanır; NuGet paketleme geçicidir.

## 16. Test stratejisi

| Suite | Proje | Odak |
| --- | --- | --- |
| Unit | `tests/unit/SmartEnum.Tests` | Core kurucu doğrulaması |
| Generator | `tests/generators/SmartEnum.Generators.Tests` | Driver tabanlı üretim, diagnostics, Verify snapshot |
| Integration | `tests/integration/SmartEnum.Generator.IntegrationTests` | Analyzer referansı ile gerçek derleme + static abstract dispatch |

Yığın:

- xUnit v3 via `xunit.v3.mtp-v2` (Microsoft.Testing.Platform; .NET 10 SDK `dotnet test` için gerekli)
- `Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing` (çatıdan bağımsız; **kullanılmayan** deprecated `*.Testing.XUnit` adapter)
- Üretilen kaynak snapshot’ları için `Verify.XunitV3`
- Hassas iddialar için özel `CSharpGeneratorDriver` yardımcıları

## 17. Proje bağımlılık sınırları

```text
SmartEnum (Core)          — runtime PackageReference yok; Generators’a ProjectReference yok
SmartEnum.Generators      — yalnızca Roslyn paketleri (PrivateAssets); Core’a ProjectReference YOK
Generator.Tests           — Core + Generators (driver testleri için normal referans)
Integration.Tests         — Core + Generators (Analyzer olarak)
```

## 18. Gelecek genişletme düşünceleri (geçici)

- Generator’ı Core NuGet paketine analyzer olarak paketlemek.
- Diagnostics’i genişletmek (duplicate, accessibility, naming).
- Nested/generic alan desteğini, iddia etmeden önce açık testlerle eklemek.
- Equality/identity tasarımı kapandıktan sonra `GetInstances()` üzerine lookup yardımcıları.
- Ayrı Native AOT / trim doğrulama kilometre taşı.

## 19. Geçici vs kabul edildi — hızlı kontrol listesi

| Konu | Sınıflandırma |
| --- | --- |
| Derleme zamanı keşif | Kabul edildi |
| Reflection keşfi yok | Kabul edildi |
| Partial zorunlu | Kabul edildi |
| Static abstract dispatch | Kabul edildi |
| ModuleInitializer keşfi yok | Kabul edildi |
| CS9203-güvenli statik dizi üretimi | Geçici uygulama detayı (doğruluk odaklı) |
| Diagnostic kimlikleri SMARTENUM001/002 | Geçici katalog |
| NuGet paketleme topolojisi | Geçici |
| Nested/generic alanlar | Milestone 1 iddiası dışında |
| Native AOT kanıtlandı | İddia edilmiyor |
