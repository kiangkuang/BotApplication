using System;
using System.IO;
using System.Net;
using System.Net.Http;
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

            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;
        }

        private async Task ProcessMessage(Activity activity)
        {
            if (activity.Attachments != null)
            {
                foreach (var attachment in activity.Attachments)
                {
                    await Reply(activity, Ocr(attachment.ContentUrl));
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
            await connector.Conversations.ReplyToActivityAsync(activity.CreateReply(text));
        }

        private string Ocr(string picUrl)
        {
            try
            {
                var file = Path.GetTempPath() + picUrl.GetHashCode();
                new WebClient().DownloadFile(picUrl, file);
                using (var engine = new TesseractEngine(HostingEnvironment.MapPath(@"~/tessdata"), "eng"))
                using (var page = engine.Process(Pix.LoadFromFile(file)))
                {
                    var text = page.GetText();
                    Console.WriteLine("Mean confidence: {0}", page.GetMeanConfidence());
                    Console.WriteLine("Text (GetText): \r\n{0}", text);
                    File.Delete(file);
                    return text;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}