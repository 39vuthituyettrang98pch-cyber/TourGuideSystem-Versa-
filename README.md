# TourGuideSystem - VERSA Guide

TourGuideSystem là hệ thống thuyết minh du lịch gồm **AdminWeb** chạy bằng ASP.NET Core MVC/Web API và **UserMobile** chạy bằng .NET MAUI. Hệ thống dùng chung dữ liệu từ SQLite thông qua API của `AdminWeb`; ứng dụng MAUI **không mở trực tiếp file SQLite**.

```text
UserMobile (.NET MAUI)
        |
        | HTTP/HTTPS + JSON + JWT
        v
AdminWeb (ASP.NET Core MVC + Web API)
        |
        | Entity Framework Core
        v
AdminWeb/he_thong_thuyet_minh.db (SQLite)
```

## 1. Thành phần chính

| Thành phần | Công nghệ | Chức năng |
|---|---|---|
| `AdminWeb` | ASP.NET Core MVC, Web API, EF Core, SQLite | Quản trị POI, danh mục, tour, media, người dùng, duyệt nội dung, thanh toán, báo cáo |
| `Areas/DuKhach` | ASP.NET Core MVC Area | Website dành cho du khách: bản đồ, POI, tour, QR, Premium, AI chat, đơn món |
| `Areas/Owner` | ASP.NET Core MVC Area | Chủ POI/chủ gian hàng: hồ sơ, POI, menu, đơn hàng, gói nổi bật, báo cáo |
| `Areas/Editor` | ASP.NET Core MVC Area | Biên tập nội dung, chuẩn bị POI để gửi duyệt |
| `Areas/Reviewer` | ASP.NET Core MVC Area | Kiểm duyệt POI/nội dung trước khi xuất bản |
| `UserMobile` | .NET MAUI | Ứng dụng mobile cho du khách: bản đồ, POI, QR, yêu thích, lịch sử, profile, achievement, AI |

## 2. Chức năng hiện có

### Admin

- Đăng nhập quản trị bằng cookie riêng `AdminScheme`.
- Quản lý tài khoản, vai trò, POI, danh mục, bản dịch, tour, media, beacon, ngôn ngữ.
- Duyệt/từ chối nội dung POI.
- Tạo QR cho POI.
- Quản lý gói thanh toán, giao dịch, doanh thu, lượt mua, thống kê.
- Quản lý chủ POI/chủ gian hàng và yêu cầu nhận POI.

### Du khách web

- Trang chính tại `/` và portal tại `/DuKhach`.
- Đăng ký, đăng nhập, đổi mật khẩu, quên mật khẩu, hồ sơ.
- Xem bản đồ POI bằng Leaflet/OpenStreetMap.
- Đồng bộ ngôn ngữ giữa navbar và bản đồ.
- Xem POI, chi tiết POI, POI nổi bật, POI đã khám phá.
- Quét QR POI.
- Check-in GPS/QR để nhận điểm khám phá và thành tựu.
- Yêu thích, lịch sử gần đây, đánh giá POI.
- Gói **Du khách Premium** để mở khóa audio/thuyết minh không giới hạn, tour premium và AI hướng dẫn viên.
- Thanh toán Premium qua VNPay/MoMo hoặc chế độ demo.
- Xem menu tại POI và đặt món/dịch vụ demo.

### Chủ POI / chủ gian hàng

- Đăng ký/đăng nhập tại `/Owner/Login` và `/Owner/Register`.
- Tạo/cập nhật hồ sơ chủ gian hàng.
- Tạo POI mới hoặc gửi yêu cầu nhận POI có sẵn.
- Quản lý menu món/dịch vụ tại POI.
- Xem và xử lý đơn hàng từ du khách.
- Xem đánh giá và báo cáo.
- Mua gói Owner để mở khóa tính năng nâng cao.
- Gói **Owner Premium/Gói nổi bật** giúp POI được ưu tiên hiển thị trên bản đồ du khách và khu khám phá, marker có biểu tượng nổi bật.
- Báo cáo chủ POI có phần GMV, doanh thu đơn hoàn tất, phí nền tảng và số tiền chủ cửa hàng thực nhận. Mặc định phần báo cáo đang tính phí nền tảng 10%.

