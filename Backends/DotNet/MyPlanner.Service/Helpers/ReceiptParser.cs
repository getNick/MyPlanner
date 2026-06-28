using System.Text.Json;
using MyPlanner.Service.Models;

namespace MyPlanner.Service.Helpers;

public static class ReceiptParser
{
    public static async Task<ReceiptDto> ProcessReceiptAsync(ILlmService llmService, Stream imageStream, string contentType)
    {
        var prompt = BuildExtractionPrompt();
        var schema = BuildReceiptSchema();
        var rawResponse = await llmService.SendImageRequestAsync(imageStream, contentType, prompt, schema);
        return ParseReceiptResponse(rawResponse);
    }

    private static LlmSchema BuildReceiptSchema()
    {
        return new LlmSchema
        {
            Type = LlmSchemaType.Object,
            Properties = new Dictionary<string, LlmSchema>
            {
                { "merchantName", new LlmSchema { Type = LlmSchemaType.String, Nullable = true } },
                { "timestamp", new LlmSchema { Type = LlmSchemaType.String, Nullable = true, Description = "ISO 8601 date string" } },
                { "date", new LlmSchema { Type = LlmSchemaType.String, Nullable = true, Description = "Date of purchase in YYYY-MM-DD format" } },
                { "time", new LlmSchema { Type = LlmSchemaType.String, Nullable = true, Description = "Time of purchase in HH:MM format" } },
                { "totalAmount", new LlmSchema { Type = LlmSchemaType.Number, Nullable = true } },
                { "currency", new LlmSchema { Type = LlmSchemaType.String, Nullable = true } },
                { "paymentMethod", new LlmSchema { Type = LlmSchemaType.String, Nullable = true } },
                { "items", new LlmSchema
                    {
                        Type = LlmSchemaType.Array,
                        Items = new LlmSchema
                        {
                            Type = LlmSchemaType.Object,
                            Properties = new Dictionary<string, LlmSchema>
                            {
                                { "name", new LlmSchema { Type = LlmSchemaType.String, Description = "Cleaned product name for grouping" } },
                                { "fullName", new LlmSchema { Type = LlmSchemaType.String, Description = "Exact product name as on receipt" } },
                                { "unitPrice", new LlmSchema { Type = LlmSchemaType.Number, Description = "Price for one unit" } },
                                { "quantity", new LlmSchema { Type = LlmSchemaType.Number, Description = "Quantity or weight purchased" } },
                                { "totalPrice", new LlmSchema { Type = LlmSchemaType.Number, Description = "Total price for this line item" } },
                                { "category", new LlmSchema { Type = LlmSchemaType.String, Description = "One budget category from the allowed list" } },
                                { "subcategory", new LlmSchema { Type = LlmSchemaType.String, Description = "Specific subcategory" } },
                                { "barcode", new LlmSchema { Type = LlmSchemaType.String, Nullable = true, Description = "Barcode number (EAN-13, EAN-8, UPC) if visible on the receipt next to the item. Extract exactly as printed — digits only." } }
                            }
                        }
                    }
                }
            }
        };
    }

    private static string BuildExtractionPrompt()
    {
        var categories = ReceiptCategories.Categories.Select(c => $"- {c.Name}: {string.Join(", ", c.Subcategories)}").ToList();

        return """
You are a receipt analysis assistant. Extract all items from the provided receipt image.

## Line item cleaning rules:
- **name vs fullName:** `fullName` is always the exact text from the receipt — never modify it. `name` is cleaned for grouping: remove weight/volume/quantity suffixes (ваг, кг, мл, шт, etc.) and numeric descriptors that don't affect product identity. Example: "Банан ваг" → "Банан", "Йогурт Турецький 8% Яготин 260г" → "Йогурт Турецький".
- **barcode:** Look for barcode numbers (EAN-13, EAN-8, UPC) printed below or next to each item on the receipt. Extract them as digits only — no spaces, dashes, or check digits unless they're part of the number. If a barcode is not visible for an item, omit the field.

## Available categories and subcategories:
"""
+ string.Join("\n", categories)
+ """

Rules:
- date MUST be in YYYY-MM-DD format only (no time, no timezone).
- time MUST be in HH:MM 24-hour format.
- totalPrice should be calculated as unitPrice × quantity if not explicitly shown on the receipt.
- category must be one of the categories listed above.
- subcategory must be a valid subcategory within the chosen category.
""";
    }

