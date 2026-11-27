# SmartEnum Registry — Davranış Notları (Milestone 1.3)

Bu notlar gözlemlenen davranışı belgeler. ADR-002 veya ADR-003 kararlarını değiştirmez.

İngilizce karşılık: [REGISTRY.md](REGISTRY.md).

## Resmi giriş noktası

`SmartEnum<TEnum, TValue>.Registry` tek public Registry giriş noktasıdır.

`SmartEnumRegistry<TEnum, TValue>` **public** kalır; çünkü `Registry` bu türü döndürür. Public bir özellik daha az erişilebilir bir tür döndüremez (CS0050).

`SmartEnumRegistry<TEnum, TValue>.Instance` **internal**dır. `Registry` tarafından kullanılan lazy fabrikadır. Dış assembly’ler onu çağıramaz.

## TValue sözleşmesi

- `TValue` tercihen immutable olmalıdır.
- `Equals` ve `GetHashCode` birbiriyle tutarlı olmalıdır.
- Eşitlik sonuçları Registry başlatıldıktan sonra kararlı kalmalıdır. Registry, doğrulama sırasında görülen değerlerin ötesinde eşitlik davranışının özel bir kopyasını almaz.
- Mutable reference-type `TValue`, yayından sonra değişebilir ve doğrulama başarılı olsa bile gelecekteki sözlük lookup’ını (`FromValue`) geçersiz kılabilir.
- SmartEnum, `TValue` değerini deep-copy etmez ve dondurmaz.

Runtime yinelenme tespiti `EqualityComparer<TValue>.Default` kullanır. Derleme zamanı tespiti yalnızca Roslyn sabit değerlerini kullanır.

## Lookup indeksleri

Name ve value dictionary'leri `CreateValidated` içinde `StringComparer.Ordinal` ve `EqualityComparer<TValue>.Default` ile oluşturulur. Doğrulama başarılı olursa aynı dictionary örnekleri registry üzerinde saklanır. Lookup bunları yeniden kurmaz veya kopyalamaz. Public yüzeyin parçası değildirler.

`SmartEnum<TEnum, TValue>.List`, `Registry.Items` ile aynı salt okunur koleksiyonu döndürür.

`FromName`, `FromValue`, `TryFromName`, `TryFromValue`, `ContainsName` ve `ContainsValue` bu indeksleri okur. Registry initialization hatalarını yakalamazlar. Null, boş ve whitespace name değerleri ile null value, indeksler okunmadan sınıflanır. Kabul edilen sözleşme ADR-005'tir.

`List` ve lookup metotları `Registry` ile aynı initialization kuralına uyar, çünkü gerçek bir arama registry'yi okur. Domain'in kendi statik alanları hâlâ başlarken bunları çağırmak desteklenmez.

## Özyinelemeli Registry erişimi

### Statik kurucu gövdesi

C#, statik kurucu gövdesinden önce statik alan başlatıcılarını çalıştırır. Bu başlatıcılardan sonra statik kurucu gövdesinden `Registry` okumak, entegrasyon testlerinin kapsadığı senaryodur. Alan başlatması tamamlandıysa Registry doğrulaması başarılı olabilir. Bu, her özyinelemeli erişimin desteklendiği anlamına **gelmez**.

### Alan başlatması sırasında

Aynı alan türünün statik alanları hâlâ başlarken `Registry` okumak, eksik bir örnek dizisi görebilir (null girdiler veya henüz atanmamış destek dizisi). Doğrulama o zaman `SmartEnumValidationException` ile başarısız olur; ya da `Lazy<T>` fabrikası aynı iş parçacığında `Value`’ya yeniden girerse çalışma zamanı `InvalidOperationException` fırlatabilir.

Bu özyinelemeli durum **desteklenmez**. Çağıranlar, alan türünün kendi statik başlatmasının parçası olarak çalışan örnek kurucularından veya alan başlatıcılarından `Registry` okumamalıdır.

Başarısız `Lazy<T>` başlatması önbelleğe alınır. Sonraki erişim aynı exception örneğini fırlatır. Kısmen kurulmuş bir `SmartEnumRegistry` yayınlanmaz.

## Kurucu argüman bağlama (generator)

Tekillik diagnostics’i, nesne oluşturma argümanlarını seçilen kurucunun `IParameterSymbol` değerine bağlar (herhangi bir sıradaki named argument’ler ve opsiyonel parametreler dahil). Sabitler `SemanticModel.GetConstantValue` ile okunur. Parametreler `name` ve `value` adlarıyla tanınır. Seçilen kurucunun public olması gerekmez. `tests/integration/SmartEnum.Generator.IntegrationTests/OrderStatus.cs` bu parametre adlarına sahip private bir kurucu kullanır.
