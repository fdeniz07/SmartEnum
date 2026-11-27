# ADR-005 — SmartEnum Lookup ve Equality API

| Alan | Değer |
| --- | --- |
| Durum | **Kabul edildi (Accepted)** |
| Tarih | 26.11.2026 |
| Düzeltme | 26.11.2026 — operatör parametreleri CS0563 için düzeltildi |
| Kapsam | SmartEnum Core |
| İlgili kararlar | ADR-002, ADR-003, ADR-004 |
| İngilizce | [ADR-005-lookup-and-equality.md](ADR-005-lookup-and-equality.md) |

Bu belge, kabul edilmiş Lookup ve Equality sözleşmesini tanımlar. Lookup ve Equality API'si uygulanmıştır. Bu ADR artık uygulanmış API'nin kabul edilmiş sözleşmesini tanımlar.

[ADR-004](ADR-004-registry-public-access.md) vardır ve **Kabul edildi (Accepted)** durumundadır. Public Registry erişim kararını belgeler: `SmartEnum<TEnum, TValue>.Registry` tek public giriş noktasıdır ve `SmartEnumRegistry<TEnum, TValue>.Instance` internal'dır.

## 1. Bağlam ve problem

SmartEnum, zorunlu Source Generator mimarisiyle instance keşfini ve runtime Registry doğrulamasını destekler.

Registry şu garantileri sağlar:

- En az bir geçerli instance.
- Null instance olmaması.
- Name değerlerinin benzersiz olması.
- Value değerlerinin benzersiz olması.
- Public instance koleksiyonunun değiştirilemez olması.
- Thread-safe ve tek seferlik initialization.

Kullanıcılar henüz Name veya Value üzerinden doğrudan arama yapamamaktadır. SmartEnum instance'ları standart bir domain equality sözleşmesine sahip değildir.

## 2. Karar

SmartEnum aşağıdaki API'leri sağlayacaktır:

- `List`
- `FromName`
- `FromValue`
- `TryFromName`
- `TryFromValue`
- `ContainsName`
- `ContainsValue`

Ayrıca uygulanacaktır:

- `IEquatable<TEnum>`
- `Equals(TEnum?)`
- `Equals(object?)`
- `GetHashCode()`
- `operator ==`
- `operator !=`

Mevcut zorunlu Source Generator ve Registry mimarisi değiştirilmeyecektir.

## 3. Lookup API

### 3.1 List

Bütün kayıtlı instance'lar salt okunur bir koleksiyonla sunulur.

```csharp
public static IReadOnlyList<TEnum> List { get; }
```

Koleksiyon:

- Registry'nin deterministik sıralamasını korur.
- Kullanıcı tarafından değiştirilemez.
- Mevcut Registry koleksiyonunu kullanır.
- Her erişimde yeni koleksiyon oluşturmaz.

### 3.2 FromName

```csharp
public static TEnum FromName(string name);
```

| Durum | Sonuç |
| --- | --- |
| Name mevcut | Eşleşen instance |
| Name bulunamadı | `KeyNotFoundException` |
| Name null | `ArgumentNullException` |
| Name boş veya whitespace | `ArgumentException` |

Karşılaştırma `StringComparer.Ordinal` ile case-sensitive yapılır.

### 3.3 FromValue

```csharp
public static TEnum FromValue(TValue value);
```

| Durum | Sonuç |
| --- | --- |
| Value mevcut | Eşleşen instance |
| Value bulunamadı | `KeyNotFoundException` |
| Value runtime'da null | `ArgumentNullException` |

Value karşılaştırmasında `EqualityComparer<TValue>.Default` kullanılır.

### 3.4 TryFromName

```csharp
public static bool TryFromName(string? name, out TEnum? result);
```

- Bulunursa `true` ve eşleşen instance.
- Bulunamazsa `false` ve `null`.
- Null, empty veya whitespace girişte `false` ve `null`.
- Normal arama başarısızlıklarında exception fırlatılmaz.

Registry initialization veya validation hataları Try metotları tarafından yutulmamalıdır.

### 3.5 TryFromValue

