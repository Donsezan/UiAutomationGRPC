using UiAutomationGRPC.Library;
using UiAutomationGRPC.Library.Selectors;
using UiAutomationGRPC.Library.Elements;

namespace UiAutomationGRPC.Client.Calc.Pages;

public class CalcPage : BasePageObject<CalcPage>
{    
    private readonly CalcPageLocators _locators;
    
    public CalcPage(UiAutomationDriver driver) : base(driver)
    {
        _driver = driver;
        _locators = new CalcPageLocators(driver);
    }

    /// <summary>
    /// Waits for the page to be ready. Call after construction.
    /// </summary>
    public Task<CalcPage> WaitForReady() =>
        ResolveAsync(async () => { await _locators.ResultText.WaitForElementExistAsync(); return this; });

    public CalcPage ClickTwo()
    {
        Enqueue(() => _locators.ButtonTwo.ClickAsync());
        return this;
    }

    public CalcPage ClickPlus()
    {
        Enqueue(() => _locators.ButtonPlus.ClickAsync());
        return this;
    }

    public CalcPage ClickEqual()
    {
        Enqueue(() => _locators.ButtonEqual.ClickAsync());
        return this;
    }

    public Task<string> GetResult() =>
        ResolveAsync(() => _locators.ResultText.NameAsync());

    public Task<CalcNavigationPaget<CalcPage>> ClickNavigationButton() =>
        ResolveAsync(async () => { await _locators.ResultText.NameAsync(); return new CalcNavigationPaget<CalcPage>(_driver, this); });

    public CalcPage ClickResultText()
    {
        Enqueue(() => _locators.ResultText.ClickAsync());
        return this;
    }

    /// <summary>
    /// Sends a key using VirtualKeyboard.
    /// </summary>
    public CalcPage SendKey(string key)
    {
        Enqueue(() => Keyboard.SendKeyAsync(key));
        return this;
    }
}

public class CalcPageLocators
{
    private readonly UiAutomationDriver _driver;

    public CalcPageLocators(UiAutomationDriver driver) => _driver = driver;

    private IAutomationElement CreateElement(Func<BaseSelector> selector) => new UiElement(_driver, selector);

    private Selector Window => new Selector(new PropertyConditions().NameProperty("Calculator"));

    private IAutomationElement E(string automationId) =>
        CreateElement(() => Window.Descendants(new PropertyConditions().AutomationIdProperty(automationId)));

    public IAutomationElement ButtonOne => E("num1Button");
    public IAutomationElement ButtonTwo => E("num2Button");
    public IAutomationElement ButtonPlus => E("plusButton");
    public IAutomationElement ButtonEqual => E("equalButton");
    public IAutomationElement ResultText => E("CalculatorResults");
    public IAutomationElement NavigationButton => CreateElement(() => Window.Descendants().ControlType("Button").NameContain("Close Navigation"));
}
