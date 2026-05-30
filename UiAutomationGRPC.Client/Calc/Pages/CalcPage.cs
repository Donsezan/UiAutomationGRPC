using UiAutomationGRPC.Library;
using UiAutomationGRPC.Library.Selectors;
using UiAutomationGRPC.Library.Elements;

namespace UiAutomationGRPC.Client.Calc.Pages;

/// <summary>
/// Page Object for the main Calculator screen (Standard mode).
/// Action methods enqueue work and return <c>this</c> for fluent chaining; the queue is flushed when a
/// value-returning or page-transitioning method is awaited (see <see cref="BasePageObject{T}"/>).
/// All locators are real Windows Calculator AutomationIds (verified live via GetAppStructure).
/// </summary>
public class CalcPage : BasePageObject<CalcPage>
{
    private readonly CalcPageLocators _locators;

    public CalcPage(UiAutomationDriver driver) : base(driver)
    {
        _locators = new CalcPageLocators(driver);
    }

    /// <summary>Waits until the result display is present. Call right after construction.</summary>
    public Task<CalcPage> WaitForReady() =>
        ResolveAsync(async () => { await _locators.ResultText.WaitForElementExistAsync(); return this; });

    public CalcPage ClickOne() { Enqueue(() => _locators.ButtonOne.ClickAsync()); return this; }
    public CalcPage ClickTwo() { Enqueue(() => _locators.ButtonTwo.ClickAsync()); return this; }
    public CalcPage ClickPlus() { Enqueue(() => _locators.ButtonPlus.ClickAsync()); return this; }
    public CalcPage ClickEqual() { Enqueue(() => _locators.ButtonEqual.ClickAsync()); return this; }

    /// <summary>Reads the result display, e.g. "Display is 4".</summary>
    public Task<string> GetResult() =>
        ResolveAsync(() => _locators.ResultText.NameAsync());

    /// <summary>Reads the mode header, e.g. "Standard Calculator mode" / "Scientific Calculator mode".</summary>
    public Task<string> GetMode() =>
        ResolveAsync(() => _locators.ModeHeader.NameAsync());

    /// <summary>
    /// Opens the hamburger navigation pane and transitions to <see cref="CalcNavigationPage"/>.
    /// </summary>
    public Task<CalcNavigationPage> OpenNavigation() =>
        ResolveAsync(async () =>
        {
            await _locators.NavToggle.ClickAsync();
            return await new CalcNavigationPage(_driver).WaitForReady();
        });
}

/// <summary>
/// Locators for the Calculator screen. Keeping *how to find* elements here (separate from *what to do*
/// with them in <see cref="CalcPage"/>) is the core of the Page Object Model.
/// </summary>
public class CalcPageLocators
{
    private readonly UiAutomationDriver _driver;

    public CalcPageLocators(UiAutomationDriver driver) => _driver = driver;

    private IAutomationElement CreateElement(Func<BaseSelector> selector) => new UiElement(_driver, selector);

    private Selector Window => new Selector(new PropertyConditions().NameProperty("Calculator"));

    /// <summary>A descendant of the Calculator window matched by its AutomationId.</summary>
    private IAutomationElement E(string automationId) =>
        CreateElement(() => Window.Descendants(new PropertyConditions().AutomationIdProperty(automationId)));

    public IAutomationElement ButtonOne => E("num1Button");
    public IAutomationElement ButtonTwo => E("num2Button");
    public IAutomationElement ButtonPlus => E("plusButton");
    public IAutomationElement ButtonEqual => E("equalButton");
    public IAutomationElement ResultText => E("CalculatorResults");
    public IAutomationElement ModeHeader => E("Header");
    public IAutomationElement NavToggle => E("TogglePaneButton");
}
