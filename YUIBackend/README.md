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
- [Docker durumu](#docker-durumu)
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
| `tests/YUIBackend.Tests` | xUnit birim testleri |

## Gereksinimler

- .NET 10 SDK
- SQL Server (bağlantı dizesi User Secrets veya ortam değişkeninde tanımlanmalı)
- İsteğe bağlı: Redis ve RabbitMQ. Bu servisler kullanılacak özellikler için çalışır durumda olmalıdır.
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

`Remove-Variable` sadece PowerShell değişkenini temizler; User Secrets'taki ayarı silmez. Gerekli servisleri kullanıyorsanız Redis ve RabbitMQ bağlantılarını da ekleyin:

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

## İlk kullanıcı ve JWT token

### Kullanıcı adı ve parola

Uygulama ilk çalışmada otomatik, bilinen bir parola oluşturmaz. Bu yüzden şu an paylaşılabilecek sabit bir giriş parolası yoktur. İlk hesabın kullanıcı adını ve parolasını siz belirleyip User Secrets'a kaydedin. Örnek kullanıcı adı `admin` olabilir; aşağıdaki parolayı güçlü ve benzersiz bir parolayla değiştirin (en az 12 karakter):

```powershell
dotnet user-secrets set "InitialUser:Username" "admin" --project ".\src\YUIBackend.WebAPI\YUIBackend.WebAPI.csproj"
dotnet user-secrets set "InitialUser:Password" "<kendi-guclu-parolaniz>" --project ".\src\YUIBackend.WebAPI\YUIBackend.WebAPI.csproj"
```

İlk kullanıcı, veritabanındaki `InitialUsers` tablosu boşken, bu secret değerleriyle **ilk başarılı login isteğinde** oluşturulur. Kullanıcı parolası ASP.NET Core `PasswordHasher` ile hash'lenerek saklanır; düz metin parola veritabanına yazılmaz. Sonraki login'lerde veritabanındaki hash doğrulanır. İlk kullanıcı oluşturulduktan sonra User Secrets'taki başlangıç parolasını değiştirmek mevcut kullanıcının parolasını değiştirmez.

`InitialUser:Username` ve `InitialUser:Password` yalnızca ilk kullanıcıyı kurmak için kullanılır. Kullanıcı tablosu boş değilse farklı bir kullanıcı yaratma/registration akışı yoktur. Parolanızı unutursanız veritabanı ve kimlik yönetimi için kontrollü bir yönetici/reset prosedürü gerekir.

### Login isteği

API çalışırken `POST /api/auth/login` adresine JSON gönderin. Windows PowerShell örneği:

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

Controller uçları JWT ister; istisnalar login ve health endpoint'idir. Aşağıdaki uçlarda `/api` öneki kullanılır.

| Method | Endpoint | Açıklama |
|---|---|---|
| `POST` | `/api/auth/login` | İlk kullanıcı ayarlandıysa ilk hesabı kurar veya kullanıcıyı doğrular; JWT döndürür. Tek anonim controller endpoint'idir. |
| `GET` | `/api/customers?pageNumber=1&pageSize=20` | Müşteri sayfalı listesi |
| `GET` | `/api/customers/{id}` | Müşteri detayı |
| `POST` | `/api/customers` | Müşteri oluşturur |
| `PUT` | `/api/customers/{id}` | Müşteri bilgilerini günceller |
| `DELETE` | `/api/customers/{id}` | Müşteriyi soft delete eder; hesabı olan müşteri silinemez. |
| `GET` | `/api/accounts?pageNumber=1&pageSize=20` | Hesap sayfalı listesi |
| `GET` | `/api/accounts/{id}` | Hesap detayı ve bakiye |
| `POST` | `/api/accounts` | Müşteriye hesap açar |
| `PUT` | `/api/accounts/{id}` | Hesap durumunu (`Active`, `Inactive`, `Closed`) günceller |
| `DELETE` | `/api/accounts/{id}` | Bakiyesi sıfır olan hesabı soft delete eder. |
| `GET` | `/api/financialtransactions?pageNumber=1&pageSize=20` | Finansal hareketlerin sayfalı, en yeniden eskiye listesi |
| `GET` | `/api/financialtransactions/{id}` | Finansal işlem detayı |
| `GET` | `/api/financialtransactions/account/{accountId}?pageNumber=1&pageSize=20` | Hesap bazlı sayfalı finansal işlem geçmişi |
| `GET` | `/api/products?pageNumber=1&pageSize=20` | Ürün sayfalı listesi |
| `GET` | `/api/products/{id}` | Ürün detayı |
| `POST` | `/api/products` | Ürün oluşturur |
| `PUT` | `/api/products/{id}` | Ürünü günceller |
| `DELETE` | `/api/products/{id}` | Ürünü soft delete eder. |
| `GET` | `/health` | Servis health-check özeti; JWT istemez. |
| `GET` | `/metrics` | Prometheus metrics çıktısı; dış erişimi production'da ağ/policy ile sınırlayın. |

Müşteri oluşturma JSON örneği:

```json
{
  "firstName": "Ada",
  "lastName": "Yilmaz",
  "email": "ada@example.com",
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

Hesap güncelleme JSON örneği:

```json
{
  "id": "<account-guid>",
  "status": "Inactive"
}
```

`PUT` isteklerinin route içindeki `id` değeri ile JSON gövdesindeki `id` aynı olmalıdır.

> Finansal işlem controller'ı şu anda sadece işlem kaydı ve geçmişini okur. Para yatırma/çekme/transfer yazma endpoint'leri bu API yüzeyinde henüz uygulanmış değildir. Finansal işlem kayıtları silinemez/güncellenemez; bu, denetim geçmişinin korunması içindir.

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

Testleri çalıştırmak için:

```powershell
dotnet test ".\tests\YUIBackend.Tests\YUIBackend.Tests.csproj"
```

Test projesindeki `CreateProductCommandHandlerTests` şu anda `CS0122` (handler erişilebilir değil) derleme hatası verebilir. Test assembly adı ile Application projesindeki `InternalsVisibleTo` assembly adı eşleşmelidir. Bu durum düzeltilmeden testler çalışmaz.

## Docker durumu

Depoda `Dockerfile` ve `docker-compose.yml` vardır; ancak mevcut dosyalardaki proje yolları/eski proje adları (`MyProject.*`) ve Dockerfile'ın .NET 9 taban imajları, güncel .NET 10 `YUIBackend.*` proje yapısıyla eşleşmiyor. Compose dosyasında ayrıca örnek/development amaçlı sabit servis ayarları bulunur. Bu nedenle Docker yapılandırmasını production için hazır veya doğrulanmış kabul etmeyin; kullanmadan önce Dockerfile/Compose yollarını, .NET imajlarını, secret yönetimini ve servis health/dependency ayarlarını güncelleyin. SQL Server parolası veya başka gerçek sırları bu dosyalara yazmayın.

## Bilinen notlar

- SQL Server bağlantısı ve geçerli JWT ayarları olmadan DB işlemleri ve login çalışmaz; migration ve API başlatma birbirinden ayrı adımlardır.
- Redis/RabbitMQ kullanımı ilgili servislerin erişilebilir olmasını gerektirir. Broker kapalıysa mesajlaşma çalışmaz ve bağlantı uyarıları görülebilir.
- Paket geri yükleme/derleme sırasında bazı doğrudan veya transitif paketlerin NuGet güvenlik uyarıları görülebilir; paketlerin güvenlik bültenleri ayrıca kontrol edilmelidir.
- Finansal yatırma/çekme/transfer komutları, bakiye güncelleme ve raporlama endpoint'leri bu belgede listelenen mevcut API uçlarına dahil değildir.
