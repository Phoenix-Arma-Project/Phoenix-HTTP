using System.Text.Json.Serialization;

namespace PhoenixHttp.Core;

/// <summary>
/// Source-generated JSON contract for <see cref="Config"/>. Native AOT forbids the reflection-based
/// serializer, so the generator emits the (de)serialization code at compile time instead. Property
/// matching is case-insensitive so operators can write the config in whatever casing they prefer.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Config))]
internal partial class ConfigJsonContext : JsonSerializerContext
{
}
