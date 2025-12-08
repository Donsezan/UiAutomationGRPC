using UiAutomation;

namespace UiAutomationGRPC.Client.Framework.Locators
{
    public class Locators : ILocators
    {
        public Locators(UiAutomationService.UiAutomationServiceClient client)
        {
            CalcPage = new CalcPageLocators(client);
        }

        public ICalcPageLocators CalcPage { get; }
    }
}
