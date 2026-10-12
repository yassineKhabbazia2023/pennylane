using Moq;

namespace Pennylane.Common.Tests;

public class BaseMocker
{
    public Mock<T> Initialize<T>() where T : class
    {
        return new Mock<T>();
    }

    public T MakeFake<T>() where T : class
    {
        return new Mock<T>().Object;
    }
}