    public static ReceiptDto ParseReceiptResponse(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // Validate that the response is valid JSON before parsing
            if (root.ValueKind == JsonValueKind.Null || root.ValueKind == JsonValueKind.Undefined)
                return new ReceiptDto();

            var items = new List<ReceiptItemDto>();
            if (root.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind != JsonValueKind.Null)
            {
                foreach (var item in itemsProp.EnumerateArray())
                {
                    string name = "";
                    string fullName = "";
                    decimal unitPrice = 0;
                    double quantity = 1;
                    decimal totalPrice = 0;
                    string? category = null;
                    string? subcategory = null;
                    string? barcode = null;

                    if (item.TryGetProperty("name", out var nameProp) && nameProp.ValueKind != JsonValueKind.Null)
                        name = nameProp.GetString() ?? "";

                    if (item.TryGetProperty("fullName", out var fullNameProp) && fullNameProp.ValueKind != JsonValueKind.Null)
                        fullName = fullNameProp.GetString() ?? "";

                    if (item.TryGetProperty("unitPrice", out var priceProp) && priceProp.ValueKind != JsonValueKind.Null)
                        unitPrice = priceProp.GetDecimal();

                    if (item.TryGetProperty("quantity", out var qtyProp) && qtyProp.ValueKind != JsonValueKind.Null)
                        quantity = qtyProp.GetDouble();

                    // totalPrice: use from receipt if available, otherwise calculate as unitPrice × quantity
                    if (item.TryGetProperty("totalPrice", out var totalItemProp) && totalItemProp.ValueKind != JsonValueKind.Null)
                        totalPrice = totalItemProp.GetDecimal();
                    else
                        totalPrice = unitPrice * (decimal)quantity;

                    // category: one of the allowed budget categories
                    if (item.TryGetProperty("category", out var catProp) && catProp.ValueKind != JsonValueKind.Null)
                        category = catProp.GetString();

                    // subcategory: specific subcategory within the chosen category
                    if (item.TryGetProperty("subcategory", out var subcatProp) && subcatProp.ValueKind != JsonValueKind.Null)
                        subcategory = subcatProp.GetString();

                    // barcode: extracted from the receipt image
                    if (item.TryGetProperty("barcode", out var barcodeProp) && barcodeProp.ValueKind != JsonValueKind.Null)
                        barcode = barcodeProp.GetString();

                    items.Add(new ReceiptItemDto
                    {
                        Name = name,
                        FullName = fullName,
                        UnitPrice = unitPrice,
                        Quantity = quantity,
                        TotalPrice = totalPrice,
                        Category = category,
                        Subcategory = subcategory,
                        Barcode = barcode
                    });
                }
            }

            decimal? totalAmount = null;
            if (root.TryGetProperty("totalAmount", out var totalProp) && totalProp.ValueKind != JsonValueKind.Null)
            {
                totalAmount = totalProp.GetDecimal();
            }

            string? merchantName = null;
            if (root.TryGetProperty("merchantName", out var storeProp) && storeProp.ValueKind != JsonValueKind.Null)
            {
                merchantName = storeProp.GetString();
            }

            DateTime? timestamp = null;
            // Try ISO 8601 format first
            if (root.TryGetProperty("timestamp", out var tsProp) && tsProp.ValueKind != JsonValueKind.Null)
            {
                var tsString = tsProp.GetString();
                if (!string.IsNullOrWhiteSpace(tsString) && DateTime.TryParse(tsString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedTs))
                    timestamp = parsedTs;
            }
            // Fallback: combine separate date and time fields
            else
            {
                string? dateString = null;
                if (root.TryGetProperty("date", out var dateProp) && dateProp.ValueKind != JsonValueKind.Null)
                {
                    dateString = dateProp.GetString();
                }

                string? timeString = null;
                if (root.TryGetProperty("time", out var timeProp) && timeProp.ValueKind != JsonValueKind.Null)
                {
                    timeString = timeProp.GetString();
                }

                if (!string.IsNullOrWhiteSpace(dateString))
                {
                    if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var parsedDate))
                    {
                        timestamp = parsedDate;
                        if (!string.IsNullOrWhiteSpace(timeString) && DateTime.TryParseExact(timeString, "HH:mm", null, System.Globalization.DateTimeStyles.None, out var parsedTime))
                            timestamp = timestamp.Value.Add(parsedTime.TimeOfDay);
                    }
                }
            }

            string? currency = null;
            if (root.TryGetProperty("currency", out var currencyProp) && currencyProp.ValueKind != JsonValueKind.Null)
            {
                currency = currencyProp.GetString();
            }

            string? paymentMethod = null;
            if (root.TryGetProperty("paymentMethod", out var pmProp) && pmProp.ValueKind != JsonValueKind.Null)
            {
                paymentMethod = pmProp.GetString();
            }

            return new ReceiptDto
            {
                Items = items,
                TotalAmount = totalAmount,
                MerchantName = merchantName,
                Timestamp = timestamp,
                Currency = currency,
                PaymentMethod = paymentMethod
            };
        }
        catch (JsonException)
        {
            return new ReceiptDto();
        }
    }
}
