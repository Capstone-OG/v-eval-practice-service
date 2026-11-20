using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;
using V_Eval_Practice_Service.Application.Features.Payments.Commands.ProcessPayOsWebhook;
using V_Eval_Practice_Service.Application.Features.Payments.DTOs;
using V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.GenerateRoadmap;
using V_Eval_Practice_Service.Domain.Entities;
using Xunit;

namespace V_Eval_Practice_Service.Tests.Commands.Payments;

public class ProcessPayOsWebhookCommandHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepoMock;
    private readonly Mock<ILearningRoadmapRepository> _roadmapRepoMock;
    private readonly Mock<IExamSubmissionRepository> _submissionRepoMock;
    private readonly Mock<IPayOsPaymentService> _payOsServiceMock;
    private readonly Mock<ISender> _mediatorMock;
    private readonly Mock<ILogger<ProcessPayOsWebhookCommandHandler>> _loggerMock;
    private readonly Mock<IExecutionTransaction> _transactionMock;
    private readonly ProcessPayOsWebhookCommandHandler _handler;

    public ProcessPayOsWebhookCommandHandlerTests()
    {
        _paymentRepoMock = new Mock<IPaymentRepository>();
        _roadmapRepoMock = new Mock<ILearningRoadmapRepository>();
        _submissionRepoMock = new Mock<IExamSubmissionRepository>();
        _payOsServiceMock = new Mock<IPayOsPaymentService>();
        _mediatorMock = new Mock<ISender>();
        _loggerMock = new Mock<ILogger<ProcessPayOsWebhookCommandHandler>>();
        _transactionMock = new Mock<IExecutionTransaction>();

        _paymentRepoMock.Setup(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _handler = new ProcessPayOsWebhookCommandHandler(
            _paymentRepoMock.Object,
            _roadmapRepoMock.Object,
            _submissionRepoMock.Object,
            _payOsServiceMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WhenSignatureIsInvalid_ShouldReturnValidationError()
    {
        var command = new ProcessPayOsWebhookCommand("{\"raw\":\"bad\"}", "127.0.0.1");

        _payOsServiceMock.Setup(s => s.VerifyWebhookDataAsync(command.RawPayload, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifyWebhookResultDto(false, null, command.RawPayload, "Invalid HMAC signature"));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PayOS.InvalidSignature");
        _paymentRepoMock.Verify(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOrderCodeNotFound_ShouldReturnNotFoundError()
    {
        var orderCode = 9876543210L;
        var command = new ProcessPayOsWebhookCommand("{\"orderCode\":9876543210}", "127.0.0.1");

        var webhookData = new PayOsWebhookDataDto(
            orderCode, 1500000, "VEVAL DGNL", "12345678", "TXN123", "2026-11-03 20:00:00",
            "MBBANK", "NGUYEN VAN A", "99998888");

        _payOsServiceMock.Setup(s => s.VerifyWebhookDataAsync(command.RawPayload, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifyWebhookResultDto(true, webhookData, command.RawPayload, null));

        _paymentRepoMock.Setup(r => r.GetByOrderCodeAsync(orderCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.NotFound");
    }

    [Fact]
    public async Task Handle_WhenPaymentAlreadySuccess_ShouldBeIdempotentAndCommitEarly()
    {
        var orderCode = 9876543210L;
        var command = new ProcessPayOsWebhookCommand("{\"orderCode\":9876543210}", "127.0.0.1");

        var webhookData = new PayOsWebhookDataDto(
            orderCode, 1500000, "VEVAL DGNL", "12345678", "TXN123", "2026-11-03 20:00:00",
            "MBBANK", "NGUYEN VAN A", "99998888");

        var existingPayment = new Payment
        {
            OrderCode = orderCode,
            Status = "SUCCESS",
            PaidAt = DateTime.UtcNow.AddMinutes(-10)
        };

        _payOsServiceMock.Setup(s => s.VerifyWebhookDataAsync(command.RawPayload, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifyWebhookResultDto(true, webhookData, command.RawPayload, null));

        _paymentRepoMock.Setup(r => r.GetByOrderCodeAsync(orderCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPayment);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _transactionMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(m => m.Send(It.IsAny<GenerateRoadmapCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenValidSuccessWebhook_ShouldAtomicallyActivateRegistrationAndTriggerRealRoadmapGeneration()
    {
        var orderCode = 5566778899L;
        var registrationId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var diagnosticSubmissionId = Guid.NewGuid();

        var command = new ProcessPayOsWebhookCommand("{\"orderCode\":5566778899}", "192.168.1.1");

        var webhookData = new PayOsWebhookDataDto(
            orderCode, 2000000, "VEVAL MAT 5566778899", "987654", "REF_PAYOS_999", "2026-11-03 21:00:00",
            "VIETCOMBANK", "TRAN VAN B", "101020203030");

        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            OrderCode = orderCode,
            RegistrationId = registrationId,
            Status = "PENDING"
        };

        var registration = new CourseRegistration
        {
            RegistrationId = registrationId,
            StudentId = studentId,
            CourseId = courseId,
            Status = "PENDING_PAYMENT"
        };

        var course = new Course
        {
            CourseId = courseId,
            DurationWeeks = 16,
            Name = "Khóa Toán Học Thích Ứng"
        };

        var availableClass = new Class
        {
            ClassId = classId,
            CourseId = courseId,
            Status = "ACTIVE",
            Name = "Lớp Toán A1"
        };

        _payOsServiceMock.Setup(s => s.VerifyWebhookDataAsync(command.RawPayload, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifyWebhookResultDto(true, webhookData, command.RawPayload, null));

        _paymentRepoMock.Setup(r => r.GetByOrderCodeAsync(orderCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        _paymentRepoMock.Setup(r => r.GetRegistrationByIdAsync(registrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(registration);

        _paymentRepoMock.Setup(r => r.GetCourseByIdAsync(courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(course);

        _paymentRepoMock.Setup(r => r.GetAvailableClassByCourseIdAsync(courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(availableClass);

        _roadmapRepoMock.Setup(r => r.GetActiveByStudentIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LearningRoadmap?)null);

        _submissionRepoMock.Setup(r => r.GetByStudentIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExamSubmission>
            {
                new()
                {
                    SubmissionId = diagnosticSubmissionId,
                    StudentId = studentId,
                    ExamType = "DIAGNOSTIC",
                    Status = "COMPLETED",
                    CompletedAt = DateTime.UtcNow.AddMinutes(-5)
                }
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be("SUCCESS");
        payment.TransactionRef.Should().Be("REF_PAYOS_999");
        payment.CounterAccountBankName.Should().Be("VIETCOMBANK");
        payment.CounterAccountName.Should().Be("TRAN VAN B");
        payment.PaidAt.Should().NotBeNull();

        registration.Status.Should().Be("ACTIVE");
        registration.StartDate.Should().NotBeNull();
        registration.EndDate.Should().NotBeNull();

        _paymentRepoMock.Verify(r => r.AddClassEnrollmentAsync(
            It.Is<ClassEnrollment>(e => e.ClassId == classId && e.StudentId == studentId && e.Status == "ENROLLED"),
            It.IsAny<CancellationToken>()), Times.Once);

        _mediatorMock.Verify(m => m.Send(
            It.Is<GenerateRoadmapCommand>(c => c.StudentId == studentId && c.DiagnosticSubmissionId == diagnosticSubmissionId),
            It.IsAny<CancellationToken>()), Times.Once);

        _paymentRepoMock.Verify(r => r.AddTransactionAsync(
            It.Is<PaymentTransaction>(t =>
                t.PaymentId == payment.PaymentId &&
                t.EventType == "WEBHOOK_PROCESSED_SUCCESS"),
            It.IsAny<CancellationToken>()), Times.Once);

        _paymentRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ShouldRollbackTransactionAndRethrow()
    {
        var orderCode = 1122334455L;
        var command = new ProcessPayOsWebhookCommand("{\"orderCode\":1122334455}", "127.0.0.1");

        var webhookData = new PayOsWebhookDataDto(
            orderCode, 1000000, "VEVAL TEST", "123", "REF123", "2026-11-03", null, null, null);

        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            OrderCode = orderCode,
            Status = "PENDING"
        };

        _payOsServiceMock.Setup(s => s.VerifyWebhookDataAsync(command.RawPayload, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifyWebhookResultDto(true, webhookData, command.RawPayload, null));

        _paymentRepoMock.Setup(r => r.GetByOrderCodeAsync(orderCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        _paymentRepoMock.Setup(r => r.GetRegistrationByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database deadlock simulation"));

        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();

        _transactionMock.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