### Editor / Reviewer

- Editor đăng nhập bằng route riêng `/Editor/Login`.
- Reviewer đăng nhập bằng route riêng `/Reviewer/Login`.
- Mỗi vai trò dùng cookie scheme riêng để tránh lẫn phiên đăng nhập giữa Admin, Editor, Reviewer, Owner và Du khách.
- Editor tập trung biên tập nội dung, Reviewer tập trung duyệt nội dung.

### UserMobile

- Đăng ký, đăng nhập, lưu JWT bằng `SecureStorage`.
- Gọi API thật từ `AdminWeb`.
- Xem POI đã duyệt theo ngôn ngữ.
- Xem danh mục, tour, chi tiết POI, audio/video URL.
- Bản đồ, QR scanner, yêu thích, lịch sử gần đây.
- Check-in GPS hoặc QR để nhận điểm khám phá.
- Trang hồ sơ, thành tựu, AI chat, menu POI và đơn hàng.

## 3. Cấu trúc thư mục

```text
TourGuideSystem-AdminWeb-UserApp/
├── AdminWeb/
│   ├── Areas/
│   │   ├── DuKhach/
│   │   ├── Editor/
│   │   ├── Owner/
│   │   └── Reviewer/
│   ├── Controllers/
│   ├── Data/
│   ├── Migrations/
│   ├── Models/
│   ├── Services/
│   ├── ViewModels/
│   ├── wwwroot/
│   ├── AdminWeb.csproj
│   ├── appsettings.json
│   └── Program.cs
├── UserMobile/
│   ├── Models/
│   ├── Services/
│   ├── ViewModels/
│   ├── Views/
│   ├── Resources/
│   ├── Platforms/
│   ├── MauiProgram.cs
│   └── UserMobile.csproj
├── TourGuideSystem-AdminWeb-UserApp.sln
└── README.md
```

## 4. Yêu cầu môi trường

### Bắt buộc

- Visual Studio 2022 bản mới có workload **ASP.NET and web development**.
- .NET 8 SDK.
- SQLite dùng qua EF Core, không cần cài server database riêng.
- Công cụ EF Core CLI:

```powershell
dotnet tool update --global dotnet-ef
```

### Cho mobile

- Cài workload **.NET MAUI** trong Visual Studio.
- Android SDK/emulator nếu build Android.
- Muốn build iOS thật cần Mac hoặc Mac build host theo yêu cầu của Apple/.NET MAUI.

### Cho xử lý AI/media

- Gemini API key nếu dùng AI tối ưu nội dung/chat.
- OpenAI API key nếu dùng transcription.
- `edge-tts` nếu dùng tạo giọng đọc.
- `ffmpeg` nếu dùng xử lý audio/video.

## 5. Cấu hình bảo mật

Không lưu API key, JWT signing key, SMTP password, VNPay/MoMo secret thật trực tiếp trong `appsettings.json` khi đưa code lên GitHub hoặc deploy production.

Dùng **User Secrets** khi chạy local:

```powershell
dotnet user-secrets set "Gemini:ApiKey" "<gemini-api-key>" --project .\AdminWeb\AdminWeb.csproj
dotnet user-secrets set "OpenAI:ApiKey" "<openai-api-key>" --project .\AdminWeb\AdminWeb.csproj
dotnet user-secrets set "Jwt:SigningKey" "<long-random-signing-key>" --project .\AdminWeb\AdminWeb.csproj
```

Cấu hình Gmail SMTP để gửi OTP quên mật khẩu (tài khoản Gmail phải bật xác minh 2 bước và tạo **Mật khẩu ứng dụng**, không dùng mật khẩu đăng nhập Gmail):

