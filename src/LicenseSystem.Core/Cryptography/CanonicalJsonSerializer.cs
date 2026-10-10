using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LicenseSystem.Core.Cryptography;

public static class CanonicalJsonSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public static string Serialize<T>(T value)
    {
        var jsonNode = JsonSerializer.SerializeToNode(value, SerializerOptions);
        if (jsonNode == null)
            return "null";

        var canonicalNode = CanonicalizeNode(jsonNode);
        return canonicalNode.ToJsonString(SerializerOptions);
    }

    private static JsonNode CanonicalizeNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var sortedObj = new JsonObject();
            var sortedProperties = obj
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .ToList();

            foreach (var property in sortedProperties)
            {
                sortedObj.Add(property.Key, property.Value != null ? CanonicalizeNode(property.Value.DeepClone()) : null);
            }

            return sortedObj;
        }

        if (node is JsonArray array)
        {
            var canonicalArray = new JsonArray();
            foreach (var item in array)
            {
                canonicalArray.Add(item != null ? CanonicalizeNode(item.DeepClone()) : null);
            }

            return canonicalArray;
        }

        return node.DeepClone();
    }
}
