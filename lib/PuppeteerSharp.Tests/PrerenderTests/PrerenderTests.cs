using System.Threading.Tasks;
using NUnit.Framework;
using PuppeteerSharp.Nunit;

namespace PuppeteerSharp.Tests.PrerenderTests;

public class PrerenderTests : PuppeteerPageBaseTest
{
    [Test, PuppeteerTest("prerender.spec", "Prerender", "can navigate to a prerendered page via input")]
    public async Task CanNavigateToAPrerenderedPageViaInput()
    {
        await Page.GoToAsync(TestConstants.ServerUrl + "/prerender/index.html");

        var button = await Page.WaitForSelectorAsync("button");
        await button.ClickAsync();

        var link = await Page.WaitForSelectorAsync("a");
        await Task.WhenAll(
            Page.WaitForNavigationAsync(),
            link.ClickAsync()
        );

        Assert.That(await Page.EvaluateExpressionAsync<string>("document.body.innerText"), Is.EqualTo("target"));
    }

    [Test, PuppeteerTest("prerender.spec", "Prerender", "can navigate to a prerendered page via Puppeteer")]
    public async Task CanNavigateToAPrerenderedPageViaPuppeteer()
    {
        await Page.GoToAsync(TestConstants.ServerUrl + "/prerender/index.html");

        var button = await Page.WaitForSelectorAsync("button");
        await button.ClickAsync();


        await Page.GoToAsync(TestConstants.ServerUrl + "/prerender/target.html");
        Assert.That(await Page.EvaluateExpressionAsync<string>("document.body.innerText"), Is.EqualTo("target"));
    }
}
