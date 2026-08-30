using System.Text.Json.Serialization;

namespace BCETL.BusinessCentral;

public sealed class BcSalesHeader
{
    [JsonPropertyName("systemId")] public Guid SystemId { get; init; }
    [JsonPropertyName("documentType")] public string DocumentType { get; init; } = "";
    [JsonPropertyName("documentNo")] public string DocumentNo { get; init; } = "";
    [JsonPropertyName("sellToCustomerNo")] public string? SellToCustomerNo { get; init; }
    [JsonPropertyName("billToCustomerNo")] public string? BillToCustomerNo { get; init; }
    [JsonPropertyName("customerName")] public string? CustomerName { get; init; }
    [JsonPropertyName("externalDocumentNo")] public string? ExternalDocumentNo { get; init; }
    [JsonPropertyName("postingDate")] public DateTime? PostingDate { get; init; }
    [JsonPropertyName("documentDate")] public DateTime? DocumentDate { get; init; }
    [JsonPropertyName("orderDate")] public DateTime? OrderDate { get; init; }
    [JsonPropertyName("dueDate")] public DateTime? DueDate { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("currencyCode")] public string? CurrencyCode { get; init; }
    [JsonPropertyName("systemCreatedAt")] public DateTime SystemCreatedAt { get; init; }
    [JsonPropertyName("systemModifiedAt")] public DateTime SystemModifiedAt { get; init; }
    [JsonPropertyName("systemCreatedBy")] public Guid? SystemCreatedBy { get; init; }
    [JsonPropertyName("systemModifiedBy")] public Guid? SystemModifiedBy { get; init; }
}

public sealed class BcSalesLine
{
    [JsonPropertyName("systemId")] public Guid SystemId { get; init; }
    [JsonPropertyName("documentType")] public string DocumentType { get; init; } = "";
    [JsonPropertyName("documentNo")] public string DocumentNo { get; init; } = "";
    [JsonPropertyName("lineNo")] public int LineNo { get; init; }
    [JsonPropertyName("sellToCustomerNo")] public string? SellToCustomerNo { get; init; }
    [JsonPropertyName("lineType")] public string? LineType { get; init; }
    [JsonPropertyName("itemNo")] public string? ItemNo { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("locationCode")] public string? LocationCode { get; init; }
    [JsonPropertyName("postingDate")] public DateTime? PostingDate { get; init; }
    [JsonPropertyName("quantity")] public decimal? Quantity { get; init; }
    [JsonPropertyName("unitPrice")] public decimal? UnitPrice { get; init; }
    [JsonPropertyName("lineAmount")] public decimal? LineAmount { get; init; }
    [JsonPropertyName("systemCreatedAt")] public DateTime SystemCreatedAt { get; init; }
    [JsonPropertyName("systemModifiedAt")] public DateTime SystemModifiedAt { get; init; }
    [JsonPropertyName("systemCreatedBy")] public Guid? SystemCreatedBy { get; init; }
    [JsonPropertyName("systemModifiedBy")] public Guid? SystemModifiedBy { get; init; }
}
