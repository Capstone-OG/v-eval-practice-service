# Nhật ký & Kế hoạch Phát triển Practice Service (Daily Process and Planning)

Tài liệu này dùng để theo dõi tiến độ phát triển thực tế hàng ngày, các cột mốc tính năng (Milestones) và kế hoạch tương lai của phân hệ **V-Eval-Practice_Service**.

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

---

## 📅 Kế hoạch Tiếp theo (Upcoming Roadmap)

### 🎯 Milestone 2: Nộp bài thi, Chấm điểm & Thống kê Kỹ năng
- [ ] API nộp bài thi `POST /api/practice/submit-exam`.
- [ ] Chấm điểm tự động và tính độ thành thạo theo từng domain/skill.
- [ ] Trả về kết quả phân tích năng lực chi tiết cho học sinh.
