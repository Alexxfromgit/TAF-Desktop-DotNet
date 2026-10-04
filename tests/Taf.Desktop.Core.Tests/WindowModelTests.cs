using FlaUI.Core.Definitions;
using FluentAssertions;
using NUnit.Framework;
using Taf.Desktop.Core.Config;
using Taf.Desktop.Core.Failures;
using Taf.Desktop.Core.Ui;

namespace Taf.Desktop.Core.Tests;

public class WindowModelTests
{
    private FakeNode window = null!;
    private FakeNode productList = null!;

    [SetUp]
    public void BuildApp()
    {
        TestConfig.UseDefaults();
        productList = new FakeNode(ControlType.List, "ProductList").Add(
            new FakeNode(ControlType.ListItem, name: "Backpack - $29.99") { IsSelected = false },
            new FakeNode(ControlType.ListItem, name: "Bike Light - $9.99") { IsSelected = false });
        window = new FakeNode(ControlType.Window, "MainWindow", "Shop").Add(
            new FakeNode(ControlType.Menu, "MainMenu"),
            productList,
            new FakeNode(ControlType.Edit, "SearchBox") { Value = "" },
            new FakeNode(ControlType.Group, "Details").Add(new FakeNode(ControlType.Text, "Price", "$29.99")),
            new FakeNode(ControlType.Text, "Price", "$0.00 (outside the panel)"));
        UiRuntime.TopLevel = () => [window];
    }

    [TearDown]
    public void Restore() => TestConfig.UseDefaults();

    [Test]
    public void ElementsKnowTheirNameAndLocator()
    {
        var shop = WindowFactory.Create<ShopWindow>(UiRuntime.Windows);

        shop.Name.Should().Be("ShopWindow");
        shop.Search.Name.Should().Be("ShopWindow.search");
        shop.Search.Locator.ToString().Should().Be("AutomationId=SearchBox");
    }

    [Test]
    public void OnWaitsForIdentifiersAndReturnsTheWindow()
    {
        var shop = WindowFactory.On<ShopWindow>();

        shop.IsOpen.Should().BeTrue();
        shop.Title.Should().Be("Shop");
    }

    [Test]
    public void WindowNotReadyNamesTheMissingIdentifier()
    {
        window.Remove(productList);

        FluentActions.Invoking(WindowFactory.On<ShopWindow>).Should().Throw<WindowNotReadyException>()
            .WithMessage("ShopWindow is not ready after 300ms: no window matches [AutomationId=MainWindow] with productList inside");
    }

    [Test]
    public void WindowIsNotReadyWhileALoadingIndicatorIsVisible()
    {
        window.Add(new FakeNode(ControlType.ProgressBar, "Loading"));

        FluentActions.Invoking(WindowFactory.On<ShopWindow>).Should().Throw<WindowNotReadyException>()
            .WithMessage("*ShopWindow.loading is still visible [AutomationId=Loading]");
    }

    [Test]
    public void IdentifiersPickTheRightWindowAmongSimilarOnes()
    {
        var lookalike = new FakeNode(ControlType.Window, "MainWindow", "Lookalike without product list");
        UiRuntime.TopLevel = () => [lookalike, window];

        WindowFactory.On<ShopWindow>().Title.Should().Be("Shop");
    }

    [Test]
    public void ComponentsSearchInsideTheirRoot()
    {
        var shop = WindowFactory.On<ShopWindow>();

        shop.Details.Price.Text.Should().Be("$29.99");
        shop.Details.Price.Name.Should().Be("ShopWindow.details.price");
    }

    [Test]
    public void ListItemsAreIndexedComponents()
    {
        var shop = WindowFactory.On<ShopWindow>();

        shop.Rows.Count.Should().Be(2);
        shop.Rows[1].Title.Should().Be("Bike Light");
        shop.Rows.First(r => r.Title == "Bike Light", "named Bike Light").Name.Should().Be("ShopWindow.rows[1]");
    }

    [Test]
    public void MissingListItemNamesTheList() =>
        FluentActions.Invoking(() => WindowFactory.On<ShopWindow>().Rows.First(r => r.Title == "Hat", "named Hat"))
            .Should().Throw<ElementNotFoundException>().WithMessage("ShopWindow.rows: no item named Hat among 2 items [ControlType=ListItem]");

