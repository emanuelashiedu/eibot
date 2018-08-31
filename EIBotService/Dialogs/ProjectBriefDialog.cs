using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.FormFlow;
using Microsoft.Bot.Connector;

namespace EIBot.Dialogs
{
    [Serializable]
    public class InternetResearchQuery
    {
        [Prompt("Please provide {&}. Be as detailed as possible")]
        public string Description { get; set; }

        [Prompt("When do you want this by?")]
        public DateTime Deadline { get; set; }
    }

    [Serializable]
    public class InternetResearchDialog : IDialog<object>
    {
        public async Task StartAsync(IDialogContext context)
        {
            await context.PostAsync("I can help you do internet research.");

            var hotelsFormDialog = FormDialog.FromForm(this.BuildInternetResearchForm, FormOptions.PromptInStart);

            context.Call(hotelsFormDialog, this.ResumeAfterInternetResearchForm);
        }

        private async Task ResumeAfterInternetResearchForm(IDialogContext context, IAwaitable<InternetResearchQuery> result)
        {
            try
            {
                var message = await result;
            }
            catch (Exception ex)
            {
                await context.PostAsync($"Failed with message: {ex.Message}");
            }
        }

        private IForm<InternetResearchQuery> BuildInternetResearchForm()
        {
            OnCompletionAsyncDelegate<InternetResearchQuery> process = async (context, state) =>
            {
                await context.PostAsync($"Thanks. Let me create an entry for this research project " +
                                        $"due on {state.Deadline}.");
            };

            return new FormBuilder<InternetResearchQuery>()
                .Field(nameof(InternetResearchQuery.Description))
                .Field(nameof(InternetResearchQuery.Deadline))
                .OnCompletion(process)
                .Build();
        }
    }

    [Serializable]
    public class ProjectBriefDialog : IDialog<object>
    {
        private const string InternetResearchOption = "Internet Research";

        private const string PowerpointImprovements = "Powerpoint Improvements";

        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);
        }

        public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;

            if (message.Text.ToLower().Contains("help") || message.Text.ToLower().Contains("support") || message.Text.ToLower().Contains("problem"))
            {
                await context.Forward(new SupportDialog(), this.ResumeAfterSupportDialog, message, CancellationToken.None);
            }
            else
            {
                await context.PostAsync("Hello, I am Expert Intelligence Bot. " +
                                        "I currently support 2 capabilities – Internet Research and Powerpoint Improvements. ");

                AskUserWhatTypeOfProjectItIs(context);
            }
        }

        private void AskUserWhatTypeOfProjectItIs(IDialogContext context)
        {
            PromptDialog.Choice(context,
                OneUserSelectingProjectType,
                new List<string>()
                {
                    InternetResearchOption,
                    PowerpointImprovements
                },
                "Are you looking for a research or powerpoint?",
                "Not a valid option",
                3);
        }

        private async Task OneUserSelectingProjectType(IDialogContext context, IAwaitable<string> result)
        {
            try
            {
                string optionSelected = await result;

                switch (optionSelected)
                {
                    case InternetResearchOption:
                        context.Call(new InternetResearchDialog(), ResumeAfterInternetResearchDialog);
                        break;

                    case PowerpointImprovements:
                        //Gracefully exit the dialog, because its implementing the IDialog<object>, so use 
                        context.Done<object>(null);
                        break;
                }
            }
            catch (TooManyAttemptsException ex)
            {
                await context.PostAsync($"Ooops! Too many attempts :(. But don't worry, I'm handling that exception and you can try again!");

                context.Wait(this.MessageReceivedAsync);
            }
        }

        private async Task ResumeAfterSupportDialog(IDialogContext context, IAwaitable<int> result)
        {
            var ticketNumber = await result;

            await context.PostAsync($"Thanks for contacting our support team. Your ticket number is {ticketNumber}.");
            context.Wait(this.MessageReceivedAsync);
        }

        private async Task ResumeAfterInternetResearchDialog(IDialogContext context, IAwaitable<object> result)
        {
            try
            {
                var message = await result;
            }
            catch (Exception ex)
            {
                await context.PostAsync($"Failed with message: {ex.Message}");
            }
            finally
            {
                context.Done<object>(null);
            }
        }
    }

    [Serializable]
    public class SupportDialog : IDialog<int>
    {
        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(this.MessageReceivedAsync);
        }

        public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;

            var ticketNumber = new Random().Next(0, 20000);

            await context.PostAsync($"Your message '{message.Text}' was registered. Once we resolve it; we will get back to you.");

            context.Done(ticketNumber);
        }
    }
}