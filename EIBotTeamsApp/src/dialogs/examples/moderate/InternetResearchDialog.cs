using System.Configuration;
using System.Diagnostics;
using System.Linq;
using Microsoft.Teams.TemplateBotCSharp.Properties;
using System.Threading.Tasks;
using Microsoft.Bot.Connector.Teams;
using Microsoft.Bot.Connector.Teams.Models;

namespace Microsoft.Teams.TemplateBotCSharp.Dialogs
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Connector;

    /// <summary>
    /// This is Game Dialog Class. Here are the steps to play the games -
    ///  1. Its gives 3 options to users to choose.
    ///  2. If user choose any of the option, Bot take confirmation from the user about the choice.
    ///  3. Bot reply to the user based on user choice.
    /// </summary>
    [Serializable]
    public class InternetResearchDialog : IDialog<bool>
    {
        private static readonly Random Random = new Random();

        public static int GetRandomNumber(int min = 10000, int max = 500000)
        {
            lock (Random) // synchronize
            {
                return Random.Next(min, max);
            }
        }

        /// <summary>
        /// This is start of the Dialog and Prompting for User name
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task StartAsync(IDialogContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            //Set the Last Dialog in Conversation Data
            context.UserData.SetValue(Strings.LastDialogKey, Strings.LastDialogGameDialog);

            // This will Prompt for Name of the user.
            await context.PostAsync("Tell me what research I can help with. Please be as detailed as possible.");
            context.Wait(this.MessageReceivedAsync);
        }

        /// <summary>
        /// Prompt the welcome message. 
        /// Few options for user to choose any.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            if (result == null)
            {
                throw new InvalidOperationException((nameof(result)) + Strings.NullException);
            }

            //Prompt the user with welcome message before game starts
            var resultActivity = await result;
            await context.PostAsync($"I can help with {resultActivity.Text}.");

            // Store description
            context.ConversationData.SetValue("description", resultActivity.Text);

            await context.PostAsync($"When do you need this by?");

            // Prompt for delivery date
            var prompt = new DeadlinePrompt(GetCurrentCultureCode());
            context.Call(prompt, this.OnDeadlineSelected);
        }

        private async Task OnDeadlineSelected(IDialogContext context, IAwaitable<IEnumerable<DateTime>> result)
        {
            try
            {
                // "result" contains the date (or array of dates) returned from the prompt
                var momentOrRange = await result;
                var deadline = DeadlinePrompt.MomentOrRangeToString(momentOrRange);

                // Store date
                context.ConversationData.SetValue("deadline", deadline);

                var description = context.ConversationData.GetValue<string>("description");

                var fakeVsoTicketNumber = GetRandomNumber();
                var text = $"Thank you! I'll have {description} done by {deadline}. " +
                           $"Please reference {fakeVsoTicketNumber} ticket number for this task in future.";

                await context.PostAsync(text);

                var connectorClient = new ConnectorClient(new Uri(context.Activity.ServiceUrl));

                var channelInfo = GetChannelId(connectorClient, context, ConfigurationManager.AppSettings["ResearchAgentChannelName"]);
                var conversationResourceResponse = await CreateChannelConversation(channelInfo,
                    $"<b>Who</b>{context.Activity.Recipient.Name}<br/>" +
                    $"<b>What</b>{description}<br/>" +
                    $"<b>When</b>{deadline}<br/>" +
                    $"<b>VSO Ticket</b>{fakeVsoTicketNumber}",
                    $"New research request from {context.Activity.Recipient.Name}",
                    connectorClient);
            }
            catch (TooManyAttemptsException)
            {
                await context.PostAsync("TooManyAttemptsException. Restarting now...");
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
                throw;
            }
            finally
            {
                context.Done(false);
            }
        }

        private static ChannelInfo GetChannelId(ConnectorClient connectorClient, IDialogContext context, string channelName)
        {
            return new ChannelInfo("19:c20b196747424d8db51f6c00a8a9efa8 @thread.skype");

            //var teamInfo = context.Activity.GetChannelData<TeamsChannelData>().Team;
            //ConversationList channels = connectorClient.GetTeamsConnectorClient().Teams.FetchChannelList(teamInfo.Id);
            //var channelInfo = channels.Conversations.FirstOrDefault(c => c.Name.Equals(channelName));
            //if (channelInfo == null) throw new Exception($"{channelName} doesn't exist in {context.Activity.GetChannelData<TeamsChannelData>().Team.Name} Team");
            //return channelInfo;
        }

        private static string GetCurrentCultureCode()
        {
            // Use English as default culture since the this sample bot that does not include any localization resources
            // Thread.CurrentThread.CurrentUICulture.IetfLanguageTag.ToLower() can be used to obtain the user's preferred culture
            return "en-us";
        }

        private async Task<ConversationResourceResponse> CreateChannelConversation(ChannelInfo targetChannelInfo,
            string message,
            string topicName,
            ConnectorClient connector)
        {
            var channelData = new TeamsChannelData { Channel = targetChannelInfo };
            IMessageActivity newMessage = Activity.CreateMessageActivity();
            newMessage.Type = ActivityTypes.Message;
            newMessage.Text = message;
            ConversationParameters conversationParams = new ConversationParameters(
                isGroup: true,
                bot: null,
                members: null,
                topicName: topicName,
                activity: (Activity)newMessage,
                channelData: channelData);
            return await connector.Conversations.CreateConversationAsync(conversationParams);
        }
    }
}