using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Serialization;

namespace TalonAdmin.Auth
{
    /// <summary>
    /// Breeze needs Newtonsoft's <c>$type</c> discriminators to materialise nested entities, which
    /// means TypeNameHandling is enabled for the whole MVC pipeline — including deserialisation,
    /// where it is a well-known remote-code-execution vector: an attacker who can post JSON could
    /// otherwise name any type on the server and have it constructed.
    ///
    /// This binder closes that off by resolving only types declared in this assembly. Anything
    /// else throws instead of being instantiated. Serialisation is unaffected.
    /// </summary>
    public class TalonSerializationBinder : ISerializationBinder
    {
        private static readonly Assembly AllowedAssembly = typeof(TalonSerializationBinder).Assembly;
        private static readonly string AllowedAssemblyName = AllowedAssembly.GetName().Name;

        private readonly Dictionary<string, Type> _cache = new Dictionary<string, Type>(StringComparer.Ordinal);
        private readonly object _sync = new object();

        public Type BindToType(string assemblyName, string typeName)
        {
            if (!string.Equals(assemblyName, AllowedAssemblyName, StringComparison.Ordinal))
            {
                throw new System.Runtime.Serialization.SerializationException(
                    $"Refusing to deserialise '{typeName}' from assembly '{assemblyName}'. " +
                    "Only types declared by the application may carry a $type discriminator.");
            }

            var key = assemblyName + "|" + typeName;

            lock (_sync)
            {
                Type cached;
                if (_cache.TryGetValue(key, out cached))
                {
                    return cached;
                }

                var resolved = AllowedAssembly.GetType(typeName, throwOnError: false);
                if (resolved == null)
                {
                    throw new System.Runtime.Serialization.SerializationException(
                        $"Type '{typeName}' was not found in '{assemblyName}'.");
                }

                _cache[key] = resolved;
                return resolved;
            }
        }

        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            // Emit the same shape Newtonsoft would by default, so Breeze clients see what they expect.
            assemblyName = serializedType.Assembly.GetName().Name;
            typeName = serializedType.FullName;
        }
    }
}
