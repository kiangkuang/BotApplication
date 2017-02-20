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
using Rect = OpenCvSharp.CPlusPlus.Rect;

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
                    var processedImage = ProcessImage(file);
                    var results = Ocr(processedImage);
                    await Reply(activity, string.Join("\n", results.Select(x => string.Join(" ", x)).ToList()));
                }
            }
            else
            {
                await Reply(activity, activity.Text);
            }
        }

        private string ProcessImage(string file)
        {
            var orig = Cv2.ImRead(file);
            var ratio = 1000f / Math.Max(orig.Height, orig.Width);
            var image = orig.Clone();

            var edged = CannyEdge(image, ratio);
            var screenContour = GetContour(edged);

            Mat final;
            if (screenContour != null)
            {
                var boundRect = Cv2.BoundingRect(screenContour);
                var transformed = FixPerspective(orig, screenContour, boundRect, ratio);

                var cropped = transformed[ResizeBoundRect(boundRect, ratio)];
                final = CleanImage(cropped);
            }
            else
            {
                final = CleanImage(orig);
            }

            /*
            using (new Window("img", final))
            {
                Cv2.WaitKey();
            }
            */

            // File.Delete(file);
            var newFile = file + "-final.jpg";
            Cv2.ImWrite(newFile, final);

            return newFile;
        }

        private Mat FixPerspective(Mat orig, Point[] screenContour, Rect boundRect, float ratio)
        {
            var transMtx = GetTransMtx(screenContour, boundRect, ratio);
            var transformed = new Mat();
            Cv2.WarpPerspective(orig, transformed, transMtx, orig.Size());
            return transformed;
        }

        private static Mat CleanImage(Mat cropped)
        {
            var grey = cropped.CvtColor(ColorConversion.RgbToGray);
            return grey.Threshold(127, 255, ThresholdType.Binary);
        }

        private Mat GetTransMtx(IList<Point> screenContour, Rect boundRect, float ratio)
        {
            var src = RatioPoints(ratio,
                screenContour[0].X, screenContour[0].Y,
                screenContour[1].X, screenContour[1].Y,
                screenContour[2].X, screenContour[2].Y,
                screenContour[3].X, screenContour[3].Y);
            var dest = RatioPoints(ratio,
                boundRect.Right, boundRect.Top,
                boundRect.Left, boundRect.Top,
                boundRect.Left, boundRect.Bottom,
                boundRect.Right, boundRect.Bottom);

            return Cv2.GetPerspectiveTransform(src, dest);
        }

        private Point[] GetContour(Mat edged)
        {
            Point[][] contours;
            HierarchyIndex[] hierarchyIndexes;
            Cv2.FindContours(edged.Clone(), out contours, out hierarchyIndexes, ContourRetrieval.List, ContourChain.ApproxSimple);
            var area = edged.Height * edged.Width;
            var sortedContours = contours.Where(c => GetContourArea(c) / area > 0.3).OrderByDescending(GetContourArea);

            Point[] selectedContour = null;
            foreach (var contour in sortedContours)
            {
                var epsilon = 0.02 * Cv2.ArcLength(contour, true);
                var approx = Cv2.ApproxPolyDP(contour, epsilon, true);
                if (approx.Length == 4)
                {
                    selectedContour = approx;
                    break;
                }
            }
            return selectedContour;
        }

        private Mat CannyEdge(Mat image, float ratio)
        {
            var blur = new Mat();
            var edge = new Mat();
            image = image.Resize(new Size(), ratio, ratio);
            Cv2.GaussianBlur(image, blur, new Size(5, 5), 0);
            Cv2.Canny(blur, edge, 75, 200);
            return edge;
        }

        private Rect ResizeBoundRect(Rect boundRect, float ratio)
        {
            return new Rect(
                (int)(boundRect.X / ratio),
                (int)(boundRect.Y / ratio),
                (int)(boundRect.Width / ratio),
                (int)(boundRect.Height / ratio));
        }

        private IEnumerable<Point2f> RatioPoints(float ratio, int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4)
        {
            return new List<Point2f>
            {
                new Point2f(x1/ratio, y1/ratio),
                new Point2f(x2/ratio, y2/ratio),
                new Point2f(x3/ratio, y3/ratio),
                new Point2f(x4/ratio, y4/ratio)
            };
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

            return Math.Abs(area / 2);
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

            // File.Delete(file);
            return results;
        }

        private string DownloadFile(string picUrl)
        {
            var file = Path.Combine(Path.GetTempPath(), picUrl.GetHashCode() + ".jpg");
            new WebClient().DownloadFile(picUrl, file);
            return file;
        }
    }
}