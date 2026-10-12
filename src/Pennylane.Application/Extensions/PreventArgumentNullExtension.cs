// <copyright file="PreventArgumentNullExtension.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Pennylane.Application.Extensions;

/// <summary>
/// Extension define method to verify a null param.
/// </summary>
public static class PreventArgumentNullExtension
{
    /// <summary>
    /// Parmaters validator to test the nullable.
    /// </summary>
    /// <typeparam name="T">Generic param.</typeparam>
    /// <param name="param">Param to test the nullable.</param>
    /// <param name="paramName">Name of the parameter.</param>
    public static void ValidateParamWithException<T>(this T param, string? paramName)
    {
        if (param is null)
            throw new ArgumentNullException(paramName ?? nameof(param), "Le paramètre ne peut pas être null.");

        if (param is string s && string.IsNullOrWhiteSpace(s))
            throw new ArgumentException("Le paramètre ne peut pas être une chaîne vide ou composée uniquement d'espaces.", paramName ?? nameof(param));
    }

    /// <summary>
    /// Parmaters validator to test the nullable.
    /// </summary>
    /// <typeparam name="T">Generic param.</typeparam>
    /// <param name="param">Param to test the nullable.</param>
    /// <returns>True if param is null or invalid.</returns>
    public static bool ValidateParamWithException<T>(this T param)
    {
        var result = param != null && (param.GetType().IsPrimitive || param.GetType().Equals(typeof(string)))
                       ? string.IsNullOrWhiteSpace(param.ToString())
                       : EqualityComparer<T>.Default.Equals(param, default) && !param.GetType().IsEnum;

        if (result)
        {
            throw new ArgumentNullException($"'{param}' cannot be null or whitespace");
        }

        return result;
    }
}