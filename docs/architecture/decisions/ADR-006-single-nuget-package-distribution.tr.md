# ADR-006 — Tek NuGet Paketi Dağıtımı

| Alan | Değer |
| --- | --- |
| Durum | **Kabul edildi (Accepted)** |
| Tarih | 27.11.2026 |
| Karar verenler | SmartEnum bakımcıları |
| Kapsam | NuGet paketleme ve dağıtım |
| İlişkili | [ADR-002](ADR-002-instance-discovery.tr.md) |
| İngilizce | [ADR-006-single-nuget-package-distribution.md](ADR-006-single-nuget-package-distribution.md) |

Bu kayıt, uygulanmış bir paketleme kararını belgeler. Üretim runtime davranışını, keşif kurallarını, registry doğrulamasını veya ADR-002 ile ADR-005 arasında kabul edilen public Core API’yi değiştirmez.

## Bağlam

ADR-002, artımlı bir Source Generator ile derleme zamanı instance keşfini zorunlu kılar. Tüketicilerin hem Core runtime assembly’sini hem de generator’ı alması gerekir. Böylece `partial` domain’ler `ISmartEnumGenerated<TSelf>` kısıtını karşılar ve Registry, lookup ile equality API’lerini kullanabilir.

Bu karardan önce paketleme topolojisi ADR-002’de ve `docs/design/SOURCE_GENERATOR.md` içinde geçiciydi. Entegrasyon testleri generator’ı Analyzer `ProjectReference` olarak bağlıyordu. NuGet tüketicileri analyzer’ları bu yolla almaz.

İki dağıtım şekli vardı:

1. Core ve generator’ı içeren tek NuGet paketi.
2. Ayrı Core ve Generators paketleri.

Proje sahibi tek paket şeklini kabul etti. R2 paketleme milestone’ları bu şekli uyguladı.

## Karar

**Kabul edildi:** Core ve Source Generator, `PackageId` değeri `SmartEnum` olan tek bir NuGet paketiyle dağıtılır.

Kabul edilen yerleşim ve sahiplik:

1. Core ve Source Generator tek `SmartEnum` paketiyle yayınlanır.
2. Paket üretimini ayrı paketleme projesi üstlenir: `src/SmartEnum.Packaging/SmartEnum.Packaging.csproj`.
3. Core ve Generator bağımsız projeler olarak kalır. Birbirlerine `ProjectReference` vermezler.
4. Core assembly `lib/net10.0/SmartEnum.dll` konumuna konur.
5. Generator assembly `analyzers/dotnet/cs/SmartEnum.Generators.dll` konumuna konur.
6. Core XML dokümantasyonu `lib/net10.0/SmartEnum.xml` konumuna eklenir.
7. `README.md` paket köküne eklenir.
8. Roslyn derleyici assembly’leri tüketici paketine kopyalanmaz.
9. Core ve Generator `IsPackable=false` olur. Dağıtım paketini yalnızca Packaging projesi üretir.

Bu karar altındaki ilk önizleme sürümü `1.0.0-preview.1`’dir.

## Değerlendirilen seçenekler

### Seçenek A — Tek `SmartEnum` paketi

Tek `PackageReference`, Core’u `lib/` altına ve generator’ı `analyzers/dotnet/cs/` altına getirir. Yalnızca paketleme yapan proje, Core ↔ Generator derleme bağımlılığı oluşturmadan `GetTargetPath` ve dokümantasyon çıktılarını toplar.

### Seçenek B — Ayrı Core ve Generators paketleri

`SmartEnum` ve `SmartEnum.Generators` ayrı yayınlanır. Tüketici ikisini de referans etmek zorunda kalır veya Core, analyzer paketini çeken bir bağımlılık bildirir.

Bu seçenek reddedildi. İki paket, dikkatsiz kurulumda keşfi isteğe bağlı bırakır ve ADR-002’nin tek ürün yeteneği olarak gördüğü çifti sürüm olarak böler.

### Seçenek C — Core projesinden Analyzer project reference ile paketleme

`SmartEnum.csproj` kendini paketler ve generator’ı `OutputItemType="Analyzer"` ile referanslar.

Bu seçenek reddedildi. Core’a generator’a giden bir project reference verir ve kabul edilen proje sınırına aykırıdır: Core Generators’a `ProjectReference` vermez; Generators da Core’a `ProjectReference` vermez.

