using System;
using UiAutomation;

namespace UiAutomationGRPC.Client.Framework
{
    // Wrapper to allow the syntax: new UiAutomationAdapter(() => new Selector(...))
    // We need to inject the client somehow or make it context-aware.
    // For now, I'll pass the client via constructor to the Locator class and then to this adapter,
    // but to support the EXACT syntax "new UiAutomationAdapter(() => ...)" without passing client explicitly every time in the property syntax,
    // we might need a Factory or pass it. 
    // The user's example: public IAutomationElement CalendarButton => new UiAutomationAdapter(() => new Selector(...));
    // This implies UiAutomationAdapter might have a static context OR the class declaring it has context.
    
    // I will use a constructor that takes the client for now, which changes the user's exact syntax slightly but is safer.
    // OR create a base Locator class that holds the client.
    
    public class UiAutomationAdapter : RpcUiAutomationAdapter
    {
         public UiAutomationAdapter(UiAutomationService.UiAutomationServiceClient client, Func<BaseSelector> selectorFunc) 
            : base(client, selectorFunc)
        {
        }
    }
}
