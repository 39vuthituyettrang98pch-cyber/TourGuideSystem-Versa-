# UserMobile

Ứng dụng `.NET MAUI` dành cho du khách, sử dụng dữ liệu thật từ API của
`AdminWeb`.

## Đã kết nối

- POI đã duyệt, bản dịch, audio/video URL và vị trí.
- Danh mục và tour theo ngôn ngữ người dùng.
- Đăng ký, đăng nhập và hồ sơ du khách bằng JWT.
- JWT được lưu bằng `SecureStorage` và tự gắn vào request cần xác thực.
- POI yêu thích và lịch sử gần đây được đồng bộ với SQLite của Web khi đăng nhập.
- Khách chưa đăng nhập vẫn có lịch sử gần đây cục bộ trên thiết bị.
- Bản đồ OpenStreetMap qua Leaflet, quét QR, chi tiết POI và phát audio đa ngôn ngữ.
- Check-in GPS hoặc quét QR để nhận điểm khám phá, tăng hạng và mở khóa thành tựu.

## Điểm khám phá và thành tựu

- Mỗi tài khoản chỉ nhận `10 điểm` một lần cho mỗi POI.
- Check-in GPS được API xác thực theo bán kính POI và độ chính xác vị trí.
- Quét QR được API xác thực bằng token của POI.
- Trang Cá nhân hiển thị điểm, hạng và liên kết đến màn hình Thành tựu.
- Màn Thành tựu hiển thị tiến độ bản đồ, mốc hạng, huy hiệu và lịch sử check-in.

## Chạy local

Khởi động Web đúng cổng mà app đang dùng:

```powershell
dotnet run --project ../AdminWeb --launch-profile http
```

Build app:

```powershell
dotnet build UserMobile.csproj -f net10.0-windows10.0.19041.0
dotnet build UserMobile.csproj -f net10.0-android
```

URL debug được cấu hình tại `MauiProgram.cs`:

- Android emulator: `http://10.0.2.2:5297/`
- Windows và iOS simulator: `http://localhost:5297/`

Thiết bị thật không dùng được `localhost` hoặc `10.0.2.2`; hãy truyền IP LAN của
máy chạy `AdminWeb` khi build:

```powershell
dotnet run --project ../AdminWeb --urls http://0.0.0.0:5297
dotnet build UserMobile.csproj -f net10.0-android -p:ApiBaseUrl=http://192.168.1.10:5297/
```

Production phải truyền URL HTTPS thật bằng `-p:ApiBaseUrl=https://.../`.

OpenStreetMap không yêu cầu API key hoặc thẻ thanh toán. Thiết bị vẫn cần kết nối
Internet để tải Leaflet và lớp nền CARTO dựa trên dữ liệu OpenStreetMap.

## Chưa nằm trong phiên bản này

- GPS geofence và tự động phát thuyết minh.
- SQLite offline, tải trước media và đồng bộ delta.
- Push notification và theo dõi lỗi production.