```powershell
dotnet user-secrets set "Smtp:Username" "your-account@gmail.com" --project .\AdminWeb\AdminWeb.csproj
dotnet user-secrets set "Smtp:Password" "<gmail-app-password-16-characters>" --project .\AdminWeb\AdminWeb.csproj
dotnet user-secrets set "Smtp:From" "your-account@gmail.com" --project .\AdminWeb\AdminWeb.csproj
```

Host `smtp.gmail.com`, cổng `587` và TLS đã có sẵn trong `appsettings.json`. Khi chạy Development mà chưa cấu hình Gmail, OTP test chỉ được hiển thị trên màn hình đặt lại mật khẩu; production không bao giờ trả OTP trong response.

Khi production, bắt buộc cấu hình `Jwt:SigningKey` thật. Nếu key từng bị đưa lên source/chat, hãy thu hồi key cũ và tạo key mới.

## 6. Database và migration

Database local mặc định:

```text
AdminWeb/he_thong_thuyet_minh.db
```

Production mặc định:

```text
AdminWeb/App_Data/he_thong_thuyet_minh.db
```

`Program.cs` đã chuẩn hóa đường dẫn SQLite theo thư mục chạy app và tự chạy migration khi khởi động. Có thể cập nhật thủ công bằng:

```powershell
dotnet ef database update `
  --project .\AdminWeb\AdminWeb.csproj `
  --startup-project .\AdminWeb\AdminWeb.csproj `
  --context AppDbContext
```

Nếu muốn reset database local hoàn toàn:

```powershell
Remove-Item .\AdminWeb\he_thong_thuyet_minh.db* -ErrorAction SilentlyContinue
dotnet ef database update --project .\AdminWeb\AdminWeb.csproj --startup-project .\AdminWeb\AdminWeb.csproj --context AppDbContext
```

Lưu ý: app mobile chỉ hiển thị POI có trạng thái `Approved`. Nếu mobile hoặc bản đồ không có POI, hãy kiểm tra dữ liệu POI đã được duyệt chưa.

## 7. Chạy AdminWeb local

Từ thư mục gốc project:

```powershell
dotnet restore .\TourGuideSystem-AdminWeb-UserApp.sln
dotnet build .\AdminWeb\AdminWeb.csproj
dotnet run --project .\AdminWeb\AdminWeb.csproj --launch-profile http
```

URL mặc định:

```text
http://localhost:5297
```

Các cổng/route quan trọng:

| Màn hình | URL |
|---|---|
| Du khách web | `http://localhost:5297/` |
| Du khách portal | `http://localhost:5297/DuKhach` |
| Admin | `http://localhost:5297/Admin/Login` |
| Editor | `http://localhost:5297/Editor/Login` |
| Reviewer | `http://localhost:5297/Reviewer/Login` |
| Owner | `http://localhost:5297/Owner/Login` |
| Owner đăng ký | `http://localhost:5297/Owner/Register` |

## 8. Tài khoản demo khi chạy Development

Khi chạy môi trường Development, hệ thống tự seed một số tài khoản demo nếu database chưa có.

| Vai trò | Email/Username | Mật khẩu | Đường dẫn đăng nhập |
|---|---|---|---|
| Admin | `admin` hoặc `admin@local` | `admin123` | `/Admin/Login` |
| Editor | `editor` hoặc `editor@local` | `editor123` | `/Editor/Login` |
| Reviewer | `reviewer` hoặc `reviewer@local` | `reviewer123` | `/Reviewer/Login` |
| Owner demo | `owner` hoặc `owner@local` | `owner123` | `/Owner/Login` |
| Du khách demo | `tourist@local` | `tourist123` | `/DuKhach/Account/Login` hoặc mobile |

