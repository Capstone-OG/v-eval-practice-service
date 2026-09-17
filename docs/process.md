# KIẾN TRÚC & BẢNG THEO DÕI TIẾN ĐỘ CHỦ THỂ (PROCESS & PLANNING) - V-EVAL PRACTICE SERVICE

---

## PHẦN 1: KIẾN TRÚC DỊCH VỤ & CÁC THÀNH PHẦN CẦN TRIỂN KHAI

### 1. Kiến Trúc Clean Architecture & Quản Lý Thi Trực Tuyến
- **Cổng Dịch Vụ**: `5002` (HTTP) / Container `v_eval_practice_service`.
- **Nhiệm Vụ Chính**:
  - Tiếp nhận phiên làm bài thi trực tuyến của Học sinh.
  - Theo dõi tiến trình làm bài real-time (thời gian còn lại, danh sách câu đã chọn).
  - Tự động chấm điểm trắc nghiệm và đồng bộ kết quả vào CSDL PostgreSQL Schema `practice`.

### 2. Sơ Đồ CSDL PostgreSQL Schema `practice`
- `exam_sessions`: Phiên làm bài thi trực tuyến (`session_id`, `user_id`, `exam_id`, `start_time`, `submit_time`, `status`).
- `user_answers`: Danh sách lựa chọn câu trả lời chi tiết của học sinh.
- `exam_results`: Kết quả chấm điểm tổng hợp (`total_score`, `correct_count`, `wrong_count`, `rank`).

---

## PHẦN 2: BẢNG THEO DÕI TIẾN ĐỘ CHI TIẾT THEO TỪNG MỤC (PROGRESS MATRIX)

| STT | Hạng Mục / Chức Năng | Vị Trí Triển Khai trong Code | Trạng Thái | Tiến Độ (%) | Ghi Chú Chi Tiết |
| :---: | :--- | :--- | :---: | :---: | :--- |
| 1 | **Clean Architecture 4 Tầng** | Entire Solution | 🟢 Hoàn thành | 100% | `Domain`, `Application`, `Infrastructure`, `API` |
| 2 | **Cấu Hình Production & Security**| `appsettings.example.json` | 🟢 Hoàn thành | 100% | Khởi tạo cấu hình mẫu & ẩn secrets qua `.gitignore` |
| 3 | **Định Tuyến Gateway YARP** | Gateway YARP Config | 🟢 Hoàn thành | 100% | Route `/api/practice/{**catch-all}` cổng 5002 |
| 4 | **Dockerfile & Compose** | `Dockerfile` | 🟢 Hoàn thành | 100% | Multi-Stage .NET 9 cổng 5002 trên `veval_network` |
| 5 | **Script Push Độc Lập** | `Scripts/push.bat` | 🟢 Hoàn thành | 100% | Hỗ trợ 3 chế độ push kèm kiểm tra lịch sử |
| 6 | **API Bắt Đầu Làm Bài Thi** | `Features/Sessions/Commands/Start/`| 🟡 Đang chờ | 0% | Khởi tạo phiên thi `POST /api/practice/sessions/start` |
| 7 | **API Nộp Bài Thi & Chấm Điểm**| `Features/Sessions/Commands/Submit/`| 🟡 Đang chờ | 0% | Tự động chấm trắc nghiệm & tính tổng điểm |
| 8 | **API Lịch Sử Bài Làm Học Sinh**| `Features/Results/Queries/` | 🟡 Đang chờ | 0% | Trả về kết quả bài thi cá nhân |
