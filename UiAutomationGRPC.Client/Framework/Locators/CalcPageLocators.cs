using System;
using UiAutomation;

namespace UiAutomationGRPC.Client.Framework.Locators
{
    public class CalcPageLocators : ICalcPageLocators
    {
        private readonly UiAutomationService.UiAutomationServiceClient _client;

        public CalcPageLocators(UiAutomationService.UiAutomationServiceClient client)
        {
            _client = client;
        }

        private IAutomationElement CreateElement(Func<BaseSelector> selector)
        {
            return new UiAutomationAdapter(_client, selector);
        }

        private Selector Window => new Selector(new PropertyConditions().NameProperty("Calculator"));

        public IAutomationElement ButtonOne => CreateElement(() => 
            Window.Descendants(new PropertyConditions().AutomationIdProperty("num1Button")));

        public IAutomationElement ButtonTwo => CreateElement(() => 
             Window.Descendants(new PropertyConditions().AutomationIdProperty("num2Button")));

        public IAutomationElement ButtonPlus => CreateElement(() => 
             Window.Descendants(new PropertyConditions().AutomationIdProperty("plusButton")));

        public IAutomationElement ButtonEqual => CreateElement(() => 
             Window.Descendants(new PropertyConditions().AutomationIdProperty("equalButton")));

        public IAutomationElement ResultText => CreateElement(() => 
             Window.Descendants(new PropertyConditions().AutomationIdProperty("CalculatorResults")));
    }
}
