# SmartEnum Source Generator — Mimari (Milestone 1)

| Alan | Değer |
| --- | --- |
| Durum | Prototip uygulandı |
| Kapsam | Yalnızca derleme zamanı örnek keşfi |
| İlişkili ADR | [ADR-002](../architecture/decisions/ADR-002-instance-discovery.tr.md) |
| İngilizce karşılık | [SOURCE_GENERATOR.md](SOURCE_GENERATOR.md) |

Bu belge, Milestone 1’de doğrulanan Source Generator mimarisini açıklar. **Kabul edilen** kararları **geçici** teknik seçimlerden ayırır. Onaylanmamış ürün önerileri “Kabul edildi” olarak işaretlenmez.

Lookup, equality ve SMARTENUM003–SMARTENUM005 tanıları Milestone 1’den sonra uygulandı. Bu olgular aşağıda kayıtlıdır. Bu belge ADR-002 kararını değiştirmez.

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
public abstract class SmartEnum<TEnum, TValue> : IEquatable<TEnum>
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

### Sorumluluklar

Generator lookup veya equality uygulamaz.

Generator:

- `public static readonly` alanların derleme zamanı keşfi.
- Üretilen `ISmartEnumGenerated<T>` uygulaması.
- Statik örnek dizisi.
- SMARTENUM001–SMARTENUM005 tanıları.

Core (`src/SmartEnum`):

- Registry başlatması ve doğrulaması.
- Lookup: `List`, `FromName`, `FromValue`, `TryFromName`, `TryFromValue`, `ContainsName`, `ContainsValue`.
- Equality, `GetHashCode()` ve `==` / `!=` operatörleri.
- `SmartEnumValidationException`, `KeyNotFoundException`, `ArgumentNullException` ve `ArgumentException` dahil runtime istisnaları.

### Keşif

1. Sözdizimi predikatı: base listesi olan class/record.
2. Anlamsal filtre: taban tür kimliği `SmartEnum.SmartEnum<TEnum, TValue>` ve `TEnum` bildiren türdür (yalnızca string eşleştirme değil; namespace + metadata kimliği).
3. `partial` zorunlu (aksi halde SMARTENUM001). Partial olmayan alan için keşif kaynağı üretilmez.
4. Türü alan türüne eşit olan `public static readonly` alanları topla.
5. İlgisiz statik alanları yok say (`string`, `int` vb.).
6. `readonly` olmayan `public static` alan-türündeki alanlar için SMARTENUM002 bildir. Bu alanlar üretilen diziye girmez.
7. `name` ve `value` adlı kurucu argümanları yinelenen derleme zamanı sabiti olduğunda SMARTENUM003 ve SMARTENUM004 bildir.
8. Geçerli örnek alanı kalmadığında SMARTENUM005 bildir. Üretim yine boş bir dizi yazar. Tanı şiddeti Error olduğu için tüketici derlemesi başarısız olur.

Doğrulanmış bildirim şekli: üst düzey, generic olmayan `partial` class. Nested türler ve açık generic alanlar desteklenmiş sayılmaz.

### Tanı kataloğu

Her tanımlayıcı kategori `SmartEnum.Generators`, şiddet Error ve varsayılan olarak açıktır. Aşağıdaki iletiler `SmartEnumDiagnostics.cs` içindeki `messageFormat` dizeleridir.

| ID | Başlık | Ne zaman bildirilir | İleti |
| --- | --- | --- | --- |
| SMARTENUM001 | SmartEnum domain type must be partial | Alan `SmartEnum<,>` türünden türer ve `partial` değildir. | `Type '{0}' inherits from SmartEnum<,> and must be declared partial so the source generator can emit instance discovery` |
| SMARTENUM002 | SmartEnum instance field must be static readonly | Alan türünde `public static` bir alan `readonly` değildir. | `Field '{0}' on SmartEnum domain '{1}' must be 'static readonly' to participate in compile-time instance discovery` |
| SMARTENUM003 | Duplicate SmartEnum Name | İki geçerli alan, `name` parametresine aynı derleme zamanı sabitini geçirir. Karşılaştırma ordinaldır. | `SmartEnum domain '{0}' declares duplicate Name '{1}' on fields '{2}' and '{3}'` |
| SMARTENUM004 | Duplicate SmartEnum Value | İki geçerli alan, `value` parametresine aynı null olmayan derleme zamanı sabitini geçirir. | `SmartEnum domain '{0}' declares duplicate Value '{1}' on fields '{2}' and '{3}'` |
| SMARTENUM005 | SmartEnum domain has no instances | Alanda geçerli `public static readonly` örnek yoktur. | `SmartEnum domain '{0}' has no valid public static readonly instances` |

