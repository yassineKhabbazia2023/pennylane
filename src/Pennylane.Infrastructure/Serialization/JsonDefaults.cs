// <copyright file="JsonDefaults.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Pulse.Back.Events.Abstractions;

namespace Pennylane.Infrastructure.Serialization;

/// <summary>
/// Provides the shared <see cref="JsonSerializerOptions"/> used across the solution when
/// deserializing integration event payloads.
/// </summary>
public static class JsonDefaults
{
    /// <summary>
    /// Gets the shared serializer options.
    /// </summary>
    /// <remarks>
    /// Based on <see cref="JsonSerializerDefaults.Web"/> so that property matching stays
    /// case-insensitive (the behaviour Newtonsoft.Json provided by default) and numbers can be
    /// read from strings. <see cref="JsonStringEnumConverter"/> allows enums to be read either as
    /// their string name or their numeric value, and <see cref="UseParameterlessConstruction"/>
    /// keeps the <see cref="BaseEvent{TEventData}"/> hierarchy deserializable.
    /// </remarks>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { UseParameterlessConstruction },
        },
    };

    /// <summary>
    /// Deserializes <paramref name="json"/>, returning <c>null</c> when the payload is empty or
    /// whitespace. A syntactically invalid payload, or one missing a <c>required</c> property,
    /// propagates as a <see cref="JsonException"/> so callers (or their message-bus middleware)
    /// can decide how to handle it (retry, dead-letter, log-and-rethrow, etc.).
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="json">The JSON payload.</param>
    /// <returns>The deserialized value, or <c>null</c> when the payload is empty or whitespace.</returns>
    /// <exception cref="JsonException">The payload is malformed or missing a required property.</exception>
    public static T? TryDeserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
           throw;
        }
    }

    /// <summary>
    /// Integration events inherit from <see cref="BaseEvent{TEventData}"/> and only expose a
    /// constructor whose <c>eventData</c> parameter has no matching property, which
    /// System.Text.Json refuses to bind. Supplying a parameterless factory makes the serializer
    /// build the instance first and then populate <c>Data</c> through its public setter.
    /// </summary>
    /// <param name="typeInfo">The contract being built.</param>
    private static void UseParameterlessConstruction(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object || !DerivesFromBaseEvent(typeInfo.Type))
        {
            return;
        }

        var constructor = typeInfo.Type
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(candidate => candidate.GetParameters().Length)
            .FirstOrDefault();

        if (constructor is null)
        {
            return;
        }

        var arguments = constructor.GetParameters()
            .Select(parameter => parameter.HasDefaultValue
                ? parameter.DefaultValue
                : parameter.ParameterType.IsValueType
                    ? Activator.CreateInstance(parameter.ParameterType)
                    : null)
            .ToArray();

        var type = typeInfo.Type;

        typeInfo.CreateObject = () =>
        {
            try
            {
                return constructor.Invoke(arguments);
            }
            catch (TargetInvocationException)
            {
                return RuntimeHelpers.GetUninitializedObject(type);
            }
        };
    }

    private static bool DerivesFromBaseEvent(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(BaseEvent<>))
            {
                return true;
            }
        }

        return false;
    }
}
