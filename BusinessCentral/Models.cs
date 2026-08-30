using System.Text.Json.Serialization;

namespace BCETL.BusinessCentral;

public sealed class ODataResponse<T>
{
    [JsonPropertyName("value")]
    public List<T> Value { get; init; } = [];

    [JsonPropertyName("@odata.nextLink")]
    public string? NextLink { get; init; }
}

public interface IBcStampedRecord
{
    Guid SystemId { get; }
    DateTime SystemModifiedAt { get; }
}

public sealed class BcCustomer : IBcStampedRecord
{
    [JsonPropertyName("systemId")] public Guid SystemId { get; init; }
    [JsonPropertyName("customerNo")] public string CustomerNo { get; init; } = "";
    [JsonPropertyName("customerName")] public string? CustomerName { get; init; }
    [JsonPropertyName("corpGroup")] public string? CorpGroup { get; init; }
    [JsonPropertyName("address")] public string? Address { get; init; }
    [JsonPropertyName("city")] public string? City { get; init; }
    [JsonPropertyName("postCode")] public string? PostCode { get; init; }
    [JsonPropertyName("systemCreatedAt")] public DateTime SystemCreatedAt { get; init; }
    [JsonPropertyName("systemModifiedAt")] public DateTime SystemModifiedAt { get; init; }
    [JsonPropertyName("systemCreatedBy")] public Guid? SystemCreatedBy { get; init; }
    [JsonPropertyName("systemModifiedBy")] public Guid? SystemModifiedBy { get; init; }
}

public sealed class BcSalesInvoiceHeader : IBcStampedRecord
{
    [JsonPropertyName("systemId")] public Guid SystemId { get; init; }
    [JsonPropertyName("invoiceNo")] public string InvoiceNo { get; init; } = "";
    [JsonPropertyName("orderNo")] public string? OrderNo { get; init; }
    [JsonPropertyName("sellToCustomerNo")] public string? SellToCustomerNo { get; init; }
    [JsonPropertyName("billToCustomerNo")] public string? BillToCustomerNo { get; init; }
    [JsonPropertyName("customerName")] public string? CustomerName { get; init; }
    [JsonPropertyName("externalDocumentNo")] public string? ExternalDocumentNo { get; init; }
    [JsonPropertyName("postingDate")] public DateTime? PostingDate { get; init; }
    [JsonPropertyName("documentDate")] public DateTime? DocumentDate { get; init; }
    [JsonPropertyName("dueDate")] public DateTime? DueDate { get; init; }
    [JsonPropertyName("currencyCode")] public string? CurrencyCode { get; init; }
    [JsonPropertyName("amount")] public decimal? Amount { get; init; }
    [JsonPropertyName("amountIncludingVat")] public decimal? AmountIncludingVat { get; init; }
    [JsonPropertyName("systemCreatedAt")] public DateTime SystemCreatedAt { get; init; }
    [JsonPropertyName("systemModifiedAt")] public DateTime SystemModifiedAt { get; init; }
    [JsonPropertyName("systemCreatedBy")] public Guid? SystemCreatedBy { get; init; }
    [JsonPropertyName("systemModifiedBy")] public Guid? SystemModifiedBy { get; init; }
}

public sealed class BcSalesInvoiceLine : IBcStampedRecord
{
    [JsonPropertyName("systemId")] public Guid SystemId { get; init; }
    [JsonPropertyName("invoiceNo")] public string InvoiceNo { get; init; } = "";
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
    [JsonPropertyName("amountIncludingVat")] public decimal? AmountIncludingVat { get; init; }
    [JsonPropertyName("systemCreatedAt")] public DateTime SystemCreatedAt { get; init; }
    [JsonPropertyName("systemModifiedAt")] public DateTime SystemModifiedAt { get; init; }
    [JsonPropertyName("systemCreatedBy")] public Guid? SystemCreatedBy { get; init; }
    [JsonPropertyName("systemModifiedBy")] public Guid? SystemModifiedBy { get; init; }
}

public sealed record WatermarkState(DateTime? LastStamp, int OverlapSeconds);
public sealed record MergeResult(int Fetched, int Inserted, int Updated, DateTime? MaxStamp);
