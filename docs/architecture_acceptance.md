# BÁO CÁO NGHIỆM THU VÀ THẤU HIỂU KIẾN TRÚC (ARCHITECTURE ACCEPTANCE) - PRACTICE SERVICE

## 1. TỔNG QUAN DỊCH VỤ
- **Tên Dịch Vụ**: V-Eval Practice Service (Thi trực tuyến & Chấm điểm).
- **Cổng Dịch Vụ**: `5002`.
- **Kiến Trúc**: Clean Architecture / Microservice.

## 2. KIẾN TRÚC DỮ LIỆU & CHIẾN LƯỢC CHẤM ĐIỂM
- Quản lý Bài làm (Submissions), Kết quả thi (ExamResults) và Theo dõi tiến trình làm bài real-time.
- Tự động chấm điểm trắc nghiệm và đồng bộ kết quả thi vào CSDL PostgreSQL V2 Schema.

## 3. KẾT QUẢ NGHIỆM THU
- Docker Build: Multi-Stage Build .NET 9 hoạt động ổn định trên cổng `5002`.
- Sẵn sàng giao tiếp với Gateway và AI Engine.
