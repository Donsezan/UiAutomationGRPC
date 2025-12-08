namespace UiAutomationGRPC.Client.Calc.Pages
{
    public abstract class BasePageObject<T> where T : BasePageObject<T>
    {
        protected readonly CalcPageLocators Locators;
    }
}
