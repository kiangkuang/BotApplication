using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace Bot_Application.Controllers
{
    public class Toto
    {
        private readonly OpenCv _openCv = new OpenCv();
        private readonly Tesseract _tesseract = new Tesseract();

        public IEnumerable<string> Process(string url)
        {
            var file = DownloadFile(url);
            var processedRows = _openCv.ProcessImage(file);
            var ocrResults = _tesseract.OcrImages(processedRows);
            var filtered = FilterOcrResults(ocrResults);

            var messages = filtered.Select(row => string.Join(" ", row));
            return messages;
        }

        private string DownloadFile(string picUrl)
        {
            var file = Path.Combine(Path.GetTempPath(), picUrl.GetHashCode().ToString());
            new WebClient().DownloadFile(picUrl, file);
            return file;
        }

        private IEnumerable<IEnumerable<string>> FilterOcrResults(IEnumerable<IEnumerable<string>> ocrResults)
        {
            var twoDigits = ocrResults.Select(row => row.Where(num => num.Length == 2)).Where(row => row.Any());
            var ascending = twoDigits.Select(row =>
            {
                var tmpList = row.ToList();
                return tmpList.Where(num => num != tmpList.Last() || int.Parse(num) == tmpList.Max(int.Parse));
            });
            return @ascending;
        }
    }
}