using Pennylane.Application.Exceptions;
using Xunit;

namespace Pennylane.Application.Tests.Exceptions;

public class HeaderValidationExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_ShouldSetMessage()
    {
        // Arrange
        var expectedMessage = "Header invalide";

        // Act
        var exception = new HeaderValidationException(expectedMessage);

        // Assert
        Assert.Equal(expectedMessage, exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_ShouldSetBoth()
    {
        // Arrange
        var expectedMessage = "Header invalide";
        var innerException = new InvalidOperationException("Erreur interne");

        // Act
        var exception = new HeaderValidationException(expectedMessage, innerException);

        // Assert
        Assert.Equal(expectedMessage, exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void Constructor_Default_ShouldSetDefaultMessage()
    {
        // Act
        var exception = new HeaderValidationException();

        // Assert
        Assert.Equal("Le fichier CSV ne contient pas de ligne d'en-tête valide.", exception.Message);
        Assert.Null(exception.InnerException);
    }
}