## Seçenek A’nın seçilme nedeni

- Tüketici tek paket kurar; runtime türleri ve otomatik generator çalışması birlikte gelir.
- NuGet, ikinci bir referans olmadan `analyzers/dotnet/cs` klasörünü C# derlemesine yükler.
- Paketleme, Core ve Generator derleme grafiğinin dışında kalır; proje bağımsızlığı korunur.
- Dağıtım metadata’sı, README ve XML dokümantasyon tek shipping projesinde toplanır.
- ADR-003’ün “Core runtime paket bağımlılığı yok” kuralı durur: `net10.0` nuspec bağımlılık grubu boş kalır; Roslyn paketleri yalnızca generator derlemesinde `PrivateAssets` olarak kalır.

## Paket içeriği

| Yol | İçerik |
| --- | --- |
| `lib/net10.0/SmartEnum.dll` | Core runtime |
| `lib/net10.0/SmartEnum.xml` | Core XML dokümantasyonu |
| `analyzers/dotnet/cs/SmartEnum.Generators.dll` | Source Generator |
| `README.md` | Paket readme |

Shipping paketinden çıkarılanlar:

- `SmartEnum.Packaging.dll`
- `Microsoft.CodeAnalysis*.dll` ve diğer Roslyn uygulama assembly’leri
- Core veya Generator’ın başka bir paket klasöründeki ikinci kopyası

## Önceki ADR’larla ilişki

- ADR-002 keşif kararı olarak kalır. Bu ADR yalnızca daha önce geçici bırakılan NuGet paketleme şeklini kaydeder.
- ADR-003 registry doğrulama kararı olarak kalır. Core’un runtime NuGet bağımlılığı yoktur.
- ADR-004 ve ADR-005, Registry erişimi ile Lookup/Equality API kararları olarak kalır. Paketleme bu üyeleri değiştirmez.

## Sonuçlar ve ödünleşimler

### Olumlu

- Belgelenmiş tek kurulum yolu: `SmartEnum` için `PackageReference`.
- Source Generator, analyzer varlık klasöründen otomatik yüklenir.
- Core ve Generator ayrı derlenebilir ve ayrı test edilebilir kalır.
- Paket kimliği, sürüm, lisans, README ve repository metadata’sı tek yerde yönetilir.

### Ödünleşimler ve bakım maliyeti

- Paketleme mantığı üçüncü bir projede ve özel MSBuild hedeflerinde yaşar (`GetTargetPath`, `DocumentationProjectOutputGroup`, açık `PackagePath` değerleri). Bu yüzey SDK pack davranışıyla hizalı tutulmalıdır.
- Core, solution restore çakışmasını önlemek için yayımlanmayan `PackageId` (`SmartEnum.Core`) kullanır; shipping kimlik `SmartEnum`’dir. Core yanlışlıkla pack edilebilir yapılırsa yanlış kimlik yayınlanır.
- İleride multi-targeting, sembol paketleri veya ek analyzer varlıkları Packaging projesinde eşgüdümlü güncelleme ister.
- Yalnızca restore ile tek `PackageReference` kullanan tüketici kanıtı paket doğrulama konusudur; bu ADR onu Core API değişikliği olarak yeniden tanımlamaz.

## Test ve doğrulama beklentileri

Paketleme doğrulaması şunları doğrulamalıdır:

- Solution pack tam olarak bir `SmartEnum` nupkg üretir.
- Arşiv yukarıdaki dört shipping varlığı içerir; Roslyn veya Packaging assembly içermez.
- Nuspec `net10.0` bağımlılık grubu boştur.
- Paketteki Core XML, Core dokümantasyon çıktısıyla eşleşir.

Runtime birim testleri, generator driver testleri ve Analyzer `ProjectReference` entegrasyon testleri davranış için geçerli kalır. Paket yerleşimi doğrulamasının yerini tutmazlar.

## Referanslar

- `src/SmartEnum.Packaging/SmartEnum.Packaging.csproj`
- `src/SmartEnum/SmartEnum.csproj`
- `src/SmartEnum.Generators/SmartEnum.Generators.csproj`
- `docs/design/SOURCE_GENERATOR.md`
- `ADR-006-single-nuget-package-distribution.md`
