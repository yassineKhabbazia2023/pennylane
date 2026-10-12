namespace Pennylane.Domain;

public record Document(long Id, string Name, string Path, string Url, DateTime CreatedAt, DateTime UpdatedAt);