```csharp
public static bool TryFromValue(TValue? value, out TEnum? result);
```

- Bulunursa `true` ve eşleşen instance.
- Bulunamazsa `false` ve `null`.
- Null girişte `false` ve `null`.
- Normal arama başarısızlıklarında exception fırlatılmaz.

## 4. Ek Lookup API'leri

```csharp
public static bool ContainsName(string? name);
public static bool ContainsValue(TValue? value);
```

`ContainsName`:

- `ContainsName(null)` → `false`.
- `ContainsName("")` → `false`.
- `ContainsName("   ")` → `false`.
- Bu girişler exception fırlatmaz.
- Eşleşen instance `true` döndürür.
- Kayıtlı olmayan bir name `false` döndürür.
- Normal arama başarısızlıklarında exception fırlatılmaz.

`ContainsValue`:

- Eşleşen instance `true` döndürür.
- Kayıtlı olmayan bir value `false` döndürür.
- Null giriş `false` döndürür.
- Normal arama başarısızlıklarında exception fırlatılmaz.

`FromName`, null için `ArgumentNullException`, boş veya whitespace için `ArgumentException` fırlatmaya devam eder. `TryFromName`, null, boş veya whitespace için `false` ve `null` döndürmeye devam eder. Bu kurallar değişmez.

`Find` metotları ertelenmiştir. `TryFromName` ve `TryFromValue` zaten exception fırlatmayan arama sağlar.

Bu sürümde case-insensitive overload, predicate tabanlı arama, default-value overload veya LINQ extension eklenmeyecektir.

## 5. Name karşılaştırması

Name karşılaştırması mevcut karar doğrultusunda devam eder:

```csharp
StringComparer.Ordinal
```

- `Pending`, `Pending` ile eşleşir.
- `Pending`, `pending` ile eşleşmez.
- `PENDING`, `Pending` ile eşleşmez.

Otomatik case normalization veya culture-dependent karşılaştırma yapılmayacaktır.

## 6. Value karşılaştırması

Value karşılaştırmasında:

```csharp
EqualityComparer<TValue>.Default
```

kullanılır.

Uygulama, verilen `TValue` türünün equality davranışını desteklemelidir.

`TValue`, Registry initialization sonrasında da kararlı equality ve hash davranışını korumalıdır. Immutable `TValue` kullanılması kuvvetle tavsiye edilir.

## 7. Equality semantiği

İki SmartEnum instance'ı ancak şu iki koşul birlikte sağlanırsa eşittir:

1. Aynı SmartEnum domain tipine (`TEnum`) ait olmaları.
2. Value değerlerinin `EqualityComparer<TValue>.Default` ile eşit bulunması.

Name, equality'nin parçası değildir.

Runtime concrete type, equality'nin parçası değildir.

Registry üyeliği, equality'nin parçası değildir.

Aynı domain içinde Name değerleri farklı olsa bile Value değerleri eşitse instance'lar eşittir.

Farklı SmartEnum domain'lerine ait iki instance, Value değerleri aynı olsa bile eşit değildir. Bu runtime sonucu `Equals(object?)` ile gözlenir ve `false` döner.

Equality üyeleri:

```csharp
IEquatable<TEnum>
bool Equals(TEnum? other)
override bool Equals(object? obj)
override int GetHashCode()
public static bool operator ==(SmartEnum<TEnum, TValue>? left, SmartEnum<TEnum, TValue>? right)
public static bool operator !=(SmartEnum<TEnum, TValue>? left, SmartEnum<TEnum, TValue>? right)
```

`Equals(object?)`, `object.Equals(object?)` metodunun override'ıdır. Gizleyen (`new`) bir metot değildir.

`GetHashCode()`, `object.GetHashCode()` metodunun override'ıdır. Gizleyen (`new`) bir metot değildir.

Operatör imzaları tam olarak şunlardır:

```csharp
public static bool operator ==(
    SmartEnum<TEnum, TValue>? left,
    SmartEnum<TEnum, TValue>? right);

public static bool operator !=(
    SmartEnum<TEnum, TValue>? left,
    SmartEnum<TEnum, TValue>? right);
```

