using Microsoft.EntityFrameworkCore;
using Pennylane.Domain.Entities;
using Pennylane.Domain.Entities.Offers;
using Pennylane.Domain.Entities.Pennylane;
using Pennylane.Domain.Entities.Permissions;
using Pulse.ContactRegistry.Infrastructure.Context;

namespace Pennylane.Infrastructure.Tests.Utils;

public class TestContext : PennylaneContext
{
    public TestContext(DbContextOptions<PennylaneContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // On part de la config du contexte réel
        base.OnModelCreating(modelBuilder);

        // On ignore les entités infra qui ne servent pas dans ces tests
        modelBuilder.Ignore<RoleEntity>();
        modelBuilder.Ignore<SubscriptionEntity>();

        // On remappe explicitement ce qu’on veut tester

        modelBuilder.Entity<PnlRoleEntity>(entity =>
        {
            entity.ToTable("Roles", "Pennylane");

            entity.HasKey(e => new { e.ContactId, e.AccountId })
                  .HasName("C_Role_PK");

            entity.HasOne(e => e.Account)
                  .WithMany()
                  .HasForeignKey(e => e.AccountId)
                  .HasConstraintName("C_Account_Role_FK");

            entity.HasOne(e => e.Contact)
                  .WithMany()
                  .HasForeignKey(e => e.ContactId)
                  .HasConstraintName("C_Account_Contact_FK");
        });

        modelBuilder.Entity<AccountEntity>(entity =>
        {
            entity.ToTable("Accounts", "Account");

            entity.HasKey(e => e.AccountId)
                  .HasName("C_Account_PK");

            entity.Property(e => e.AccountId)
                  .IsRequired();

            entity.Property(e => e.AccountNumber)
                  .IsRequired()
                  .HasMaxLength(100)
                  .HasColumnType("VARCHAR");
        });

        modelBuilder.Entity<ContactEntity>(entity =>
        {
            entity.ToTable("Contacts", "Contact");

            entity.HasKey(e => e.ContactId)
                  .HasName("C_Contact_PK");

            entity.Property(e => e.ContactId)
                  .IsRequired();

            entity.Property(e => e.ContactGlobalUniqueId)
                  .HasColumnType("UNIQUEIDENTIFIER");

            entity.Property(e => e.Email)
                  .IsRequired()
                  .HasMaxLength(250)
                  .HasColumnType("VARCHAR");

            entity.Property(e => e.Type)
                  .IsRequired()
                  .HasMaxLength(20)
                  .HasColumnType("VARCHAR");
        });

        modelBuilder.Entity<DocumentEntity>(entity =>
        {
            entity.ToTable("Documents", "Pennylane");

            entity.HasKey(e => e.Id).HasName("PK_Documents");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Name).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Path).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
            entity.Property(e => e.Url).HasMaxLength(100).IsUnicode(false);

            entity.HasOne(d => d.Account).WithMany(p => p.DocumentEntity)
                  .HasForeignKey(d => d.AccountId)
                  .HasConstraintName("FK_Documents_Accounts");
        });

        modelBuilder.Entity<PennylaneGedPermissionEntity>(entity =>
        {
            entity.ToTable("PennylaneGedPermissions", "Pennylane");

            entity.HasKey(e => new { e.ContactId, e.AccountId })
                  .HasName("PK_PennylaneGedPermissions");

            entity.Property(e => e.GrantedAt)
                  .HasColumnType("datetime2");

            entity.HasIndex(e => e.AccountId)
                  .HasDatabaseName("IX_PennylaneGedPermissions_AccountId");
        });

        // Mapping de l’entité EF SignatoryAccess (et non SignatoryAccessEntity)
        modelBuilder.Entity<SignatoryAccessEntity>(entity =>
        {
            entity.ToTable("SignatoryAccess", "Amend");

            entity.HasKey(e => e.AccountNumber)
                  .HasName("PK_SignatoryAccess");

            entity.Property(e => e.AccountNumber)
                  .HasMaxLength(20)
                  .IsRequired();
        });
    }
}
