// <copyright file="PennylaneServiceApiTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using AutoFixture;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Models;
using Pennylane.Application.Models.Pagers;
using Pennylane.Application.services;
using Pennylane.Common.Tests;
using Pennylane.Domain.ExternalApis.CommandParams;
using Pennylane.Application.Exceptions;
using Pulse.ExceptionMiddleware.Exceptions;
using System.Net;
using FluentAssertions;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Pennylane.Infrastructure.Tests.ExternalApis;

public class TestPennylaneServiceApi() : PennylaneApiGroupService(new HttpClient(), NullLogger<TestPennylaneServiceApi>.Instance, OptionsFactory.Create(new PennylaneServiceOptions
{
    ApiUrl = "http://test",
    ApiFirmUrl = "http://test",
    ApiKey = "test",
    AuthenticationMode = "test",
    CollaboratorAuthenticationMode = "test",
    DefaultRole = "test",
    SupportedSaasPlans = "test",
    Token = "test"
}))
{
    public static string GetUserFilterUrlFoTest(string email) => GetUserFilterUrl(email);
}

public class PennylaneServiceApiTests : BaseTests
{
    private const string BaseAddress = "http://localhost/";

    private static readonly IOptions<PennylaneServiceOptions> DefaultOptions = OptionsFactory.Create(new PennylaneServiceOptions
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

    private static PennylaneApiGroupService GetPennylaneServiceApi(HttpClient httpClient) =>
        new(httpClient, NullLogger<PennylaneApiGroupService>.Instance, DefaultOptions);

    private PennylaneApiGroupService GetPennylaneServiceApi<TExpected>(TExpected expected, HttpStatusCode statusCode) =>
        new(HttpMessageHandler.MockHttpClient(BaseAddress, expected, statusCode), NullLogger<PennylaneApiGroupService>.Instance, DefaultOptions);

    private PennylaneApiGroupService GetPennylaneServiceApi<TExpected>(TExpected expected) =>
        new(HttpMessageHandler.MockHttpClient(BaseAddress, expected, HttpStatusCode.OK), NullLogger<PennylaneApiGroupService>.Instance, DefaultOptions);
    
    [Fact]
    public void GetUserFilterUrl_EncodesEmail_Successfully()
    {
        // Arrange
        var emailToEncode =
            "machin+truc@bidule.chose.com";
        var expected = """users?filter=[{"field":"email","operator":"eq","value":"machin%2btruc%40bidule.chose.com"}]""";

        // Act
        var result = TestPennylaneServiceApi.GetUserFilterUrlFoTest(emailToEncode);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetCompaniesAsync_ReturnsPager_WhenSuccessful()
    {
        // Arrange
        var expectedPager = Fixture.Create<PennylaneCompanyPager>();
        expectedPager.Companies = Fixture
            .CreateMany<PennylaneCompany>(3)
            .ToList();

        var service = GetPennylaneServiceApi(expectedPager);

        // Act
        var result = await service.GetCompaniesAsync();

        // Assert
        result.Should().BeEquivalentTo(expectedPager); 
    }

    [Fact]
    public async Task GetCompaniesAsync_ThrowsBadRequestException_WhenResponseNull()
    {
        // Arrange
        var httpClient = HttpMessageHandler.MockHttpClient<string>(BaseAddress, null, HttpStatusCode.OK);
        var service = GetPennylaneServiceApi(httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => service.GetCompaniesAsync());
    }

    [Fact]
    public async Task GetCompanyUsersAsync_ReturnsPager_WhenSuccessful()
    {
        // Arrange
        var companyId = Fixture.Create<string>();
        var expectedPager = Fixture.Create<PennylaneUsersPager>();
        expectedPager.Users = Fixture
            .CreateMany<PennylaneUsers>(2)
            .ToList();

        var service = GetPennylaneServiceApi(expectedPager);

        // Act
        var result = await service.GetCompanyUsersAsync(companyId);

        // Assert
        result.Should().BeEquivalentTo(expectedPager); 
    }

    [Fact]
    public async Task GetCompanyUsersAsync_ThrowsBadRequestException_WhenResponseNull()
    {
        // Arrange
        var companyId = Fixture.Create<string>();
        var httpClient = HttpMessageHandler.MockHttpClient<string>(BaseAddress, null, HttpStatusCode.OK);
        var service = GetPennylaneServiceApi(httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => service.GetCompanyUsersAsync(companyId));
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsPager_WhenSuccessful()
    {
        // Arrange
        var expectedPager = Fixture.Create<PennylaneUsersPager>();
        expectedPager.Users = Fixture
            .CreateMany<PennylaneUsers>(4)
            .ToList();

        var service = GetPennylaneServiceApi(expectedPager);

        // Act
        var result = await service.GetUsersAsync();

        // Assert
        result.Should().BeEquivalentTo(expectedPager); 
    }

    [Fact]
    public async Task GetUsersAsync_ThrowsBadRequestException_WhenResponseNull()
    {
        // Arrange
        var httpClient = HttpMessageHandler.MockHttpClient<string>(BaseAddress, null, HttpStatusCode.OK);
        var service = GetPennylaneServiceApi(httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => service.GetUsersAsync());
    }

    [Fact]
    public async Task GrantCompanyUserAccessAsync_ReturnsId_WhenSuccessful()
    {
        // Arrange
        var companyId = Fixture.Create<string>();
        var command = Fixture.Create<GrantUserAccess>();
        var expectedResult = new PennylaneResult<int> { Id = Fixture.Create<int>() };

        var service = GetPennylaneServiceApi(expectedResult);

        // Act
        var result = await service.GrantCompanyUserAccessAsync(companyId, command);

        // Assert
        result.Should().Be(expectedResult.Id); 
    }

    [Fact]
    public async Task GrantCompanyUserAccessAsync_ThrowsBadRequestException_WhenResponseNull()
    {
        // Arrange
        var companyId = Fixture.Create<string>();
        var command = Fixture.Create<GrantUserAccess>();

        var httpClient = HttpMessageHandler.MockHttpClient<string>(BaseAddress, null, HttpStatusCode.OK);
        var service = GetPennylaneServiceApi(httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => service.GrantCompanyUserAccessAsync(companyId, command));
    }

    [Fact]
    public async Task PostUsersAsync_ReturnsId_WhenSuccessful()
    {
        // Arrange
        var contact = Fixture.Create<PennylaneContact>();
        var expectedResult = new PennylaneResult<string> { Id = Fixture.Create<string>() };

        var service = GetPennylaneServiceApi(expectedResult);

        // Act
        var result = await service.PostUsersAsync(contact);

        // Assert
        result.Should().Be(expectedResult.Id); 
    }

    [Fact]
    public async Task PostUsersAsync_ThrowsBadRequestException_WhenResponseNull()
    {
        // Arrange
        var contact = Fixture.Create<PennylaneContact>();

        var httpClient = HttpMessageHandler.MockHttpClient<string>(BaseAddress, null, HttpStatusCode.OK);
        var service = GetPennylaneServiceApi(httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => service.PostUsersAsync(contact));
    }

    [Fact]
    public async Task RevokeAccessUserCompanyAsync_CompletesWithoutException()
    {
        // Arrange
        var companyId = Fixture.Create<string>();
        var userId = Fixture.Create<string>();

        var httpClient = HttpMessageHandler.MockHttpClient(BaseAddress, string.Empty, HttpStatusCode.OK);
        var service = GetPennylaneServiceApi(httpClient);

        // Act & Assert
        await service.RevokeAccessUserCompanyAsync(companyId, userId);
    }

    [Fact]
    public async Task GetCompanyByIdAsync_ReturnsCompany_WhenSuccessful()
    {
        // Arrange
        var companyId = Fixture.Create<string>();
        var expectedCompany = Fixture.Create<PennylaneCompany>();

        var service = GetPennylaneServiceApi(expectedCompany);

        // Act
        var result = await service.GetCompanyByIdAsync(companyId);

        // Assert
        result.Should().BeEquivalentTo(expectedCompany); 
    }

    [Fact]
    public async Task GetCompanyByIdAsync_ThrowsJsonException_WhenResponseNull()
    {
        // Arrange
        var companyId = Fixture.Create<string>();

        var httpClient = HttpMessageHandler.MockHttpClient<string>(BaseAddress, null, HttpStatusCode.OK);
        var service = GetPennylaneServiceApi(httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => service.GetCompanyByIdAsync(companyId));
    }
    
    [Fact] 
    public async Task TryGrantCompanyUserAccessAsync_ReturnsInteger_WhenSuccessful() 
    { 
        // Arrange 
        var companyId = Fixture.Create<string>(); 
        var expectedResult = new PennylaneResult<int> { Id = 1 };

        var service = GetPennylaneServiceApi(expectedResult);

        // Act
        GrantUserAccess userAccess = new GrantUserAccess{UserId = "userId", Role = "role"};
        var result = await service.TryGrantCompanyUserAccessAsync(companyId, userAccess);

        // Assert
        result.Should().Be(expectedResult.Id); 
    } 

    [Fact] 
    public async Task TryGrantCompanyUserAccessAsync_ReturnsNull_WhenFailing() 
    { 
        // Arrange 
        var companyId = Fixture.Create<string>(); 
        int? expectedResult = null;
 
        var service = GetPennylaneServiceApi(new {Error = "Not ok, sorry buddy"}, HttpStatusCode.NotFound); 
 
        // Act 
        GrantUserAccess userAccess = new GrantUserAccess{UserId = "userId", Role = "role"}; 
        var result = await service.TryGrantCompanyUserAccessAsync(companyId, userAccess); 
 
        // Assert 
        result.Should().Be(expectedResult); 
    } 

    [Fact] 
    public async Task TryGetCompanyByIdAsync_ReturnsCompany_WhenSuccessful() 
    { 
        // Arrange 
        var companyId = Fixture.Create<string>(); 
        var expectedResult = new PennylaneCompany(); 
 
        var service = GetPennylaneServiceApi(expectedResult); 
 
        // Act 
        var result = await service.TryGetCompanyByIdAsync(companyId); 
 
        // Assert 
        result.Should().BeEquivalentTo(expectedResult); 
    } 
 
    [Fact] 
    public async Task TryGetUserAsync_ReturnsUser_WhenSuccessful() 
    { 
        // Arrange 
        var companyId = Fixture.Create<string>(); 
        var expectedResult = new PennylaneUsersPager(); 
 
        var service = GetPennylaneServiceApi(expectedResult); 
 
        // Act 
        var result = await service.TryGetUserAsync(companyId); 
 
        // Assert 
        result.Should().BeEquivalentTo(expectedResult.Users.FirstOrDefault()); 
    } 
        [Fact] 
        public async Task TryGetCompanyUserAsyncAsync_ReturnsUser_WhenSuccessful() 
        { 
            // Arrange 
            var expectedResult = Fixture.Create<PennylaneUsersPager>();
     
            var service = GetPennylaneServiceApi(expectedResult); 
     
            // Act 
            var result = await service.TryGetCompanyUserAsync(expectedResult.Users[0].Email, "123456"); 
     
            // Assert 
            result.Should().BeEquivalentTo(expectedResult.Users.FirstOrDefault()); 
        } 
    [Theory]
    [InlineData(HttpStatusCode.Forbidden, typeof(ServiceAuthenticationException))]
    [InlineData(HttpStatusCode.Unauthorized, typeof(ServiceAuthenticationException))]
    [InlineData(HttpStatusCode.BadRequest, typeof(BadRequestException))]
    [InlineData(HttpStatusCode.NotFound, typeof(RequestHttpException))]
    [InlineData(HttpStatusCode.InternalServerError, typeof(RequestHttpException))]
    [InlineData(HttpStatusCode.BadGateway, typeof(RequestHttpException))]
    [InlineData(HttpStatusCode.ServiceUnavailable, typeof(RequestHttpException))]
    [InlineData(HttpStatusCode.GatewayTimeout, typeof(RequestHttpException))]
    [InlineData((HttpStatusCode)418, typeof(RequestHttpException))] // 418, cas "_" du switch
    public async Task GetCompaniesAsync_ThrowsExpectedException_OnHttpError(HttpStatusCode statusCode, Type expectedExceptionType)
    {
        // Arrange
        var httpClient = HttpMessageHandler.MockHttpClient<string>(BaseAddress, "error", statusCode);
        var service = GetPennylaneServiceApi(httpClient);

        // Act
        var act = async () => await service.GetCompaniesAsync();

        // Assert
        (await Assert.ThrowsAsync(expectedExceptionType, act)).Should().NotBeNull();
    }
}