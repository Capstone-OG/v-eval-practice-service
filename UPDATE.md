# Nhật Ký Cập Nhật (Update Log) - Practice Service

## \[23/09/2026\] - Đồng Bộ Schema `v_eval_practice` Trong PracticeDbContext

- **Đồng Bộ Default Schema & Table Schemas**: Đã cập nhật `PracticeDbContext.cs` thiết lập `modelBuilder.HasDefaultSchema("v_eval_practice");` và ToTable `"ExamSubmissions"`, `"SubmissionAnswers"` thuộc schema `"v_eval_practice"` đồng bộ 100% với DDL CSDL PostgreSQL Schema V2.
- **Kiểm Thử Biên Dịch**: `dotnet build` giải pháp `V-Eval-Practice_Service.sln` thành công 100% (0 Errors, 0 Warnings).