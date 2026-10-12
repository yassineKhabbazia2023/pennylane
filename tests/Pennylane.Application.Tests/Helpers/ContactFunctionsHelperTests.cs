// <copyright file="ContactFunctionsHelperTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using FluentAssertions;
using Pennylane.Application.Consts;
using Pennylane.Application.Helpers;
using Pennylane.WebApi.Models;
using Xunit;

namespace Pennylane.Application.Tests.Helpers
{
    public class ContactFunctionsHelperTests
    {
        #region GetAccountingContacts Tests

        [Fact]
        public void GetAccountingContacts_ShouldReturnEmptyObject_WhenContactFunctionsIsNull()
        {
            // Arrange
            IReadOnlyList<ContactFunctions>? contactFunctions = null;

            // Act
            var result = ContactFunctionsHelper.GetAccountingContacts(contactFunctions!);

            // Assert
            result.Should().NotBeNull();
            result.AccountingManagerId.Should().Be(0);
            result.AccountingSupervisorId.Should().Be(0);
            result.AccountantId.Should().Be(0);
        }

        [Fact]
        public void GetAccountingContacts_ShouldReturnEmptyObject_WhenContactFunctionsIsEmpty()
        {
            // Arrange
            var contactFunctions = new List<ContactFunctions>();

            // Act
            var result = ContactFunctionsHelper.GetAccountingContacts(contactFunctions);

            // Assert
            result.Should().NotBeNull();
            result.AccountingManagerId.Should().Be(0);
            result.AccountingSupervisorId.Should().Be(0);
            result.AccountantId.Should().Be(0);
        }

        [Fact]
        public void GetAccountingContacts_ShouldExtractAccountingManager_WhenPresent()
        {
            // Arrange
            var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 100, FunctionId = PennylaneFunctions.AccountingManagerFunctionId }
            };

            // Act
            var result = ContactFunctionsHelper.GetAccountingContacts(contactFunctions);

