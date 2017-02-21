using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Bot.Connector;

namespace Bot_Application.Controllers
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        private readonly Toto _toto = new Toto();

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
                    await Reply(activity, _toto.Process(attachment.ContentUrl));
                }
            }
            else
            {
                await Reply(activity, activity.Text);
            }
        }

        private async Task Reply(Activity activity, string message)
        {
            var connector = new ConnectorClient(new Uri(activity.ServiceUrl));
            await connector.Conversations.ReplyToActivityAsync(activity.CreateReply(message.Replace("\n", "\n\n")));
        }

        private async Task Reply(Activity activity, IEnumerable<string> messages)
        {
            await Reply(activity, string.Join("\n", messages));
        }
    }
}