Ngoài ra dữ liệu demo có thể tạo thêm một số owner thật như `huynhhoa.owner@versa.local`, `phovietnam.owner@versa.local`, `baghien.owner@versa.local`, `dophu.owner@versa.local`, `pizza4ps.owner@versa.local`; mật khẩu demo thường là `owner123`.

## 9. Chạy UserMobile local

Khởi động `AdminWeb` trước:

```powershell
dotnet run --project .\AdminWeb\AdminWeb.csproj --launch-profile http
```

Build mobile Windows:

```powershell
dotnet build .\UserMobile\UserMobile.csproj -f net8.0-windows10.0.19041.0
```

Build Android:

```powershell
dotnet build .\UserMobile\UserMobile.csproj -f net8.0-android
```

URL API mặc định trong Debug:

| Môi trường chạy app | API base URL |
|---|---|
| Windows | `http://localhost:5297/` |
| Android emulator | `http://10.0.2.2:5297/` |
| iOS simulator | `http://localhost:5297/` |

Nếu chạy trên điện thoại thật, `localhost` và `10.0.2.2` không trỏ về máy tính. Hãy cho AdminWeb lắng nghe LAN và truyền IP máy tính khi build:

```powershell
dotnet run --project .\AdminWeb\AdminWeb.csproj --urls http://0.0.0.0:5297

dotnet build .\UserMobile\UserMobile.csproj `
  -f net8.0-android `
  -p:ApiBaseUrl=http://192.168.1.10:5297/
```

Thay `192.168.1.10` bằng IP LAN thật của máy đang chạy `AdminWeb`. Nhớ mở firewall cho cổng `5297`.

Production bắt buộc dùng HTTPS:

```powershell
dotnet build .\UserMobile\UserMobile.csproj -f net8.0-android -p:ApiBaseUrl=https://your-domain.com/
```

## 10. API chính cho UserMobile

API trả về dữ liệu JSON và nhiều endpoint dùng JWT Bearer token sau khi đăng nhập.

| Method | Endpoint | Mục đích | Cần JWT |
|---|---|---|---|
| `GET` | `/api/languages` | Danh sách ngôn ngữ hỗ trợ | Không |
| `GET` | `/api/ui-translations?lang=vi` | Bản dịch giao diện mobile/web | Không |
| `GET` | `/api/poi?lang=vi` | Danh sách POI đã duyệt | Không |
| `GET` | `/api/poi/by-qr?qr=<token>&lang=vi` | Tìm POI bằng QR token | Không |
| `GET` | `/api/category?lang=vi` | Danh mục đang hoạt động | Không |
| `GET` | `/api/tour?lang=vi&categoryId=1` | Tour đang hoạt động | Không/Premium tùy màn hình |
| `GET` | `/api/tour/{tourId}/pois` | POI trong tour | Không/Premium tùy màn hình |
| `POST` | `/api/auth/register` | Đăng ký du khách | Không |
| `POST` | `/api/auth/login` | Đăng nhập du khách, nhận JWT | Không |
| `GET` | `/api/auth/me` | Hồ sơ du khách hiện tại | Có |
| `PUT` | `/api/auth/me` | Cập nhật hồ sơ du khách | Có |
| `POST` | `/api/auth/change-password` | Đổi mật khẩu | Có |
| `POST` | `/api/auth/forgot-password` | Quên mật khẩu | Không |
| `GET` | `/api/favorite` | Danh sách POI yêu thích | Có |
| `POST` | `/api/favorite` | Bật/tắt yêu thích POI | Có |
| `GET` | `/api/recent` | Danh sách POI gần đây | Có |
| `POST` | `/api/recent` | Ghi POI gần đây | Có |
| `GET` | `/api/achievement` | Điểm, hạng, thành tựu | Có |
| `POST` | `/api/achievement/discover` | Check-in GPS/QR | Có |
| `GET` | `/api/poi/{poiId}/reviews` | Xem đánh giá POI | Không |
| `POST` | `/api/poi/{poiId}/reviews` | Gửi đánh giá POI | Có |
| `POST` | `/api/ai-chat` | Hỏi AI hướng dẫn viên | Có/Premium |
| `GET` | `/api/menu/poi/{poiId}` | Menu món/dịch vụ của POI | Không |
| `POST` | `/api/menu/orders` | Tạo đơn hàng | Có |
| `GET` | `/api/menu/orders/my` | Đơn hàng của du khách | Có |
| `POST` | `/api/menu/orders/{id}/cancel` | Hủy đơn hàng | Có |

