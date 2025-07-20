using System.ComponentModel.DataAnnotations;

namespace InvoiceOCRApp.DTO;

public class UploadInvoiceDto
{
    [Required]
    public IFormFile File { get; set; }

}
