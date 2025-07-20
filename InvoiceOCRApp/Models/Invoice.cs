using System;
using System.Collections.Generic;

namespace InvoiceOCRApp.Models;

public partial class Invoice
{
    public int Id { get; set; }

    public string? Invoicenumber { get; set; }

    public DateOnly? Invoicedate { get; set; }

    public string? Customername { get; set; }

    public decimal? Totalamount { get; set; }

    public decimal? Vat { get; set; }

    public virtual ICollection<Invoicedetail> Invoicedetails { get; set; } = new List<Invoicedetail>();
}
