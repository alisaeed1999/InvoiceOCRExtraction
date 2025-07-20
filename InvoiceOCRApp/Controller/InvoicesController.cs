using InvoiceOCRApp.DTO;
using InvoiceOCRApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using InvoiceOCRApp.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using InvoiceOCRApp.Models;

namespace InvoiceOCRApp.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvoicesController : ControllerBase
    {
        private readonly IOcrService _ocrService;
        private readonly InvoiceAppContext _dbContext;

        public InvoicesController(IOcrService ocrService, InvoiceAppContext dbContext)
        {
            _ocrService = ocrService;
            _dbContext = dbContext;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadInvoice([FromForm] UploadInvoiceDto uploadDto)
        {
            Console.WriteLine(uploadDto.File.FileName);
            if (uploadDto.File == null || uploadDto.File.Length == 0)
                return BadRequest("No file uploaded.");

            using var stream = uploadDto.File.OpenReadStream();
            var result = await _ocrService.ExtractInvoiceDataAsync(uploadDto.File.OpenReadStream(), uploadDto.File.FileName);

            return Ok(result);
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveInvoice([FromBody] InvoiceOcrResultDto invoiceDto)
        {
            if (invoiceDto == null)
                return BadRequest("Invalid invoice data.");
            
            var invoice = new Invoice
            {
                Invoicenumber = invoiceDto.InvoiceNumber,
                Invoicedate = invoiceDto.InvoiceDate.HasValue ? invoiceDto.InvoiceDate.Value : null,
                Customername = invoiceDto.CustomerName,
                Totalamount = invoiceDto.TotalAmount,
                Vat = invoiceDto.VAT,
                Invoicedetails = invoiceDto.Items.Select(item => new Invoicedetail
                {
                    Description = item.Description,
                    Quantity = item.Quantity,
                    Unitprice = item.UnitPrice,
                    Linetotal = item.LineTotal
                }).ToList()
            };

            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync();
            

            return Ok(new { invoice.Id });
        }

        [HttpGet]
        public async Task<IActionResult> GetAllInvoices()
        {
            try
            {
                var invoices = await _dbContext.Invoices
                    .Select(i => new InvoiceOcrResultDto
                    {
                        Id = i.Id,
                        InvoiceNumber = i.Invoicenumber,
                        InvoiceDate = i.Invoicedate,
                        CustomerName = i.Customername,
                        TotalAmount = i.Totalamount,
                        VAT = i.Vat,
                        Items = i.Invoicedetails.Select(id => new InvoiceOcrItemDto
                        {
                            Description = id.Description,
                            Quantity = id.Quantity,
                            UnitPrice = id.Unitprice,
                            LineTotal = id.Linetotal
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(invoices);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // GET: api/Invoices/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetInvoiceById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest("Invoice ID is required.");

            try
            {
                var invoice = await _dbContext.Invoices
                    .Where(i => i.Id.ToString() == id)
                    .Select(i => new InvoiceOcrResultDto
                    {
                        Id = i.Id,
                        InvoiceNumber = i.Invoicenumber,
                        InvoiceDate = i.Invoicedate,
                        CustomerName = i.Customername,
                        TotalAmount = i.Totalamount,
                        VAT = i.Vat,

                        Items = i.Invoicedetails.Select(id => new InvoiceOcrItemDto
                        {
                            Description = id.Description,
                            Quantity = id.Quantity,
                            UnitPrice = id.Unitprice,
                            LineTotal = id.Linetotal
                        }).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (invoice == null)
                    return NotFound($"Invoice with ID '{id}' not found.");

                return Ok(invoice);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
