# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [18/09/2026] - Phát Hành Công Cụ Push Độc Lập `Scripts/push.bat` Cho Practice Service
- **Tích Hợp `Scripts/push.bat` Độc Lập**:
  - Khởi tạo script [`Scripts/push.bat`](file:///e:/CapStone/All%20Services/V-Eval-Practice_Service/Scripts/push.bat) độc lập cho Practice Service.
  - Hỗ trợ Push nhanh trên nhánh hiện tại, chọn nhánh đã có qua Menu đánh số, hoặc tạo nhánh mới tự động.
  - Tích hợp tự động kiểm tra đồng bộ lịch sử Git với Remote, tự động pull code khi bi cham (behind) và đưa ra **Cảnh báo Đỏ (Red Warning)** ngắt quy trình khi bị xung đột lịch sử (Conflict/Diverged).

## [15/09/2026] - Dockerize Practice Service & Chuẩn Hóa Docker Compose
- **Dockerfile Multi-Stage .NET 9**:
  - Khởi tạo `Dockerfile` chuẩn cho Practice Service (`V-Eval-Practice_Service.API`) với cổng `5002`.
  - Kết nối chung mạng nội bộ `veval_network` trong `docker-compose.yml`.

## [14/09/2026] - Chuẩn Hóa Cấu Hình Production & Git Security
- **Khởi Tạo `appsettings.example.json`**:
  - Tạo file cấu hình mẫu chứa `ConnectionStrings` và `JwtSettings`.
- **Bảo Mật Git Security**:
  - Cập nhật `.gitignore` ẩn tất cả file `appsettings.json` chứa mật khẩu cá nhân.
