using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace InvoiceOCRApp.DTO;

public class OcrWord
{
    public string Text { get; set; }
    public int X1 { get; set; }
    public int Y1 { get; set; }
    public int X2 { get; set; }
    public int Y2 { get; set; }


    public static List<OcrWord> Parse(string hocrText)
    {
        var words = new List<OcrWord>();
        try
        {
            var doc = XDocument.Parse(hocrText);
            var ns = doc.Root.GetDefaultNamespace();

            var wordElements = doc.Descendants(ns + "span")
                .Where(e => e.Attribute("class")?.Value == "ocrx_word");

            foreach (var word in wordElements)
            {
                var titleAttr = word.Attribute("title")?.Value;
                var text = word.Value;

                if (string.IsNullOrEmpty(titleAttr)) continue;

                var bboxMatch = Regex.Match(titleAttr, @"bbox\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)");
                if (bboxMatch.Success)
                {
                    var x1 = int.Parse(bboxMatch.Groups[1].Value);
                    var y1 = int.Parse(bboxMatch.Groups[2].Value);
                    var x2 = int.Parse(bboxMatch.Groups[3].Value);
                    var y2 = int.Parse(bboxMatch.Groups[4].Value);

                    words.Add(new OcrWord
                    {
                        Text = text,
                        X1 = x1,
                        Y1 = y1,
                        X2 = x2,
                        Y2 = y2
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to parse hOCR: " + ex.Message);
        }

        return words;

    }
}
