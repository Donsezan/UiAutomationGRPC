using System;
using UiAutomation;
using UiAutomationGRPC.Library.Helpers;

namespace UiAutomationGRPC.Client.Calc.Pages
{
    public abstract class BasePageObject<T> where T : BasePageObject<T>
    {
        protected readonly CalcPageLocators Locators;
        public UiAutomationService.UiAutomationServiceClient _client;
        protected BasePageObject(UiAutomationService.UiAutomationServiceClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            KeyboardHelper.Init(_client);
        }
    }
}
