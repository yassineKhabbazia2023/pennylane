// <copyright file="AccountService.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Consts;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Pennylane.Domain.Entities;
using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Application.Services;

public class AccountService : IAccountService
{
    private readonly IAccountRepository accountRepository;
    private readonly ILogger<AccountService> logger;

    public AccountService(IAccountRepository accountRepository, ILogger<AccountService> logger)
    {
        this.accountRepository = accountRepository;
        this.logger = logger;
    }

    public async Task<string?> GetAccountNumberByIdAsync(int accountId)
    {
        var account = await accountRepository.GetAccountByNumberOrIdAsync(accountId.ToString());
        return account?.AccountNumber;
    }


    public async Task<bool> SyncAcountAsync(AccountStateEventData accountEvent, string syncType)
    {
        var account = await this.accountRepository.GetAccountByNumberOrIdAsync(accountEvent.AccountNumber);

        string[] syncTypesThatNeedsAnExistingAccount = { OperationAction.Update, OperationAction.Delete };
        if (account is null && syncTypesThatNeedsAnExistingAccount.Contains(syncType))
        {
            this.logger.LogWarning("{Instance} Unable to trigger Sync operation of type {SyncType}, the account {AccountNumber} does not exists", nameof(AccountService), syncType, accountEvent.AccountNumber);
            return false;
        }

        if (account is not null && syncType.Equals(OperationAction.Insert))
        {
            this.logger.LogWarning("{Instance} Unable to trigger Sync operation of type {SyncType}, the account {AccountNumber} already exists", nameof(AccountService), syncType, accountEvent.AccountNumber);
            return false;
        }

        switch (syncType)
        {
            case OperationAction.Insert:
                return await CreateNewAccountAsync(accountEvent);
            case OperationAction.Update:
                return await UpdateAccountAsync(accountEvent);
            case OperationAction.Delete:
                return await RemoveAccountAsync(account);
            default:
                return false;
        }
    }



    private async Task<bool> CreateNewAccountAsync(AccountStateEventData accountStateEventData)
    {
        var accountEntity = new AccountEntity()
        {
            AccountId = accountStateEventData!.AccountId,
            AccountNumber = accountStateEventData.AccountNumber,
            LegalName = accountStateEventData.LegalName,
            AccountGlobalUniqueId = accountStateEventData.AccountGlobalUniqueId,
            CreatedBy = accountStateEventData.CreatedBy,
            ModifiedBy = accountStateEventData.ModifiedBy
        };
        try
        {
            await this.accountRepository.AddAccountAsync(accountEntity);
            return true;
        }
        catch (DbOperationException ex)
        {
            this.logger.LogError("{Instance} faced issues while adding account {AccountNumber} in the AccountEntity table,{Details}", nameof(AccountService), accountEntity.AccountNumber, ex.InnerException);
            return false;
        }
    }

    private async Task<bool> UpdateAccountAsync(AccountStateEventData accountStateEventData)
    {
        var accountEntity = new AccountEntity()
        {
            AccountId = accountStateEventData!.AccountId,
            AccountNumber = accountStateEventData.AccountNumber,
            AccountGlobalUniqueId = accountStateEventData.AccountGlobalUniqueId,
            LegalName = accountStateEventData.LegalName,
            CreatedBy = accountStateEventData.CreatedBy,
            ModifiedBy = accountStateEventData.ModifiedBy
        };
        try
        {
            await this.accountRepository.UpdateAccountAsync(accountEntity);
            return true;
        }
        catch (DbOperationException ex)
        {
            this.logger.LogError("{Instance} faced issues while updating account {AccountNumber} in the AccountEntity table,{Details}", nameof(AccountService), accountEntity.AccountNumber, ex.InnerException);
            return false;
        }
    }

    private async Task<bool> RemoveAccountAsync(AccountEntity account)
    {
        try
        {
            if(account is null)
            {
                return false;
            }

            await this.accountRepository.RemoveAccountAsync(account);
            return true;
        }
        catch (DbOperationException ex)
        {
            this.logger.LogError("{Instance} faced issues while removing account {AccountNumber} in the AccountEntity table,{Details}", nameof(AccountService), account.AccountNumber, ex.InnerException);
            return false;
        }
    }
}
