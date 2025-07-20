using System.Collections.Generic;

namespace InvoiceOCRApp.DTO;

public class InvoiceOcrResultDto
{
    public int? Id { get; set; }
    public string InvoiceNumber { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public string CustomerName { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? VAT { get; set; }
    public decimal? Subtotal { get; set; }
    public List<InvoiceOcrItemDto> Items { get; set; } = new();
    public string? RawText { get; set; }
    public double? OcrConfidence { get; set; }
}
