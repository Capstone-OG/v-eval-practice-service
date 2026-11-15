using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Features.Payments.Queries.GetStudentPayments;
using V_Eval_Practice_Service.Application.Features.Payments.Queries.GetStudentTuitionStatus;
using V_Eval_Practice_Service.Domain.Entities;
using Xunit;

namespace V_Eval_Practice_Service.Tests.Queries.Payments;

public class PaymentQueriesTests
{
    private readonly Mock<IPaymentRepository> _paymentRepoMock;

    public PaymentQueriesTests()
    {
        _paymentRepoMock = new Mock<IPaymentRepository>();
    }

    [Fact]
    public async Task GetStudentPayments_WhenPaymentsExist_ShouldReturnMappedDtos()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var course = new Course
        {
            CourseId = Guid.NewGuid(),
            Name = "Khóa Luyện Thi Chuẩn (Standard Adaptive)",
            Code = "CRS_STD",
            TuitionFee = 5000000
        };
        var registration = new CourseRegistration
        {
            RegistrationId = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.CourseId,
            Course = course,
            Status = "ACTIVE"
        };
        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            RegistrationId = registration.RegistrationId,
            Registration = registration,
            ParentId = studentId,
            OrderCode = 123456789L,
            Amount = 5000000,
            Status = "SUCCESS",
            PaidAt = DateTime.UtcNow,
            TransactionRef = "PAYOS-REF-001",
            CounterAccountBankName = "MBBANK"
        };

        _paymentRepoMock.Setup(r => r.GetPaymentsByStudentIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Payment> { payment });

        var handler = new GetStudentPaymentsQueryHandler(_paymentRepoMock.Object);

        // Act
        var result = await handler.Handle(new GetStudentPaymentsQuery(studentId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].OrderCode.Should().Be(123456789L);
        result.Value[0].CourseName.Should().Be("Khóa Luyện Thi Chuẩn (Standard Adaptive)");
        result.Value[0].Status.Should().Be("SUCCESS");
        result.Value[0].CounterAccountBankName.Should().Be("MBBANK");
    }

    [Fact]
    public async Task GetStudentTuitionStatus_WhenPaymentIsSuccess_ShouldReturnIsPaidTrueAndUnlockedFeatures()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var course = new Course
        {
            CourseId = Guid.NewGuid(),
            Name = "Khóa Chuyên Đề Bứt Phá (Intensive Plus)",
            Code = "CRS_PRO",
            PlanType = "INTENSIVE",
            DurationWeeks = 16
        };
        var registration = new CourseRegistration
        {
            RegistrationId = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.CourseId,
            Course = course,
            Status = "ACTIVE",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(112))
        };
        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            RegistrationId = registration.RegistrationId,
            Registration = registration,
            Status = "SUCCESS",
            OrderCode = 999888L,
            Amount = 10000000,
            PaidAt = DateTime.UtcNow
        };
        var enrolledClass = new Class
        {
            ClassId = Guid.NewGuid(),
            Name = "Lớp Chuyên Đề Toán - Tư Duy ĐGNL",
            Status = "ACTIVE"
        };
        var enrollment = new ClassEnrollment
        {
            EnrollmentId = Guid.NewGuid(),
            ClassId = enrolledClass.ClassId,
            Class = enrolledClass,
            StudentId = studentId,
            Status = "ENROLLED"
        };

        _paymentRepoMock.Setup(r => r.GetPaymentsByStudentIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Payment> { payment });
        _paymentRepoMock.Setup(r => r.GetLatestActiveRegistrationByStudentIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(registration);
        _paymentRepoMock.Setup(r => r.GetEnrollmentByStudentIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(enrollment);

        var handler = new GetStudentTuitionStatusQueryHandler(_paymentRepoMock.Object);

        // Act
        var result = await handler.Handle(new GetStudentTuitionStatusQuery(studentId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsPaid.Should().BeTrue();
        result.Value.CourseName.Should().Be("Khóa Chuyên Đề Bứt Phá (Intensive Plus)");
        result.Value.PlanType.Should().Be("INTENSIVE");
        result.Value.AssignedClassName.Should().Be("Lớp Chuyên Đề Toán - Tư Duy ĐGNL");
        result.Value.UnlockedFeatures.Should().Contain("Lộ trình học thích ứng 4 bước PLAR");
        result.Value.UnlockedFeatures.Should().Contain("Gia sư AI Socratic kèm cặp 24/7");
        result.Value.Payments.Should().HaveCount(1);
    }
}

