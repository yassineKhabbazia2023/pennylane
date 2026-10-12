namespace Pennylane.Domain.Entities.Pennylane;

public partial class CompanyDirectoryEntity
{
    public string CompanyId { get; set; } = null!;

    public string Siren { get; set; } = null!;

    public DateTime SnapshotDate { get; set; }
}
