using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Hosting;
using Tesseract;

namespace Bot_Application.Controllers
{
    public class Tesseract
    {
        public IEnumerable<IEnumerable<string>> Ocr(string file)
        {
            var results = new List<List<string>>();

            using (var engine = new TesseractEngine(HostingEnvironment.MapPath(@"~/tessdata"), "eng"))
            {
                engine.SetVariable("tessedit_char_whitelist", "0123456789");
                using (var page = engine.Process(Pix.LoadFromFile(file)))
                {
                    var iter = page.GetIterator();
                    iter.Begin();
                    do
                    {
                        var line = Regex.Replace(iter.GetText(PageIteratorLevel.TextLine), @"\t|\n|\r", "").Split(' ').ToList();
                        if (line.Exists(s => s.Length > 2)) continue;

                        var row = new List<string>();
                        foreach (var num in line)
                        {
                            if (num.Length == 2 && !row.Exists(s => int.Parse(s) > int.Parse(num)))
                            {
                                // 2 digits and ascending
                                row.Add(num);
                            }
                        }
                        Debug.WriteLine(iter.GetConfidence(PageIteratorLevel.TextLine));
                        results.Add(row);
                    } while (iter.Next(PageIteratorLevel.TextLine));
                }
            }

            // File.Delete(file);
            return results;
        }
    }
}