using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PuppeteerSharp.Cdp.Messaging;
using PuppeteerSharp.Helpers;
using PuppeteerSharp.Nunit;

namespace PuppeteerSharp.Tests.OOPIFTests
{
    public class OOPIFTests : PuppeteerPageBaseTest
    {
        private static int _port = 21221;

        public OOPIFTests()
        {
            DefaultOptions = TestConstants.DefaultBrowserOptions();
            DefaultOptions.Args =
            [
                "--site-per-process",
                $"--remote-debugging-port={++_port}",
                "--host-rules=\"MAP * 127.0.0.1\""
            ];
        }

        [Test, PuppeteerTest("oopif.spec", "OOPIF", "should treat OOP iframes and normal iframes the same")]
        public async Task ShouldTreatOopIframesAndNormalIframesTheSame()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            var frameTask = Page.WaitForFrameAsync((frame) => frame.Url?.EndsWith("/empty.html") == true);
            await FrameUtils.AttachFrameAsync(Page, "frame1", TestConstants.EmptyPage);
            await FrameUtils.AttachFrameAsync(
              Page,
              "frame2",
              TestConstants.CrossProcessHttpPrefix + "/empty.html"
            );
            await frameTask.WithTimeout();
            Assert.That(Page.MainFrame.ChildFrames, Has.Count.EqualTo(2));
        }

