using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Text.RegularExpressions;
using BloxFruitsScraper.Structs;

namespace BloxFruitsScraper;

public class GamersbergScraper : IDisposable
{
    private static readonly string BaseUrl = "https://www.gamersberg.com";
    private static readonly string StockUrl = $"{BaseUrl}/blox-fruits/stock";
    private static readonly Regex NumberRegex = new(@"\d"); // Matches any numbers
    private readonly IWebDriver _driver;

    public GamersbergScraper()
    {
        // Try preventing the browser from remaining open
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        Console.CancelKeyPress += OnCancelKeyPress;

        var options = new ChromeOptions();
        options.SetLoggingPreference(LogType.Browser, LogLevel.Off);
        options.SetLoggingPreference(LogType.Client, LogLevel.Off);
        options.SetLoggingPreference(LogType.Driver, LogLevel.Off);
        options.SetLoggingPreference(LogType.Performance, LogLevel.Off);
        options.SetLoggingPreference(LogType.Profiler, LogLevel.Off);
        options.SetLoggingPreference(LogType.Server, LogLevel.Off);
        options.AddArguments("log-level=3");
        options.AddArguments("disable-logging");

#if !DEBUG
        options.AddArguments("headless");
        options.AddArguments("no-sandbox");
        options.AddArguments("disable-dev-shm-usage");
#endif

        _driver = new ChromeDriver(options);
        _driver.Manage().Window.Size = new Size(1920, 1080);
    }

    public void Dispose()
    {
        _driver.Quit();
        _driver.Dispose();
    }

    private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        Dispose();
        Environment.Exit(0);
    }

    private void OnProcessExit(object sender, EventArgs e)
    {
        Dispose();
    }

    private static void ClickElement(IWebDriver driver, IWebElement element)
    {
        var builder = new Actions(driver);
        builder.MoveToElement(element, 0, 0).Perform();
        element.Click();
    }

    private static string FormatCategory(string categoryId)
    {
        var lastPart = categoryId.Split("-").Last();
        return char.ToUpper(lastPart[0]) + lastPart.Substring(1);
    }

    private static IEnumerable<Fruit> GetFruitsFromGrid(IWebDriver driver, HtmlNode gridNode, string url)
    {
        return gridNode.ChildNodes
            .Where(fruitNode => fruitNode.ChildNodes.Any())
            .Select(fruitNode =>
            {
                var imageNode = fruitNode.ChildNodes.FirstOrDefault(x => x.FirstChild.Name == "img");
                var imageElement = driver.FindElement(By.XPath(fruitNode.XPath)).FindElement(By.TagName("img"));
                var nameNode =
                    fruitNode.ChildNodes.FirstOrDefault(x => !x.Equals(imageNode) && !NumberRegex.IsMatch(x.InnerText));
                var priceNode = fruitNode.ChildNodes.FirstOrDefault(x =>
                    !x.Equals(imageNode) && !x.Equals(nameNode) && NumberRegex.IsMatch(x.InnerText));

                var fruit = new Fruit(
                    nameNode?.InnerText,
                    priceNode?.InnerText,
                    GetImgBytes(driver, imageElement));
                return fruit;
            });
    }

    private static byte[] GetImgBytes(IWebDriver driver, IWebElement imgElement)
    {
        try
        {
            Thread.Sleep(100); // Jank way to ensure render

            var script = @"
                var img = arguments[0];
                var canvas = document.createElement('canvas');
                canvas.width = img.naturalWidth;
                canvas.height = img.naturalHeight;

                var ctx = canvas.getContext('2d');
                ctx.drawImage(img, 0, 0);

                return canvas.toDataURL('image/png').substring(22);
            ";

            var base64Image = (string)((IJavaScriptExecutor)driver).ExecuteScript(script, imgElement);

            var imageBytes = Convert.FromBase64String(base64Image);
            return imageBytes;
        }
        catch
        {
        }

        return [];
    }

    private static HtmlNode? GetGridNode(HtmlDocument docNode)
    {
        return docNode.QuerySelectorAll("div.grid")
            .FirstOrDefault(x =>
                x.ChildNodes.Any(y =>
                    y.ChildNodes.Any(z => z.Name == "div" && z.ChildNodes.Any(e => e.Name == "img"))));
    }

    public async Task<ReadOnlyDictionary<string, List<Fruit>>?> GetFruits()
    {
        await _driver.Navigate().GoToUrlAsync(StockUrl);

        // Faded overlay that sometimes appears
        try
        {
            var clickElement = _driver.FindElement(
                By.CssSelector("div[data-state='open'][data-aria-hidden='true']"));

            ClickElement(_driver, clickElement);
        }
        catch
        {
        }

        var html = _driver.PageSource;
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var btnTabNodes = doc.DocumentNode.QuerySelectorAll("div button")
            .Where(x => x.Id.StartsWith("radix-:") && x.Id.Contains(":-trigger-") &&
                        x.Attributes.Any(y => y.Name == "role" && y.Value == "tab"));

        var fruitsList = new Dictionary<string, List<Fruit>>();

        foreach (var button in btnTabNodes)
        {
            var category = FormatCategory(button.Id);

            var buttonElement = _driver.FindElement(By.Id(button.Id));
            ClickElement(_driver, buttonElement);

            var currentDocument = new HtmlDocument();
            currentDocument.LoadHtml(_driver.PageSource);

            var grid = GetGridNode(currentDocument);
            if (grid == null) break;

            var fruits = GetFruitsFromGrid(_driver, grid, BaseUrl).ToList();
            fruitsList[category] = fruits.ToList();
        }

        return fruitsList.Count == 0 ? null : fruitsList.AsReadOnly();
    }
}