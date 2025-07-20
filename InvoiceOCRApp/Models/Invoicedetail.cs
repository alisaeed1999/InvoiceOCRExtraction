using System;
using System.Collections.Generic;

namespace InvoiceOCRApp.Models;

public partial class Invoicedetail
{
    public int Id { get; set; }

    public int? Invoiceid { get; set; }

    public string? Description { get; set; }

    public int? Quantity { get; set; }

    public decimal? Unitprice { get; set; }

    public decimal? Linetotal { get; set; }

    public virtual Invoice? Invoice { get; set; }
}
