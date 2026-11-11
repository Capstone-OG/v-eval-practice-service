using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;
using V_Eval_Practice_Service.Application.Features.Payments.Commands.CreatePaymentLink;
using V_Eval_Practice_Service.Application.Features.Payments.DTOs;
using V_Eval_Practice_Service.Domain.Entities;
using Xunit;

namespace V_Eval_Practice_Service.Tests.Commands.Payments;

public class CreatePaymentLinkCommandHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepoMock;
    private readonly Mock<IPayOsPaymentService> _payOsServiceMock;
    private readonly Mock<ILogger<CreatePaymentLinkCommandHandler>> _loggerMock;
    private readonly CreatePaymentLinkCommandHandler _handler;

    public CreatePaymentLinkCommandHandlerTests()
    {
        _paymentRepoMock = new Mock<IPaymentRepository>();
        _payOsServiceMock = new Mock<IPayOsPaymentService>();
        _loggerMock = new Mock<ILogger<CreatePaymentLinkCommandHandler>>();
        _handler = new CreatePaymentLinkCommandHandler(
            _paymentRepoMock.Object,
            _payOsServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WhenCourseNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        var command = new CreatePaymentLinkCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "https://return", "https://cancel");

        _paymentRepoMock.Setup(r => r.GetCourseByIdAsync(command.CourseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Course?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Course.NotFound");
    }

    [Fact]
    public async Task Handle_WhenRegistrationAlreadyActive_ShouldReturnConflictError()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var command = new CreatePaymentLinkCommand(studentId, Guid.NewGuid(), courseId, "https://return", "https://cancel");

        var course = new Course
        {
            CourseId = courseId,
            Name = "Luyện đề ĐGNL Chuyên Sâu",
            Code = "DGNL_2026",
            IsActive = true,
            TuitionFee = 1500000m
        };

        var activeReg = new CourseRegistration
        {
            StudentId = studentId,
            CourseId = courseId,
            Status = "ACTIVE"
        };

        _paymentRepoMock.Setup(r => r.GetCourseByIdAsync(courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(course);
        _paymentRepoMock.Setup(r => r.GetActiveRegistrationAsync(studentId, courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeReg);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Registration.AlreadyActive");
    }

    [Fact]
    public async Task Handle_WhenValidRequest_ShouldCreatePaymentAndReturnCheckoutUrl()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var command = new CreatePaymentLinkCommand(studentId, parentId, courseId, "https://return", "https://cancel");

        var course = new Course
        {
            CourseId = courseId,
            Name = "Luyện đề ĐGNL Chuyên Sâu",
            Code = "DGNL",
            IsActive = true,
            TuitionFee = 2500000m
        };

        _paymentRepoMock.Setup(r => r.GetCourseByIdAsync(courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(course);
        _paymentRepoMock.Setup(r => r.GetActiveRegistrationAsync(studentId, courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CourseRegistration?)null);

        var fakeCheckoutUrl = "https://pay.payos.vn/web/123456789";
        _payOsServiceMock.Setup(s => s.CreatePaymentLinkAsync(
                It.IsAny<long>(),
                course.TuitionFee,
                It.IsAny<string>(),
                command.ReturnUrl,
                command.CancelUrl,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentLinkResultDto(123456789, fakeCheckoutUrl, "link_123", "PENDING", null));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.CheckoutUrl.Should().Be(fakeCheckoutUrl);
        result.Value.Amount.Should().Be(2500000m);
        result.Value.Status.Should().Be("PENDING");

        _paymentRepoMock.Verify(r => r.AddRegistrationAsync(It.IsAny<CourseRegistration>(), It.IsAny<CancellationToken>()), Times.Once);
        _paymentRepoMock.Verify(r => r.AddPaymentAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Once);
        _paymentRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

