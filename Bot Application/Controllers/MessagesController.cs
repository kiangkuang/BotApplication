using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Web.Http;
using Microsoft.Bot.Connector;
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
                System.Diagnostics.Debug.WriteLine(e);
                await Reply(activity, e.ToString());
            }

            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;
        }

        private async Task ProcessMessage(Activity activity)
        {
            if (activity.Attachments != null)
            {
                foreach (var attachment in activity.Attachments)
                {
                    await Reply(activity, string.Join("\n", Ocr(attachment.ContentUrl).Select(x => string.Join(" ", x)).ToList()));
                }
            }
            else
            {
                await Reply(activity, activity.Text);
            }
        }

        private async Task Reply(Activity activity, string text)
        {
            var connector = new ConnectorClient(new Uri(activity.ServiceUrl));
            await connector.Conversations.ReplyToActivityAsync(activity.CreateReply(text.Replace("\n", "\n\n")));
        }

        private IEnumerable<List<string>> Ocr(string picUrl)
        {
            var results = new List<List<string>>();
            var file = Path.Combine(Path.GetTempPath(), picUrl.GetHashCode().ToString());
            new WebClient().DownloadFile(picUrl, file);
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
                                // 2 digit and ascending
                                row.Add(num);
                            }
                        }
                        System.Diagnostics.Debug.WriteLine(iter.GetConfidence(PageIteratorLevel.TextLine));
                        results.Add(row);
                    } while (iter.Next(PageIteratorLevel.TextLine));
                }
            }
            File.Delete(file);
            return results;
        }
    }
}