Parametreler nullable'dır. Nullable olmayan parametre listesi sözleşme değildir, çünkü `== null` nullable açık bir tüketici projesinde derlenmelidir.

### Düzeltme — operatör parametre tipi

Bu, kabul edilmiş sözleşmenin teknik açıklamasıdır. Durum **Kabul edildi (Accepted)** olarak kalır.

İlk kabul edilen metin `operator ==(TEnum? left, TEnum? right)` ve eşleşen `!=` imzasını kullanıyordu. Bu bildirim derlenmez. Derleyici **CS0563** bildirir: ikili bir operatörün en az bir parametresi, operatörün içinde bulunduğu tip olmalıdır.

`==` ve `!=`, `SmartEnum<TEnum, TValue>` üzerinde bildirilir. İçeren tip bu sınıftır. `TEnum` bir tip parametresidir. `TEnum : SmartEnum<TEnum, TValue>` kısıtı, `TEnum` türünü içeren tip yapmaz. Bu nedenle `TEnum?` burada yasal bir operatör parametresi değildir.

Geçerli parametre tipi `SmartEnum<TEnum, TValue>?` türüdür. Equality semantiği değişmez.

### Operatör çözümlemesi

- `OrderStatus == OrderStatus` gibi iki concrete domain instance karşılaştırması, kalıtılan taban operatöre bağlanır. Concrete tip `SmartEnum<TEnum, TValue>` tipine dönüşür.
- Nullable karşılaştırmalar, nullable reference type açıkken derlenir. `instance == null`, `null == instance` ve `null == null` aynı operatöre bağlanır.
- Static tipi `SmartEnum<TEnum, TValue>` olan bir değişken aynı operatörü kullanır.
- İki farklı kapalı SmartEnum domain'i arasında dönüşüm yoktur. Bu çift için `==` ve `!=` desteklenmez.
- Bu karşılaştırma derleme zamanı operatör çözümleme hatası üretir (incelenen tüketici senaryosunda CS0019).
- Bu, derleme zamanı API sınırıdır. `==` operatörünün runtime equality sonucu değildir.
- Çapraz domain runtime eşitsizliği `Equals(object?)` ile gözlenir ve `false` döner.

Null davranışı:

- `null == null` → `true`.
- `null == instance` → `false`.
- `instance == null` → `false`.
- `!=`, `==` operatörünün mantıksal tersidir.
- Equality simetrik ve tutarlı olmalıdır.

## 8. GetHashCode

```csharp
HashCode.Combine(typeof(TEnum), Value)
```

HashCode, equality ile aynı kimliği temsil eder: domain type + value.

HashCode kalıcı bir identifier değildir. Domain kimliği olarak saklanmamalı veya iletilmemelidir.

## 9. Registry index'leri ve performans

Registry şunları tutacaktır:

- Değiştirilemez instance koleksiyonu.
- Private Name dictionary.
- Private Value dictionary.

Her iki dictionary, mevcut doğrulama ile birlikte Registry initialization sırasında oluşturulur. Lookup çağrıları dictionary'yi yeniden oluşturmaz.

Beklenen ortalama erişim karmaşıklığı:

- `FromName`: O(1)
- `FromValue`: O(1)
- `TryFromName`: O(1)
- `TryFromValue`: O(1)
- `ContainsName`: O(1)
- `ContainsValue`: O(1)

Hash collision durumları dictionary equality mekanizmasıyla yönetilir. Yalnızca hash değerinin eşit olması Value eşitliği anlamına gelmez.

## 10. Public API ve erişim

`SmartEnum<TEnum, TValue>.Registry` resmi public Registry giriş noktası olarak kalır.

ADR-004 uyarınca:

`SmartEnumRegistry<TEnum, TValue>.Instance` internal kalır.

Lookup API'leri, SmartEnum domain'inin kalıtılan static API'si üzerinden sunulur. Registry implementasyon ayrıntıları ve dictionary yapıları kapsüllenmiş kalır.

Mevcut public Registry tipi ve `Items` property'si geriye dönük uyumluluğunu korur.

