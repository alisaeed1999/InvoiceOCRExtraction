using InvoiceOCRApp.DTO;

namespace InvoiceOCRApp.Services;

public interface IOcrService
{
    Task<InvoiceOcrResultDto> ExtractInvoiceDataAsync(Stream fileStream, string fileName);
    Task<List<InvoiceOcrResultDto>> GetAllInvoicesAsync();
    Task<InvoiceOcrResultDto> GetInvoiceByIdAsync(int invoiceId);
}
