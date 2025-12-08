namespace UiAutomationGRPC.Client.Framework.Locators
{
    public interface ICalcPageLocators
    {
        IAutomationElement ButtonOne { get; }
        IAutomationElement ButtonTwo { get; }
        IAutomationElement ButtonPlus { get; }
        IAutomationElement ButtonEqual { get; }
        IAutomationElement ResultText { get; }
    }
}
