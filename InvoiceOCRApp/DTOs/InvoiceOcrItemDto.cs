namespace InvoiceOCRApp.DTO;

public class InvoiceOcrItemDto
{
      public string Description { get; set; }
    public int? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? LineTotal { get; set; }
}
