using Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Consts;
using Pennylane.Application.Consts.Enums;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Extensions;
using Pennylane.Application.Helpers;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Mappers;
using Pennylane.Application.Models.Signatory;
using Pennylane.Application.Requests;
using Pennylane.Domain.Entities.Pennylane;
using Pulse.ExceptionMiddleware.Exceptions;
using System.Globalization;
using InvalidOperationException = Pulse.ExceptionMiddleware.Exceptions.InvalidOperationException;

namespace Pennylane.Application.services;

public class SignatoryAccessesImportService(
    ISignatoryAccessesImportRepository importRepository,
    IAccountRepository accountRepository,
    IComplianceEmailService complianceEmailService,
    ILogger<SignatoryAccessesImportService> logger,
    IBlobStorageManager blobStorageManager,
    IContactRepository contactRepository,
    IOptions<FreeTrialInfoOptions> options,
    IHistoryEventPublisher historyEventPublisher,
    IRoleRepository roleRepository) : ISignatoryAccessesImportService
{
    private readonly ISignatoryAccessesImportRepository _importRepository = importRepository;
    private readonly IAccountRepository _accountRepository = accountRepository;
    private readonly IComplianceEmailService _complianceEmailService = complianceEmailService;
    private readonly ILogger<SignatoryAccessesImportService> _logger = logger;
    private readonly IBlobStorageManager _blobStorageManager = blobStorageManager;
    private readonly IContactRepository _contactRepository = contactRepository;
    private readonly List<DateOnly> _allowedDates = options.Value.AllowedFreeTrialEndDates.Select(d => DateOnly.ParseExact(d, GlobalConstants.DateFormat, CultureInfo.InvariantCulture)).ToList();
    private readonly FreeTrialInfoOptions _freeTrialOptions = options.Value;
    private readonly IHistoryEventPublisher _historyEventPublisher = historyEventPublisher;
    private readonly IRoleRepository _roleRepository = roleRepository;

    private const int DEFAULT_LINE_NUMBER = 0;
    private const int DUE_DATE_INDEX = 1;

    public async Task<SignatoryAccessImportResponse> ImportAsync(IFormFile file)
    {
        var result = new SignatoryAccessImportResponse();

        if (file is null || file.Length == 0)
        {
            _logger.LogWarning("[SignatoryAccessImportService] Tentative d'import avec un fichier vide");
            result.AddError(DEFAULT_LINE_NUMBER, "Le fichier est vide.", null, null);
            return result;
        }

        _logger.LogInformation("[SignatoryAccessImportService] Début de l'import du fichier {FileName}", file.FileName);

        List<SignatoryAccessImportRow> rows;
        List<SignatoryAccessEntity> existingEntities;

        try
        {
            // Parse avec validation stricte du format (header et nombre de colonnes)
            rows = await SignatoryAccessImportHelper.ParseCsvFile(file);

            result.TotalRows = rows.Count;
        }
        catch (HeaderValidationException ex)
        {
            _logger.LogError(ex, "[SignatoryAccessImportService] Validation du header échouée");
            var errorMessage = ex.Message.Contains("was not found")
                ? "Le fichier CSV doit commencer par une ligne de header avec les colonnes suivantes : Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix HT de la mise en conformité;Date de fin de gratuité"
                : $"Format CSV invalide: {ex.Message}";

            result.AddError(DEFAULT_LINE_NUMBER, errorMessage, null, null);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SignatoryAccessImportService] Erreur lors du parsing du fichier");
            result.AddError(DEFAULT_LINE_NUMBER, $"Erreur lors de la lecture du fichier: {ex.Message}", null, null);
            return result;
        }

        try
        {
            existingEntities = await _importRepository.GetAllSignatoryAccessAsync();
        }
        catch (Exception ex)
        {
            result.AddError(DEFAULT_LINE_NUMBER, $"Erreur lors de la récupération des données existantes: {ex.Message}", null, null);
            return result;
        }

        // --- PREMIÈRE PASSE : VALIDATION COMPLÈTE (BLOQUANTE) ---
        var processedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingByKey = existingEntities.ToDictionary(
            e => e.AccountNumber ?? string.Empty,
            e => e,
            StringComparer.OrdinalIgnoreCase);

        await ValidateFile(rows, processedKeys, result, existingByKey);

        // Si au moins une erreur : on bloque TOUT le fichier
        if (result.Errors.Count > 0)
        {
            _logger.LogWarning("[SignatoryAccessImportService] Import rejeté : {ErrorCount} erreurs détectées. Aucune modification en base.", result.Errors.Count);
            result.SuccessCount = 0;
            return result;
        }

        // --- DEUXIÈME PASSE : TRAITEMENT ---
        await ProcessFileTreatment(rows, processedKeys, result, existingByKey);

        return result;
    }

    private async Task ValidateFile(IEnumerable<SignatoryAccessImportRow> rows, HashSet<string> processedKeys, SignatoryAccessImportResponse result, Dictionary<string, SignatoryAccessEntity> existingByKey)
    {
        int currentLine = 1; // Header

        foreach (var rowDto in rows)
        {
            currentLine++;

            existingByKey.TryGetValue(rowDto.AccountNumber ?? string.Empty, out var existingEntity);

            if (existingEntity?.CanAccess == true)
            {
                continue;
            }

            // 1. Validation intrinsèque (AccountNumber, Email format, prix négatifs)
            var rowErrors = SignatoryAccessImportHelper.ValidateRow(rowDto, currentLine, _allowedDates);
            if (rowErrors.Count > 0)
            {
                result.Errors.AddRange(rowErrors);
            }

            if (!string.IsNullOrWhiteSpace(rowDto.AccountNumber))
            {
                // 2. Validation doublon dans le fichier
                if (!processedKeys.Add(rowDto.AccountNumber))
                {
                    result.AddError(
                        currentLine,
                        "Code dossier dupliqué dans le fichier. Un seul enregistrement par dossier est autorisé.",
                        "Code dossier",
                        rowDto);
                }

                // 3. Vérification de l'existance du Role
                await CheckExistingRole(rowDto.Email, rowDto.AccountNumber, result, currentLine, rowDto);
            }
        }

        SignatoryAccessImportHelper.ValidateEmailFreeTrialConsistency(rows, result);
        SignatoryAccessImportHelper.ValidateWaveConsistency(rows, result);
    }

    private async Task ProcessFileTreatment(IEnumerable<SignatoryAccessImportRow> rows, HashSet<string> processedKeys, SignatoryAccessImportResponse result, Dictionary<string, SignatoryAccessEntity> existingByKey)
    {
        var importedAtUtc = DateTime.UtcNow;

        try
        {
            var existingEntities = existingByKey.Values;
            processedKeys.Clear();
            int rowNumber = 1;

            var addedRows = new List<SignatoryAccessImportRow>();

            foreach (var rowDto in rows)
            {
                rowNumber++;
                var wasProcessed = await ProcessRowAsync(rowDto, rowNumber, existingByKey, processedKeys, importedAtUtc, addedRows);
                if (wasProcessed)
                {
                    result.SuccessCount++;
                }
            }

            // Règle 1 : Lignes en base mais absentes du fichier
            // CanAccess null = pas répondu → supprimer
            // CanAccess true/false = a répondu (accepté/refusé) → conserver
            var accountNumbersInFile = new HashSet<string>(
                rows.Select(r => r.AccountNumber ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

            var missingFromFile = existingEntities
                .Where(e => !accountNumbersInFile.Contains(e.AccountNumber))
                .ToList();

            var accountNumbersToDelete = missingFromFile
                .Where(m => m.CanAccess is null)
                .Select(m => m.AccountNumber)
                .ToList();

            // Sauvegarde des inserts/updates
            if (result.SuccessCount > 0)
            {
                _logger.LogInformation("[SignatoryAccessImportService] Sauvegarde de {SuccessCount} modifications.", result.SuccessCount);
                await _importRepository.SaveChangesAsync();

                foreach (var row in addedRows)
                {
                    await _historyEventPublisher.PublishHistoryCreatedEventAsync(row);
                }
            }

            // Suppression batch via ExecuteDeleteAsync (exécution immédiate)
            if (accountNumbersToDelete.Count > 0)
            {
                _logger.LogInformation("[SignatoryAccessImportService] Suppression de {DeletedCount} lignes sans réponse.", accountNumbersToDelete.Count);
                await _importRepository.DeleteSignatoryAccessesAsync(accountNumbersToDelete);
                result.DeletedCount = accountNumbersToDelete.Count;

                var deletedEntities = missingFromFile.Where(r => accountNumbersToDelete.Contains(r.AccountNumber));

                foreach (var rowToDelete in deletedEntities)
                {
                    await _historyEventPublisher.PublishHistoryDeletedEventAsync(rowToDelete);
                }
            }

            // Log final avec statistiques
            _logger.LogInformation(
                "[SignatoryAccessImportService] Import terminé - Total: {Total}, Succès: {Success}",
                result.TotalRows, result.SuccessCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SignatoryAccessImportService] Erreur générale lors de la phase de traitement");
            result.AddError(DEFAULT_LINE_NUMBER, $"Erreur lors de l'import : {ex.Message}", null, null);
            result.SuccessCount = 0;
            result.DeletedCount = 0;
        }
    }

    private async Task<bool> ProcessRowAsync(
    SignatoryAccessImportRow rowDto,
    int rowNumber,
    Dictionary<string, SignatoryAccessEntity> existingByKey,
    HashSet<string> processedKeys,
    DateTime importedAtUtc,
    List<SignatoryAccessImportRow> addedRows)
    {
        var accountNumber = rowDto.AccountNumber ?? string.Empty;

        // Note: La validation (Check si vide, email format, doublon) a déjà été faite dans la première passe.
        processedKeys.Add(accountNumber);

        // Vérifier si l'entité existe
        bool entityExists = existingByKey.TryGetValue(accountNumber, out var existingEntity);

        // Helper pour vérifier si une valeur est vide ou NULL
        bool IsEmpty(string? v) =>
            string.IsNullOrWhiteSpace(v) || v.Trim().Equals("NULL", StringComparison.OrdinalIgnoreCase);

        var emailIsEmpty = IsEmpty(rowDto.Email);

        // Règle "Email vide sur entité existante → forcer CanAccess=true"
        if (entityExists && emailIsEmpty)
        {
            _logger.LogInformation(
                "[SignatoryAccessImportService] Ligne {RowNumber} - Email vide pour le dossier existant {AccountNumber}, forçage CanAccess=true",
                rowNumber, accountNumber);

            // Ne modifier que si CanAccess est actuellement false
            if (existingEntity!.CanAccess != true)
            {
                existingEntity.Email = null; // On peut choisir de vider l'email
                existingEntity.CanAccess = true;
                existingEntity.UpdatedDate = importedAtUtc;
                var termsId = await ResolveTermsIdAsync(rowDto.FreeTrialEndDate, existingEntity.CanAccess, rowDto.Wave);
                existingEntity.TermsId = termsId;
                _importRepository.UpdateSignatoryAccess(existingEntity);
                return true;
            }

            // Déjà à true, rien à faire
            return false;
        }

        //   Email vide sur nouvelle entité → ignorer la ligne
        if (!entityExists && emailIsEmpty)
        {
            _logger.LogInformation(
                "[SignatoryAccessImportService] Ligne {RowNumber} - Nouveau dossier {AccountNumber} avec email vide, ligne ignorée",
                rowNumber, accountNumber);
            // Pas d'erreur, juste skip silencieux selon la spec
            return false;
        }

        if (entityExists)
        {
            return await UpdateExistingEntityAsync(existingEntity!, rowDto, rowNumber, importedAtUtc);
        }
        else
        {
            await CreateNewEntityAsync(rowDto, rowNumber, existingByKey);
            addedRows.Add(rowDto);

            return true;
        }
    }

    private async Task<bool> UpdateExistingEntityAsync(
        SignatoryAccessEntity existing,
        SignatoryAccessImportRow rowDto,
        int rowNumber,
        DateTime importedAtUtc)
    {
        var accountNumber = existing.AccountNumber;

        //   Règle "Freeze" - Ne jamais modifier si CanAccess=true
        if (existing.CanAccess == true)
        {
            _logger.LogInformation(
                "[SignatoryAccessImportService] Ligne {RowNumber} - Dossier {AccountNumber} ignoré (CanAccess=true, freeze activé)",
                rowNumber, accountNumber);
            return false;
        }

        // Vérifier s'il y a des changements avant de faire l'update
        if (!SignatoryAccessImportHelper.HasChanges(existing, rowDto))
        {
            _logger.LogDebug(
                "[SignatoryAccessImportService] Ligne {RowNumber} - Dossier {AccountNumber} ignoré (aucune modification)",
                rowNumber, accountNumber);
            return false;
        }

        _logger.LogInformation(
            "[SignatoryAccessImportService] Ligne {RowNumber} - Mise à jour du dossier {AccountNumber}",
            rowNumber, accountNumber);

        //   MODIFICATION 4 : S'assurer que UpdatedDate est bien mis à jour
        var termsId = await ResolveTermsIdAsync(rowDto.FreeTrialEndDate, existing.CanAccess, rowDto.Wave);
        var oldEntity = new SignatoryAccessEntity
        {
            AccountNumber = existing.AccountNumber,
            Email = existing.Email,
            LastName = existing.LastName,
            FirstName = existing.FirstName,
            PlanName = existing.PlanName,
            UserCount = existing.UserCount,
            PlanPriceExclTax = existing.PlanPriceExclTax,
            CompliancePriceExclTax = existing.CompliancePriceExclTax,
            CanAccess = existing.CanAccess,
            CreationDate = existing.CreationDate,
            UpdatedDate = existing.UpdatedDate,
            FreeTrialEndDate = existing.FreeTrialEndDate,
            TermsId = existing.TermsId,
            Wave = existing.Wave
        };

        SignatoryAccessImportEntityMapper.ApplyUpdate(existing, rowDto, importedAtUtc, termsId);
        _importRepository.UpdateSignatoryAccess(existing);

        await _historyEventPublisher.PublishHistoryUpdatedEventAsync(oldEntity, rowDto);

        return true;
    }

    private async Task CreateNewEntityAsync(
        SignatoryAccessImportRow rowDto,
        int rowNumber,
        Dictionary<string, SignatoryAccessEntity> existingByKey)
    {
        var accountNumber = rowDto.AccountNumber ?? string.Empty;

        _logger.LogInformation(
            "Ligne {RowNumber} - Création du nouveau dossier {AccountNumber} avec email {Email}",
            rowNumber, accountNumber, rowDto.Email);

        var termsId = await ResolveTermsIdAsync(rowDto.FreeTrialEndDate, null, rowDto.Wave);
        var entity = SignatoryAccessImportEntityMapper.ToEntity(rowDto, termsId);
        await _importRepository.InsertSignatoryAccessAsync(entity);
        existingByKey[accountNumber] = entity;
    }

    public async Task<SignatoryAccess> CheckPennylaneAccessAsync(string currentUserEmail, string contactType, int? accountId)
    {
        if (ContactType.collaborator.ToString().Equals(contactType, StringComparison.InvariantCultureIgnoreCase))
        {
            return new SignatoryAccess
            {
                CanAccess = true,
            };
        }

        if (accountId is null)
        {
            return await HandleSignatoryAccessForPeriodsR4AndEOrFoodServiceWaveAsync(currentUserEmail);
        }

        return await HandleSignatoryAccessAsync(currentUserEmail, accountId.Value);
    }

    public async Task<IEnumerable<SignatoryValidationHistory>> ValidatePennylaneTermsAsync(string currentUserEmail, PennylaneSignatoryValidationRequest request)
    {
        var result = new List<SignatoryValidationHistory>();
        var signatoryAccesses = new List<SignatoryAccessEntity>();

        foreach (var accountNumber in request.AccountNumbers)
        {
            var signatoryAccess = await _importRepository.GetSignatoryAccessByAccountNumberAsync(accountNumber);

            if (!string.Equals(signatoryAccess.Email, currentUserEmail, StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException(Errors.UserNotSignatoryCode, Errors.UserNotSignatoryMessage);
            }

            if (signatoryAccess.CanAccess == true)
            {
                continue;
            }

            _importRepository.ValidateTerms(accountNumber, signatoryAccess.TermsId, request.Status, signatoryAccess);

            signatoryAccess.CanAccess = request.Status;
            signatoryAccess.UpdatedDate = DateTime.UtcNow;
            _importRepository.UpdateSignatoryAccess(signatoryAccess);

            signatoryAccesses.Add(signatoryAccess);

            result.Add(new SignatoryValidationHistory
            {
                AccountNumber = accountNumber,
                Status = request.Status,
                TermsId = signatoryAccess.TermsId,
                CreationDate = signatoryAccess.CreationDate,
                UpdatedDate = signatoryAccess.UpdatedDate,
                CanAccess = request.Status,
            });

            await _historyEventPublisher.PublishHistoryValidatedEventAsync(signatoryAccess, request.Status);
        }

        if (result.Count > 0)
        {

            await _importRepository.SaveChangesAsync();
            var isFreeTrialExpired = SignatoryAccessImportHelper.IsFreeTrialExpiredByDate(signatoryAccesses[0].FreeTrialEndDate?.ToString(GlobalConstants.DateFormat), _freeTrialOptions);

            if (request.Status)
            {
                await SendComplianceEmailAsync(currentUserEmail, request.AccountNumbers, signatoryAccesses, isFreeTrialExpired);
            }
            else
            {
                await SendRefusalEmailAsync(currentUserEmail, signatoryAccesses, isFreeTrialExpired);
                await SendCustomerRelationRefusalEmailAsync(currentUserEmail, request.AccountNumbers);
            }
        }

        return result;
    }

    private async Task<SignatoryAccess> HandleSignatoryAccessAsync(string currentUserEmail, int accountId)
    {
        var account = await _accountRepository.GetAccountByNumberOrIdAsync(accountId.ToString())
            ?? throw new NotFoundException(Errors.AccountNotFoundCode, string.Format(Errors.AccountNotFoundMessage, accountId));

        try
        {
            var currentEntity = await _importRepository.GetSignatoryAccessByAccountNumberAsync(account.AccountNumber);
            var userAccessList = await _importRepository.GetSignatoryAccessesByUserEmailAsync(currentUserEmail);

            var signatoryAccess = SignatoryAccessImportEntityMapper.MapToSignatoryAccessModel(currentEntity, userAccessList, currentUserEmail);
            signatoryAccess.IsFreeTrialExpired = SignatoryAccessImportHelper.IsFreeTrialExpired(currentEntity.FreeTrialEndDate?.ToString(GlobalConstants.DateFormat), currentEntity.CanAccess, _freeTrialOptions, signatoryAccess.Wave);

            return signatoryAccess;
        }
        catch (NotFoundException)
        {
            return new SignatoryAccess
            {
                CanAccess = true,
            };
        }
    }

    private async Task<SignatoryAccess> HandleSignatoryAccessForPeriodsR4AndEOrFoodServiceWaveAsync(string currentUserEmail)
    {
        var dueDate = _allowedDates[DUE_DATE_INDEX];
        var userAccessList = (await _importRepository.GetSignatoryAccessesByUserEmailAsync(currentUserEmail))
            .Where(s => s.Item1.CanAccess != true
                && (s.Item1.FreeTrialEndDate.HasValue && dueDate.Equals(DateOnly.FromDateTime(s.Item1.FreeTrialEndDate.Value)) || !string.IsNullOrWhiteSpace(s.Item1.Wave) && SignatoryAccessImportHelper.IsValideWave(s.Item1.Wave)));
        var signatoryAccess = new SignatoryAccess
        {
            CanAccess = true,
        };

        if (userAccessList.Any())
        {
            signatoryAccess = SignatoryAccessImportEntityMapper.MapToSignatoryAccessModel(userAccessList.First().Item1, userAccessList, currentUserEmail, true);
        }
        else
        {
            var roles = await _roleRepository.GetUserRolesAsync(currentUserEmail);
            var signatoryAccesses = (await _importRepository.GetSignatoryAccessesByAccountNumbersAsync(roles.Select(r => r.Account.AccountNumber)))
                .Where(s => s.Item1.CanAccess != true
                    && (s.Item1.FreeTrialEndDate.HasValue && dueDate.Equals(DateOnly.FromDateTime(s.Item1.FreeTrialEndDate.Value)) || !string.IsNullOrWhiteSpace(s.Item1.Wave) && SignatoryAccessImportHelper.IsValideWave(s.Item1.Wave)));

            if (signatoryAccesses.Any())
            {
                signatoryAccess = SignatoryAccessImportEntityMapper.MapToSignatoryAccessModel(signatoryAccesses.First().Item1, signatoryAccesses, currentUserEmail, true);
            }
        }

        return signatoryAccess;
    }

    private async Task SendComplianceEmailAsync(
        string userEmail,
        IEnumerable<string> accountNumbers,
        List<SignatoryAccessEntity> signatoryAccesses,
        bool? isFreeTrialExpired)
    {
        var accounts = await _accountRepository.GetAccountsByNumbersAsync(accountNumbers);
        var accountsDict = accounts.ToDictionary(a => a.AccountNumber, StringComparer.OrdinalIgnoreCase);

        var firstSignatory = signatoryAccesses.First();

        var companies = signatoryAccesses.Select(sa => new ComplianceCompanyInfo
        {
            AccountNumber = sa.AccountNumber,
            Name = accountsDict.TryGetValue(sa.AccountNumber, out var acc)
                ? acc.LegalName ?? sa.AccountNumber
                : sa.AccountNumber,
            CompliancePriceExclTax = sa.CompliancePriceExclTax,
            SubscriptionPriceExclTax = sa.PlanPriceExclTax
        }).ToList();

        var context = new ComplianceEmailContext
        {
            RecipientEmail = userEmail,
            UserFirstName = firstSignatory.FirstName,
            UserLastName = firstSignatory.LastName,
            Companies = companies,
            FreeTrialEndDate = firstSignatory.FreeTrialEndDate.ToFrenchLongDate(),
            IsNewComplianceFlow = IsNewComplianceFlow(firstSignatory),
            IsFreeTrialExpired = isFreeTrialExpired,
            Wave = firstSignatory.Wave
        };

        await _complianceEmailService.SendAcceptanceEmailAsync(context, firstSignatory.Terms);
    }

    private async Task SendRefusalEmailAsync(string userEmail, List<SignatoryAccessEntity> signatoryAccesses, bool? isFreeTrialExpired)
    {
        var firstSignatory = signatoryAccesses.First();

        var context = new ComplianceRefusalContext
        {
            RecipientEmail = userEmail,
            UserFirstName = firstSignatory.FirstName,
            UserLastName = firstSignatory.LastName,
            FreeTrialEndDate = firstSignatory.FreeTrialEndDate.ToFrenchLongDate(),
            IsNewComplianceFlow = IsNewComplianceFlow(firstSignatory),
            IsFreeTrialExpired = isFreeTrialExpired,
            Wave = firstSignatory.Wave
        };

        await _complianceEmailService.SendRefusalEmailAsync(context);
    }

    private bool IsNewComplianceFlow(SignatoryAccessEntity signatory)
    {
        return signatory.FreeTrialEndDate == null ? false :
            _allowedDates[DUE_DATE_INDEX].Equals(DateOnly.FromDateTime(signatory.FreeTrialEndDate.Value));
    }


    private async Task SendCustomerRelationRefusalEmailAsync(string userEmail, IEnumerable<string> accountNumbers)
    {
        var contact = await _contactRepository.GetContactByEmailOrIdAsync(userEmail);
        var context = new CustomerRelationRefusalContext
        {
            UserEmail = userEmail,
            UserFirstName = contact?.FirstName ?? string.Empty,
            UserLastName = contact?.LastName ?? string.Empty,
            AccountNumbers = accountNumbers,
        };

        await _complianceEmailService.SendCustomerRelationRefusalEmailAsync(context);
    }

    public async Task<FileStreamResult> GetLatestTermsContentAsync(string currentUserEmail)
    {
        var signatoryAccesses = await _importRepository.GetSignatoryAccessesByUserEmailAsync(currentUserEmail);

        if (signatoryAccesses is null || !signatoryAccesses.Any())
        {
            throw new NotFoundException(Errors.SignatoryAccessNotFoundWithEmailCode, string.Format(Errors.SignatoryAccessNotFoundWithEmailMessage, currentUserEmail));
        }

        var signatoryAccess = signatoryAccesses.FirstOrDefault()!.Item1;

        var blobContent = await _blobStorageManager.GetFileAsync(signatoryAccess.Terms.LinkFr);

        return blobContent ?? throw new NotFoundException(Errors.CGUFileNotFoundCode, string.Format(Errors.CGUFileNotFoundMessage, signatoryAccess.Terms.LinkFr));
    }

    /// <summary>
    /// Résout le TermsId à partir de la date de fin de gratuité ou de la wave.
    /// </summary>
    /// <param name="freeTrialEndDate">Date au format DD/MM/YYYY.</param>
    /// <param name="canAccess">Statut d'accès du signataire.</param>
    /// <param name="wave">Vague de déploiement du dossier.</param>
    /// <returns>ID du Terme applicable.</returns>
    /// <exception cref="InvalidOperationException">Si ni la date ni la wave ne sont renseignées, ou si la valeur n'est pas trouvée dans la configuration.</exception>
    private async Task<int> ResolveTermsIdAsync(string? freeTrialEndDate, bool? canAccess, string? wave)
    {
        var parsedDate = freeTrialEndDate?.Replace("/", string.Empty);

        if (string.IsNullOrWhiteSpace(parsedDate) && string.IsNullOrWhiteSpace(wave))
        {
            throw new InvalidOperationException(Errors.InvalidTermsCode, string.Format(Errors.InvalidTermsMessage, freeTrialEndDate));
        }

        string? termName;

        if (!string.IsNullOrWhiteSpace(wave))
        {
            if (!_freeTrialOptions.TermsByWave.TryGetValue(wave, out termName))
            {
                throw new InvalidOperationException(Errors.InvalidTermsCode, string.Format(Errors.InvalidTermsMessage, wave));
            }
        }
        else if (!_freeTrialOptions.TermsByDate.TryGetValue(parsedDate!, out termName))
        {
            throw new InvalidOperationException(Errors.InvalidTermsCode, string.Format(Errors.InvalidTermsMessage, freeTrialEndDate));
        }

        // A partir du 01/07/2026, tous les dossiers qui n'ont pas encore accepté le formulaire se voient afficher le nouveau fichier de CGU
        if (SignatoryAccessImportHelper.IsFreeTrialExpired(freeTrialEndDate, canAccess, _freeTrialOptions, wave) == true)
        {
            termName = _freeTrialOptions.TermsOverridesAfterDate[parsedDate!].LinkFr;
        }

        return await _importRepository.GetTermsIdByLinkFrAsync(termName);
    }

    private async Task CheckExistingRole(string? userEmail, string? accountNumber, SignatoryAccessImportResponse result, int currentLine, SignatoryAccessImportRow rowDto)
    {
        if (string.IsNullOrWhiteSpace(userEmail) || string.IsNullOrWhiteSpace(accountNumber))
        {
            return;
        }

        var roleExists = await _roleRepository.CheckRoleExistsAsync(accountNumber, userEmail);

        if (!roleExists)
        {
            result.AddError(currentLine, "L'utilisateur doit avoir un rôle sur le dossier pour être importé.", "Code dossier/Email signataire", rowDto);
        }
    }
}
