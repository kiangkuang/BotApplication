using System.Collections.Generic;
using System.Linq;
using System.Web.Hosting;
using OpenCvSharp.CPlusPlus;
using Tesseract;

namespace Bot_Application.Controllers
{
    public class Tesseract
    {
        public IEnumerable<IEnumerable<string>> OcrImages(IEnumerable<IEnumerable<Mat>> imageRows)
        {
            var engine = new TesseractEngine(HostingEnvironment.MapPath(@"~/tessdata"), "eng");
            engine.SetVariable("tessedit_char_whitelist", "0123456789");

            return imageRows.Select(r => r.Select(i =>
            {
                using (var page = engine.Process(Pix.LoadTiffFromMemory(i.ToBytes(".tiff"))))
                {
                    return page.GetText().Trim();
                }
            }));
        }
    }
}
