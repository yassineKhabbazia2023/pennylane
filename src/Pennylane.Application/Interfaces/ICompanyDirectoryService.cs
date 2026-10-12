namespace Pennylane.Application.Interfaces;

public interface ICompanyDirectoryService
{
    Task<int> RefreshAsync(CancellationToken cancellationToken = default);
}
