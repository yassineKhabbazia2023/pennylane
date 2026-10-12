// <copyright file="PennylaneGedPermissionHandlerTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using Pennylane.Application.Interfaces;
using Pennylane.Infrastructure.Handlers.Permissions;

namespace Pennylane.Infrastructure.Tests.Handlers.Permissions;

public class PennylaneGedPermissionHandlerTests
{
    private readonly Mock<IPennylaneGedPermissionRepository> _repositoryMock;
    private readonly Mock<ILogger<PennylaneGedPermissionHandler>> _loggerMock;
    private readonly PennylaneGedPermissionHandler _handler;

    public PennylaneGedPermissionHandlerTests()
    {
        _repositoryMock = new Mock<IPennylaneGedPermissionRepository>();
        _loggerMock = new Mock<ILogger<PennylaneGedPermissionHandler>>();
        _handler = new PennylaneGedPermissionHandler(_repositoryMock.Object, _loggerMock.Object);
    }

    #region PermissionCodes Tests

    [Fact]
    public void PermissionCodes_ReturnsCorrectCodes()
    {
        // Assert
        Assert.Contains("CLGEDPEN01", _handler.PermissionCodes);
    }

    [Fact]
    public void PermissionCodes_ReturnsNonEmptyCollection()
    {
        // Assert
        Assert.NotEmpty(_handler.PermissionCodes);
    }

    #endregion

    #region HandlePermissionGrantedAsync Tests

    [Fact]
    public async Task HandlePermissionGrantedAsync_CallsRepositoryAddPermission()
    {
        // Arrange
        var permissionCode = "CLGEDPEN01";
        var contactId = 1;
        var accountId = 100;
        _repositoryMock
            .Setup(r => r.AddPermissionAsync(contactId, accountId))
            .ReturnsAsync(true);

        // Act
        await _handler.HandlePermissionGrantedAsync(permissionCode, contactId, accountId);

        // Assert
        _repositoryMock.Verify(r => r.AddPermissionAsync(contactId, accountId), Times.Once);
    }

    [Fact]
    public async Task HandlePermissionGrantedAsync_PassesCorrectParameters()
    {
        // Arrange
        var permissionCode = "CLGEDPEN01";
        var contactId = 42;
        var accountId = 999;
        int? capturedContactId = null;
        int? capturedAccountId = null;

        _repositoryMock
            .Setup(r => r.AddPermissionAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Callback<int, int>((c, a) =>
            {
                capturedContactId = c;
                capturedAccountId = a;
            })
            .ReturnsAsync(true);

        // Act
        await _handler.HandlePermissionGrantedAsync(permissionCode, contactId, accountId);

        // Assert
        Assert.Equal(contactId, capturedContactId);
        Assert.Equal(accountId, capturedAccountId);
    }

    [Fact]
    public async Task HandlePermissionGrantedAsync_DoesNotThrow_WhenRepositoryReturnsTrue()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.AddPermissionAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Record.ExceptionAsync(() => _handler.HandlePermissionGrantedAsync("CLGEDPEN01", 1, 100));
        Assert.Null(exception);
    }

    [Fact]
    public async Task HandlePermissionGrantedAsync_DoesNotThrow_WhenRepositoryReturnsFalse()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.AddPermissionAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Record.ExceptionAsync(() => _handler.HandlePermissionGrantedAsync("CLGEDPEN01", 1, 100));
        Assert.Null(exception);
    }

    [Fact]
    public async Task HandlePermissionGrantedAsync_WorksWithAnyPermissionCode()
    {
        // Arrange
        var permissionCode = "CLOTHER99";
        _repositoryMock
            .Setup(r => r.AddPermissionAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(true);

        // Act & Assert - should work with any code, not just the ones in PermissionCodes
        var exception = await Record.ExceptionAsync(() => _handler.HandlePermissionGrantedAsync(permissionCode, 1, 100));
        Assert.Null(exception);
        _repositoryMock.Verify(r => r.AddPermissionAsync(1, 100), Times.Once);
    }

    #endregion

    #region HandlePermissionRevokedAsync Tests

    [Fact]
    public async Task HandlePermissionRevokedAsync_CallsRepositoryRemovePermission()
    {
        // Arrange
        var permissionCode = "CLGEDPEN01";
        var contactId = 1;
        var accountId = 100;
        _repositoryMock
            .Setup(r => r.RemovePermissionAsync(contactId, accountId))
            .ReturnsAsync(true);

        // Act
        await _handler.HandlePermissionRevokedAsync(permissionCode, contactId, accountId);

        // Assert
        _repositoryMock.Verify(r => r.RemovePermissionAsync(contactId, accountId), Times.Once);
    }

    [Fact]
    public async Task HandlePermissionRevokedAsync_PassesCorrectParameters()
    {
        // Arrange
        var permissionCode = "CLGEDPEN01";
        var contactId = 42;
        var accountId = 999;
        int? capturedContactId = null;
        int? capturedAccountId = null;

        _repositoryMock
            .Setup(r => r.RemovePermissionAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Callback<int, int>((c, a) =>
            {
                capturedContactId = c;
                capturedAccountId = a;
            })
            .ReturnsAsync(true);

        // Act
        await _handler.HandlePermissionRevokedAsync(permissionCode, contactId, accountId);

        // Assert
        Assert.Equal(contactId, capturedContactId);
        Assert.Equal(accountId, capturedAccountId);
    }

    [Fact]
    public async Task HandlePermissionRevokedAsync_DoesNotThrow_WhenRepositoryReturnsTrue()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.RemovePermissionAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Record.ExceptionAsync(() => _handler.HandlePermissionRevokedAsync("CLGEDPEN01", 1, 100));
        Assert.Null(exception);
    }

    [Fact]
    public async Task HandlePermissionRevokedAsync_DoesNotThrow_WhenRepositoryReturnsFalse()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.RemovePermissionAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Record.ExceptionAsync(() => _handler.HandlePermissionRevokedAsync("CLGEDPEN01", 1, 100));
        Assert.Null(exception);
    }

    [Fact]
    public async Task HandlePermissionRevokedAsync_WorksWithAnyPermissionCode()
    {
        // Arrange
        var permissionCode = "CLOTHER99";
        _repositoryMock
            .Setup(r => r.RemovePermissionAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(true);

        // Act & Assert - should work with any code, not just the ones in PermissionCodes
        var exception = await Record.ExceptionAsync(() => _handler.HandlePermissionRevokedAsync(permissionCode, 1, 100));
        Assert.Null(exception);
        _repositoryMock.Verify(r => r.RemovePermissionAsync(1, 100), Times.Once);
    }

    #endregion

    #region IPermissionHandler Interface Tests

    [Fact]
    public void Handler_ImplementsIPermissionHandler()
    {
        // Assert
        Assert.IsAssignableFrom<IPermissionHandler>(_handler);
    }

    #endregion
}
