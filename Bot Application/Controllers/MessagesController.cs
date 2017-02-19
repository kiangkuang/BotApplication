using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Web.Http;
using Microsoft.Bot.Connector;
using OpenCvSharp;
using OpenCvSharp.CPlusPlus;
using Tesseract;

namespace Bot_Application.Controllers
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            try
            {
                switch (activity.Type)
                {
                    case ActivityTypes.Message:
                        await ProcessMessage(activity);
                        break;
                    case ActivityTypes.DeleteUserData:
                        // Implement user deletion here
                        // If we handle user deletion, return a real message
                        break;
                    case ActivityTypes.ConversationUpdate:
                        // Handle conversation state changes, like members being added and removed
                        // Use Activity.MembersAdded and Activity.MembersRemoved and Activity.Action for info
                        // Not available in all channels
                        break;
                    case ActivityTypes.ContactRelationUpdate:
                        // Handle add/remove from contact lists
                        // Activity.From + Activity.Action represent what happened
                        break;
                    case ActivityTypes.Typing:
                        // Handle knowing that the user is typing
                        break;
                    case ActivityTypes.Ping:
                        break;
                }

            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                await Reply(activity, e.ToString());
                return Request.CreateResponse(HttpStatusCode.InternalServerError);
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        private async Task ProcessMessage(Activity activity)
        {
            if (activity.Attachments != null)
            {
                foreach (var attachment in activity.Attachments)
                {
                    var file = DownloadFile(attachment.ContentUrl);
                    ProcessImage(file);
                    var results = Ocr(file);
                    await Reply(activity, string.Join("\n", results.Select(x => string.Join(" ", x)).ToList()));
                }
            }
            else
            {
                await Reply(activity, activity.Text);
            }
        }

        private void ProcessImage(string file)
        {
            var orig = Cv2.ImRead(file);
            var ratio = 800.0 / orig.Height;
            var blur = new Mat();
            var edged = new Mat();
            var image = orig.Clone();

            image = image.Resize(new Size(), ratio, ratio);
            Cv2.GaussianBlur(image, blur, new Size(5, 5), 0);
            Cv2.Canny(blur, edged, 75, 200);

            Point[][] contours;
            HierarchyIndex[] hierarchyIndexes;
            Cv2.FindContours(edged.Clone(), out contours, out hierarchyIndexes, ContourRetrieval.List, ContourChain.ApproxSimple);
            var sortedContours = contours.OrderByDescending(GetContourArea);
            Point[] screenContour = null;
            foreach (var contour in sortedContours)
            {
                var epsilon = 0.02 * Cv2.ArcLength(contour, true);
                var approx = Cv2.ApproxPolyDP(contour, epsilon, true);
                if (approx.Length == 4)
                {
                    screenContour = approx;
                    break;
                }
            }

            Cv2.DrawContours(image, new List<Point[]> { screenContour }, 0, Scalar.Lime, 2);
            using (new Window("img", image))
            {
                Cv2.WaitKey();
            }
        }

        private double GetContourArea(Point[] polygon)
        {
            double area = 0;
            for (var i = 0; i < polygon.Length; i++)
            {
                var j = (i + 1) % polygon.Length;

                area += polygon[i].X * polygon[j].Y;
                area -= polygon[i].Y * polygon[j].X;
            }

            var contourArea = Math.Abs(area / 2);
            return contourArea;
        }

        private async Task Reply(Activity activity, string text)
        {
            var connector = new ConnectorClient(new Uri(activity.ServiceUrl));
            await connector.Conversations.ReplyToActivityAsync(activity.CreateReply(text.Replace("\n", "\n\n")));
        }

        private IEnumerable<IEnumerable<string>> Ocr(string file)
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
            File.Delete(file);
            return results;
        }

        private string DownloadFile(string picUrl)
        {
            var file = Path.Combine(Path.GetTempPath(), picUrl.GetHashCode().ToString());
            new WebClient().DownloadFile(picUrl, file);
            return file;
        }
    }
}