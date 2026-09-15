# Nhật ký & Kế hoạch Phát triển Practice Service (Daily Process and Planning)

Tài liệu này dùng để theo dõi tiến độ phát triển thực tế hàng ngày, các cột mốc tính năng (Milestones) và kế hoạch tương lai của phân hệ **V-Eval-Practice_Service**.

---

## 📅 Cập nhật ngày 15/09/2026

### 🎯 Mục tiêu hiện tại (Milestone 2 - Docker Containerization)
Tạo Dockerfile Multi-stage cho Practice Service (Cổng `:5002`) và kết nối vào `docker-compose.yml`.

### 📋 Danh sách Task & Trạng thái

| Tên Task | Trạng thái | Ghi chú |
| :--- | :---: | :--- |
| **Tạo `Dockerfile` Multi-stage** | 🟢 Hoàn thành | Build và publish .NET 9 API image trên cổng `5002`. |
| **Tích hợp Docker Compose** | 🟢 Hoàn thành | Khai báo container `v_eval_practice_service` tham gia mạng `veval_network`. |

---

## 📅 Cập nhật ngày 14/09/2026

### 🎯 Mục tiêu hiện tại (Milestone 1 - Security & Configuration)
Khởi tạo cấu hình mẫu Production (`appsettings.example.json`), ẩn các file cấu hình bí mật local qua `.gitignore`, và đồng bộ định tuyến Gateway API.

### 📋 Danh sách Task & Trạng thái

| Tên Task | Trạng thái | Ghi chú |
| :--- | :---: | :--- |
| **Khởi Tạo `appsettings.example.json`** | 🟢 Hoàn thành | Tạo file mẫu chứa đầy đủ `ConnectionStrings` và `JwtSettings` với label mẫu. |
| **Bảo Mật Git Security** | 🟢 Hoàn thành | Cập nhật `.gitignore` ẩn tất cả file `appsettings.json` chứa thông tin nhạy cảm. |
| **Định Tuyến Gateway YARP** | 🟢 Hoàn thành | Định tuyến `/api/practice/{**catch-all}` tại Gateway V-Eval trỏ về cổng `:5002`. |