## 11. Thanh toán và Premium

Hệ thống có 2 nhóm gói:

| Nhóm | Ví dụ gói | Tác dụng |
|---|---|---|
| Du khách | `USER_PREMIUM` | Mở audio/thuyết minh không giới hạn, tour premium, AI hướng dẫn viên |
| Owner | `OWNER_BASIC`, `OWNER_PREMIUM` | Mở quản lý nâng cao, AI tối ưu nội dung, POI nổi bật, báo cáo |

Thanh toán hỗ trợ các luồng:

- VNPay sandbox.
- MoMo test/demo.
- Xác nhận demo/manual trong hệ thống.

Khi cấu hình thật, cần thay toàn bộ mã merchant/secret bằng giá trị riêng của môi trường deploy, không dùng key test đã từng commit.

## 12. Ghi chú về bản đồ

- Web DuKhach và mobile dùng OpenStreetMap/Leaflet, không bắt buộc Google Maps API key.
- POI nổi bật của chủ gian hàng Premium được đưa lên đầu danh sách và marker nổi bật trên bản đồ.
- Nếu marker không hiện, kiểm tra POI có `Latitude`, `Longitude`, trạng thái `Approved` và có dữ liệu trong API `/api/poi`.

## 13. Deploy AdminWeb lên hosting/IIS

Publish framework-dependent nếu host đã có .NET 8 Hosting Bundle:

```powershell
dotnet publish .\AdminWeb\AdminWeb.csproj -c Release -o .\publish
```

Nếu host không có runtime .NET 8, publish self-contained theo đúng kiến trúc host:

```powershell
# Host Windows 64-bit
dotnet publish .\AdminWeb\AdminWeb.csproj -c Release -r win-x64 --self-contained true -o .\publish

# Host Windows 32-bit
dotnet publish .\AdminWeb\AdminWeb.csproj -c Release -r win-x86 --self-contained true -o .\publish
```

Khi upload hosting:

1. Upload toàn bộ nội dung trong thư mục `publish` lên thư mục web root.
2. Tạo thư mục `App_Data` nếu chưa có.
3. Cấp quyền ghi cho `App_Data` để SQLite tạo/mở database.
4. Đặt biến môi trường `ASPNETCORE_ENVIRONMENT=Production`.
5. Cấu hình `Jwt:SigningKey`, SMTP, Gemini/OpenAI, VNPay/MoMo bằng biến môi trường hoặc cấu hình bảo mật của hosting.
6. Nếu hosting yếu hoặc không muốn chạy worker nền, để `MediaProcessing:WorkerEnabled=false` trong production.

## 14. Lỗi thường gặp

### Mobile/bản đồ không có POI

Nguyên nhân thường gặp:

- Database chưa có POI.
- POI chưa ở trạng thái `Approved`.
- Mobile đang gọi sai API base URL.
- Web chưa chạy đúng cổng `5297`.

Cách kiểm tra nhanh:

```text
http://localhost:5297/api/poi?lang=vi
```

### Android emulator không gọi được API

Dùng URL:

```text
http://10.0.2.2:5297/
```

Không dùng `localhost` trong Android emulator.

### Điện thoại thật không gọi được API

Chạy AdminWeb bằng LAN:

```powershell
dotnet run --project .\AdminWeb\AdminWeb.csproj --urls http://0.0.0.0:5297
```

Sau đó build mobile với IP LAN thật:

