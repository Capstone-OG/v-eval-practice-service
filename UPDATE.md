# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [27/09/2026] - Phát Hành Giao Diện Làm Thử Đề Thi Chẩn Đoán, AI Exam Studio (Custom Prompt, Bloom 6 Cấp, Lưu DB Chờ Duyệt) & Radar Chart

- **Phát Hành Giao Diện Web Khảo Sát Năng Lực Đầu Vào (`wwwroot/view-diagnostic.html`)**:
  - Xây dựng giao diện web độc lập phong cách Glassmorphism hiện đại (Inter, Plus Jakarta Sans, KaTeX, Chart.js) hỗ trợ kiểm thử thực tế và mô phỏng luồng Core Flow 1.
  - Tích hợp 3 Tab hoàn chỉnh:
    1. **Tab 1: AI Exam Studio**: Giáo viên nhập prompt tùy biến, chọn 5 môn học hoặc tự động nhận diện môn, chọn cấp độ Bloom (1-6) và số câu (10, 20, 30).
    2. **Tab 2: Phòng Thi Học Sinh**: Làm bài thi, điều hướng 30 câu, nộp bài hoặc dùng Demo Solver tự động điền theo các kịch bản học sinh.
    3. **Tab 3: Báo Cáo Năng Lực & Radar Chart**: Điểm IRT 2PL, xếp lớp, radar chart, phân tích Bloom và bảng BKT.
- **Nút Lưu CSDL Chờ Duyệt & Quy Trình Phê Duyệt**:
  - Thêm nút **`💾 Lưu Vào Database (Chờ Duyệt)`**: Lưu đề thi vào PostgreSQL Supabase qua `POST /api/v1/content/exams/import` với trạng thái mặc định **`IsPublished = false` (Chờ duyệt / Pending Approval)**.
  - Nút **`✅ ACCEPT: Phê Duyệt & Chuyển Sang Phòng Thi Học Sinh ➔`**: Gọi `PATCH /api/v1/content/exams/{id}/publish` cập nhật trạng thái thành `IsPublished = true` (Đã duyệt / Published) và chuyển đề thi sang phòng thi học sinh.
- **Tự Động Lưu (Auto-Save LocalStorage)**:
  - Tự động lưu bản nháp đề vừa tạo ngay khi AI sinh xong và tự động lưu đề thi chính thức khi giáo viên bấm `ACCEPT`. Khi F5/tải lại trang, hệ thống tự động nhận diện và khôi phục lại đề thi từ bộ nhớ trình duyệt kèm nút `📂 Khôi Phục Đề Đã Lưu`.
- **Kích Hoạt Static Files Trong Pipeline Kestrel (`Program.cs`)**:
  - Đã thêm `app.UseStaticFiles()` giúp phục vụ trực tiếp `http://localhost:5261/view-diagnostic.html`.
- **Cập Nhật Ma Trận Tiến Độ & Báo Cáo Nghiệm Thu**:
  - Cập nhật [`docs/process.md`](./docs/process.md) đạt 20 hạng mục 100% hoàn thành, chuẩn hóa độ khó theo 6 cấp Bloom.
  - Cập nhật tiêu chuẩn nghiệm thu kiến trúc trong [`docs/architecture_acceptance.md`](./docs/architecture_acceptance.md).