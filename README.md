# TourGuideSystem

Hệ thống dùng một nguồn dữ liệu duy nhất:

```text
UserMobile (.NET MAUI)
        |
        | HTTP/HTTPS + JSON + JWT
        v
AdminWeb (ASP.NET Core MVC + Web API)
        |
        | EF Core
        v
AdminWeb/he_thong_thuyet_minh.db (SQLite)
```

MAUI không mở file SQLite của Web. Mọi POI, danh mục, tour, hồ sơ du khách,
yêu thích và lịch sử gần đây đều được đọc/ghi qua API của `AdminWeb`.

## API cho UserMobile

- `GET /api/poi?lang=vi`: POI đã duyệt, bản dịch, media và danh mục.
- `GET /api/category?lang=vi`: danh mục đang hoạt động.
- `GET /api/tour?lang=vi&categoryId=1`: tour đang hoạt động.
- `POST /api/auth/register`, `POST /api/auth/login`: tạo tài khoản và nhận JWT.
- `GET /api/auth/me`: hồ sơ du khách hiện tại, yêu cầu JWT.
- `GET|POST /api/favorite`: đồng bộ POI yêu thích, yêu cầu JWT.
- `GET|POST /api/recent`: đồng bộ POI gần đây, yêu cầu JWT.

Tài khoản quản trị trong bảng `users` không được trả về mobile. App chỉ dùng tài
khoản du khách trong bảng `tourists`.

## Cấu hình local

Không lưu API key hoặc JWT signing key trong `appsettings.json`. Cấu hình bằng
User Secrets:

```powershell
dotnet user-secrets set "Gemini:ApiKey" "<new-gemini-key>" --project AdminWeb
dotnet user-secrets set "OpenAI:ApiKey" "<new-openai-key>" --project AdminWeb
dotnet user-secrets set "Jwt:SigningKey" "<long-random-signing-key>" --project AdminWeb
```

Các key từng xuất hiện trong source hoặc chat phải được thu hồi trước khi tạo key
mới.

## Database và migration

`AdminWeb` chuẩn hóa đường dẫn SQLite theo thư mục project và tự chạy migration
khi khởi động. Có thể cập nhật thủ công bằng:

```powershell
dotnet ef database update --project AdminWeb --startup-project AdminWeb --context AppDbContext
```

## Chạy local

```powershell
dotnet run --project AdminWeb --launch-profile http
dotnet build UserMobile/UserMobile.csproj -f net10.0-windows10.0.19041.0
dotnet build UserMobile/UserMobile.csproj -f net10.0-android
```

URL debug của MAUI:

- Windows và iOS simulator: `http://localhost:5297/`
- Android emulator: `http://10.0.2.2:5297/`

Thiết bị Android/iOS thật phải dùng IP LAN của máy chạy Web, cho Kestrel lắng
nghe trên mạng LAN và mở firewall tương ứng. Có thể ghi đè URL khi build:

```powershell
dotnet run --project AdminWeb --urls http://0.0.0.0:5297
dotnet build UserMobile/UserMobile.csproj -f net10.0-android -p:ApiBaseUrl=http://192.168.1.10:5297/
```

Production bắt buộc truyền một URL HTTPS thật bằng `-p:ApiBaseUrl=https://.../`.

## Lưu ý vận hành

- Native MAUI không bị CORS như ứng dụng JavaScript chạy trong trình duyệt.
- Bản đồ mobile dùng OpenStreetMap + Leaflet, không cần Google Maps API key.
- Development dùng HTTP để tránh lỗi chứng chỉ local trên emulator.
- Production bắt buộc HTTPS và phải cấu hình `Jwt:SigningKey`.
- App chỉ hiển thị POI có trạng thái `Approved`; database chưa có POI được duyệt
  thì danh sách mobile sẽ trống.
- GPS geofence và SQLite offline trên mobile chưa nằm trong luồng dữ liệu hiện tại.
