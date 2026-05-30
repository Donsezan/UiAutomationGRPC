using UiAutomationGRPC.Library;
using UiAutomationGRPC.Library.Selectors;
using UiAutomationGRPC.Library.Elements;

namespace UiAutomationGRPC.Client.Calc.Pages;

/// <summary>
/// Page Object for Calculator's hamburger navigation pane (opened from <see cref="CalcPage"/>).
/// Lists the calculator modes (Standard, Scientific, …) plus the footer Settings item.
/// Locators are real <c>NavigationViewItem</c> AutomationIds, verified live.
/// </summary>
public class CalcNavigationPage : BasePageObject<CalcNavigationPage>
{
    private readonly CalcNavigationLocators _locators;

    public CalcNavigationPage(UiAutomationDriver driver) : base(driver)
    {
        _locators = new CalcNavigationLocators(driver);
    }

    /// <summary>Waits until the pane is open (the Settings item is visible).</summary>
    public Task<CalcNavigationPage> WaitForReady() =>
        ResolveAsync(async () => { await _locators.SettingsItem.WaitForElementExistAsync(); return this; });

    /// <summary>Opens the Settings/About page and transitions to <see cref="CalcSettingsPage"/>.</summary>
    public Task<CalcSettingsPage> OpenSettings() =>
        ResolveAsync(async () =>
        {
            await _locators.SettingsItem.ClickAsync();
            return await new CalcSettingsPage(_driver).WaitForReady();
        });

    /// <summary>Switches to Scientific mode and returns to <see cref="CalcPage"/>.</summary>
    public Task<CalcPage> SwitchToScientific() =>
        ResolveAsync(async () =>
        {
            await _locators.Scientific.ClickAsync();
            return await new CalcPage(_driver).WaitForReady();
        });

    /// <summary>Switches to Standard mode and returns to <see cref="CalcPage"/>.</summary>
    public Task<CalcPage> SwitchToStandard() =>
        ResolveAsync(async () =>
        {
            await _locators.Standard.ClickAsync();
            return await new CalcPage(_driver).WaitForReady();
        });
}

public class CalcNavigationLocators
{
    private readonly UiAutomationDriver _driver;

    public CalcNavigationLocators(UiAutomationDriver driver) => _driver = driver;

    private Selector Window => new Selector(new PropertyConditions().NameProperty("Calculator"));

    private IAutomationElement E(string automationId) =>
        new UiElement(_driver, () => Window.Descendants(new PropertyConditions().AutomationIdProperty(automationId)));

    public IAutomationElement Standard => E("Standard");
    public IAutomationElement Scientific => E("Scientific");
    public IAutomationElement Programmer => E("Programmer");
    public IAutomationElement SettingsItem => E("SettingsItem");
}