    [Test]
    public void MissingElementFailsWithNameTimeoutAndLocator() =>
        FluentActions.Invoking(() => WindowFactory.On<ShopWindow>().SignIn.Click())
            .Should().Throw<ElementNotFoundException>().WithMessage("ShopWindow.signIn is not visible after 300ms [AutomationId=SignIn]");

    [Test]
    public void TypeSetsTheValueOfPlainFields()
    {
        var box = (FakeNode)window.FindAll(Locator.ById("SearchBox"))[0];

        WindowFactory.On<ShopWindow>().Search.Type("backpack");

        box.Actions.Should().Equal("set:backpack");
    }

    [Test]
    public void PasswordFieldsThatRefuseValuesAreTypedKeyByKey()
    {
        var box = new FakeNode(ControlType.Edit, "PasswordBox") { Value = "", IsPassword = true, RejectsSetValue = true };
        window.Add(box);

        WindowFactory.On<ShopWindow>().Password.Type("s3cret");

        box.Actions.Should().Equal("set:s3cret", "type:s3cret");
    }

    [Test]
    public void FieldsWithoutValuePatternAreTyped()
    {
        var box = (FakeNode)window.FindAll(Locator.ById("SearchBox"))[0];
        box.Value = null;

        WindowFactory.On<ShopWindow>().Search.Type("backpack");

        box.Actions.Should().Equal("type:backpack");
    }

    [Test]
    public void CheckTogglesOnlyWhenNeeded()
    {
        var checkBox = new FakeNode(ControlType.CheckBox, "GiftWrap") { IsToggled = false };
        window.Add(checkBox);
        var shop = WindowFactory.On<ShopWindow>();

        shop.GiftWrap.Check();
        shop.GiftWrap.Check();

        checkBox.Actions.Should().Equal("toggle");
        shop.GiftWrap.IsChecked.Should().BeTrue();
    }

    [Test]
    public void SelectExpandsTheComboBoxSelectsTheItemAndCollapses()
    {
        var express = new FakeNode(ControlType.ListItem, name: "Express") { IsSelected = false };
        var combo = new FakeNode(ControlType.ComboBox, "Shipping") { IsExpanded = false };
        combo.OnExpand = c => c.Add(new FakeNode(ControlType.ListItem, name: "Standard") { IsSelected = true }, express);
        window.Add(combo);

        WindowFactory.On<ShopWindow>().Shipping.Select("Express");

        combo.Actions.Should().Equal("expand", "collapse");
        express.Actions.Should().Equal("select");
    }

    [Test]
    public void SelectListsTheAvailableItemsWhenTheItemIsMissing()
    {
        var combo = new FakeNode(ControlType.ComboBox, "Shipping") { IsExpanded = false };
        combo.OnExpand = c => c.Add(new FakeNode(ControlType.ListItem, name: "Standard"));
        window.Add(combo);

        FluentActions.Invoking(() => WindowFactory.On<ShopWindow>().Shipping.Select("Overnight"))
            .Should().Throw<ElementNotFoundException>().WithMessage("ShopWindow.shipping has no item 'Overnight'*[items: Standard]");
    }

    [Test]
    public void MenusOpenThroughPopupWindowsLikeWinForms()
    {
        var about = new FakeNode(ControlType.MenuItem, name: "About", frameworkId: "WinForm");
        var popup = new FakeNode(ControlType.Menu).Add(about);
        var help = new FakeNode(ControlType.MenuItem, name: "Help", frameworkId: "WinForm") { IsExpanded = false };
        help.OnExpand = _ => UiRuntime.TopLevel = () => [window, popup];
        window.FindAll(Locator.ById("MainMenu")).Cast<FakeNode>().Single().Add(help);

        WindowFactory.On<ShopWindow>().Menu.Open("Help", "About");

        help.Actions.Should().Equal("expand");
        about.Actions.Should().Equal("mouse");
    }

