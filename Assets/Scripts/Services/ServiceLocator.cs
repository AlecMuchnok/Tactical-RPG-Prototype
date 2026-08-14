using System;
using System.Collections.Generic;

/// <summary>
/// The one explicit registry in this project. Keyed by concrete type, so a
/// consumer only ever gets the ONE service it asked for.
/// </summary>
public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

    public static void Register<T>(T service) where T : class => Services[typeof(T)] = service;

    public static void Unregister<T>() where T : class => Services.Remove(typeof(T));

    public static T Get<T>() where T : class {
        if (Services.TryGetValue(typeof(T), out object service)) { return (T)service; }
        throw new InvalidOperationException(
            $"{typeof(T).Name} is not registered yet. Fetch it in Start, not Awake — see the ordering rule in architecture.md §6.");
    }
}