```powershell
dotnet build .\UserMobile\UserMobile.csproj -f net8.0-android -p:ApiBaseUrl=http://192.168.1.10:5297/
```

### Hosting báo không mở được SQLite

Kiểm tra:

- Có thư mục `App_Data` chưa.
- App pool/user hosting có quyền ghi vào `App_Data` chưa.
- Connection string production đang là `Data Source=App_Data/he_thong_thuyet_minh.db` chưa.

### 500.31 hoặc thiếu .NET Runtime

Host chưa có .NET 8 runtime/hosting bundle, hoặc app đang chạy sai kiến trúc x86/x64. Có 2 cách:

- Cài .NET 8 Hosting Bundle trên server.
- Publish self-contained đúng runtime `win-x86` hoặc `win-x64`.

### Audio/TTS không chạy

Kiểm tra:

```powershell
edge-tts --version
ffmpeg -version
```

Nếu hosting không có `edge-tts` hoặc `ffmpeg`, hãy cấu hình đường dẫn executable chính xác hoặc tắt worker media trong production.

### Lỗi quota OpenAI/Gemini

Nếu API trả về lỗi quota hoặc insufficient quota, đó là lỗi hết hạn mức/tài khoản billing của API provider. Cần đổi key, nạp billing hoặc tắt tính năng tương ứng.

## 15. Trạng thái hiện tại và phần chưa hoàn chỉnh

Đã có:

- AdminWeb + Web API + SQLite chung.
- DuKhach web, Owner web, Editor, Reviewer.
- Mobile MAUI gọi API thật.
- QR, GPS check-in, điểm khám phá, thành tựu.
- Premium du khách, gói owner, POI nổi bật.
- Menu POI và đơn hàng demo.
- Nhiều ngôn ngữ cho nội dung/API/UI.

Chưa nên xem là hoàn chỉnh production tuyệt đối:

- Background GPS tracking liên tục và tự động phát thuyết minh theo geofence chưa phải luồng chính ổn định.
- Offline SQLite/mobile cache media đầy đủ chưa hoàn thiện.
- Push notification chưa có.
- Thanh toán thật cần cấu hình merchant/secret thật và kiểm thử callback/webhook trên domain HTTPS.
- Cần đổi toàn bộ secret/key từng xuất hiện trong source trước khi public/deploy.

## 16. Quy trình chạy nhanh cho người mới

```powershell
# 1. Restore/build web
dotnet restore .\TourGuideSystem-AdminWeb-UserApp.sln
dotnet build .\AdminWeb\AdminWeb.csproj

# 2. Tạo/cập nhật database
dotnet ef database update --project .\AdminWeb\AdminWeb.csproj --startup-project .\AdminWeb\AdminWeb.csproj --context AppDbContext

# 3. Chạy web
dotnet run --project .\AdminWeb\AdminWeb.csproj --launch-profile http

# 4. Mở trình duyệt
# http://localhost:5297/
# http://localhost:5297/Admin/Login

# 5. Build mobile nếu cần
dotnet build .\UserMobile\UserMobile.csproj -f net8.0-windows10.0.19041.0
```

## 17. Ghi chú cho phát triển tiếp

- Không cho mobile truy cập SQLite trực tiếp; mọi dữ liệu phải đi qua API.
- Khi thêm tính năng mobile, tạo API ở `AdminWeb/Controllers/*ApiController.cs`, sau đó tạo service tương ứng trong `UserMobile/Services`.
- Khi thêm bảng mới, tạo model, cập nhật `AppDbContext`, tạo migration và kiểm tra seed dữ liệu.
- Khi thêm route cho Area, chú ý thứ tự route trong `Program.cs` để không bị lẫn link giữa Admin, DuKhach, Owner, Editor và Reviewer.
- Khi thêm tính năng Premium, kiểm tra cả `TouristSubscriptions`, `OwnerSubscriptions`, `PaymentTransactions` và `PaymentPlans`.