    [Test]
    public void AutoClickInvokesWpfElementsAndClicksWinFormsElementsWithTheMouse()
    {
        var wpf = new FakeNode(ControlType.Button, "SignIn");
        window.Add(wpf);
        WindowFactory.On<ShopWindow>().SignIn.Click();
        wpf.FrameworkId = "WinForm";
        WindowFactory.On<ShopWindow>().SignIn.Click();

        wpf.Actions.Should().Equal("invoke", "mouse");
    }

    [Test]
    public void ClickModeCanForceTheMouse()
    {
        var button = new FakeNode(ControlType.Button, "SignIn");
        window.Add(button);
        RuntimeOverrides.Set("Ui:ClickMode", "Mouse");

        WindowFactory.On<ShopWindow>().SignIn.Click();

        button.Actions.Should().Equal("mouse");
    }

    [Test]
    public void InvokeThatOpensAModalDialogDoesNotBlock()
    {
        var button = new FakeNode(ControlType.Button, "SignIn") { OnInvoke = _ => Thread.Sleep(2_000) };
        window.Add(button);
        var watch = System.Diagnostics.Stopwatch.StartNew();

        WindowFactory.On<ShopWindow>().SignIn.Click();

        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Test]
    public void DisabledElementIsAProductProblem()
    {
        window.Add(new FakeNode(ControlType.Button, "SignIn") { IsEnabled = false });

        FluentActions.Invoking(() => WindowFactory.On<ShopWindow>().SignIn.Click())
            .Should().Throw<PotentialDefectException>().WithMessage("ShopWindow.signIn is visible but not enabled after 300ms*");
    }

    [Test]
    public void WaitTextExplainsWhatItSaw() =>
        FluentActions.Invoking(() => WindowFactory.On<ShopWindow>().Details.Price.WaitText("$10.00"))
            .Should().Throw<PotentialDefectException>().WithMessage("ShopWindow.details.price shows '$10.00': still not the case after 300ms (text: '$29.99')");

    [Test]
    public void WindowsNeedAClassLevelLocate() =>
        FluentActions.Invoking(() => WindowFactory.Create<NotLocatedWindow>(UiRuntime.Windows))
            .Should().Throw<FrameworkException>().WithMessage("NotLocatedWindow needs a class-level [Locate]*");

    [Test]
    public void LocateNeedsExactlyOneStrategy() =>
        FluentActions.Invoking(() => WindowFactory.Create<AmbiguousWindow>(UiRuntime.Windows))
            .Should().Throw<FrameworkException>().WithMessage("AmbiguousWindow.both: [Locate] needs exactly one of*found 2");

    [Test]
    public void UiTreeDumpShowsWhatTheFrameworkSaw()
    {
        var xml = Evidence.UiTree.Dump([window]);

        xml.Should().Contain("<Window AutomationId=\"MainWindow\" Name=\"Shop\"").And.Contain("<ListItem Name=\"Bike Light - $9.99\"");
    }
}

public class LocatorTests
{
    [Test]
    public void ControlTypeAloneIsAStrategy() =>
        new LocateAttribute { ControlType = ControlType.List }.ToLocator("x").Should().Be(Locator.Of(ControlType.List));

    [Test]
    public void StrategyCanBeNarrowedByControlType()
    {
        var locator = new LocateAttribute { Name = "OK", ControlType = ControlType.Button }.ToLocator("x");

        locator.ToString().Should().Be("Name='OK', ControlType=Button");
        locator.Matches(new FakeNode(ControlType.Button, name: "OK")).Should().BeTrue();
        locator.Matches(new FakeNode(ControlType.Text, name: "OK")).Should().BeFalse();
    }

    [Test]
    public void NameContainsIgnoresCase() =>
        Locator.ByNameContaining("order").Matches(new FakeNode(ControlType.Text, name: "Order ORD-1 placed")).Should().BeTrue();

    [Test]
    public void XPathCannotBeCombinedWithControlType() =>
        FluentActions.Invoking(() => new LocateAttribute { XPath = "//Button", ControlType = ControlType.Button }.ToLocator("W.f"))
            .Should().Throw<FrameworkException>().WithMessage("W.f: [Locate] cannot combine XPath*");
}
