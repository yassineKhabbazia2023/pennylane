using System.Net;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Models.Pagers;
using Pennylane.Application.Models;
using Pennylane.Application.services;
using Pennylane.Domain.ExternalApis.Models.FirmAPI;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Pulse.ExceptionMiddleware.Exceptions;
using Xunit;

namespace Pennylane.Application.Tests.Services
{
    public class PennylaneApiGroupServiceTests
    {
        private static readonly IOptions<PennylaneServiceOptions> DefaultOptions = Options.Create(new PennylaneServiceOptions
        {
            ApiUrl = "http://test",
            ApiFirmUrl = "http://test",
            ApiKey = "test",
            AuthenticationMode = "test",
            CollaboratorAuthenticationMode = "test",
            DefaultRole = "test",
            SupportedSaasPlans = "test",
            Token = "test"
        });
        private HttpClient CreateHttpClient(HttpResponseMessage responseMessage)
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            return new HttpClient(handlerMock.Object)
            {
                BaseAddress = new System.Uri("http://localhost")
            };
        }

        private HttpClient CreateHttpClientSequence(params HttpResponseMessage[] responseMessages)
        {
            var responseQueue = new Queue<HttpResponseMessage>(responseMessages);
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => responseQueue.Dequeue());

            return new HttpClient(handlerMock.Object)
            {
                BaseAddress = new System.Uri("http://localhost")
            };
        }

        [Fact]
        public async Task UpdateUserEmailAsync_ShouldReturnId_WhenResponseIsValid()
        {
            // Arrange
            var pennylaneContact = new PennylaneContact { Id = "user-123" };
            var expectedId = "updated-user-123";

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new PennylaneResult<string> { Id = expectedId })
            };

            var httpClient = CreateHttpClient(httpResponse);
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act
            var result = await service.UpdateUserEmailAsync(pennylaneContact, "new@email.com");

            // Assert
            result.Should().Be(expectedId);
        }

        [Fact]
        public async Task UpdateUserEmailAsync_ShouldThrowBadRequestException_WhenResponseIsNull()
        {
            // Arrange
            var pennylaneContact = new PennylaneContact { Id = "user-123" };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create<object?>(null) // Simulates null response body
            };

            var httpClient = CreateHttpClient(httpResponse);
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act
            var act = async () => await service.UpdateUserEmailAsync(pennylaneContact, "new@email.com");

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("Failed to update user.");
        }

        #region CreateCompanyAsync Tests

        [Fact]
        public async Task CreateCompanyAsync_ShouldReturnResponse_WhenSuccessful()
        {
            // Arrange
            var request = new CreatePennylaneCompanyRequest
            {
                Name = "Test Company",
                RegNo = "123456789",
                FirmId = "firm-123",
                Id = "ACC-001"
            };

            var expectedResponse = new CreatePennylaneCompanyResponse
            {
                Id = "ACC-001"
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedResponse)
            };

            var httpClient = CreateHttpClient(httpResponse);
            var loggerMock = new Mock<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, loggerMock.Object, DefaultOptions);

            // Act
            var result = await service.CreateCompanyAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Response.Should().NotBeNull();
            result.Response!.Id.Should().Be("ACC-001");

            // Verify that success was logged
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully created company")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateCompanyAsync_ShouldReturnFailedResult_WhenRequestFails()
        {
            // Arrange
            var request = new CreatePennylaneCompanyRequest
            {
                Name = "Test Company",
                RegNo = "123456789",
                FirmId = "firm-123",
                Id = "ACC-001"
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Error creating company: Invalid data")
            };

            var httpClient = CreateHttpClient(httpResponse);
            var loggerMock = new Mock<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, loggerMock.Object, DefaultOptions);

            // Act
            var result = await service.CreateCompanyAsync(request);

            // Assert
            result.Success.Should().BeFalse();
            result.Response.Should().BeNull();
            result.ErrorContent.Should().Contain("Error creating company");

            // Verify that error was logged
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to create company in Pennylane")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateCompanyAsync_ShouldReturnFailedResult_WhenResponseIsNull()
        {
            // Arrange
            var request = new CreatePennylaneCompanyRequest
            {
                Name = "Test Company",
                RegNo = "123456789",
                FirmId = "firm-123",
                Id = "ACC-001"
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create<object?>(null)
            };

            var httpClient = CreateHttpClient(httpResponse);
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act
            var result = await service.CreateCompanyAsync(request);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorContent.Should().Be("Failed to deserialize the company creation response.");
        }

        [Fact]
        public async Task CreateCompanyAsync_ShouldReturnFailedResult_WhenServerErrorOccurs()
        {
            // Arrange
            var request = new CreatePennylaneCompanyRequest
            {
                Name = "Test Company",
                RegNo = "123456789",
                FirmId = "firm-123",
                Id = "ACC-001"
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Internal server error")
            };

            var httpClient = CreateHttpClient(httpResponse);
            var loggerMock = new Mock<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, loggerMock.Object, DefaultOptions);

            // Act
            var result = await service.CreateCompanyAsync(request);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorContent.Should().Be("Internal server error");
        }

        [Fact]
        public async Task CreateCompanyAsync_ShouldReturnFailedResult_WhenNotFoundErrorOccurs()
        {
            // Arrange
            var request = new CreatePennylaneCompanyRequest
            {
                Name = "Test Company",
                RegNo = "123456789",
                FirmId = "firm-123",
                Id = "ACC-001"
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("Firm not found")
            };

            var httpClient = CreateHttpClient(httpResponse);
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act
            var result = await service.CreateCompanyAsync(request);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorContent.Should().Be("Firm not found");
        }

        [Fact]
        public async Task CreateCompanyAsync_ShouldReturnRegNoAlreadyExists_WhenRegNoConflict()
        {
            // Arrange
            var request = new CreatePennylaneCompanyRequest
            {
                Name = "Test Company",
                RegNo = "123456789",
                FirmId = "firm-123",
                Id = "ACC-001"
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("A company with the same reg_no already exist under the same firm.")
            };

            var httpClient = CreateHttpClient(httpResponse);
            var loggerMock = new Mock<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, loggerMock.Object, DefaultOptions);

            // Act
            var result = await service.CreateCompanyAsync(request);

            // Assert
            result.Success.Should().BeFalse();
            result.RegNoAlreadyExists.Should().BeTrue();
            result.ErrorContent.Should().Contain("reg_no already");
        }

        [Fact]
        public async Task CreateCompanyAsync_ShouldReturnRequiresFirmAssignment_WhenClientMustRequestAssignment()
        {
            // Arrange
            var request = new CreatePennylaneCompanyRequest
            {
                Name = "Test Company",
                RegNo = "123456789",
                FirmId = "firm-123",
                Id = "ACC-001"
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Company already exists, the client must request to be assigned to your firm")
            };

            var httpClient = CreateHttpClient(httpResponse);
            var loggerMock = new Mock<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, loggerMock.Object, DefaultOptions);

            // Act
            var result = await service.CreateCompanyAsync(request);

            // Assert
            result.Success.Should().BeFalse();
            result.RequiresFirmAssignment.Should().BeTrue();
            result.RegNoAlreadyExists.Should().BeFalse();
            result.ErrorContent.Should().Contain("must request to be assigned to your firm");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateCompanyAsync_ShouldFallbackToFailed_WhenErrorContentIsBlank(string blankContent)
        {
            // Covers the IsNullOrWhiteSpace early-return branch in RequiresFirmAssignment / IsRegNoConflict / DoesAlreadyExists.
            var request = new CreatePennylaneCompanyRequest
            {
                Name = "Test Company",
                RegNo = "123456789",
                FirmId = "firm-123",
                Id = "ACC-001"
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(blankContent)
            };

            var httpClient = CreateHttpClient(httpResponse);
            var loggerMock = new Mock<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, loggerMock.Object, DefaultOptions);

            var result = await service.CreateCompanyAsync(request);

            result.Success.Should().BeFalse();
            result.RequiresFirmAssignment.Should().BeFalse();
            result.RegNoAlreadyExists.Should().BeFalse();
        }

        [Fact]
        public async Task CreateCompanyAsync_ShouldPrioritizeRequiresFirmAssignment_OverGenericAlreadyExists()
        {
            // The firm-assignment message is a specialized form of "Company already exists";
            // we must classify it as RequiresFirmAssignment, not as the generic AlreadyExists/RegNoExists.
            var request = new CreatePennylaneCompanyRequest
            {
                Name = "Test Company",
                RegNo = "123456789",
                FirmId = "firm-123",
                Id = "ACC-001"
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Company already exists, the client must request to be assigned to your firm")
            };

            var httpClient = CreateHttpClient(httpResponse);
            var loggerMock = new Mock<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, loggerMock.Object, DefaultOptions);

            var result = await service.CreateCompanyAsync(request);

            result.RequiresFirmAssignment.Should().BeTrue();
            result.RegNoAlreadyExists.Should().BeFalse();
        }

        [Fact]
        public async Task CreateCompanyAsync_ShouldReturnRequiresFirmTransfer_WhenCompanyRequiresTransfer()
        {
            // Arrange
            var request = new CreatePennylaneCompanyRequest
            {
                Name = "Test Company",
                RegNo = "123456789",
                FirmId = "firm-123",
                Id = "ACC-001"
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Company already exists, you can request its transfer from its current firm")
            };

            var httpClient = CreateHttpClient(httpResponse);
            var loggerMock = new Mock<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, loggerMock.Object, DefaultOptions);

            // Act
            var result = await service.CreateCompanyAsync(request);

            // Assert
            result.Success.Should().BeFalse();
            result.RequiresFirmTransfer.Should().BeTrue();
            result.RequiresFirmAssignment.Should().BeFalse();
            result.RegNoAlreadyExists.Should().BeFalse();
            result.ErrorContent.Should().Contain("you can request its transfer from its current firm");
        }

        [Fact]
        public async Task CreateCompanyAsync_ShouldPrioritizeRequiresFirmTransfer_OverGenericAlreadyExists()
        {
            // The firm-transfer message is a specialized form of "Company already exists";
            // we must classify it as RequiresFirmTransfer, not as the generic AlreadyExists/RegNoExists.
            var request = new CreatePennylaneCompanyRequest
            {
                Name = "Test Company",
                RegNo = "123456789",
                FirmId = "firm-123",
                Id = "ACC-001"
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Company already exists, you can request its transfer from its current firm")
            };

            var httpClient = CreateHttpClient(httpResponse);
            var loggerMock = new Mock<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, loggerMock.Object, DefaultOptions);

            var result = await service.CreateCompanyAsync(request);

            result.RequiresFirmTransfer.Should().BeTrue();
            result.RequiresFirmAssignment.Should().BeFalse();
            result.RegNoAlreadyExists.Should().BeFalse();
        }

        #endregion

        #region GrantFirmUserAccessAsync Tests

        [Fact]
        public async Task GrantFirmUserAccessAsync_ShouldReturnSuccess_WhenApiReturnsOk()
        {
            // Arrange
            var firmId = "firm-123";
            var userEmail = "user@example.com";
            var role = "accountant";

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\": \"access-123\"}")
            };

            var httpClient = CreateHttpClient(httpResponse);
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act
            var (success, errorMessage) = await service.GrantFirmUserAccessAsync(firmId, role, userEmail: userEmail);

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
        }

        [Fact]
        public async Task GrantFirmUserAccessAsync_ShouldReturnSuccess_WhenUserAlreadyHasAccess()
        {
            // Arrange
            var firmId = "firm-123";
            var userEmail = "user@example.com";
            var role = "accountant";

            var httpResponse = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent("{\"error\": {\"message\": \"L'utilisateur ou utilisatrice a déjà accès à ce cabinet\"}}")
            };

            var httpClient = CreateHttpClient(httpResponse);
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act
            var (success, errorMessage) = await service.GrantFirmUserAccessAsync(firmId, role, userEmail: userEmail);

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
        }

        [Fact]
        public async Task GrantFirmUserAccessAsync_ShouldReturnSuccess_WhenUserAlreadyHasAccessWithEnglishMessage()
        {
            // Arrange
            var firmId = "firm-123";
            var userEmail = "user@example.com";
            var role = "accountant";

            var httpResponse = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent("{\"error\":{\"message\":\"The user already has access to the firm\"}}")
            };

            var httpClient = CreateHttpClient(httpResponse);
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act
            var (success, errorMessage) = await service.GrantFirmUserAccessAsync(firmId, role, userEmail: userEmail);

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
        }

        [Fact]
        public async Task GrantFirmUserAccessAsync_ShouldReturnFailure_WhenApiFails()
        {
            // Arrange
            var firmId = "firm-123";
            var userEmail = "user@example.com";
            var role = "accountant";

            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Invalid request")
            };

            var httpClient = CreateHttpClient(httpResponse);
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act
            var (success, errorMessage) = await service.GrantFirmUserAccessAsync(firmId, role, userEmail: userEmail);

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Be("Invalid request");
        }

        [Fact]
        public async Task GrantFirmUserAccessAsync_ShouldReturnFailure_WhenFirmNotFound()
        {
            // Arrange
            var firmId = "non-existent-firm";
            var userEmail = "user@example.com";
            var role = "accountant";

            var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("Firm not found")
            };

            var httpClient = CreateHttpClient(httpResponse);
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act
            var (success, errorMessage) = await service.GrantFirmUserAccessAsync(firmId, role, userEmail: userEmail);

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Be("Firm not found");
        }

        [Fact]
        public async Task GrantFirmUserAccessAsync_UseUserIdForGrantAccess_True_WithNullUserId_ShouldThrowArgumentException()
        {
            // Arrange
            var optionsWithFlag = Options.Create(new PennylaneServiceOptions
            {
                ApiUrl = "http://test",
                ApiFirmUrl = "http://test",
                ApiKey = "test",
                AuthenticationMode = "test",
                CollaboratorAuthenticationMode = "test",
                DefaultRole = "test",
                SupportedSaasPlans = "test",
                Token = "test",
                UseUserIdForGrantAccess = true
            });

            var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, optionsWithFlag);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.GrantFirmUserAccessAsync("firm-123", "accountant", userId: null, userEmail: "test@example.com")
            );
            exception.ParamName.Should().Be("userId");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GrantFirmUserAccessAsync_UseUserIdForGrantAccess_True_WithEmptyUserId_ShouldThrowArgumentException(string emptyUserId)
        {
            // Arrange
            var optionsWithFlag = Options.Create(new PennylaneServiceOptions
            {
                ApiUrl = "http://test",
                ApiFirmUrl = "http://test",
                ApiKey = "test",
                AuthenticationMode = "test",
                CollaboratorAuthenticationMode = "test",
                DefaultRole = "test",
                SupportedSaasPlans = "test",
                Token = "test",
                UseUserIdForGrantAccess = true
            });

            var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, optionsWithFlag);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.GrantFirmUserAccessAsync("firm-123", "accountant", userId: emptyUserId, userEmail: "test@example.com")
            );
            exception.ParamName.Should().Be("userId");
        }

        [Fact]
        public async Task GrantFirmUserAccessAsync_UseUserIdForGrantAccess_False_WithNullUserEmail_ShouldThrowArgumentException()
        {
            // Arrange - DefaultOptions has UseUserIdForGrantAccess = false by default
            var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.GrantFirmUserAccessAsync("firm-123", "accountant", userId: "user-456", userEmail: null)
            );
            exception.ParamName.Should().Be("userEmail");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GrantFirmUserAccessAsync_UseUserIdForGrantAccess_False_WithEmptyUserEmail_ShouldThrowArgumentException(string emptyEmail)
        {
            // Arrange - DefaultOptions has UseUserIdForGrantAccess = false by default
            var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.GrantFirmUserAccessAsync("firm-123", "accountant", userId: "user-456", userEmail: emptyEmail)
            );
            exception.ParamName.Should().Be("userEmail");
        }

        #endregion

        #region TryGetFirmIdByNameAsync Tests

        [Fact]
        public async Task TryGetFirmIdByNameAsync_ShouldReturnFirmId_WhenHubIsFound()
        {
            // Arrange
            var hubName = "Grande Couronne";
            var expectedId = "firm-789";

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new PennylaneFirmsPager
                {
                    CurrentPage = 1,
                    TotalPages = 1,
                    Firms = new List<PennylaneFirm>
                    {
                        new PennylaneFirm { Id = expectedId, Name = $"R/ - {hubName}", RegNo = "123456789" }
                    }
                })
            };

            var httpClient = CreateHttpClient(httpResponse);
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act
            var result = await service.TryGetFirmIdByNameAsync(hubName);

            // Assert
            result.Should().Be(expectedId);
        }

        [Fact]
        public async Task TryGetFirmIdByNameAsync_ShouldReturnNull_WhenHubIsNotFound()
        {
            // Arrange
            var hubName = "Missing Hub";

            var page1Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new PennylaneFirmsPager
                {
                    CurrentPage = 1,
                    TotalPages = 2,
                    Firms = new List<PennylaneFirm>
                    {
                        new PennylaneFirm { Id = "firm-001", Name = "Hub One", RegNo = "111111111" }
                    }
                })
            };

            var page2Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new PennylaneFirmsPager
                {
                    CurrentPage = 2,
                    TotalPages = 2,
                    Firms = new List<PennylaneFirm>
                    {
                        new PennylaneFirm { Id = "firm-002", Name = "Hub Two", RegNo = "222222222" }
                    }
                })
            };

            var httpClient = CreateHttpClientSequence(page1Response, page2Response);
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act
            var result = await service.TryGetFirmIdByNameAsync(hubName);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region TryGetCompanyUserAsync Tests

        private static IOptions<PennylaneServiceOptions> CreateRetryOptions(int retryCount) => Options.Create(new PennylaneServiceOptions
        {
            ApiUrl = "http://test",
            ApiFirmUrl = "http://test",
            ApiKey = "test",
            AuthenticationMode = "test",
            CollaboratorAuthenticationMode = "test",
            DefaultRole = "test",
            SupportedSaasPlans = "test",
            Token = "test",
            CompanyUsersRetryCount = retryCount,
            CompanyUsersRetryDelayInMilliseconds = 0
        });

        private static HttpResponseMessage CreateCompanyNotInScopeResponse() => new(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{\"error\":{\"message\":\"Company not found in your scope\"}}")
        };

        [Fact]
        public async Task TryGetCompanyUserAsync_ShouldRetryAndReturnUser_WhenCompanyNotYetVisibleInScope()
        {
            // Arrange
            var contactEmail = "client@example.com";
            var usersResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new PennylaneUsersPager
                {
                    CurrentPage = 1,
                    TotalPages = 1,
                    Users = new List<PennylaneUsers>
                    {
                        new PennylaneUsers { Id = "user-1", Email = contactEmail }
                    }
                })
            };

            var httpClient = CreateHttpClientSequence(CreateCompanyNotInScopeResponse(), usersResponse);
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, CreateRetryOptions(retryCount: 3));

            // Act
            var result = await service.TryGetCompanyUserAsync(contactEmail, "ACC-001");

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be("user-1");
        }

        [Fact]
        public async Task TryGetCompanyUserAsync_ShouldNotRetry_WhenRetryCountIsZero()
        {
            // Arrange - a single response in the queue: any retry would dequeue a second one and fail
            var httpClient = CreateHttpClientSequence(CreateCompanyNotInScopeResponse());
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, CreateRetryOptions(retryCount: 0));

            // Act
            var result = await service.TryGetCompanyUserAsync("client@example.com", "ACC-001");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task TryGetCompanyUserAsync_ShouldThrow_WhenNotFoundIsNotAScopeError()
        {
            // Arrange
            var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"error\":{\"message\":\"Company not found\"}}")
            };
            var httpClient = CreateHttpClient(httpResponse);
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, CreateRetryOptions(retryCount: 3));

            // Act
            var act = async () => await service.TryGetCompanyUserAsync("client@example.com", "ACC-001");

            // Assert
            await act.Should().ThrowAsync<RequestHttpException>();
        }

        [Fact]
        public async Task TryGetCompanyUserAsync_ShouldReturnNull_WhenCompanyStillNotFoundAfterRetries()
        {
            // Arrange
            var httpClient = CreateHttpClientSequence(
                CreateCompanyNotInScopeResponse(),
                CreateCompanyNotInScopeResponse(),
                CreateCompanyNotInScopeResponse());
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, CreateRetryOptions(retryCount: 2));

            // Act
            var result = await service.TryGetCompanyUserAsync("client@example.com", "ACC-001");

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetCompaniesAsync Tests

        [Fact]
        public async Task GetCompaniesAsync_WithPerPage_AppendsPerPageQueryParameter()
        {
            // Arrange
            HttpRequestMessage? captured = null;
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) => captured = req)
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new PennylaneCompanyPager { TotalPages = 1, CurrentPage = 1 }),
                });
            var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new System.Uri("https://unit.test/api/") };
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act
            await service.GetCompaniesAsync(page: 3, perPage: 500);

            // Assert
            captured.Should().NotBeNull();
            captured!.RequestUri!.Query.Should().Contain("page=3").And.Contain("per_page=500");
        }

        #endregion

        #region TryUpdateCompanyIdAsync Tests

        [Fact]
        public async Task TryUpdateCompanyIdAsync_WhenApiReturns200_ReturnsTrue()
        {
            // Arrange
            var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = "EC86906448" }),
            });
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act
            var result = await service.TryUpdateCompanyIdAsync("EC86906447", "EC86906448");

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task TryUpdateCompanyIdAsync_WhenApiFails_ReturnsFalse()
        {
            // Arrange
            var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent("{\"error\":\"cannot update\"}"),
            });
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act
            var result = await service.TryUpdateCompanyIdAsync("EC86906447", "EC86906448");

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region TryUpdateUserAuthenticationMethodAsync Tests

        [Fact]
        public async Task TryUpdateUserAuthenticationMethodAsync_WhenApiReturns200_ReturnsTrue()
        {
            // Arrange
            var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = "user-1" }),
            });
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act
            var result = await service.TryUpdateUserAuthenticationMethodAsync("user-1", "kpmg-customer");

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task TryUpdateUserAuthenticationMethodAsync_WhenApiFails_ReturnsFalse()
        {
            // Arrange
            var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent("{\"error\":\"cannot update\"}"),
            });
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act
            var result = await service.TryUpdateUserAuthenticationMethodAsync("user-1", "kpmg-customer");

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region TryPostUsersAsync Tests

        [Fact]
        public async Task TryPostUsersAsync_ShouldReturnUserId_WhenSuccessful()
        {
            // Arrange
            var pennylaneContact = new PennylaneContact
            {
                Email = "newuser@example.com",
                FirstName = "John",
                LastName = "Doe"
            };
            var expectedId = "new-user-456";

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new PennylaneResult<string> { Id = expectedId })
            };

            var httpClient = CreateHttpClient(httpResponse);
            var logger = Mock.Of<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, logger, DefaultOptions);

            // Act
            var (userId, alreadyExists) = await service.TryPostUsersAsync(pennylaneContact);

            // Assert
            userId.Should().Be(expectedId);
            alreadyExists.Should().BeFalse();
        }

        [Fact]
        public async Task TryPostUsersAsync_ShouldReturnAlreadyExists_WhenEmailAlreadyTaken()
        {
            // Arrange
            var pennylaneContact = new PennylaneContact
            {
                Email = "existing@example.com",
                FirstName = "John",
                LastName = "Doe"
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Email already taken")
            };

            var httpClient = CreateHttpClient(httpResponse);
            var loggerMock = new Mock<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, loggerMock.Object, DefaultOptions);

            // Act
            var (userId, alreadyExists) = await service.TryPostUsersAsync(pennylaneContact);

            // Assert
            userId.Should().BeNull();
            alreadyExists.Should().BeTrue();
        }

        [Fact]
        public async Task TryPostUsersAsync_ShouldReturnFailure_WhenOtherErrorOccurs()
        {
            // Arrange
            var pennylaneContact = new PennylaneContact
            {
                Email = "newuser@example.com",
                FirstName = "John",
                LastName = "Doe"
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Internal server error")
            };

            var httpClient = CreateHttpClient(httpResponse);
            var loggerMock = new Mock<ILogger<PennylaneApiGroupService>>();
            var service = new PennylaneApiGroupService(httpClient, loggerMock.Object, DefaultOptions);

            // Act
            var (userId, alreadyExists) = await service.TryPostUsersAsync(pennylaneContact);

            // Assert
            userId.Should().BeNull();
            alreadyExists.Should().BeFalse();

            // Verify error was logged
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to create user in Pennylane")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion
    }
}