            // Assert
            result.AccountingManagerId.Should().Be(100);
            result.AccountingSupervisorId.Should().Be(0);
            result.AccountantId.Should().Be(0);
        }

        [Fact]
        public void GetAccountingContacts_ShouldExtractAccountingSupervisor_WhenPresent()
        {
            // Arrange
            var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 200, FunctionId = PennylaneFunctions.AccountingSupervisorFunctionId }
            };

            // Act
            var result = ContactFunctionsHelper.GetAccountingContacts(contactFunctions);

            // Assert
            result.AccountingManagerId.Should().Be(0);
            result.AccountingSupervisorId.Should().Be(200);
            result.AccountantId.Should().Be(0);
        }

        [Fact]
        public void GetAccountingContacts_ShouldExtractAccountant_WhenPresent()
        {
            // Arrange
            var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 300, FunctionId = PennylaneFunctions.AccountantFunctionId }
            };

            // Act
            var result = ContactFunctionsHelper.GetAccountingContacts(contactFunctions);

            // Assert
            result.AccountingManagerId.Should().Be(0);
            result.AccountingSupervisorId.Should().Be(0);
            result.AccountantId.Should().Be(300);
        }

        [Fact]
        public void GetAccountingContacts_ShouldExtractAllThreeRoles_WhenAllPresent()
        {
            // Arrange
            var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 100, FunctionId = PennylaneFunctions.AccountingManagerFunctionId },
                new() { ContactId = 200, FunctionId = PennylaneFunctions.AccountingSupervisorFunctionId },
                new() { ContactId = 300, FunctionId = PennylaneFunctions.AccountantFunctionId }
            };

            // Act
            var result = ContactFunctionsHelper.GetAccountingContacts(contactFunctions);

            // Assert
            result.AccountingManagerId.Should().Be(100);
            result.AccountingSupervisorId.Should().Be(200);
            result.AccountantId.Should().Be(300);
        }

        [Fact]
        public void GetAccountingContacts_ShouldUseFirstOccurrence_WhenDuplicateFunctionIds()
        {
            // Arrange - multiple contacts with same function ID
            var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 100, FunctionId = PennylaneFunctions.AccountingManagerFunctionId },
                new() { ContactId = 101, FunctionId = PennylaneFunctions.AccountingManagerFunctionId }, // Duplicate - should be ignored
                new() { ContactId = 200, FunctionId = PennylaneFunctions.AccountingSupervisorFunctionId }
            };

            // Act
            var result = ContactFunctionsHelper.GetAccountingContacts(contactFunctions);

            // Assert - should use first occurrence (100), not second (101)
            result.AccountingManagerId.Should().Be(100);
            result.AccountingSupervisorId.Should().Be(200);
            result.AccountantId.Should().Be(0);
        }

        [Fact]
        public void GetAccountingContacts_ShouldIgnoreUnknownFunctionIds()
        {
            // Arrange - unknown function ID (999)
            var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 100, FunctionId = 999 }, // Unknown function
                new() { ContactId = 200, FunctionId = PennylaneFunctions.AccountingManagerFunctionId }
            };

            // Act
            var result = ContactFunctionsHelper.GetAccountingContacts(contactFunctions);

            // Assert
            result.AccountingManagerId.Should().Be(200);
            result.AccountingSupervisorId.Should().Be(0);
            result.AccountantId.Should().Be(0);
        }

        [Fact]
        public void GetAccountingContacts_ShouldHandleMixedOrder()
        {
            // Arrange - functions not in order (3, 1, 2)
            var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 300, FunctionId = PennylaneFunctions.AccountantFunctionId },
                new() { ContactId = 100, FunctionId = PennylaneFunctions.AccountingManagerFunctionId },
                new() { ContactId = 200, FunctionId = PennylaneFunctions.AccountingSupervisorFunctionId }
            };

            // Act
            var result = ContactFunctionsHelper.GetAccountingContacts(contactFunctions);

            // Assert - should extract all correctly regardless of order
            result.AccountingManagerId.Should().Be(100);
            result.AccountingSupervisorId.Should().Be(200);
            result.AccountantId.Should().Be(300);
        }

        [Theory]
        [InlineData(PennylaneFunctions.AccountingManagerFunctionId)]
        [InlineData(PennylaneFunctions.AccountingSupervisorFunctionId)]
        [InlineData(PennylaneFunctions.AccountantFunctionId)]
        public void GetAccountingContacts_ShouldHandleSingleFunction(int functionId)
        {
            // Arrange
            var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 123, FunctionId = functionId }
            };

            // Act
            var result = ContactFunctionsHelper.GetAccountingContacts(contactFunctions);

            // Assert
            if (functionId == PennylaneFunctions.AccountingManagerFunctionId)
            {
                result.AccountingManagerId.Should().Be(123);
            }
            else if (functionId == PennylaneFunctions.AccountingSupervisorFunctionId)
            {
                result.AccountingSupervisorId.Should().Be(123);
            }
            else if (functionId == PennylaneFunctions.AccountantFunctionId)
            {
                result.AccountantId.Should().Be(123);
            }
        }

        [Fact]
        public void GetAccountingContacts_ShouldHandleOnlyUnknownFunctions()
        {
            // Arrange - only unknown function IDs
            var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 100, FunctionId = 0 },
                new() { ContactId = 200, FunctionId = 99 },
                new() { ContactId = 300, FunctionId = -1 }
            };

            // Act
            var result = ContactFunctionsHelper.GetAccountingContacts(contactFunctions);

            // Assert - all should be 0
            result.AccountingManagerId.Should().Be(0);
            result.AccountingSupervisorId.Should().Be(0);
            result.AccountantId.Should().Be(0);
        }

        #endregion

        #region PennylaneFunctions Constants Tests

        [Fact]
        public void PennylaneFunctions_AccountingManagerFunctionId_ShouldBe1()
        {
            PennylaneFunctions.AccountingManagerFunctionId.Should().Be(1);
        }

        [Fact]
        public void PennylaneFunctions_AccountingSupervisorFunctionId_ShouldBe2()
        {
            PennylaneFunctions.AccountingSupervisorFunctionId.Should().Be(2);
        }

        [Fact]
        public void PennylaneFunctions_AccountantFunctionId_ShouldBe3()
        {
            PennylaneFunctions.AccountantFunctionId.Should().Be(3);
        }

        #endregion
    }
}
