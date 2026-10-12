using AutoFixture;
using Moq;

namespace Pennylane.Common.Tests;

public class BaseTests
{
    protected BaseMocker Mock => new();

    protected Fixture Fixture => new();

    protected Mock<HttpMessageHandler> HttpMessageHandler => Mock.Initialize<HttpMessageHandler>();
}