        [Test, PuppeteerTest("oopif.spec", "OOPIF", "should track navigations within OOP iframes")]
        public async Task ShouldTrackNavigationsWithinOopIframes()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            var frameTask = Page.WaitForFrameAsync((frame) => frame != Page.MainFrame);
            await FrameUtils.AttachFrameAsync(
              Page,
              "frame1",
              TestConstants.CrossProcessHttpPrefix + "/empty.html"
            );
            var frame = await frameTask.WithTimeout();
            Assert.That(frame.Url, Does.Contain("/empty.html"));
            var nav = frame.WaitForNavigationAsync();
            await FrameUtils.NavigateFrameAsync(
              Page,
              "frame1",
              TestConstants.CrossProcessHttpPrefix + "/assets/frame.html"
            );
            await nav.WithTimeout();
            Assert.That(frame.Url, Does.Contain("/assets/frame.html"));
        }

        [Test, PuppeteerTest("oopif.spec", "OOPIF", "should support OOP iframes becoming normal iframes again")]
        public async Task ShouldSupportOopIframesBecomingNormalIframesAgain()
        {
            await Page.GoToAsync(TestConstants.EmptyPage).WithTimeout();
            var frameTask = Page.WaitForFrameAsync((frame) => frame != Page.MainFrame);
            await FrameUtils.AttachFrameAsync(Page, "frame1", TestConstants.EmptyPage).WithTimeout();
            var frame = await frameTask.WithTimeout();
            var nav = frame.WaitForNavigationAsync();
            await FrameUtils.NavigateFrameAsync(
              Page,
              "frame1",
              TestConstants.CrossProcessHttpPrefix + "/empty.html"
            ).WithTimeout();
            await nav.WithTimeout();
            nav = frame.WaitForNavigationAsync();
            await FrameUtils.NavigateFrameAsync(Page, "frame1", TestConstants.EmptyPage).WithTimeout();
            await nav.WithTimeout();
            Assert.That(Page.Frames, Has.Length.EqualTo(2));
        }

        [Test, PuppeteerTest("oopif.spec", "OOPIF", "should support frames within OOP frames")]
        public async Task ShouldSupportFramesWithinOopFrames()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            var frame1Task = Page.WaitForFrameAsync((frame) => frame != Page.MainFrame && frame.ParentFrame == Page.MainFrame);
            var frame2Task = Page.WaitForFrameAsync((frame) => frame != Page.MainFrame && frame.ParentFrame != Page.MainFrame);

            await FrameUtils.AttachFrameAsync(
              Page,
              "frame1",
              TestConstants.CrossProcessHttpPrefix + "/frames/one-frame.html"
            );

            var frame1 = await frame1Task;
            var frame2 = await frame2Task;

            Assert.That(await frame1.EvaluateExpressionAsync<string>("document.location.href"), Does.Contain("one-frame"));
            Assert.That(await frame2.EvaluateExpressionAsync<string>("document.location.href"), Does.Contain("frame.html"));
        }

        [Test, PuppeteerTest("oopif.spec", "OOPIF", "should recover cross-origin frames on reconnect")]
        public async Task ShouldRecoverCrossOriginFramesOnReconnect()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            var frame1Task = Page.WaitForFrameAsync((frame) => frame != Page.MainFrame && frame.ParentFrame == Page.MainFrame);
            var frame2Task = Page.WaitForFrameAsync((frame) => frame != Page.MainFrame && frame.ParentFrame != Page.MainFrame);

            await FrameUtils.AttachFrameAsync(
                Page,
                "frame1",
                TestConstants.CrossProcessHttpPrefix + "/frames/one-frame.html"
            );

            await Task.WhenAll(frame1Task, frame2Task);
            var dump1 = await FrameUtils.DumpFramesAsync(Page.MainFrame);

            await using var browserTwo = await Puppeteer.ConnectAsync(new ConnectOptions()
            {
                BrowserWSEndpoint = Browser.WebSocketEndpoint,
            });

            var pages = await browserTwo.PagesAsync();
            var emptyPages = pages.Where(page => page.Url == TestConstants.EmptyPage).ToArray();
            Assert.That(emptyPages, Has.Length.EqualTo(1));
            var dump2 = await FrameUtils.DumpFramesAsync(emptyPages[0].MainFrame);
            Assert.That(dump2, Is.EqualTo(dump1));
        }

        [Test, PuppeteerTest("oopif.spec", "OOPIF", "should support OOP iframes getting detached")]
        public async Task ShouldSupportOopIframesGettingDetached()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            var frameTask = Page.WaitForFrameAsync((frame) => frame != Page.MainFrame);
            await FrameUtils.AttachFrameAsync(Page, "frame1", TestConstants.EmptyPage).WithTimeout();
            var frame = await frameTask.WithTimeout();
            await FrameUtils.NavigateFrameAsync(
              Page,
              "frame1",
              TestConstants.CrossProcessHttpPrefix + "/empty.html"
            ).WithTimeout();
            var detachedTcs = new TaskCompletionSource<bool>();
            Page.FrameDetached += (sender, e) => detachedTcs.TrySetResult(true);
            await FrameUtils.DetachFrameAsync(Page, "frame1").WithTimeout();
            await detachedTcs.Task.WithTimeout();
            Assert.That(Page.Frames, Has.Exactly(1).Items);
        }

        [Test, PuppeteerTest("oopif.spec", "OOPIF", "should support wait for navigation for transitions from local to OOPIF")]
        public async Task ShouldSupportWaitForNavigationForTransitionsFromLocalToOopif()
        {
            await Page.GoToAsync(TestConstants.EmptyPage).WithTimeout();
            var frameTask = Page.WaitForFrameAsync((frame) => frame != Page.MainFrame);
            await FrameUtils.AttachFrameAsync(Page, "frame1", TestConstants.EmptyPage).WithTimeout();
            var frame = await frameTask.WithTimeout();
            var nav = frame.WaitForNavigationAsync();
            await FrameUtils.NavigateFrameAsync(
              Page,
              "frame1",
              TestConstants.CrossProcessHttpPrefix + "/empty.html"
            ).WithTimeout();
            await nav.WithTimeout();
            var detachedTcs = new TaskCompletionSource<bool>();
            Page.FrameDetached += (sender, e) => detachedTcs.TrySetResult(true);
            await FrameUtils.DetachFrameAsync(Page, "frame1").WithTimeout();
            await detachedTcs.Task.WithTimeout();
            Assert.That(Page.Frames, Has.Exactly(1).Items);
        }

        [Test, PuppeteerTest("oopif.spec", "OOPIF", "should keep track of a frames OOP state")]
        public async Task ShouldKeepTrackOfAFramesOopState()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            var frameTask = Page.WaitForFrameAsync((frame) => frame != Page.MainFrame);
            await FrameUtils.AttachFrameAsync(
              Page,
              "frame1",
              TestConstants.CrossProcessHttpPrefix + "/empty.html"
            );
            var frame = await frameTask.WithTimeout();
            Assert.That(frame.Url, Does.Contain("/empty.html"));
            await FrameUtils.NavigateFrameAsync(Page, "frame1", TestConstants.EmptyPage);
            Assert.That(frame.Url, Is.EqualTo(TestConstants.EmptyPage));
        }

        [Test, PuppeteerTest("oopif.spec", "OOPIF", "should support evaluating in oop iframes")]
        public async Task ShouldSupportEvaluatingInOopIframes()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            var frameTask = Page.WaitForFrameAsync((frame) => frame != Page.MainFrame);
            await FrameUtils.AttachFrameAsync(
              Page,
              "frame1",
              TestConstants.CrossProcessHttpPrefix + "/empty.html"
            );
            var frame = await frameTask.WithTimeout();
            await frame.EvaluateFunctionAsync("() => _test = 'Test 123!'");
            var result = await frame.EvaluateFunctionAsync<string>("() => window._test");
            Assert.That(result, Is.EqualTo("Test 123!"));
        }

        [Test, PuppeteerTest("oopif.spec", "OOPIF", "should provide access to elements")]
        public async Task ShouldProvideAccessToElements()
        {
            if (PuppeteerTestAttribute.Headless is HeadlessMode.False or HeadlessMode.True)
            {
                // TODO: this test is partially blocked on crbug.com/1334119. Enable test once
                // the upstream is fixed.
                // TLDR: when we dispatch events ot the frame the compositor might
                // not be up-to-date yet resulting in a misclick (the iframe element
                // becomes the event target instead of the content inside the iframe).
                // The solution is to use InsertVisualCallback on the backend but that causes
                // another issue that events cannot be dispatched to inactive tabs as the
                // visual callback is never invoked.
                // The old headless mode does not have this issue since it operates with
                // special scheduling settings that keep even inactive tabs updating.
                return;
            }

            await Page.GoToAsync(TestConstants.EmptyPage);
            var frameTask = Page.WaitForFrameAsync((frame) => frame != Page.MainFrame);
            await FrameUtils.AttachFrameAsync(
              Page,
              "frame1",
              TestConstants.CrossProcessHttpPrefix + "/empty.html"
            );
            var frame = await frameTask.WithTimeout();
            await frame.EvaluateFunctionAsync(@"() => {
                const button = document.createElement('button');
                button.id = 'test-button';
                button.innerText = 'click';
                button.onclick = () => {
                    button.id = 'clicked';
                };
                document.body.appendChild(button);
            }");

            await Page.EvaluateFunctionAsync(@"() => {
                document.body.style.border = '150px solid black';
                document.body.style.margin = '250px';
                document.body.style.padding = '50px';
            }");
            await frame.WaitForSelectorAsync("#test-button", new WaitForSelectorOptions { Visible = true });
            await frame.ClickAsync("#test-button");
            await frame.WaitForSelectorAsync("#clicked");
        }

        [Test, PuppeteerTest("oopif.spec", "OOPIF", "should report oopif frames")]
        public async Task ShouldReportOopifFrames()
        {
            var frameTask = Page.WaitForFrameAsync((frame) => frame.Url.EndsWith("oopif.html"));
            await Page.GoToAsync($"http://mainframe:{TestConstants.Port}/dynamic-oopif.html");
            var frame = await frameTask.WithTimeout();
            Assert.That((await GetIframesAsync()), Has.Length.EqualTo(1));
            Assert.That(Page.Frames, Has.Length.EqualTo(2));
        }

        [Test, PuppeteerTest("oopif.spec", "OOPIF", "should wait for inner OOPIFs")]
        public async Task ShouldWaitForInnerOopifs()
        {
            var frameTask = Page.WaitForFrameAsync((frame) => frame.Url.EndsWith("inner-frame2.html"));
            await Page.GoToAsync($"http://mainframe:{TestConstants.Port}/main-frame.html");
            var frame = await frameTask.WithTimeout();
            Assert.That((await GetIframesAsync()), Has.Length.EqualTo(2));
            Assert.That(await frame.EvaluateFunctionAsync<int>("() => document.querySelectorAll('button').length"), Is.EqualTo(1));
        }

        [Test, PuppeteerTest("oopif.spec", "OOPIF", "should load oopif iframes with subresources and request interception")]
        public async Task ShouldLoadOopifIframesWithSubresourcesAndRequestInterception()
        {
            var frameTask = Page.WaitForFrameAsync((frame) => frame.Url.EndsWith("/oopif.html"));
            await Page.SetRequestInterceptionAsync(true);
            Page.Request += (sender, e) => _ = e.Request.ContinueAsync();
            var requestTask = Page.WaitForRequestAsync(request => request.Url.Contains("requestFromOOPIF"));
            await Page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html");
            await frameTask.WithTimeout();
            await requestTask.WithTimeout();
            Assert.That(GetIframesAsync, Has.Exactly(1).Items);
            Assert.That(frameTask.Result, Is.EqualTo(requestTask.Result.Frame));
        }

        [Test, PuppeteerTest("oopif.spec", "OOPIF", "should support frames within OOP iframes")]
        public async Task ShouldSupportFramesWithinOopIframes()
        {
            var oopifFrameTask = Page.WaitForFrameAsync((frame) => frame.Url.EndsWith("/oopif.html"));
            await Page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html");
            var oopIframe = await oopifFrameTask.WithTimeout();
            await FrameUtils.AttachFrameAsync(
              oopIframe,
              "frame1",
              TestConstants.CrossProcessHttpPrefix + "/empty.html"
            ).WithTimeout(2_000);
            var frame1 = oopIframe.ChildFrames.First();
            Assert.That(frame1.Url, Does.Contain("empty.html"));
            await FrameUtils.NavigateFrameAsync(
              oopIframe,
              "frame1",
              TestConstants.CrossProcessHttpPrefix + "/oopif.html"
            ).WithTimeout();
            Assert.That(frame1.Url, Does.Contain("oopif.html"));
            await frame1.GoToAsync(
                TestConstants.CrossProcessHttpPrefix + "/oopif.html#navigate-within-document",
                new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Load } }).WithTimeout();
            Assert.That(frame1.Url, Does.Contain("/oopif.html#navigate-within-document"));
            var detachedTcs = new TaskCompletionSource<bool>();
            Page.FrameDetached += (sender, e) => detachedTcs.TrySetResult(true);
            await FrameUtils.DetachFrameAsync(oopIframe, "frame1").WithTimeout();
            await detachedTcs.Task.WithTimeout();
            Assert.That(oopIframe.ChildFrames, Is.Empty);
        }

        [Test, PuppeteerTest("oopif.spec", "OOPIF", "clickablePoint, boundingBox, boxModel should work for elements inside OOPIFs")]
        public async Task ClickablePointBoundingBoxBoxModelShouldWorkForElementsInsideOopifs()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            var frameTask = Page.WaitForFrameAsync((frame) => frame != Page.MainFrame);
            await FrameUtils.AttachFrameAsync(
              Page,
              "frame1",
              TestConstants.CrossProcessHttpPrefix + "/empty.html"
            ).WithTimeout();
            var frame = await frameTask.WithTimeout();
            await Page.EvaluateFunctionAsync(@"() => {
                document.body.style.border = '50px solid black';
                document.body.style.margin = '50px';
                document.body.style.padding = '50px';
            }");
            await frame.EvaluateFunctionAsync(@"() => {
                const button = document.createElement('button');
                button.id = 'test-button';
                button.innerText = 'click';
                document.body.appendChild(button);
            }");

            var button = await frame.WaitForSelectorAsync("#test-button", new WaitForSelectorOptions { Visible = true });
            var result = await button.ClickablePointAsync();
            Assert.That(result.X, Is.GreaterThan(150)); // padding + margin + border left
            Assert.That(result.Y, Is.GreaterThan(150)); // padding + margin + border top
            var resultBoxModel = await button.BoxModelAsync();
            foreach (var quad in new[] {
              resultBoxModel.Content,
              resultBoxModel.Border,
              resultBoxModel.Margin,
              resultBoxModel.Padding})
            {
                foreach (var part in quad)
                {
                    Assert.That(part.X, Is.GreaterThan(150)); // padding + margin + border left
                    Assert.That(part.Y, Is.GreaterThan(150)); // padding + margin + border top
                }
            }
            var resultBoundingBox = await button.BoundingBoxAsync();
            Assert.That(resultBoundingBox.X, Is.GreaterThan(150)); // padding + margin + border left
            Assert.That(resultBoundingBox.Y, Is.GreaterThan(150)); // padding + margin + border top
        }

        [Test, PuppeteerTest("oopif.spec", "OOPIF", "should detect existing OOPIFs when Puppeteer connects to an existing page")]
        public async Task ShouldDetectExistingOopifsWhenPuppeteerConnectsToAnExistingPage()
        {
            var frameTask = Page.WaitForFrameAsync((frame) => frame.Url.EndsWith("/oopif.html"));
            await Page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html");
            await frameTask.WithTimeout();
            Assert.That(GetIframesAsync, Has.Exactly(1).Items);
            Assert.That(Page.Frames, Has.Length.EqualTo(2));

            var browserURL = $"http://127.0.0.1:{_port}";
            var browser1 = await Puppeteer.ConnectAsync(new() { BrowserURL = browserURL }, TestConstants.LoggerFactory);
            var target = await browser1.WaitForTargetAsync((target) =>
              target.Url.EndsWith("dynamic-oopif.html")
            ).WithTimeout();
            await target.PageAsync();
            browser1.Disconnect();
        }

        [Test, PuppeteerTest("oopif.spec", "OOPIF", "should support lazy OOP frames")]
        public async Task ShouldSupportLazyOopframes()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/lazy-oopif-frame.html");
            await Page.SetViewportAsync(new ViewPortOptions() { Width = 1000, Height = 1000 });
            Assert.That(Page.Frames.Where(frame => !((Frame)frame).HasStartedLoading), Has.Exactly(1).Items);
        }

        [Test, PuppeteerTest("oopif.spec", "waitForFrame", "should resolve immediately if the frame already exists")]
        public async Task ShouldResolveImmediatelyIfTheFrameAlreadyExists()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            await FrameUtils.AttachFrameAsync(Page, "frame2", TestConstants.CrossProcessHttpPrefix + "/empty.html");
            await Page.WaitForFrameAsync(frame => frame.Url.EndsWith("/empty.html"));
        }


        [Test, PuppeteerTest("oopif.spec", "waitForFrame", "OOPIF: should expose events within OOPIFs")]
        public async Task OOPIFShouldExposeEventsWithinOOPIFs()
        {
            // Setup our session listeners to observe OOPIF activity.
            var session = await Page.CreateCDPSessionAsync();
            var networkEvents = new List<string>();
            var otherSessions = new List<ICDPSession>();

            await session.SendAsync("Target.setAutoAttach", new TargetSetAutoAttachRequest
            {
                AutoAttach = true,
                Flatten = true,
                WaitForDebuggerOnStart = true,
            });

            ((CDPSession)session).SessionAttached += async (_, e) =>
            {
                otherSessions.Add(e.Session);

                await e.Session.SendAsync("Network.enable");
                await e.Session.SendAsync("Runtime.runIfWaitingForDebugger");

                e.Session.MessageReceived += (_, e) =>
                {
                    if (e.MessageID.Equals("Network.requestWillBeSent", StringComparison.CurrentCultureIgnoreCase))
                    {
                        networkEvents.Add(e.MessageData.GetProperty("request").GetProperty("url").GetString());
                    }
                };
            };

            // Navigate to the empty page and add an OOPIF iframe with at least one request.
            await Page.GoToAsync(TestConstants.EmptyPage);
            await Page.EvaluateFunctionAsync(@"(frameUrl) => {
                    const frame = document.createElement('iframe');
                    frame.setAttribute('src', frameUrl);
                    document.body.appendChild(frame);
                    return new Promise((x, y) => {
                        frame.onload = x;
                        frame.onerror = y;
                    });
                }", TestConstants.ServerUrl.Replace("localhost", "oopifdomain") + "/one-style.html");
            await Page.WaitForSelectorAsync("iframe");

            // Ensure we found the iframe session.
            Assert.That(otherSessions, Has.Exactly(1).Items);

            // Resume the iframe and trigger another request.
            var iframeSession = otherSessions[0];
            await iframeSession.SendAsync("Runtime.evaluate", new RuntimeEvaluateRequest
            {
                Expression = "fetch('/fetch')",
                AwaitPromise = true,
            });

            Assert.That(networkEvents, Does.Contain($"http://oopifdomain:{TestConstants.Port}/fetch"));
        }

        [Test, PuppeteerTest("oopif.spec", "OOPIF waitForFrame", "should report google.com frame")]
        public async Task OOPIFShouldReportGoogleComFrame()
        {
            // https://google.com is isolated by default in Chromium embedder.
            var headfulOptions = TestConstants.DefaultBrowserOptions();
            headfulOptions.Headless = false;
            await using var browser = await Puppeteer.LaunchAsync(headfulOptions);
            await using var page = await browser.NewPageAsync();
            await page.GoToAsync(TestConstants.EmptyPage);
            await page.SetRequestInterceptionAsync(true);
            page.Request += async (_, e) => await e.Request.RespondAsync(
                new ResponseData { Body = "{ body: 'YO, GOOGLE.COM'}" });
            await page.EvaluateFunctionHandleAsync(@"() => {
                    const frame = document.createElement('iframe');
                    frame.setAttribute('src', 'https://google.com/');
                    document.body.appendChild(frame);
                    return new Promise(x => frame.onload = x);
                }");
            await page.WaitForSelectorAsync("iframe[src=\"https://google.com/\"]");
            var urls = Array.ConvertAll(page.Frames, frame => frame.Url);
            Array.Sort((Array)urls);
            Assert.That(urls, Is.EqualTo(new[] { TestConstants.EmptyPage, "https://google.com/" }));
        }

        private async Task<ElementHandle[]> GetIframesAsync()
        {
            var frameElements = await Task.WhenAll(Page.Frames.Select(frame => ((Frame)frame).FrameElementAsync()).ToArray());
            return frameElements.Where(element => element != null).ToArray();
        }
    }
}
