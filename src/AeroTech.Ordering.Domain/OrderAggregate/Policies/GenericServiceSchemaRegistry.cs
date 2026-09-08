using System.Text.Json;
using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Policies
{
    public static class GenericServiceSchemaRegistry
    {
        private static readonly IReadOnlyList<GenericServiceSchema> Registered =
        [
            new("Priority", "1.0", OrderServiceType.Priority, ["priorityKind"], RequiresReservation: false, RequiresDocument: false, DocumentKind: null),
            new("WiFi", "1.0", OrderServiceType.WiFi, ["accessKind"], RequiresReservation: false, RequiresDocument: false, DocumentKind: null),
            new("Cip", "1.0", OrderServiceType.Cip, ["airportId"], RequiresReservation: false, RequiresDocument: true, DocumentKind: ServiceDocumentKind.ElectronicMiscDocument),
            new("SimCard", "1.0", OrderServiceType.SimCard, ["planCode"], RequiresReservation: false, RequiresDocument: false, DocumentKind: null),
            new("ExtraSeat", "1.0", OrderServiceType.ExtraSeat, ["capacityQuantity", "reason"], RequiresReservation: true, RequiresDocument: true, DocumentKind: ServiceDocumentKind.ElectronicMiscDocument),
            new("SpecialAssistance", "1.0", OrderServiceType.Other, ["assistanceCode"], RequiresReservation: true, RequiresDocument: false, DocumentKind: null)
        ];

        public static GenericServiceSchema Resolve(string? schemaName, string? schemaVersion)
        {
            if (string.IsNullOrWhiteSpace(schemaName) || string.IsNullOrWhiteSpace(schemaVersion))
                throw ExceptionFactory.GenericServiceSchemaNotRegistered(schemaName ?? "(none)", schemaVersion ?? "(none)");

            var byName = Registered
                .Where(schema => string.Equals(schema.SchemaName, schemaName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (byName.Count == 0)
                throw ExceptionFactory.GenericServiceSchemaNotRegistered(schemaName, schemaVersion);

            return byName.FirstOrDefault(schema => string.Equals(schema.SchemaVersion, schemaVersion, StringComparison.OrdinalIgnoreCase))
                   ?? throw ExceptionFactory.GenericServiceSchemaVersionNotSupported(schemaName, schemaVersion);
        }

        public static void EnsureAttributesAreValid(GenericServiceSchema schema, string attributesJson)
        {
            JsonElement root;

            try
            {
                using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(attributesJson) ? "{}" : attributesJson);
                root = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                throw ExceptionFactory.GenericServiceAttributesInvalid(schema.SchemaName, "attributes are not valid JSON");
            }

            if (root.ValueKind != JsonValueKind.Object)
                throw ExceptionFactory.GenericServiceAttributesInvalid(schema.SchemaName, "attributes must be a JSON object");

            foreach (var required in schema.RequiredAttributes)
                if (!root.TryGetProperty(required, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    throw ExceptionFactory.GenericServiceAttributesInvalid(schema.SchemaName, $"missing required attribute '{required}'");
        }
    }
}
