# YUIBackend Mimari Tasarımı

## Kapsam ve katmanlar

API, Clean Architecture ve CQRS kullanan bir .NET 10 REST servisidir. `Domain`; müşteri, hesap, finansal işlem, SmartEnum türleri ve repository sözleşmelerini içerir. `Application`; command/query tanımları, doğrulamalar, handler'lar ve DTO'ları barındırır. `Infrastructure`; SQL Server kalıcılığı, repository'ler, işlem yürütme, mesajlaşma ve cache adaptörlerini uygular. `WebAPI`; controller'lar, kimlik doğrulama, rate limiting, hata yönetimi, health check ve OpenAPI'yi sağlar.

WebAPI, Application ve Infrastructure'a; Application ise Domain'e bağımlıdır. Domain, Infrastructure veya WebAPI'ye bağımlı değildir. MediatR CQRS isteklerini yönlendirir, FluentValidation pipeline üzerinden doğrular, EF Core migration'ları ilişkisel şemayı sürümler.

## Veri modeli ve ledger

Bir müşterinin birden çok hesabı olabilir. Hesap bakiyesi SQL Server `decimal(18,2)` olarak saklanır. Finansal işlem; benzersiz işlem numarası, tutar, SmartEnum işlem türü/durumu, kaynak ve hedef hesap ilişkileri, zaman damgası, açıklama, idempotency anahtarı ve isteğe bağlı hata nedeni içerir. Kısıtlı foreign key'ler ilişkili hesapların silinmesini önler; uygulama/repository yüzeyinde ledger'a yalnızca kayıt eklenir ve finansal hareketler soft delete edilemez.

Nullable benzersiz `IdempotencyKey` indeksi eski ledger kayıtlarıyla uyumludur ve yeni isteklerin yinelenmesini veritabanı seviyesinde önler. Destekleyici indeksler işlem zamanı/durumu, hesap geçmişi ve tür/durum/zaman raporlarını kapsar. EF global filtreleri soft delete edilen kayıtları gizler ve tenant bağlamı sağlandığında tenant filtresini uygular.

## Finansal işlem akışı ve tutarlılık

1. Controller, zorunlu `Idempotency-Key` ile yatırma, çekme veya transfer isteğini alır ve CQRS komutuna dönüştürür.
2. FluentValidation; hesap kimliklerini, pozitif ve hassasiyet sınırları içindeki tutarı, açıklama uzunluğunu ve anahtar biçimini doğrular.
3. Infrastructure işlem servisi serializable SQL transaction açar, daha önce kullanılmış anahtarı kontrol eder ve ilgili hesapları sıralı kimlik düzeninde SQL Server update lock'larıyla okur.
4. Koşullu SQL `UPDATE` ifadeleri aktif hesap, yeterli bakiye ve azami bakiye koşullarını veritabanında uygular. Transfer, iki hesabı ve ledger kaydını aynı transaction'da değiştirir; adımlardan biri başarısız olursa bütün bakiye değişiklikleri geri alınır.
5. Bakiye değişikliği, ledger kaydı ve başarılı işlem bildirimi için RabbitMQ outbox mesajı birlikte commit edilir. İş kuralı nedeniyle reddedilen geçerli hesap işlemlerinde başarısız ledger kaydı ve başarısız bildirim outbox'a aynı transaction içinde yazılır. Benzersiz idempotency indeksi son veritabanı korumasıdır. Hesap bulunamadığında foreign key bütünlüğü nedeniyle ledger satırı/bildirim oluşturulmaz.

API süreci stateless'tir ve yatay ölçeklenebilir; finansal doğruluğun tek kaynağı SQL Server'dır. Redis ve RabbitMQ bakiye/ledger doğruluğunun otoritesi değildir. Müşteri ve ürün GET yanıtları tenant/query ölçütlü anahtarlar, kapsam sürümü ve bir dakikalık TTL ile Redis'te cache'lenir; başarılı müşteri/ürün yazmaları sürümü artırıp önceki girdileri geçersiz kılar. Bakiye ve append-only finansal geçmiş/rapor okumaları cache dışındadır. Satır kilidi ve serializable izolasyon doğruluk için bir miktar throughput'tan ödün verir; üretim kapasitesi gerçek SQL Server katmanı ve indeks/yazma hacmiyle yük testiyle ölçülmelidir.

## Okuma ve raporlama

Finansal geçmiş sorguları hesap, UTC tarih aralığı, SmartEnum türü ve dahilî alt/üst tutar sınırlarına göre SQL'de filtrelenir. Tutarlı zaman/ID sıralaması ve sınırlı sayfa boyutları kullanılır. Raporlar başarılı para giriş/çıkışlarını ve başarılı/başarısız adetlerini UTC gün, Pazartesi başlayan hafta veya ay bazında toplar; hesap raporu yalnızca o hesabın hareketlerini içerir. Rapor aralığı beş yılla sınırlıdır ve `toUtc` hariç üst sınırdır. Finansal sorgular cache dışı bırakılır; böylece bakiye ve yeni ledger hareketleri Redis'ten eski veri olarak sunulmaz.

## API, doğrulama ve işletim

Controller'lar müşteri/hesap yönetimi, para yatırma/çekme/transfer, işlem detayı/geçmişi ve rapor REST uçlarını sunar. Login dışındaki controller endpoint'leri JWT ister; health endpoint'i anonimdir. Global exception handler Problem Details üretir; geçersiz command/query istekleri validator'lar tarafından reddedilir. Yeni finansal işlem `201`, idempotent tekrar `200`, anahtar çakışması `409`, bulunamayan hesap `404`, iş kuralı reddi `422` döndürür.

İlk başarılı login'de yapılandırılmış yönetici hesabı oluşturulur; müşteriler kayıt endpoint'i ile ayrı giriş hesabı açabilir. JWT rolü ve veritabanındaki kullanıcı-müşteri ilişkisi kullanılarak müşteri/hesap sahipliği her istekte doğrulanır. Finansal işlem bildirimleri `FinancialTransactionNotification` sözleşmesiyle transactional outbox üzerinden RabbitMQ'ya yayınlanır; `financial-transaction-notifications` consumer'ı başarılı ve başarısız sonuçları işler.

EF Core migration'ları açıkça uygulanır, API başlangıcında otomatik çalışmaz. Yerel Docker Compose SQL Server, Redis ve RabbitMQ'yu sağlar; sırlar ortam değişkenlerinden alınır. Swagger Development ortamında açıktır; health check ve Prometheus metrikleri yerel işletimi destekler.

## Doğrulama

Unit test projesi domain/application davranışlarını harici servis olmadan doğrular. SQL Server entegrasyon testleri benzersiz geçici veritabanı oluşturur, gerçek migration'ları uygular ve idempotency, ledger kaydı, rollback ve eşzamanlı çekim senaryolarını çalıştırır. Testleri etkinleştirmek için veritabanı oluşturma/silme yetkisi olan SQL Server bağlantısını `YUIBACKEND_TEST_SQLSERVER` ortam değişkenine verin; ayar yoksa entegrasyon testleri atlanır. Kurulum, migration, API örnekleri ve Docker Compose komutları için [README.md](README.md) dosyasına bakın.