Derleme zamanı sabiti olmayan bir `name` veya `value` argümanı SMARTENUM003 ve SMARTENUM004 tarafından atlanır. Runtime registry doğrulaması yinelenen name ve value değerlerini yine reddeder. Seçilen kurucunun public olması gerekmez. Parametreler `name` ve `value` adlarıyla tanınır. Sabitler `SemanticModel.GetConstantValue` ile okunur.

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
- deterministik sıra: önce kaynak dosya yolu, sonra o dosyadaki kaynak konumu. Tek dosya bildirim sırasını izler. Birden fazla dosya ordinal dosya yolu sırasını izler,
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

## 15. Generator sınırlamaları

- Üst düzey, non-generic alanlar doğrulanmış yoldur.
- Nested türler ve açık generic alanlar desteklenmiş sayılmaz.
- Generator lookup, equality veya serialization üretmez. Lookup ve equality Core üzerindedir.
- SMARTENUM001–SMARTENUM005 tanıları uygulanmıştır.
- Generator, entegrasyon testlerinde Analyzer `ProjectReference` olarak bağlanır. NuGet paketleme geçicidir.

## 16. Test stratejisi

| Suite | Proje | Odak |
| --- | --- | --- |
| Unit | `tests/unit/SmartEnum.Tests` | Kurucu, registry, lookup ve equality |
| Generator | `tests/generators/SmartEnum.Generators.Tests` | Driver tabanlı üretim, diagnostics, Verify snapshot |
| Integration | `tests/integration/SmartEnum.Generator.IntegrationTests` | Analyzer referansı ile gerçek derleme + static abstract dispatch |

Yığın:

- xUnit v3 via `xunit.v3.mtp-v2` (Microsoft.Testing.Platform; .NET 10 SDK `dotnet test` için gerekli)
- Test konağı için `Microsoft.CodeAnalysis.CSharp` ve `Microsoft.CodeAnalysis.CSharp.Workspaces`
- `tests/generators/SmartEnum.Generators.Tests/GeneratorTestHelpers.cs` içindeki `CSharpGeneratorDriver`
- Üretilen kaynak snapshot’ları için `Verify.XunitV3` (`*.verified.txt`)

## 17. Proje bağımlılık sınırları

```text
SmartEnum (Core)          — runtime PackageReference yok; Generators’a ProjectReference yok
SmartEnum.Generators      — yalnızca Roslyn paketleri (PrivateAssets); Core’a ProjectReference YOK
Generator.Tests           — Core + Generators (driver testleri için normal referans)
Integration.Tests         — Core + Generators (Analyzer olarak)
```

## 18. Gelecek genişletme düşünceleri (geçici)

- Generator’ı Core NuGet paketine analyzer olarak paketlemek.
- Nested ve açık generic alan desteğini, bu desteği iddia etmeden önce testlerle eklemek.
- Ayrı Native AOT / trim doğrulama kilometre taşı.

Lookup ve equality Core’da uygulanmıştır. Generator bu üyeleri üretmez. Derleme zamanı sabitleri için SMARTENUM003 ve SMARTENUM004 tanıları uygulanmıştır.

## 19. Geçici vs kabul edildi — hızlı kontrol listesi

| Konu | Sınıflandırma |
| --- | --- |
| Derleme zamanı keşif | Kabul edildi |
| Reflection keşfi yok | Kabul edildi |
| Partial zorunlu | Kabul edildi |
| Static abstract dispatch | Kabul edildi |
| ModuleInitializer keşfi yok | Kabul edildi |
| CS9203-güvenli statik dizi üretimi | Geçici uygulama detayı (doğruluk odaklı) |
| Diagnostic kimlikleri SMARTENUM001–SMARTENUM005 | Uygulandı. 001 ve 002 bu keşif tasarımındandır. 003, 004 ve 005 ADR-003 ile kabul edildi. |
| NuGet paketleme topolojisi | Geçici |
| Nested/generic alanlar | Milestone 1 iddiası dışında |
| Native AOT kanıtlandı | İddia edilmiyor |
