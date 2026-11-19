# YUIBackend

.NET 10 ile geliştirilen, katmanlı mimari kullanan bir Web API. Projede CQRS/MediatR, EF Core ve SQL Server, JWT Bearer kimlik doğrulaması, yumuşak silme (soft delete), sayfalama, loglama ve MassTransit outbox altyapısı bulunur.

> **Giriş bilgileri:** Uygulamada varsayılan bir kullanıcı adı/parola yoktur ve kayıt endpointi bulunmaz. Yerel User Secrets içinde henüz `InitialUser` bilgileri tanımlı değilse token almak için önce aşağıdaki kurulum adımlarını tamamlayın. Örnek kullanıcı adı olarak `admin` kullanabilirsiniz; parolayı kendiniz belirleyin. Gerçek parolayı README'ye, kaynak koda veya Git'e eklemeyin.

## İçindekiler

- [Teknoloji ve mimari](#teknoloji-ve-mimari)
- [Projeler](#projeler)
- [Gereksinimler](#gereksinimler)
- [Yerel kurulum](#yerel-kurulum)
- [İlk kullanıcı ve JWT token](#ilk-kullanıcı-ve-jwt-token)
- [API uçları](#api-uçları)
- [Sayfalama](#sayfalama)
- [Veri modeli ve güvenlik davranışları](#veri-modeli-ve-güvenlik-davranışları)
- [EF Core migration işlemleri](#ef-core-migration-işlemleri)
- [Kullanılan NuGet paketleri](#kullanılan-nuget-paketleri)
- [Testler](#testler)
- [Docker Compose](#docker-compose)
- [Mimari dokümanı](ARCHITECTURE.md)
- [Bilinen notlar](#bilinen-notlar)

## Teknoloji ve mimari

- **.NET 10 / ASP.NET Core Web API:** REST API, controller routing, model binding, doğrulama, middleware ve hosting.
- **Clean Architecture:** Domain katmanı iş varlıklarını ve sözleşmelerini; Application CQRS isteklerini/handler'larını; Infrastructure veri erişimi ve harici servisleri; WebAPI HTTP yüzeyini içerir.
- **CQRS:** Okuma istekleri `IQuery<T>`, yazma istekleri `ICommand<T>`/`ICommand` ile tanımlanır ve MediatR aracılığıyla handler'lara gönderilir.
- **SQL Server + Entity Framework Core 10:** İlişkisel kalıcılık, Fluent API entity konfigürasyonları ve sürümlenmiş migration'lar.
- **JWT Bearer:** Login başarılı olduğunda imzalı, bir saat geçerli JWT döndürülür. Login dışındaki controller uçları varsayılan olarak kimlik doğrulaması gerektirir.
- **ASP.NET Core User Secrets:** Yerel geliştirme parolaları, bağlantı dizeleri ve JWT anahtarı kaynak kod dışında saklanır. User Secrets üretim sırları için bir secret vault yerine geçmez.
- **Soft delete:** Silme isteği satırı fiziksel olarak silmez; `IsDeleted` işaretlenir ve EF Core global filtreleri bu satırı normal sorgulardan gizler.
- **MassTransit + RabbitMQ:** Mesajlaşma ve EF Core transactional outbox/inbox yapılandırması.
- **Serilog:** Uygulama logları konsol/dosya yapılandırmasına ve SQL Server'daki `dbo.ApplicationLogs` tablosuna yazılır.
- **OpenTelemetry + Prometheus:** HTTP tracing/metrics ve `/metrics` scrape endpoint'i.
- **Redis:** Cache/bağlantı altyapısı için yapılandırılmış istemci.
- **Rate limiting:** Controller endpoint'lerinde sabit pencere sınırlaması (100 istek/dakika, 10 istek kuyruk limiti).
- **Swagger / OpenAPI:** Development ortamında API keşfi ve Bearer token ile deneme.

## Projeler

| Proje | Sorumluluk |
|---|---|
| `src/YUIBackend.Domain` | Entity'ler, domain sözleşmeleri, `Result` tipleri ve repository arayüzleri |
| `src/YUIBackend.Application` | CQRS query/command handler'ları, FluentValidation doğrulamaları, DTO'lar ve pipeline behavior'ları |
| `src/YUIBackend.Infrastructure` | `AppDbContext`, EF Core konfigürasyonları/migration'ları, SQL Server, repository implementasyonları, Redis ve MassTransit |
| `src/YUIBackend.WebAPI` | ASP.NET Core başlangıcı, controller'lar, JWT kimlik doğrulama, middleware, Swagger ve health endpoint'i |
| `tests/YUIBackend.Tests` | xUnit birim ve SQL Server entegrasyon testleri |

## Gereksinimler

- .NET 10 SDK
- SQL Server (bağlantı dizesi User Secrets veya ortam değişkeninde tanımlanmalı)
- Redis ve RabbitMQ (mevcut API host'u bunları başlangıçta yapılandırır; yerel çalıştırmada ikisi de erişilebilir olmalıdır)
- EF CLI: `dotnet tool install --global dotnet-ef` (zaten kuruluysa yeniden yüklemeyin)

## Yerel kurulum

Komutları README'nin bulunduğu `YUIBackend` proje klasöründe PowerShell ile çalıştırın:

```powershell
cd .\YUIBackend
```

### 1. Gizli ayarları tanımlayın

Projede User Secrets zaten başlatılmıştır. SQL Server bağlantı dizenizi ve JWT ayarlarını yerel secret store'a ekleyin. Aşağıdaki `<...>` alanlarını kendi ortamınıza göre değiştirin:

```powershell
dotnet user-secrets set "ConnectionStrings:SqlServer" "<SQL-Server-connection-string>" --project ".\src\YUIBackend.WebAPI\YUIBackend.WebAPI.csproj"
dotnet user-secrets set "Jwt:Issuer" "YUIBackend" --project ".\src\YUIBackend.WebAPI\YUIBackend.WebAPI.csproj"
dotnet user-secrets set "Jwt:Audience" "YUIBackend.Client" --project ".\src\YUIBackend.WebAPI\YUIBackend.WebAPI.csproj"
```

JWT anahtarı HS256 imzalama/doğrulama için en az 32 UTF-8 byte olmalıdır. Güçlü, rastgele bir değer üretip secret olarak kaydedin; üretilen değeri repoya veya sohbete koymayın:

```powershell
$bytes = New-Object byte[] 48
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$jwtKey = [Convert]::ToBase64String($bytes)
dotnet user-secrets set "Jwt:SecretKey" $jwtKey --project ".\src\YUIBackend.WebAPI\YUIBackend.WebAPI.csproj"
$rng.Dispose()
Remove-Variable jwtKey, bytes, rng
```

`Remove-Variable` sadece PowerShell değişkenini temizler; User Secrets'taki ayarı silmez. Mevcut host yapılandırması Redis ve RabbitMQ bağlantılarını da beklediğinden yerel çalıştırma için bunları User Secrets'a ekleyin:

```powershell
dotnet user-secrets set "ConnectionStrings:Redis" "<Redis-connection-string>" --project ".\src\YUIBackend.WebAPI\YUIBackend.WebAPI.csproj"
dotnet user-secrets set "ConnectionStrings:RabbitMQ" "<RabbitMQ-connection-string>" --project ".\src\YUIBackend.WebAPI\YUIBackend.WebAPI.csproj"
```

User Secrets değerlerini görmek yerine yalnızca secret anahtarlarını listelemek için:

```powershell
dotnet user-secrets list --project ".\src\YUIBackend.WebAPI\YUIBackend.WebAPI.csproj"
```

> User Secrets geliştirme kolaylığıdır; dosyadaki değerler işletim sistemi kullanıcı hesabının profilinde saklanır, şifreli bir production vault olarak düşünülmemelidir. Üretimde uygun bir secret manager veya erişimi kısıtlı ortam değişkenleri kullanın.

### 2. Migration'ları uygulayın

API başlangıcında otomatik migration çalıştırılmaz; veritabanı güncellemesi ayrı bir adımdır:

```powershell
dotnet ef database update --project ".\src\YUIBackend.Infrastructure\YUIBackend.Infrastructure.csproj" --startup-project ".\src\YUIBackend.WebAPI\YUIBackend.WebAPI.csproj" --context AppDbContext
```

### 3. API'yi çalıştırın

```powershell
dotnet run --project ".\src\YUIBackend.WebAPI\YUIBackend.WebAPI.csproj" --launch-profile http
```

HTTP profili `http://localhost:5270` adresini kullanır ve tarayıcıda Swagger'ı açar. Swagger UI: <http://localhost:5270/swagger>. HTTPS profili: <https://localhost:7139/swagger>.

Port kullanımda hatası alırsanız, önce çalışan eski API örneğini Visual Studio Stop düğmesi veya `Shift+F5` ile durdurun. Birden fazla API örneği aynı portu aynı anda dinleyemez.

## Çoklu kullanıcı, kayıt ve JWT token

### Yönetici hesabını ilk kez oluşturma

Uygulama bilinen bir varsayılan parola üretmez. İlk yönetici hesabının kullanıcı adı ve güçlü parolası (en az 12 karakter) User Secrets ile yapılandırılır:

```powershell
dotnet user-secrets set "InitialUser:Username" "admin" --project ".\src\YUIBackend.WebAPI\YUIBackend.WebAPI.csproj"
dotnet user-secrets set "InitialUser:Password" "<kendi-guclu-parolaniz>" --project ".\src\YUIBackend.WebAPI\YUIBackend.WebAPI.csproj"
```

Veritabanında henüz kullanıcı yoksa bu bilgilerle yapılan ilk başarılı `POST /api/auth/login` hesabı oluşturur ve yönetici rolü verir. Daha önceki tek başlangıç kullanıcısını bulunan veritabanları için migration `IsAdmin=true` atar; migration'ı uygulayın:

```powershell
dotnet ef database update --project ".\src\YUIBackend.Infrastructure\YUIBackend.Infrastructure.csproj" --startup-project ".\src\YUIBackend.WebAPI\YUIBackend.WebAPI.csproj"
```

Parola ASP.NET Core `PasswordHasher` ile hash'lenir. Başlangıç secret'ı yalnızca ilk yönetici hesabını kurar; mevcut kullanıcının parolasını değiştirmez ve veritabanına başka kullanıcı eklemez. Gerçek parolayı README'ye, kaynak koda veya Git'e eklemeyin.

### Müşteri hesabı kaydı ve giriş

Müşteriler anonim `POST /api/auth/register` ile kendi müşteri profillerini ve giriş kullanıcılarını birlikte oluşturabilir. Username (harf duyarsız) ve e-posta tekrarları reddedilir. Örnek:

```powershell
$body = @{
    username = "ada.yilmaz"
    password = "<en-az-12-karakter-guclu-parola>"
    firstName = "Ada"
    lastName = "Yilmaz"
    email = "ada@example.com"
    phoneNumber = "+90 555 000 00 00"
} | ConvertTo-Json

Invoke-RestMethod `
    -Method Post `
    -Uri "http://localhost:5270/api/auth/register" `
    -ContentType "application/json" `
    -Body $body
```

Kayıt yanıtı müşteri kimliğini ve kullanıcı adını verir. Ardından `POST /api/auth/login` ile token alın:

```powershell
$body = @{ username = "ada.yilmaz"; password = "<kaydolurken-belirlediginiz-parola>" } | ConvertTo-Json
$login = Invoke-RestMethod -Method Post -Uri "http://localhost:5270/api/auth/login" -ContentType "application/json" -Body $body
$headers = @{ Authorization = "Bearer $($login.accessToken)" }
Invoke-RestMethod -Uri "http://localhost:5270/api/accounts" -Headers $headers
```

Token bir saat geçerlidir ve `Bearer` şemasıyla gönderilir. Aktif olmayan kullanıcılar veya müşteriler giriş yapamaz. Müşteri rolündeki kullanıcılar yalnız kendi müşteri profilini, hesaplarını ve bu hesaplarla ilişkili finans geçmişi/raporlarını görebilir; müşteri hesap oluştururken `customerId` sunucu tarafından oturumdan alınır. Para transferinde hedef başka müşterinin hesabı olabilir, ancak kaynak hesabın sahibi olmak gerekir. Müşteri ve ürün yönetimi yazma uçları yönetici rolüne ayrılmıştır. Hesap sahipliği her istekte token'daki müşteri kimliğine güvenmek yerine veritabanından doğrulanır.

Korunan uçlarda token'ı gizli tutun; parolayla aynı şekilde paylaşmayın veya loglamayın. Hatalı kullanıcı/parola `401 Unauthorized`, yinelenen username/e-posta `409 Conflict`, eksik ilk yönetici yapılandırması `503 Service Unavailable` döndürür. Swagger'da **Authorize** düğmesinde `Bearer <token>` kullanın.

### Yönetici girişi için PowerShell örneği

Yönetici hesabıyla `POST /api/auth/login` adresine JSON gönderin:

```powershell
$body = @{
	username = "admin"
	password = "<InitialUser:Password-icin-belirlediginiz-parola>"
} | ConvertTo-Json

$login = Invoke-RestMethod `
	-Method Post `
	-Uri "http://localhost:5270/api/auth/login" `
	-ContentType "application/json" `
	-Body $body

$login
```

Başarılı yanıtta `accessToken`, `tokenType` (`Bearer`) ve `expiresAtUtc` döner. Token'ı gizli tutun; parolayla aynı şekilde paylaşmayın veya loglamayın. Korunan bir endpoint'i çağırırken:

```powershell
$headers = @{ Authorization = "Bearer $($login.accessToken)" }
Invoke-RestMethod -Uri "http://localhost:5270/api/customers?pageNumber=1&pageSize=20" -Headers $headers
```

Token bir saat geçerlidir; süresi dolunca tekrar login olun. Hatalı kullanıcı/parola `401 Unauthorized`, ilk kullanıcı ayarları eksikse `503 Service Unavailable` döndürür. Swagger'da **Authorize** düğmesine `Bearer <accessToken>` girerek korunan uçları deneyebilirsiniz.

### Diğer ortamlarda secret tanımlama

User Secrets yalnızca yerel geliştirme içindir. Başka makine/deployment ortamında `InitialUser__Username`, `InitialUser__Password`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SecretKey` ve `ConnectionStrings__SqlServer` ortam değişkenlerini veya uygun secret manager'ı kullanın. Bootstrap parolasını dağıtım/kurulum sonrasında mümkünse kaldırın; mevcut hesabın giriş doğrulaması hash üzerinden devam eder.

## API uçları

Controller uçları JWT ister; istisnalar kayıt, login ve health endpoint'leridir. Ürün uçları yalnızca yönetici rolüne açıktır. Aşağıdaki uçlarda `/api` öneki kullanılır.

| Method | Endpoint | Açıklama |
|---|---|---|
| `POST` | `/api/auth/register` | Anonim müşteri kaydı; müşteri ve giriş hesabını atomik oluşturur. |
| `POST` | `/api/auth/login` | İlk yönetici hesabını yapılandırılmış bilgilerle kurar veya kullanıcıyı doğrular; JWT döndürür. |
| `GET` | `/api/customers?pageNumber=1&pageSize=20` | Yönetici tüm müşterileri, müşteri yalnız kendi profilini listeler |
| `GET` | `/api/customers/{id}` | Müşteri detayı |
| `POST` | `/api/customers` | (Yönetici) Müşteri oluşturur |
| `PUT` | `/api/customers/{id}` | Müşteri kendi profil bilgilerini, yönetici tüm müşteri bilgilerini günceller; müşteri kendi aktiflik durumunu değiştiremez |
| `DELETE` | `/api/customers/{id}` | (Yönetici) Müşteriyi soft delete eder; hesabı olan müşteri silinemez. |
| `GET` | `/api/accounts?pageNumber=1&pageSize=20` | Yönetici tüm hesapları, müşteri yalnız kendi hesaplarını listeler |
| `GET` | `/api/accounts/{id}` | Sahip olunan hesap detayı ve bakiye |
| `POST` | `/api/accounts` | Hesap açar; müşteri sahipliği oturumdan alınır |
| `PUT` | `/api/accounts/{id}` | Sahip olunan hesabın durumunu (`Active`, `Inactive`, `Closed`) günceller |
| `DELETE` | `/api/accounts/{id}` | Sahip olunan, bakiyesi sıfır hesabı soft delete eder. |
| `POST` | `/api/financialtransactions/deposit` | Hesaba para yatırır; `Idempotency-Key` header'ı zorunludur |
| `POST` | `/api/financialtransactions/withdraw` | Hesaptan para çeker; `Idempotency-Key` header'ı zorunludur |
| `POST` | `/api/financialtransactions/transfer` | Hesaplar arasında transfer yapar; `Idempotency-Key` header'ı zorunludur |
| `GET` | `/api/financialtransactions` | Hesap, tarih, tür ve tutar filtreleriyle sayfalı işlem geçmişi |
| `GET` | `/api/financialtransactions/{id}` | Finansal işlem detayı |
| `GET` | `/api/financialtransactions/account/{accountId}` | Hesap bazlı, filtreli ve sayfalı finansal işlem geçmişi |
| `GET` | `/api/financialtransactions/reports` | Günlük, haftalık veya aylık işlem raporu; hesap filtresi isteğe bağlı |
| `GET` | `/api/products?pageNumber=1&pageSize=20` | (Yönetici) Ürün sayfalı listesi |
| `GET` | `/api/products/{id}` | (Yönetici) Ürün detayı |
| `POST` | `/api/products` | (Yönetici) Ürün oluşturur |
| `PUT` | `/api/products/{id}` | (Yönetici) Ürünü günceller |
| `DELETE` | `/api/products/{id}` | (Yönetici) Ürünü soft delete eder. |
| `GET` | `/health` | Servis health-check özeti; JWT istemez. |
| `GET` | `/metrics` | Prometheus metrics çıktısı; dış erişimi production'da ağ/policy ile sınırlayın. |

Yönetici müşteri oluşturma JSON örneği:

```json
{
  "firstName": "Ada",
  "lastName": "Yilmaz",
  "email": "ada@example.com",
  "phoneNumber": "+90 555 000 00 00"
}
```

Müşteri kendi profilini `PUT /api/customers/{customerId}` ile güncelleyebilir. Gövdedeki `id` route ile aynı olmalı; müşteri `isActive` alanı gönderse bile bu alan dikkate alınmaz. Yalnız yönetici müşteri aktifliğini değiştirebilir.

```json
{
  "id": "<customer-guid>",
  "firstName": "Ada",
  "lastName": "Yilmaz",
  "email": "ada.yilmaz@example.com",
  "phoneNumber": "+90 555 000 00 00"
}
```

Hesap oluşturma JSON örneği:

```json
{
  "customerId": "<customer-guid>",
  "accountNumber": "TR000000000000000000000000",
  "currencyCode": "TRY"
}
```

Müşteri hesabı açarken `customerId` sunucu tarafından oturumdaki müşteri kimliğiyle değiştirilir; yönetici bu alanda müşteri seçebilir. Bir müşteri birden fazla hesap açabilir.

Hesap güncelleme JSON örneği:

```json
{
  "id": "<account-guid>",
  "status": "Inactive"
}
```

`PUT` isteklerinin route içindeki `id` değeri ile JSON gövdesindeki `id` aynı olmalıdır.

### Para yatırma, çekme ve transfer

İşlem endpoint'leri `Idempotency-Key` header'ını zorunlu tutar. İstemci bir isteğin sonucunu alamazsa aynı key ve aynı içerikle tekrar deneyebilir; yinelenen istek aynı işlemi tekrar uygulamaz. Aynı key farklı bir payload ile kullanılırsa `409 Conflict` döner. Key'i her retry'da yeniden üretmeyin.

```powershell
$headers = @{
    Authorization = "Bearer $($login.accessToken)"
    "Idempotency-Key" = [guid]::NewGuid().ToString()
}
$deposit = @{ accountId = "<account-guid>"; amount = 100.25; description = "Hesaba para yatırma" } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "http://localhost:5270/api/financialtransactions/deposit" -Headers $headers -ContentType "application/json" -Body $deposit
```

Çekme gövdesi aynı `accountId`, `amount` ve isteğe bağlı `description` alanlarını kullanır; endpoint `/api/financialtransactions/withdraw` olur. Transfer gövdesi:

```json
{
  "sourceAccountId": "<source-account-guid>",
  "destinationAccountId": "<destination-account-guid>",
  "amount": 25.50,
  "description": "Hesaplar arası transfer"
}
```

Transferi `POST /api/financialtransactions/transfer` adresine aynı zorunlu header ile gönderin. Tutar pozitif, en fazla iki ondalık basamaklı ve desteklenen bakiye sınırları içinde olmalıdır. Yetersiz bakiye, pasif hesap, farklı para birimi ve bakiye üst sınırı gibi iş kuralları başarısız işlem kaydı oluşturur ve `422 Unprocessable Entity` ile açıklanır. Başarılı yeni işlem `201`, başarılı tekrar `200`, hesap bulunamaması `404` döner.

### İşlem geçmişi ve raporlar

İşlem listeleme endpoint'leri `accountId`, `fromUtc`, `toUtc`, `type` (`Deposit`, `Withdrawal`, `Transfer`), `minimumAmount`, `maximumAmount`, `pageNumber`, `pageSize` ve `descending` query parametrelerini destekler. Tarih aralığı UTC olmalı; `fromUtc`/`toUtc` ISO-8601 biçiminde gönderilmelidir. Örnek:

```text
GET /api/financialtransactions?accountId=<account-guid>&fromUtc=2026-01-01T00:00:00Z&toUtc=2026-02-01T00:00:00Z&type=Transfer&minimumAmount=10&pageNumber=1&pageSize=20&descending=true
GET /api/financialtransactions/account/<account-guid>?fromUtc=2026-01-01T00:00:00Z&toUtc=2026-02-01T00:00:00Z
GET /api/financialtransactions/reports?fromUtc=2026-01-01T00:00:00Z&toUtc=2026-02-01T00:00:00Z&period=Weekly&accountId=<account-guid>
```

Rapor `period` değeri `Daily`, `Weekly` (Pazartesi başlangıçlı) veya `Monthly` olabilir. Yanıtta başarılı işlemler için para giriş/çıkış toplamı, başarılı/başarısız işlem sayıları ve dönem bazlı kırılım yer alır. Hesap raporunda yalnızca ilgili hesabın giriş/çıkışları toplanır; transfer iki hesap açısından da uygun yönde sayılır. Rapor tarih aralığı en fazla beş yıldır ve `toUtc` üst sınırı hariç tutulur.

İşlem geçmişi değiştirilemez ve silinemez; başarısız finansal denemeler de denetim amacıyla kaydedilir.

## Sayfalama

Liste endpoint'leri `pageNumber` (1'den başlar) ve `pageSize` (1-100) parametrelerini kabul eder; varsayılanlar sırasıyla `1` ve `20`'dir. Örnek:

```text
GET /api/customers?pageNumber=2&pageSize=25
```

Yanıtta `items`, `totalCount`, `pageNumber`, `pageSize` ve `totalPages` alanları bulunur. Sıralama sayfa sonuçlarının tutarlı kalması için entity oluşturulma zamanı/ID; finansal hareketlerde olay zamanı/ID ile yapılır.

## Veri modeli ve güvenlik davranışları

- Bir `Customer` birden fazla `Account` sahibi olabilir.
- `FinancialTransaction`, kaynak ve hedef hesapla isteğe bağlı ilişkiler kurar. Hesap ilişkilerinde işlem geçmişinin yanlışlıkla cascade silinmesini önlemek için `Restrict` kullanılır.
- Tutarlar SQL Server `decimal(18,2)` olarak saklanır; `float`/`double` kullanılmaz.
- Account number, müşteri e-postası ve finansal işlem numarası için benzersiz indeksler tanımlıdır.
- `AccountStatus`, `FinancialTransactionStatus` ve `FinancialTransactionType`, `ACY.SmartEnums` ile type-safe singleton değerleridir; EF Core veritabanında adlarını string olarak saklar.
- `IsDeleted` global query filter ile listelerden ve ID sorgularından gizlenir; fiziksel satır korunur. Soft delete geri alma endpoint'i yoktur.
- Hesabı olan müşteri ve bakiyesi sıfır olmayan hesap soft delete edilemez. Bakiye sıfır olmayan hesap `Closed` durumuna da geçirilemez.
- Para yatırma/çekme ve transferler SQL transaction içinde yürütülür; bakiye değişiklikleri koşullu atomik SQL güncellemeleriyle yapılır. Transfer hesapları GUID sırasıyla kilitlenir ve transaction içinde tamamı başarılı olur veya geri alınır.
- Her işlem için benzersiz `Idempotency-Key` kullanılır. Veritabanındaki benzersiz, nullable indeks tekrar isteklerini eşzamanlı durumda da korur; anahtar eski ledger kayıtlarında nullable kalır.
- Login endpoint'i dışında controller işlemleri fallback authorization policy ile JWT ister. Swagger/OpenAPI arayüzü Development ortamında etkinleştirilir.
- JWT issuer/audience/signing key doğrulanır; yalnızca HS256 kabul edilir. Geliştirme ve üretimde ayrı, rastgele ve güçlü imza anahtarı kullanın.

## EF Core migration işlemleri

Yeni migration oluşturmak için:

```powershell
dotnet ef migrations add <MigrationAdi> --project ".\src\YUIBackend.Infrastructure\YUIBackend.Infrastructure.csproj" --startup-project ".\src\YUIBackend.WebAPI\YUIBackend.WebAPI.csproj" --context AppDbContext --output-dir Persistence\Migrations
```

Veritabanını güncellemek için [Migration'ları uygulayın](#2-migrationları-uygulayın) bölümündeki `database update` komutunu çalıştırın. API başlangıcında migration otomatik uygulanmadığından, migration komutunu ayrıca çalıştırın.

## Kullanılan NuGet paketleri

Versiyonlar proje dosyalarındaki doğrudan `PackageReference` değerleridir. Transitive bağımlılıklar tabloya dahil değildir.

### Domain

| Paket | Sürüm | Kullanım |
|---|---:|---|
| `ACY.SmartEnums` | 1.0.0 | Finans alanındaki enum benzeri durum/tipleri, ad/değer lookup destekli SmartEnum olarak modelleme |
| `MediatR` | 12.4.1 | Domain event/notification sözleşmeleri ve Application CQRS entegrasyonu |

### Application

| Paket | Sürüm | Kullanım |
|---|---:|---|
| `AutoMapper` | 13.0.1 | Uygulama DTO/entity eşlemesi |
| `FluentValidation` | 11.11.0 | Command ve query doğrulama kuralları |
| `FluentValidation.DependencyInjectionExtensions` | 11.11.0 | Validator'ları DI ile kaydetme |
| `MediatR` | 12.4.1 | CQRS request/handler ve pipeline behavior altyapısı |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.6 | Uygulama servis kayıt sözleşmeleri |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.6 | Framework bağımsız loglama sözleşmeleri |

### Infrastructure

| Paket | Sürüm | Kullanım |
|---|---:|---|
| `AspNetCore.HealthChecks.Rabbitmq` | 9.0.0 | RabbitMQ health-check entegrasyonu için paket referansı |
| `AspNetCore.HealthChecks.Redis` | 9.0.0 | Redis health-check entegrasyonu |
| `MassTransit.EntityFrameworkCore` | 8.4.1 | EF Core transactional outbox/inbox ve saga kalıcılığı entegrasyonu |
| `MassTransit.RabbitMQ` | 8.4.1 | RabbitMQ transport ve consumer/endpoint yapılandırması |
| `Microsoft.EntityFrameworkCore` | 10.0.6 | ORM, DbContext, LINQ ve modelleme |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.6 | EF tasarım zamanı ve migration üretimi |
| `Microsoft.EntityFrameworkCore.SqlServer` | 10.0.6 | SQL Server EF provider'ı |
| `Microsoft.EntityFrameworkCore.Tools` | 10.0.6 | EF migration/database CLI araçları |
| `Microsoft.Extensions.Configuration.UserSecrets` | 10.0.0 | Tasarım zamanı yapılandırmasında User Secrets okuma |
| `OpenTelemetry.Exporter.Prometheus.AspNetCore` | 1.15.2-beta.1 | Prometheus metrics export |
| `OpenTelemetry.Extensions.Hosting` | 1.15.2 | OpenTelemetry'nin host/DI entegrasyonu |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.15.1 | ASP.NET Core request tracing/metrics |
| `Scrutor` | 7.0.0 | Infrastructure sınıflarını assembly scan ile DI'ya kaydetme |
| `Serilog.AspNetCore` | 9.0.0 | ASP.NET Core logging entegrasyonu |
| `Serilog.Sinks.Seq` | 9.0.0 | Seq sink entegrasyonu için paket referansı |
| `StackExchange.Redis` | 2.12.14 | Redis istemcisi |

### WebAPI

| Paket | Sürüm | Kullanım |
|---|---:|---|
| `AspNetCore.HealthChecks.Redis` | 9.0.0 | Redis health check |
| `AspNetCore.HealthChecks.SqlServer` | 9.0.0 | SQL Server health check |
| `AspNetCore.HealthChecks.UI.Client` | 9.0.0 | Health-check UI formatında HTTP yanıtı |
| `MassTransit.RabbitMQ` | 8.4.1 | API host'unda messaging transport desteği |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.6 | Bearer JWT authentication middleware |
| `Microsoft.AspNetCore.OpenApi` | 10.0.6 | ASP.NET Core OpenAPI desteği |
| `Serilog.Sinks.MSSqlServer` | 9.0.0 | Logları SQL Server'a yazma ve `ApplicationLogs` tablosunu oluşturma |
| `System.Configuration.ConfigurationManager` | 9.0.4 | ConfigurationManager uyumluluk bağımlılığı |
| `Swashbuckle.AspNetCore` | 10.1.4 | Swagger/OpenAPI belgesi ve Swagger UI |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.6 | WebAPI startup projesi üzerinden EF tasarım zamanı |
| `Microsoft.IdentityModel.Tokens` | 8.17.0 | JWT signing key ve token validation tipleri |
| `OpenTelemetry.Exporter.Prometheus.AspNetCore` | 1.15.2-beta.1 | Prometheus exporter |
| `OpenTelemetry.Extensions.Hosting` | 1.15.2 | Host ile OpenTelemetry entegrasyonu |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.15.1 | ASP.NET Core tracing/metrics |
| `OpenTelemetry.Instrumentation.Http` | 1.15.0 | HTTP client tracing |
| `Serilog.AspNetCore` | 9.0.0 | Web API loglama entegrasyonu |

### Test projesi

`coverlet.collector` 6.0.4 (coverage collector), `FluentAssertions` 8.9.0 (assertion API), `Microsoft.NET.Test.Sdk` 17.14.1 (test runner/build integration), `NSubstitute` 5.3.0 (test doubles), `xunit` 2.9.3 ve `xunit.runner.visualstudio` 3.1.4 (xUnit test altyapısı).

## Testler

Unit testlerini çalıştırmak için:

```powershell
dotnet test ".\tests\YUIBackend.Tests\YUIBackend.Tests.csproj"
```

SQL Server entegrasyon testleri aynı test projesindedir; transaction, idempotency, yetersiz bakiye, raporlama ve eşzamanlı çekim davranışlarını gerçek SQL Server üzerinde doğrular. Test koleksiyonu benzersiz bir geçici veritabanı oluşturup migration'ları uygular ve test bitince siler. Test kullanıcısının `master` veritabanında veritabanı oluşturup silme izni olan bir SQL Server bağlantısı gereklidir. Bağlantı dizesini `YUIBACKEND_TEST_SQLSERVER` ortam değişkenine verin; testler bu değişken yoksa `Skipped` olarak raporlanır.

```powershell
$env:YUIBACKEND_TEST_SQLSERVER = "Server=localhost,1433;Database=master;User Id=sa;Password=<test-sql-password>;Encrypt=True;TrustServerCertificate=True"
dotnet test ".\tests\YUIBackend.Tests\YUIBackend.Tests.csproj"
Remove-Item Env:YUIBACKEND_TEST_SQLSERVER
```

Unit testler çevrimdışı çalışır. SQL entegrasyon testlerinde kullanacağınız SQL Server hesabı yalnızca izole geliştirme/test sunucusunda veritabanı oluşturma yetkisine sahip olmalıdır.

## Docker Compose

Docker Desktop/Docker Engine ve Compose v2 gereklidir. Compose, SQL Server 2022, Redis, RabbitMQ ve .NET 10 API'yi başlatır. SQL/JWT/ilk kullanıcı sırları zorunlu environment değişkenleridir; repoda varsayılan parola bulunmaz:

```powershell
$env:MSSQL_SA_PASSWORD = "<guclu-ve-test-ortamina-ozel-sql-parolasi>"
$jwtBytes = New-Object byte[] 48
$jwtRng = [Security.Cryptography.RandomNumberGenerator]::Create()
$jwtRng.GetBytes($jwtBytes)
$env:JWT_SECRET_KEY = [Convert]::ToBase64String($jwtBytes)
$jwtRng.Dispose()
Remove-Variable jwtBytes, jwtRng
$env:INITIAL_USER_USERNAME = "admin"
$env:INITIAL_USER_PASSWORD = "<guclu-ve-test-ortamina-ozel-ilk-kullanici-parolasi>"
$env:GRAFANA_ADMIN_PASSWORD = "<guclu-ve-test-ortamina-ozel-grafana-parolasi>"
$env:Jwt__Issuer = "YUIBackend"
$env:Jwt__Audience = "YUIBackend.Client"
$env:Jwt__SecretKey = $env:JWT_SECRET_KEY
docker compose up --build -d sqlserver redis rabbitmq
```

SQL Server hazır olduktan sonra API veritabanını ve migration'ları oluşturun. Aşağıdaki `sqlcmd` komutu veritabanını oluşturur; EF komutu şemayı migration'larla günceller:

```powershell
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $env:MSSQL_SA_PASSWORD -C -Q "IF DB_ID(N'YUIBackend') IS NULL CREATE DATABASE [YUIBackend]"
$env:ConnectionStrings__SqlServer = "Server=localhost,1433;Database=YUIBackend;User Id=sa;Password=$env:MSSQL_SA_PASSWORD;Encrypt=True;TrustServerCertificate=True"
$env:ConnectionStrings__Redis = "localhost:6379,abortConnect=false"
$env:ConnectionStrings__RabbitMQ = "rabbitmq://localhost"
dotnet ef database update --project ".\src\YUIBackend.Infrastructure\YUIBackend.Infrastructure.csproj" --startup-project ".\src\YUIBackend.WebAPI\YUIBackend.WebAPI.csproj" --context AppDbContext

docker compose up --build -d api
```

Swagger: <http://localhost:5270/swagger>. SQL Server, Redis ve RabbitMQ health-check'leri sağlıklı olmadan API başlatılmaz. Migration uygulama başlangıcında otomatik çalışmaz. İsteğe bağlı Prometheus/Grafana'yı `observability` profili ve ayrı bir `GRAFANA_ADMIN_PASSWORD` ile başlatabilirsiniz: `docker compose --profile observability up -d`. Bu Compose yapılandırması yerel geliştirme içindir; production ortamında secret manager, TLS, ağ erişim politikası, yedekleme ve imaj sürüm sabitlemesi ayrıca sağlanmalıdır.

## Bilinen notlar

- SQL Server bağlantısı ve geçerli JWT ayarları olmadan DB işlemleri ve login çalışmaz; migration ve API başlatma birbirinden ayrı adımlardır.
- Redis/RabbitMQ kullanımı ilgili servislerin erişilebilir olmasını gerektirir. Broker kapalıysa mesajlaşma çalışmaz ve bağlantı uyarıları görülebilir.
- Paket geri yükleme/derleme sırasında bazı doğrudan veya transitif paketlerin NuGet güvenlik uyarıları görülebilir; paketlerin güvenlik bültenleri ayrıca kontrol edilmelidir.
- SQL Server'a özgü `UPDLOCK`/`ROWLOCK` ipuçları ve rapor gruplama fonksiyonları SQL Server sağlayıcısına bağlıdır; farklı veritabanı sağlayıcısına geçişte uyarlanmalıdır.
