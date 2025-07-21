using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using InvoiceOCRApp.DTO;
using InvoiceOCRApp.Services;
using InvoiceOCRApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;

public class OcrService : IOcrService, IDisposable
{
    private readonly string _tessDataPath;
    private readonly ILogger<OcrService> _logger;
    private readonly string _tempDirectory;
    private readonly InvoiceAppContext _context;

    public OcrService(ILogger<OcrService> logger, InvoiceAppContext context)
    {
        _logger = logger;
        _context = context;
        _tessDataPath = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");
        _tempDirectory = Path.GetTempPath();

        if (!Directory.Exists(_tessDataPath))
        {
            Directory.CreateDirectory(_tessDataPath);
            _logger.LogWarning($"Created missing tessdata directory at: {_tessDataPath}");
        }
    }

    public async Task<InvoiceOcrResultDto> ExtractInvoiceDataAsync(Stream fileStream, string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        string tmpInputPath = Path.Combine(_tempDirectory, $"{Guid.NewGuid()}{ext}");
        string tmpImagePath = Path.ChangeExtension(tmpInputPath, ".png");

        try
        {
            using (var fs = new FileStream(tmpInputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await fileStream.CopyToAsync(fs);
                await fs.FlushAsync();
            }

            if (ext == ".pdf")
            {
                ConvertPdfToImage(tmpInputPath, tmpImagePath);
            }
            else
            {
                tmpImagePath = await PreprocessImageAdvanced(tmpInputPath);
            }

            string rawText = await RunTesseractWithMultipleConfigs(tmpImagePath);
            var hocrText = await RunTesseractHocrAsync(tmpImagePath);
            var layoutWords = OcrWord.Parse(hocrText);

            var result = ParseInvoiceTextWithLayoutAndNLP(rawText, layoutWords);
            result.RawText = rawText;
            result.OcrConfidence = CalculateOcrConfidence(rawText);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR processing failed");
            throw;
        }
        finally
        {
            SafeDelete(tmpInputPath);
            SafeDelete(tmpImagePath);
        }
    }

    private async Task<string> PreprocessImageAdvanced(string imagePath)
    {
        var outputPath = Path.Combine(_tempDirectory, $"{Guid.NewGuid()}_processed.png");
        using var image = await Image.LoadAsync(imagePath);
        image.Mutate(x => x
            .Grayscale()
            .GaussianBlur(0.5f)
            .GaussianSharpen(1.5f)
            .Contrast(1.3f)
            .Brightness(0.1f)
            .BinaryThreshold(0.6f)
            .Resize(new ResizeOptions
            {
                Size = new Size(Math.Max(image.Width * 2, 1600), Math.Max(image.Height * 2, 1200)),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3
            })
            .Pad(20, 20, Color.White)
        );
        await image.SaveAsync(outputPath, new PngEncoder());
        return outputPath;
    }

    private async Task<string> RunTesseractWithMultipleConfigs(string imagePath)
    {
        var configs = new[]
        {
            "--psm 1 -c tessedit_char_whitelist=0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.,:-#/$%@()",
            "--psm 3 -c preserve_interword_spaces=1",
            "--psm 6 -c tessedit_char_whitelist=0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.,:-#/$%@()",
            "--psm 4"
        };

        string bestResult = "";
        double bestConfidence = 0;

        foreach (var config in configs)
        {
            try
            {
                string result = await RunTesseractWithConfig(imagePath, config);
                double confidence = CalculateTextQuality(result);
                if (confidence > bestConfidence)
                {
                    bestConfidence = confidence;
                    bestResult = result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"OCR config failed: {config}, Error: {ex.Message}");
            }
        }

        return string.IsNullOrEmpty(bestResult) ? await RunTesseractWithConfig(imagePath, configs[0]) : bestResult;
    }

    private async Task<string> RunTesseractWithConfig(string imagePath, string config)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await RunTesseractCliAsync(imagePath, config);
        }
        return RunTesseractDotNet(imagePath);
    }

    private async Task<string> RunTesseractCliAsync(string imagePath, string config)
    {
        var outputPath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString());
        var outputTextFile = outputPath + ".txt";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "tesseract",
                Arguments = $"\"{imagePath}\" \"{outputPath}\" -l eng {config}",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment = { { "TESSDATA_PREFIX", "/usr/share/tesseract-ocr/5/tessdata" } }
            }
        };

        _logger.LogDebug($"Running: tesseract {process.StartInfo.Arguments}");

        process.Start();
        string stderr = await process.StandardError.ReadToEndAsync();
        string stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        // Log parameter file warnings at debug level instead of error level
        if (stderr.Contains("read_params_file: Can't open"))
        {
            _logger.LogDebug($"Tesseract parameter file warnings (non-critical): {stderr}");
        }

        // Filter out parameter file warnings that are not critical
        var filteredErrors = stderr.Split('\n')
            .Where(line => !line.Contains("read_params_file: Can't open") && 
                          !line.Contains("Tesseract CLI warning:") &&
                          !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (process.ExitCode != 0 || !File.Exists(outputTextFile))
        {
            // Only throw if there are actual critical errors, not just parameter file warnings
            if (filteredErrors.Any() && !stderr.Contains("read_params_file"))
            {
                throw new Exception($"Tesseract CLI failed: {string.Join("\n", filteredErrors)}");
            }
        }

        string result = await File.ReadAllTextAsync(outputTextFile);
        SafeDelete(outputTextFile);
        return result;
    }

    private string RunTesseractDotNet(string imagePath)
    {
        try
        {
            using var engine = new Tesseract.TesseractEngine(_tessDataPath, "eng", Tesseract.EngineMode.LstmOnly);
            engine.SetVariable("tessedit_char_whitelist", "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.,:-#/$%@() ");
            engine.SetVariable("preserve_interword_spaces", "1");

            using var img = Tesseract.Pix.LoadFromFile(imagePath);
            using var page = engine.Process(img);
            return page.GetText();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tesseract.NET failed");
            throw;
        }
    }

    private double CalculateTextQuality(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        double score = 0.4;
        if (text.Length > 100) score += 0.2;
        if (text.Contains("INVOICE") || text.Contains("TOTAL")) score += 0.2;
        if (text.Count(c => char.IsDigit(c)) > 10) score += 0.1;
        if (text.Split('\n').Length > 5) score += 0.1;
        return Math.Min(score, 1.0);
    }

    private double CalculateOcrConfidence(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        double confidence = 0.5;
        if (text.Length > 100) confidence += 0.1;
        if (Regex.IsMatch(text, @"INVOICE|TOTAL|DATE|CUSTOMER", RegexOptions.IgnoreCase)) confidence += 0.2;
        if (Regex.IsMatch(text, @"\d+\.?\d*")) confidence += 0.1;
        if (text.Contains('\n') && text.Length > 200) confidence += 0.1;
        return Math.Min(confidence, 1.0);
    }

    private InvoiceOcrResultDto ParseInvoiceTextWithLayoutAndNLP(string text, List<OcrWord> layoutWords)
    {
        var result = new InvoiceOcrResultDto();

        result.InvoiceNumber = ExtractInvoiceNumberFromLayout(layoutWords) ?? ExtractInvoiceNumberAdvanced(text, text.Split('\n'));
        result.InvoiceDate = ExtractInvoiceDateFromLayout(layoutWords) ?? ParseDateUsingNLP(text);
        result.CustomerName = ExtractCustomerNameFromLayout(layoutWords) ?? ExtractCustomerNameAdvanced(text, text.Split('\n'));
        result.TotalAmount = ExtractTotalAmountFromLayout(layoutWords, out double totalConfidence) ?? ExtractTotalAmountAdvanced(text, text.Split('\n'));
        result.VAT = ExtractVatFromLayout(layoutWords) ?? ExtractVatAdvanced(text, text.Split('\n'));
        result.Subtotal = ExtractSubtotalFromLayout(layoutWords) ?? ExtractSubtotalAdvanced(text, text.Split('\n'));
        result.Items = ExtractLineItemsFromLayout(layoutWords);

        return result;
    }

   private DateOnly? ParseDateUsingNLP(string text)
    {
        var results = DateTimeRecognizer.RecognizeDateTime(text, Culture.English);
        foreach (var result in results)
        {
            if (result.Resolution is IDictionary<string, object> resolutionDict &&
                resolutionDict.TryGetValue("values", out var valuesObj) &&
                valuesObj is IList<Dictionary<string, string>> valuesList &&
                valuesList.Count > 0)
            {
                var firstValue = valuesList[0];
                if (firstValue.TryGetValue("value", out var dateStr))
                {
                    if (DateTime.TryParse(dateStr, out var dateTime))
                    {
                        return DateOnly.FromDateTime(dateTime);
                    }
                }
            }
        }
        return null;
    }

    private string PreprocessTextAdvanced(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var sb = new StringBuilder(text.Length);
        bool lastWasWhitespace = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasWhitespace)
                {
                    sb.Append(' ');
                    lastWasWhitespace = true;
                }
            }
            else
            {
                char corrected = c switch
                {
                    '|' or '!' or 'l' when IsLikelyI(text, sb.Length) => 'I',
                    '€' => 'E',
                    '`' or '\'' => ' ',
                    '©' => 'O',
                    '0' when IsLikelyO(text, sb.Length) => 'O',
                    'O' when IsLikelyZero(text, sb.Length) => '0',
                    '5' when IsLikelyS(text, sb.Length) => 'S',
                    'S' when IsLikelyFive(text, sb.Length) => '5',
                    _ => c
                };
                sb.Append(corrected);
                lastWasWhitespace = false;
            }
        }
        return sb.ToString().ToUpperInvariant();
    }

    private bool IsLikelyI(string text, int position) => position > 0 && position < text.Length - 1;
    private bool IsLikelyO(string text, int position) => position > 0 && char.IsLetter(text[position - 1]);
    private bool IsLikelyZero(string text, int position) => position > 0 && char.IsDigit(text[position - 1]);
    private bool IsLikelyS(string text, int position) => position > 0 && char.IsLetter(text[position - 1]);
    private bool IsLikelyFive(string text, int position) => position > 0 && char.IsDigit(text[position - 1]);

    private string ExtractInvoiceNumberFromLayout(List<OcrWord> words)
    {
        var keywords = new[] { "INVOICE", "INV", "NO", "#" };
        var keywordWords = words.Where(w => keywords.Any(k => w.Text.Contains(k, StringComparison.OrdinalIgnoreCase))).ToList();
        if (keywordWords.Count == 0) return null;

        var nearbyWords = words
            .Where(w => keywordWords.Any(kw => IsNear(kw, w, 100, 50)))
            .OrderBy(w => w.X1)
            .Select(w => w.Text)
            .FirstOrDefault();

        return nearbyWords;
    }

    private string ExtractInvoiceNumberAdvanced(string text, string[] lines)
    {
        var patterns = new[]
        {
            @"(?:INVOICE\s*(?:NUMBER|NO|#)?\s*:?\s*)([A-Z0-9\-]{3,20})",
            @"\b(INV\d{4,10})\b",
            @"\b(\d{4,8}-\d{2,6})\b",
            @"\b([A-Z]{2,3}-?\d{3,10})\b",
            @"\b(\d{6,12})\b"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups[1].Value.Length >= 3)
            {
                var value = match.Groups[1].Value.Trim();
                if (IsValidInvoiceNumber(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private bool IsValidInvoiceNumber(string value) => !string.IsNullOrWhiteSpace(value) && value.Length >= 3;

    private DateOnly? ExtractInvoiceDateFromLayout(List<OcrWord> words)
    {
        var keywords = new[] { "DATE", "ISSUED", "CREATED" };
        var keywordWords = words.Where(w => keywords.Any(k => w.Text.Contains(k, StringComparison.OrdinalIgnoreCase))).ToList();
        if (keywordWords.Count == 0) return null;

        var nearbyWords = words
            .Where(w => keywordWords.Any(kw => IsNear(kw, w, 150, 50)))
            .Where(w => Regex.IsMatch(w.Text, @"[\d/\-\.]+"))
            .OrderBy(w => w.X1)
            .ToList();

        if (nearbyWords.Count == 0) return null;
        var dateText = string.Join(" ", nearbyWords.Select(w => w.Text));
        return ParseDateAdvanced(dateText);
    }

    private DateOnly? ExtractInvoiceDateAdvanced(string text, string[] lines)
    {
        var datePatterns = new[]
        {
            @"(?:INVOICE\s*DATE|DATE|ISSUED|CREATED)\s*:?\s*(\d{1,2}[/\-\.]\d{1,2}[/\-\.]\d{2,4})",
            @"(?:DATE|DATED)\s*:?\s*(\d{1,2}\s+(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)\w*\s+\d{2,4})",
            @"\b(\d{1,2}[/\-\.]\d{1,2}[/\-\.]\d{4})\b",
            @"\b(\d{4}[/\-\.]\d{1,2}[/\-\.]\d{1,2})\b"
        };

        foreach (var pattern in datePatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups[1].Success)
            {
                var dateStr = match.Groups[1].Value;
                var parsedDate = ParseDateAdvanced(dateStr);
                if (parsedDate.HasValue)
                {
                    return parsedDate;
                }
            }
        }

        return null;
    }

    private DateOnly? ParseDateAdvanced(string dateStr)
    {
        var formats = new[]
        {
            "dd/MM/yyyy", "MM/dd/yyyy", "yyyy/MM/dd",
            "dd-MM-yyyy", "MM-dd-yyyy", "yyyy-MM-dd",
            "dd.MM.yyyy", "MM.dd.yyyy", "yyyy.MM.dd",
            "dd MMM yyyy", "MMM dd, yyyy", "dd MMMM yyyy"
        };

        foreach (var format in formats)
        {
            if (DateOnly.TryParseExact(dateStr, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        return DateOnly.TryParse(dateStr, out var fallbackDate) ? fallbackDate : null;
    }

    private string ExtractCustomerNameFromLayout(List<OcrWord> words)
    {
        var keywords = new[] { "BILL TO", "CUSTOMER", "CLIENT" };
        var keywordWords = words.Where(w => keywords.Any(k => w.Text.Contains(k, StringComparison.OrdinalIgnoreCase))).ToList();

        if (keywordWords.Count == 0) return null;

        var nameWords = words
            .Where(w => keywordWords.Any(kw => IsNear(kw, w, 200, 60)))
            .Where(w => !Regex.IsMatch(w.Text, @"[0-9]")) // Exclude numbers
            .OrderBy(w => w.X1)
            .ToList();

        if (nameWords.Count == 0) return null;

        return string.Join(" ", nameWords.Select(w => w.Text)).Trim();
    }

    private string ExtractCustomerNameAdvanced(string text, string[] lines)
    {
        var patterns = new[]
        {
            @"(?:BILL\s*TO|CUSTOMER|CLIENT)\s*:?\s*([^\n\r]{3,100})",
            @"(?:TO\s*:)\s*([A-Z\s&.,\-]+)(?=\n|$)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (match.Success && match.Groups[1].Success)
            {
                var name = CleanCustomerName(match.Groups[1].Value);
                if (IsValidCustomerName(name))
                {
                    return name;
                }
            }
        }

        return null;
    }

    private string CleanCustomerName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        name = name.Trim().Replace("\n", " ").Replace("\r", " ");
        name = Regex.Replace(name, @"\s{2,}", " ");
        name = Regex.Replace(name, @"[^\w\s&.,-]", "");
        return name.Trim();
    }

    private bool IsValidCustomerName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && name.Length >= 3 && name.Any(char.IsLetter);
    }

    private decimal? ExtractTotalAmountFromLayout(List<OcrWord> words, out double confidence)
    {
        confidence = 0.0;
        var keywords = new[] { "TOTAL", "GRAND TOTAL", "AMOUNT DUE" };
        var keywordWords = words.Where(w => keywords.Any(k => w.Text.Contains(k, StringComparison.OrdinalIgnoreCase))).ToList();

        if (keywordWords.Count == 0) return null;

        var nearbyWords = words
            .Where(w => keywordWords.Any(kw => IsNear(kw, w, 150, 50)))
            .Where(w => Regex.IsMatch(w.Text, @"[\d,\.]+"))
            .OrderByDescending(w => w.X1)
            .ToList();

        if (nearbyWords.Count == 0) return null;

        var amountText = nearbyWords.First().Text;
        var normalized = NormalizeAmountAdvanced(amountText);
        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
        {
            confidence = 0.9; // High confidence when found via layout
            return amount;
        }

        return null;
    }

    private decimal? ExtractTotalAmountAdvanced(string text, string[] lines)
    {
        var patterns = new[]
        {
            @"(?:TOTAL\s*(?:AMOUNT|DUE)?|GRAND\s*TOTAL|AMOUNT\s*DUE)\s*:?\s*[£$€]?\s*([\d,]+\.?\d{0,2})",
            @"(?:TOTAL)\s*[£$€]?\s*([\d,]+\.?\d{0,2})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups[1].Success)
            {
                var amountStr = NormalizeAmountAdvanced(match.Groups[1].Value);
                if (decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                {
                    return amount;
                }
            }
        }

        return null;
    }

    private decimal? ExtractVatFromLayout(List<OcrWord> words)
    {
        var keywords = new[] { "VAT", "TAX", "GST" };
        var keywordWords = words.Where(w => keywords.Any(k => w.Text.Contains(k, StringComparison.OrdinalIgnoreCase))).ToList();

        if (keywordWords.Count == 0) return null;

        var nearbyWords = words
            .Where(w => keywordWords.Any(kw => IsNear(kw, w, 150, 50)))
            .Where(w => Regex.IsMatch(w.Text, @"[\d,\.]+"))
            .OrderByDescending(w => w.X1)
            .ToList();

        if (nearbyWords.Count == 0) return null;

        var amountText = nearbyWords.First().Text;
        var normalized = NormalizeAmountAdvanced(amountText);
        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
        {
            return amount;
        }

        return null;
    }

    private decimal? ExtractVatAdvanced(string text, string[] lines)
    {
        var patterns = new[]
        {
            @"(?:VAT|TAX|GST)\s*(?:AMOUNT|RATE)?\s*:?\s*[£$€]?\s*([\d,]+\.?\d{0,2})",
            @"(?:VAT|TAX|GST)\s*@\s*\d+%\s*[£$€]?\s*([\d,]+\.?\d{0,2})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups[1].Success)
            {
                var amountStr = NormalizeAmountAdvanced(match.Groups[1].Value);
                if (decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                {
                    return amount;
                }
            }
        }

        return null;
    }

    private decimal? ExtractSubtotalFromLayout(List<OcrWord> words)
    {
        var keywords = new[] { "SUBTOTAL", "SUB TOTAL", "NET AMOUNT" };
        var keywordWords = words.Where(w => keywords.Any(k => w.Text.Contains(k, StringComparison.OrdinalIgnoreCase))).ToList();

        if (keywordWords.Count == 0) return null;

        var nearbyWords = words
            .Where(w => keywordWords.Any(kw => IsNear(kw, w, 150, 50)))
            .Where(w => Regex.IsMatch(w.Text, @"[\d,\.]+"))
            .OrderByDescending(w => w.X1)
            .ToList();

        if (nearbyWords.Count == 0) return null;

        var amountText = nearbyWords.First().Text;
        var normalized = NormalizeAmountAdvanced(amountText);
        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
        {
            return amount;
        }

        return null;
    }

    private decimal? ExtractSubtotalAdvanced(string text, string[] lines)
    {
        var patterns = new[]
        {
            @"(?:SUB\s*TOTAL|SUBTOTAL|NET\s*AMOUNT)\s*:?\s*[£$€]?\s*([\d,]+\.?\d{0,2})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups[1].Success)
            {
                var amountStr = NormalizeAmountAdvanced(match.Groups[1].Value);
                if (decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                {
                    return amount;
                }
            }
        }

        return null;
    }

    private string NormalizeAmountAdvanced(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "0";
        value = value.Trim();

        if (Regex.IsMatch(value, @"\d+\.\d{3},\d{2}$"))
        {
            return value.Replace(".", "").Replace(",", ".");
        }

        if (value.Count(c => c == ',') > 1)
        {
            var lastCommaIndex = value.LastIndexOf(',');
            if (lastCommaIndex > 0 && value.Length - lastCommaIndex <= 3)
            {
                value = value.Substring(0, lastCommaIndex).Replace(",", "") + "." + value.Substring(lastCommaIndex + 1);
            }
            else
            {
                value = value.Replace(",", "");
            }
        }
        else
        {
            var commaIndex = value.IndexOf(',');
            if (commaIndex > 0 && value.Length - commaIndex <= 3)
            {
                value = value.Replace(",", ".");
            }
            else
            {
                value = value.Replace(",", "");
            }
        }

        return Regex.Replace(value, @"[^\d\.]", "");
    }

    private List<InvoiceOcrItemDto> ExtractLineItemsFromLayout(List<OcrWord> words)
    {
        var items = new List<InvoiceOcrItemDto>();
        bool inItemsSection = false;
        int headerLines = 0;

        var headerPatterns = new[]
        {
            @"(?:DESCRIPTION|ITEM|PRODUCT).*(?:QTY|QUANTITY|QT).*(?:PRICE|RATE|UNIT|COST).*(?:AMOUNT|TOTAL|SUM)"
        };

        for (int i = 0; i < words.Count; i++)
        {
            var word = words[i];

            if (!inItemsSection && headerPatterns.Any(p => Regex.IsMatch(word.Text, p, RegexOptions.IgnoreCase)))
            {
                inItemsSection = true;
                headerLines = 1;
                continue;
            }

            if (inItemsSection)
            {
                if (IsTableEndLine(word))
                {
                    break;
                }

                if (headerLines > 0 || IsHeaderOrSeparatorLine(word))
                {
                    headerLines = Math.Max(0, headerLines - 1);
                    continue;
                }

                var item = ParseLineItemFromLayout(words.Skip(i).ToList());
                if (item != null)
                {
                    items.Add(item);
                    i += item.Description.Length + item.Quantity.ToString().Length + item.UnitPrice.ToString().Length + item.LineTotal.ToString().Length;
                }
            }
        }

        return items;
    }

    private InvoiceOcrItemDto ParseLineItemFromLayout(List<OcrWord> words)
    {
        if (words.Count < 4) return null;

        try
        {
            var description = words[0].Text.Trim();
            if (int.TryParse(words[1].Text, out var quantity) &&
                decimal.TryParse(NormalizeAmountAdvanced(words[2].Text), out var unitPrice) &&
                decimal.TryParse(NormalizeAmountAdvanced(words[3].Text), out var lineTotal))
            {
                return new InvoiceOcrItemDto
                {
                    Description = description,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    LineTotal = lineTotal
                };
            }
        }
        catch { }

        return null;
    }

    private bool IsTableEndLine(OcrWord word) =>
        Regex.IsMatch(word.Text, @"^\s*(?:SUB\s*TOTAL|TOTAL|VAT|TAX|GRAND\s*TOTAL)", RegexOptions.IgnoreCase);

    private bool IsHeaderOrSeparatorLine(OcrWord word) =>
        string.IsNullOrWhiteSpace(word.Text) || Regex.IsMatch(word.Text, @"^[\s\-=_]{3,}$");

    private async Task<string> RunTesseractHocrAsync(string imagePath)
    {
        var outputHocrPath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString());
        var outputHocrFile = outputHocrPath + ".hocr";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "tesseract",
                Arguments = $"\"{imagePath}\" \"{outputHocrPath}\" -l eng hocr",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment = { { "TESSDATA_PREFIX", "/usr/share/tesseract-ocr/5/tessdata" } }
            }
        };

        process.Start();
        string stderr = await process.StandardError.ReadToEndAsync();
        string stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        // Log parameter file warnings at debug level instead of error level
        if (stderr.Contains("read_params_file: Can't open"))
        {
            _logger.LogDebug($"Tesseract parameter file warnings (non-critical): {stderr}");
        }

        // Filter out parameter file warnings that are not critical
        var filteredErrors = stderr.Split('\n')
            .Where(line => !line.Contains("read_params_file: Can't open") && 
                          !line.Contains("Tesseract CLI warning:") &&
                          !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (process.ExitCode != 0 || !File.Exists(outputHocrFile))
        {
            // Only throw if there are actual critical errors, not just parameter file warnings
            if (filteredErrors.Any() && !stderr.Contains("read_params_file"))
            {
                throw new Exception($"Tesseract CLI failed: {string.Join("\n", filteredErrors)}");
            }
        }

        string hocrText = await File.ReadAllTextAsync(outputHocrFile);
        SafeDelete(outputHocrFile);

        return hocrText;
    }

    private bool IsNear(OcrWord a, OcrWord b, int xTolerance, int yTolerance)
    {
        return Math.Abs(a.X2 - b.X1) <= xTolerance && Math.Abs(a.Y1 - b.Y1) <= yTolerance;
    }

    private void ConvertPdfToImage(string pdfPath, string outputImagePath)
    {
        var baseName = Path.ChangeExtension(outputImagePath, null);
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "gs",
                Arguments = $"-dNOPAUSE -dBATCH -sDEVICE=png16m -r300 -dTextAlphaBits=4 -dGraphicsAlphaBits=4 " +
                            $"-dFirstPage=1 -dLastPage=1 -sOutputFile=\"{baseName}-%d.png\" \"{pdfPath}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Exception($"PDF conversion failed: {error}");
        }

        var generatedImage = $"{baseName}-1.png";
        if (File.Exists(generatedImage))
        {
            File.Move(generatedImage, outputImagePath, true);
        }
        else
        {
            throw new FileNotFoundException("Converted image not found", generatedImage);
        }
    }

    private void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not delete {path}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        // No unmanaged resources to dispose
    }

    public async Task<List<InvoiceOcrResultDto>> GetAllInvoicesAsync()
    {
        var invoices = await _context.Invoices.ToListAsync();
        return invoices.Select(i => new InvoiceOcrResultDto
        {
            Id = i.Id,
            InvoiceNumber = i.Invoicenumber,
            InvoiceDate = i.Invoicedate,
            CustomerName = i.Customername,
            TotalAmount = i.Totalamount,
            VAT = i.Vat,
            Items = i.Invoicedetails.Select(item => new InvoiceOcrItemDto
            {
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.Unitprice,
                LineTotal = item.Linetotal
            }).ToList()
        }).ToList();
    }

    public async Task<InvoiceOcrResultDto> GetInvoiceByIdAsync(int invoiceId)
    {
        var invoice = await _context.Invoices
                .Include(i => i.Invoicedetails)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return null;
        return new InvoiceOcrResultDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.Invoicenumber,
            InvoiceDate = invoice.Invoicedate,
            CustomerName = invoice.Customername,
            TotalAmount = invoice.Totalamount,
            VAT = invoice.Vat,
            Items = invoice.Invoicedetails.Select(item => new InvoiceOcrItemDto
            {
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.Unitprice,
                LineTotal = item.Linetotal
            }).ToList()
        };
    }
}