## 11. Değerlendirilen alternatifler

| Alternatif | Sonuç |
| --- | --- |
| Case-insensitive Name araması | Bu sürüm için reddedildi — somut gereksinim olmadan ek davranış ve belirsizlik |
| `Find` metotları | Ertelendi — Try metotları aynı non-throwing aramayı sağlar |
| Yalnızca Value üzerinden equality | Reddedildi — farklı domain'ler aynı Value nedeniyle eşit sayılır |
| Runtime concrete type üzerinden equality | Reddedildi — abstract root ile concrete derived instance arasında tutarsızlık |
| Her Lookup çağrısında dictionary oluşturulması | Reddedildi — gereksiz allocation ve maliyet |

## 12. Sonuçlar

### Olumlu

- Tahmin edilebilir lookup.
- Verimli Name ve Value araması.
- Domain instance'ları arasında tutarlı equality.
- Exception fırlatan ve fırlatmayan API'lerin ayrılması.
- Registry'nin tek doğruluk kaynağı olması.
- Reflection veya runtime discovery gerektirmemesi.

### Ödünleşimler

- `TValue` kararlı equality ve hash davranışını korumalıdır.
- Eklenen public metotlar uzun vadeli API taahhüdü oluşturur.
- HashCode süreçler arasında kalıcı değildir.
- Bu sürümde case-insensitive lookup yoktur.

## 13. Güvenlik ve performans

- Reflection tabanlı lookup yapılmayacaktır.
- Global mutable Registry kullanılmayacaktır.
- Dışarıya değiştirilebilir dictionary sunulmayacaktır.
- Registry initialization hataları yutulmayacaktır.
- Dictionary key'leri kararlı equality ve hash davranışını korumalıdır.
- Lookup işlemleri yeni dictionary veya koleksiyon oluşturmamalıdır.

## 14. Test stratejisi

### Lookup

- Mevcut Name ve Value.
- Bulunamayan Name ve Value.
- Null Name ve Value.
- Empty ve whitespace Name.
- Case sensitivity.
- Try başarı ve başarısızlık.
- Contains başarı ve başarısızlık.
- `ContainsName(null)`, `ContainsName("")` ve `ContainsName("   ")` `false` döndürür ve exception fırlatmaz.
- Runtime Registry initialization hatasının yutulmaması.

### Equality

- Aynı instance.
- Aynı domain ve Value'ya sahip farklı instance'lar.
- Aynı domain içinde farklı Value.
- Farklı domain'lerde aynı Value; `Equals(object?)` `false` döner.
- İki farklı domain tipi arasında `==` ve `!=` derlenmez (incelenen tüketici senaryosunda CS0019). Bu bir derleme zamanı sınırıdır, `==` operatörünün runtime `false` sonucu değildir.
- Aynı Value ve farklı Name.
- `null == null` → `true`.
- `null == instance` ve `instance == null` → `false`.
- `!=`, `==` operatörünün mantıksal tersidir.
- Operatör parametreleri `SmartEnum<TEnum, TValue>?` türündedir. Concrete domain karşılaştırmaları kalıtılan operatöre bağlanır. Generic taban referansları aynı operatörü kullanır. Null karşılaştırmaları nullable reference type altında derlenir.
- Operatörün `Equals` ile tutarlılığı.
- Eşit instance'larda HashCode tutarlılığı.
- Abstract root ve concrete derived instance'lar.

### Registry

- `List` değiştirilemezliği.
- Dictionary kapsüllenmesi.
- Aynı assembly içindeki farklı domain'lerin bağımsız index'leri.
- Hash collision davranışı.
- Lookup sırasında dictionary'nin tekrar oluşturulmaması.

## 15. Referanslar

- ADR-002 — Source Generator mimarisi
- ADR-003 — SmartEnum Registry doğrulama ve tekillik
- ADR-004 — Registry public erişim
- Microsoft dokümantasyonu: `EqualityComparer<T>.Default`
- Microsoft dokümantasyonu: `Dictionary<TKey, TValue>`
- Microsoft dokümantasyonu: `HashCode.Combine`
