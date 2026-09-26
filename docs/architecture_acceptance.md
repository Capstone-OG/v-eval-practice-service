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
5. **AI Subsystem Integration (Step 4)**:
   - Practice Service dispatches the 30-question diagnostic vector to `AI Engine` (`POST /api/v1/diagnostic/analyze`).
   - Estimates overall ability `\theta_0 \in [-3.0, +3.0]` (IRT 2PL + MAP).
   - Calculates initial mastery priors `P(L_0) \in [0.05, 0.95]` for all skills (Logistic Sigmoid), handling missing branch skills via domain-level fallback.
   - Generates multi-domain radar chart coordinates against the student's target score (`800/1200`).
   - Dynamically produces Socratic pedagogical feedback via Gemini.
   - Saves initial mastery priors into `LearningProfiles` (`mastery_score = p_l0`).
6. **Automatic Campus Class Placement (Step 5)**:
   - Evaluates placement tier based on `\theta_0`: `FOUNDATION` (`\theta_0 < -0.5`), `ACCELERATION` (`-0.5 \le \theta_0 \le 0.5`), `BREAKTHROUGH` (`\theta_0 > 0.5`).
   - Finds or initializes the corresponding class in `Classes` for the student's registered `CampusId`.
   - Records enrollment in `ClassEnrollments` linked to `diagnostic_submission_id`.
   - Returns full response payload with radar coordinates and Socratic guidance in under 2 seconds (Happy Case).

---

## 4. ACCEPTANCE & VERIFICATION RESULTS
- **Diagnostic Assessment & AI Exam Studio Runner UI**: Interactive web runner hosted at `http://localhost:5261/view-diagnostic.html` with KaTeX formula support, interactive Chart.js Radar Chart, multi-scenario Demo Solver, and AI Exam Studio Tab with customizable teacher prompt and Bloom 6 difficulty levels.
- **Database Save Pending & Publishing Flow**: Dedicated button to save generated exam into Supabase PostgreSQL with `IsPublished = false` (Pending Approval), seamlessly published (`IsPublished = true`) on teacher acceptance.
- **Cognitive Taxonomy Standardization**: Standardized difficulty metrics across 6 Revised Bloom's Taxonomy levels (Remembering, Understanding, Applying, Analyzing, Evaluating, Creating).
- **Solution Build**: Compiled cleanly with **0 Warning(s), 0 Error(s)** (`V-Eval-Practice_Service.sln`).
- **End-to-End Integration Verification**: Complete automated test script (`e2e_core_flow1.ps1`) verified all 5 steps across 3 microservices (Identity, Content, Practice): **Passed 100%**.
- **REST & gRPC Dual Port Protocol Architecture**:
  - Identity Service: Port 5155 (REST HTTP/1) + Port 5156 (gRPC HTTP/2).
  - Content Service: Port 5249 (REST HTTP/1) + Port 5250 (gRPC HTTP/2).
  - Practice Service: Port 5261 (REST HTTP/1 + Swagger UI).
