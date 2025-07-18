using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PuppeteerSharp.Nunit;

namespace PuppeteerSharp.Tests.PageTests
{
    public class WaitForNetworkIdleTests : PuppeteerPageBaseTest
    {
        public WaitForNetworkIdleTests() : base()
        {
        }

        [Test, PuppeteerTest("page.spec", "Page Page.waitForNetworkIdle", "should work")]
        public async Task ShouldWork()
        {
            var t1 = DateTime.UtcNow;
            var t2 = DateTime.UtcNow;

            await Page.GoToAsync(TestConstants.EmptyPage);
            var task = Page.WaitForNetworkIdleAsync()
                .ContinueWith(x =>
                {
                    if (x.IsFaulted) throw x.Exception;
                    return t1 = DateTime.UtcNow;
                });

            await Task.WhenAll(
                task,
                Page.EvaluateFunctionAsync(@"async () => {
                    await Promise.all([
                            fetch('/digits/1.png'),
                            fetch('/digits/2.png'),
                        ]);
                    await new Promise((resolve) => setTimeout(resolve, 200));
                    await fetch('/digits/3.png');
                    await new Promise((resolve) => setTimeout(resolve, 200));
                    await fetch('/digits/4.png');
                }").ContinueWith(x =>
                {
                    if (x.IsFaulted) throw x.Exception;
                    t2 = DateTime.UtcNow;
                })
            );

            Assert.That(t1, Is.GreaterThan(t2));
            Assert.That((t1 - t2).TotalMilliseconds, Is.GreaterThanOrEqualTo(400));
        }

        [Test, PuppeteerTest("page.spec", "Page Page.waitForNetworkIdle", "should respect timeout")]
        public async Task ShouldRespectTimeout()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            var exception = Assert.ThrowsAsync<TimeoutException>(async () =>
                await Page.WaitForNetworkIdleAsync(new WaitForNetworkIdleOptions { Timeout = 1 }));

            Assert.That(exception.Message, Does.Contain("Timeout of 1 ms exceeded"));
        }

        // This should work on Firefox, this ignore should be temporal
        // PRs are welcome :)
        [Test, PuppeteerTest("page.spec", "Page Page.waitForNetworkIdle", "should respect idleTime")]
        public async Task ShouldRespectIdleTimeout()
        {
            var t1 = DateTime.UtcNow;
            var t2 = DateTime.UtcNow;

            await Page.GoToAsync(TestConstants.EmptyPage);
            var task = Page.WaitForNetworkIdleAsync(new WaitForNetworkIdleOptions { IdleTime = 10 })
                .ContinueWith(x =>
                {
                    if (x.IsFaulted) throw x.Exception;
                    return t1 = DateTime.UtcNow;
                });

            await Task.WhenAll(
                task,
                Page.EvaluateFunctionAsync(@"async () => {
                    await Promise.all([
                    fetch('/digits/1.png'),
                    fetch('/digits/2.png'),
                    ]);
                    await new Promise((resolve) => setTimeout(resolve, 250));
                }").ContinueWith(x =>
                {
                    if (x.IsFaulted) throw x.Exception;
                    return t2 = DateTime.UtcNow;
                })
            );

            Assert.That(t2, Is.GreaterThan(t1));
        }

        [Test, PuppeteerTest("page.spec", "Page Page.waitForNetworkIdle", "should work with no timeout")]
        public async Task ShouldWorkWithNoTimeout()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);

            await Task.WhenAll(
                Page.WaitForNetworkIdleAsync(new WaitForNetworkIdleOptions { Timeout = 0 }),
                Page.EvaluateFunctionAsync(@"() => setTimeout(() => {
                        fetch('/digits/1.png');
                        fetch('/digits/2.png');
                        fetch('/digits/3.png');
                    }, 50)")
            );
        }
    }
}
