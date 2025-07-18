using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PuppeteerSharp.Helpers;
using PuppeteerSharp.Nunit;
using PuppeteerSharp.Transport;

namespace PuppeteerSharp.Tests.WaitTaskTests
{
    public sealed class FrameWaitForSelectorTests : PuppeteerPageBaseTest, IDisposable
    {
        private const string AddElement = "tag => document.body.appendChild(document.createElement(tag))";
        private PollerInterceptor _pollerInterceptor;

        public FrameWaitForSelectorTests()
        {
            DefaultOptions = TestConstants.DefaultBrowserOptions();

            // Set up a custom TransportFactory to intercept sent messages
            // Some of the tests require making assertions after a WaitForFunction has
            // started, but before it has resolved. We detect that reliably by
            // listening to the message that is sent to start polling.
            // This might not be an issue in upstream puppeteer.js, or may be highly unlikely,
            // due to differences between node.js's task scheduler and .net's.
            DefaultOptions.TransportFactory = async (url, options, cancellationToken) =>
            {
                _pollerInterceptor = new PollerInterceptor(await WebSocketTransport.DefaultTransportFactory(url, options, cancellationToken));
                return _pollerInterceptor;
            };
        }

        public void Dispose()
        {
            _pollerInterceptor.Dispose();
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should immediately resolve promise if node exists")]
        public async Task ShouldImmediatelyResolveTaskIfNodeExists()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            var frame = Page.MainFrame;
            await frame.WaitForSelectorAsync("*");
            await frame.EvaluateFunctionAsync(AddElement, "div");
            await frame.WaitForSelectorAsync("div");
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should work with removed MutationObserver")]
        public async Task ShouldWorkWithRemovedMutationObserver()
        {
            await Page.EvaluateExpressionAsync("delete window.MutationObserver");
            var waitForSelector = Page.WaitForSelectorAsync(".zombo");

            await Task.WhenAll(
                waitForSelector,
                Page.SetContentAsync("<div class='zombo'>anything</div>"));

            Assert.That(await Page.EvaluateFunctionAsync<string>("x => x.textContent", await waitForSelector), Is.EqualTo("anything"));
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should resolve promise when node is added")]
        public async Task ShouldResolveTaskWhenNodeIsAdded()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            var frame = Page.MainFrame;
            var watchdog = frame.WaitForSelectorAsync("div");
            await frame.EvaluateFunctionAsync(AddElement, "br");
            await frame.EvaluateFunctionAsync(AddElement, "div");
            var eHandle = await watchdog;
            var property = await eHandle.GetPropertyAsync("tagName");
            var tagName = await property.JsonValueAsync<string>();
            Assert.That(tagName, Is.EqualTo("DIV"));
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should work when node is added through innerHTML")]
        public async Task ShouldWorkWhenNodeIsAddedThroughInnerHtml()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            var watchdog = Page.WaitForSelectorAsync("h3 div");
            await Page.EvaluateFunctionAsync(AddElement, "span");
            await Page.EvaluateExpressionAsync("document.querySelector('span').innerHTML = '<h3><div></div></h3>'");
            await watchdog;
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "Page.waitForSelector is shortcut for main frame")]
        public async Task PageWaitForSelectorAsyncIsShortcutForMainFrame()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            await FrameUtils.AttachFrameAsync(Page, "frame1", TestConstants.EmptyPage);
            var otherFrame = Page.FirstChildFrame();
            var watchdog = Page.WaitForSelectorAsync("div");
            await otherFrame.EvaluateFunctionAsync(AddElement, "div");
            await Page.EvaluateFunctionAsync(AddElement, "div");
            var eHandle = await watchdog;
            Assert.That(eHandle.Frame, Is.EqualTo(Page.MainFrame));
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should run in specified frame")]
        public async Task ShouldRunInSpecifiedFrame()
        {
            await FrameUtils.AttachFrameAsync(Page, "frame1", TestConstants.EmptyPage);
            await FrameUtils.AttachFrameAsync(Page, "frame2", TestConstants.EmptyPage);
            var frame1 = Page.FirstChildFrame();
            var frame2 = Page.Frames.ElementAt(2);
            var waitForSelectorPromise = frame2.WaitForSelectorAsync("div");
            await frame1.EvaluateFunctionAsync(AddElement, "div");
            await frame2.EvaluateFunctionAsync(AddElement, "div");
            var eHandle = await waitForSelectorPromise;
            Assert.That(eHandle.Frame, Is.EqualTo(frame2));
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should throw when frame is detached")]
        public async Task ShouldThrowWhenFrameIsDetached()
        {
            await FrameUtils.AttachFrameAsync(Page, "frame1", TestConstants.EmptyPage);
            var frame = Page.FirstChildFrame();
            var waitTask = frame.WaitForSelectorAsync(".box");
            await FrameUtils.DetachFrameAsync(Page, "frame1");
            var waitException = Assert.ThrowsAsync<WaitTaskTimeoutException>(() => waitTask);

            Assert.That(waitException, Is.Not.Null);
            Assert.That(waitException.Message, Does.Contain("waitForFunction failed: frame got detached."));
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should survive cross-process navigation")]
        public async Task ShouldSurviveCrossProcessNavigation()
        {
            var boxFound = false;
            var waitForSelector = Page.WaitForSelectorAsync(".box").ContinueWith(_ => boxFound = true);
            await Page.GoToAsync(TestConstants.EmptyPage);
            Assert.That(boxFound, Is.False);
            await Page.ReloadAsync();
            Assert.That(boxFound, Is.False);
            await Page.GoToAsync(TestConstants.CrossProcessHttpPrefix + "/grid.html");
            await waitForSelector;
            Assert.That(boxFound, Is.True);
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should wait for element to be visible (display)")]
        public async Task ShouldWaitForVisibleDisplay()
        {
            var divFound = false;
            var waitForSelector = Page.WaitForSelectorAsync("div", new WaitForSelectorOptions { Visible = true })
                .ContinueWith(_ => divFound = true);
            await Page.SetContentAsync("<div style='display: none;'>text</div>");
            await Task.Delay(100);
            Assert.That(divFound, Is.False);
            await Page.EvaluateExpressionAsync("document.querySelector('div').style.removeProperty('display')");
            Assert.That(await waitForSelector, Is.True);
            Assert.That(divFound, Is.True);
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should wait for element to be visible (visibility)")]
        public async Task ShouldWaitForVisibleVisibility()
        {
            var divFound = false;
            var waitForSelector = Page.WaitForSelectorAsync("div", new WaitForSelectorOptions { Visible = true })
                .ContinueWith(_ => divFound = true);
            await Page.SetContentAsync("<div style='visibility: hidden;'>text</div>");
            await Task.Delay(100);
            Assert.That(divFound, Is.False);
            await Page.EvaluateExpressionAsync("document.querySelector('div').style.setProperty('visibility', 'collapse')");
            await Task.Delay(100);
            Assert.That(divFound, Is.False);
            await Page.EvaluateExpressionAsync("document.querySelector('div').style.removeProperty('visibility')");
            Assert.That(await waitForSelector, Is.True);
            Assert.That(divFound, Is.True);
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should wait for element to be visible (bounding box)")]
        public async Task ShouldWaitForVisibleBoundingBox()
        {
            var divFound = false;
            var _ = Page.WaitForSelectorAsync("div", new WaitForSelectorOptions { Visible = true })
                .ContinueWith(_ => divFound = true);
            await Page.SetContentAsync("<div style='width: 0;'>text</div>");
            await Task.Delay(100);
            Assert.That(divFound, Is.False);
            await Page.EvaluateFunctionAsync(@"() => {
                const div = document.querySelector('div');
                div.style.setProperty('height', '0');
                div.style.removeProperty('width');
            }");
            await Task.Delay(100);
            Assert.That(divFound, Is.False);
            await Page.EvaluateFunctionAsync(@"() => {
                const div = document.querySelector('div');
                div.style.removeProperty('height');
            }");
            await Task.Delay(100);
            Assert.That(divFound, Is.True);
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should wait for visible recursively")]
        public async Task ShouldWaitForVisibleRecursively()
        {
            var divVisible = false;
            var waitForSelector = Page.WaitForSelectorAsync("div#inner", new WaitForSelectorOptions { Visible = true })
                .ContinueWith(_ => divVisible = true);
            await Page.SetContentAsync("<div style='display: none; visibility: hidden;'><div id='inner'>hi</div></div>");
            Assert.That(divVisible, Is.False);
            await Page.EvaluateExpressionAsync("document.querySelector('div').style.removeProperty('display')");
            Assert.That(divVisible, Is.False);
            await Page.EvaluateExpressionAsync("document.querySelector('div').style.removeProperty('visibility')");
            Assert.That(await waitForSelector, Is.True);
            Assert.That(divVisible, Is.True);
        }

        [Test]
        [Retry(2)]
        [PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should wait for element to be hidden (visibility)")]
        [PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should wait for element to be hidden (display)")]
        [TestCase("visibility", "hidden")]
        [TestCase("display", "none")]
        public async Task HiddenShouldWaitForVisibility(string propertyName, string propertyValue)
        {
            var divHidden = false;
            await Page.SetContentAsync("<div style='display: block;'>text</div>");
            var waitForSelector = Page.WaitForSelectorAsync("div", new WaitForSelectorOptions { Hidden = true })
                .ContinueWith(_ => divHidden = true);
            await Page.WaitForSelectorAsync("div"); // do a round trip
            Assert.That(divHidden, Is.False);
            await Page.EvaluateExpressionAsync($"document.querySelector('div').style.setProperty('{propertyName}', '{propertyValue}')");
            Assert.That(await waitForSelector, Is.True);
            Assert.That(divHidden, Is.True);
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should wait for element to be hidden (removal) ")]
        public async Task HiddenShouldWaitForRemoval()
        {
            await Page.SetContentAsync("<div>text</div>");
            var divRemoved = false;
            var waitForSelector = Page.WaitForSelectorAsync("div", new WaitForSelectorOptions { Hidden = true })
                .ContinueWith(_ => divRemoved = true);
            await Page.WaitForSelectorAsync("div"); // do a round trip
            Assert.That(divRemoved, Is.False);
            await Page.EvaluateExpressionAsync("document.querySelector('div').remove()");
            Assert.That(await waitForSelector, Is.True);
            Assert.That(divRemoved, Is.True);
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should return null if waiting to hide non-existing element")]
        public async Task ShouldReturnNullIfWaitingToHideNonExistingElement()
        {
            var handle = await Page.WaitForSelectorAsync("non-existing", new WaitForSelectorOptions { Hidden = true });
            Assert.That(handle, Is.Null);
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should respect timeout")]
        public void XpathShouldRespectTimeout()
        {
            var exception = Assert.ThrowsAsync<WaitTaskTimeoutException>(async ()
                => await Page.WaitForSelectorAsync("div", new WaitForSelectorOptions { Timeout = 10 }));

            Assert.That(exception.Message, Does.Contain("Waiting for selector `div` failed: Waiting failed: 10ms exceeded"));
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should have an error message specifically for awaiting an element to be hidden")]
        public async Task ShouldHaveAnErrorMessageSpecificallyForAwaitingAnElementToBeHidden()
        {
            await Page.SetContentAsync("<div>text</div>");
            var exception = Assert.ThrowsAsync<WaitTaskTimeoutException>(async ()
                => await Page.WaitForSelectorAsync("div", new WaitForSelectorOptions { Hidden = true, Timeout = 10 }));

            Assert.That(exception.Message, Does.Contain("Waiting for selector `div` failed: Waiting failed: 10ms exceeded"));
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should respond to node attribute mutation")]
        public async Task ShouldRespondToNodeAttributeMutation()
        {
            var divFound = false;
            var waitForSelector = Page.WaitForSelectorAsync(".zombo").ContinueWith(_ => divFound = true);
            await Page.SetContentAsync("<div class='notZombo'></div>");
            Assert.That(divFound, Is.False);
            await Page.EvaluateExpressionAsync("document.querySelector('div').className = 'zombo'");
            Assert.That(await waitForSelector, Is.True);
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should return the element handle")]
        public async Task ShouldReturnTheElementHandle()
        {
            var waitForSelector = Page.WaitForSelectorAsync(".zombo");
            await Page.SetContentAsync("<div class='zombo'>anything</div>");
            Assert.That(await Page.EvaluateFunctionAsync<string>("x => x.textContent", await waitForSelector), Is.EqualTo("anything"));
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector", "should have correct stack trace for timeout")]
        public void ShouldHaveCorrectStackTraceForTimeout()
        {
            var exception = Assert.ThrowsAsync<WaitTaskTimeoutException>(async ()
                => await Page.WaitForSelectorAsync(".zombo", new WaitForSelectorOptions { Timeout = 10 }));
            Assert.That(exception.StackTrace, Does.Contain("WaitForSelectorTests"));
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector xpath", "should support some fancy xpath")]
        public async Task ShouldSupportSomeFancyXpath()
        {
            await Page.SetContentAsync("<p>red herring</p><p>hello  world  </p>");
            var waitForXPath = Page.WaitForSelectorAsync("xpath/.//p[normalize-space(.)=\"hello world\"]");
            Assert.That(await Page.EvaluateFunctionAsync<string>("x => x.textContent", await waitForXPath), Is.EqualTo("hello  world  "));
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector xpath", "should run in specified frame")]
        public async Task XpathShouldRunInSpecifiedFrame()
        {
            await FrameUtils.AttachFrameAsync(Page, "frame1", TestConstants.EmptyPage);
            await FrameUtils.AttachFrameAsync(Page, "frame2", TestConstants.EmptyPage);
            var frame1 = Page.ChildFrames().ElementAt(0);
            var frame2 = Page.ChildFrames().ElementAt(1);
            var waitForXPathPromise = frame2.WaitForSelectorAsync("xpath/.//div");
            await frame1.EvaluateFunctionAsync(AddElement, "div");
            await frame2.EvaluateFunctionAsync(AddElement, "div");
            var eHandle = await waitForXPathPromise;
            Assert.That(eHandle.Frame, Is.EqualTo(frame2));
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector xpath", "should throw when frame is detached")]
        public async Task XpathShouldThrowWhenFrameIsDetached()
        {
            await FrameUtils.AttachFrameAsync(Page, "frame1", TestConstants.EmptyPage);
            var frame = Page.FirstChildFrame();
            var waitPromise = frame.WaitForSelectorAsync("xpath/.//*[@class=\"box\"]");
            await FrameUtils.DetachFrameAsync(Page, "frame1");
            var exception = Assert.ThrowsAsync<WaitTaskTimeoutException>(() => waitPromise);
            Assert.That(exception.Message, Does.Contain("waitForFunction failed: frame got detached."));
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector xpath", "hidden should wait for display: none")]
        public async Task HiddenShouldWaitForDisplayNone()
        {
            var divHidden = false;
            var startedPolling = _pollerInterceptor.WaitForStartPollingAsync();
            await Page.SetContentAsync("<div style='display: block;'></div>");
            var waitForXPath = Page.WaitForSelectorAsync("xpath/.//div", new WaitForSelectorOptions { Hidden = true })
                .ContinueWith(_ => divHidden = true);
            await startedPolling;
            Assert.That(divHidden, Is.False);
            await Page.EvaluateExpressionAsync("document.querySelector('div').style.setProperty('display', 'none')");
            Assert.That(await waitForXPath.WithTimeout(), Is.True);
            Assert.That(divHidden, Is.True);
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector xpath", "should return the element handle")]
        public async Task XpathShouldReturnTheElementHandle()
        {
            var waitForXPath = Page.WaitForSelectorAsync("xpath/.//*[@class=\"zombo\"]");
            await Page.SetContentAsync("<div class='zombo'>anything</div>");
            Assert.That(await Page.EvaluateFunctionAsync<string>("x => x.textContent", await waitForXPath), Is.EqualTo("anything"));
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector xpath", "should allow you to select a text node")]
        public async Task ShouldAllowYouToSelectATextNode()
        {
            await Page.SetContentAsync("<div>some text</div>");
            var text = await Page.WaitForSelectorAsync("xpath/.//div/text()");
            Assert.That(await (await text.GetPropertyAsync("nodeType")).JsonValueAsync<int>(), Is.EqualTo(3 /* Node.TEXT_NODE */));
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector xpath", "should allow you to select an element with single slash")]
        public async Task ShouldAllowYouToSelectAnElementWithSingleSlash()
        {
            await Page.SetContentAsync("<div>some text</div>");
            var waitForXPath = Page.WaitForSelectorAsync("xpath/html/body/div");
            Assert.That(await Page.EvaluateFunctionAsync<string>("x => x.textContent", await waitForXPath), Is.EqualTo("some text"));
        }

        [Test, PuppeteerTest("waittask.spec", "waittask specs Frame.waitForSelector xpath", "should respect timeout")]
        public void ShouldRespectTimeout()
        {
            const int timeout = 10;

            var exception = Assert.ThrowsAsync<WaitTaskTimeoutException>(()
                    => Page.WaitForSelectorAsync("xpath/.//div", new WaitForSelectorOptions { Timeout = timeout }));

            Assert.That(exception.Message, Does.Contain($"Waiting failed: {timeout}ms exceeded"));
        }
    }
}
