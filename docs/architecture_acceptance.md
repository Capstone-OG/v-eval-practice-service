# ARCHITECTURE ACCEPTANCE REPORT - V-EVAL PRACTICE SERVICE

## 1. SERVICE OVERVIEW
- **Service Name**: V-Eval Practice Service (Online Examination, Auto-Grading & Competency Tracking).
- **Service Port**: `5261` (Local HTTP) / `5002` (Docker Container).
- **Architectural Paradigm**: 4-Layer Clean Architecture & CQRS Pattern.
- **Role in Core Flow 1**: Step 3 (Diagnostic Exam Submission, Automated Scoring, Micro-telemetry Tracking & Downstream AI Readiness).

---

## 2. SYSTEM ARCHITECTURE & INTER-SERVICE COMMUNICATION

### 2.1 Clean Architecture Layers
1. **Domain Layer**:
   - `ExamSubmission`: Root aggregate capturing `SubmissionId`, `StudentId`, `ExamId`, `TotalScore` (0–30 scale), `TotalCorrect`, `TotalQuestions` (30), `TotalTimeSpentSeconds`, timestamps, and status.
   - `SubmissionAnswer`: Detailed child entity recording `QuestionId`, `SelectedOption`, `IsCorrect`, and `TimeSpentSeconds` for each question item.
   - `IExamSubmissionRepository`: Persistence abstraction.
2. **Application Layer**:
   - **Result Pattern**: Uniform `Result<T>`, `Error`, and `ErrorType` enum aligned across the entire ecosystem.
   - **Pipeline Behavior**: `ValidationBehavior` leveraging FluentValidation to enforce input invariants prior to handler execution.
   - **gRPC Abstractions**: `IIdentityGrpcClient` and `IContentGrpcClient`.
   - **Use Cases (CQRS)**:
     - `SubmitDiagnosticCommand`: Executes auto-grading, micro-telemetry calculation, skill diagnosis, and database persistence.
     - `GetDiagnosticSubmissionByIdQuery`: Fetches granular test results.
     - `GetDiagnosticSubmissionsByStudentQuery`: Retrieves student submission history.
3. **Infrastructure Layer**:
   - **Persistence**: EF Core with Npgsql provider targeting Supabase PostgreSQL schema `practice` (`exam_submissions` and `submission_answers`). Automatic schema migration on startup.
   - **gRPC Clients**: Connects to `Identity Service` (dedicated HTTP/2 port `5156`) and `Content Service` (dedicated HTTP/2 port `5250`).
4. **API Layer**:
   - `ApiControllerBase`: Maps `Result<T>` to standard RFC 7807 `ProblemDetails`.
   - `DiagnosticSubmissionsController`: RESTful endpoints adhering to `/api/v1/practice/diagnostic-submissions`.
   - `GlobalExceptionHandlerMiddleware`: Handles unhandled 500 exceptions gracefully.
   - `Swagger UI`: Interactive documentation hosted at `http://localhost:5261/swagger`.

---

## 3. CORE FLOW 1 INTEGRATION (DIAGNOSTIC ASSESSMENT & PLACEMENT)

### 3.1 Flow Execution (Step 3)
1. **Client Submission**: Student posts 30 question answers with individual `time_spent_seconds`.
2. **Inter-Service Verification via Identity gRPC**: Calls `GetStudentProfileSummary` on port 5156 to verify student existence and confirm campus selection (`CampusId`).
3. **Answer Key Retrieval via Content gRPC**: Calls `GetExamAnswerKey` on port 5250 to obtain official keys, competency `SkillId`, and `DifficultyLevel` securely server-to-server.
4. **Automated Scoring & Telemetry Tracking**:
   - Raw score computed on a 0–30 scale (1 point per correct answer).
   - Per-question response time recorded (`time_spent_seconds`).
   - Grouping by `SkillId` to evaluate competency mastery and isolate `WeakSkillIds` (accuracy < 60%).
   - Grouping by `DifficultyLevel` (Easy, Medium, Hard, Very Hard) to profile cognitive performance.
5. **Database Storage**: Transactionally saved to Supabase schema `practice`.
6. **Downstream Readiness**: Yields structured feature data for **AI Subsystem (Steps 4 & 5)** to estimate IRT parameter $\theta_0 \in [-3.0, +3.0]$, calculate BKT prior probability $P(L_0) \in [0.05, 0.95]$, render radar charts, and suggest class placement (`Foundation`, `Standard`, `Advanced`).

---

## 4. ACCEPTANCE & VERIFICATION METRICS
- **Solution Build**: Compiled cleanly with **0 Warning(s), 0 Error(s)** (`V-Eval-Practice_Service.sln`).
- **End-to-End Integration Verification**: Complete automated test script (`e2e_core_flow1.ps1`) verified all 5 steps across 3 microservices (Identity, Content, Practice): **Passed 100%**.
- **REST & gRPC Dual Port Protocol Architecture**:
  - Identity Service: Port 5155 (REST HTTP/1) + Port 5156 (gRPC HTTP/2).
  - Content Service: Port 5249 (REST HTTP/1) + Port 5250 (gRPC HTTP/2).
  - Practice Service: Port 5261 (REST HTTP/1 + Swagger UI).
