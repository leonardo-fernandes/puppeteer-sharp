using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PuppeteerSharp.Nunit;

namespace PuppeteerSharp.Tests.CookiesTests
{
    public class CookiesTests : PuppeteerPageBaseTest
    {
        [Test, Retry(2), PuppeteerTest("cookies.spec", "Cookie specs Page.cookies", "should return no cookies in pristine browser context")]
        public async Task ShouldReturnNoCookiesInPristineBrowserContext()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            Assert.That(await Page.GetCookiesAsync(), Is.Empty);
        }

        [Test, Retry(2), PuppeteerTest("cookies.spec", "Cookie specs Page.cookies", "should get a cookie")]
        public async Task ShouldGetACookie()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            Assert.That(await Page.GetCookiesAsync(), Is.Empty);

            await Page.EvaluateExpressionAsync("document.cookie = 'username=John Doe'");
            var cookies = await Page.GetCookiesAsync();
            Assert.That(cookies, Has.Exactly(1).Items);
            var cookie = cookies.First();
            Assert.That(cookie.Name, Is.EqualTo("username"));
            Assert.That(cookie.Value, Is.EqualTo("John Doe"));
            Assert.That(cookie.Domain, Is.EqualTo("localhost"));
            Assert.That(cookie.Path, Is.EqualTo("/"));
            Assert.That(cookie.Expires, Is.EqualTo(-1));
            Assert.That(cookie.Size, Is.EqualTo(16));
            Assert.That(cookie.HttpOnly, Is.False);
            Assert.That(cookie.Secure, Is.False);
            Assert.That(cookie.Session, Is.True);
        }

        [Test, Retry(2), PuppeteerTest("cookies.spec", "Cookie specs Page.cookies", "should properly report httpOnly cookie")]
        public async Task ShouldProperlyReportHttpOnlyCookie()
        {
            Server.SetRoute("/empty.html", context =>
            {
                context.Response.Headers["Set-Cookie"] = "a=b; HttpOnly; Path=/";
                return Task.CompletedTask;
            });
            await Page.GoToAsync(TestConstants.EmptyPage);
            var cookies = await Page.GetCookiesAsync();
            Assert.That(cookies, Has.Exactly(1).Items);
            Assert.That(cookies[0].HttpOnly, Is.True);
        }

        [Test, Retry(2), PuppeteerTest("cookies.spec", "Cookie specs Page.cookies", "should properly report \"Strict\" sameSite cookie")]
        public async Task ShouldProperlyReportSStrictSameSiteCookie()
        {
            Server.SetRoute("/empty.html", context =>
            {
                context.Response.Headers["Set-Cookie"] = "a=b; SameSite=Strict";
                return Task.CompletedTask;
            });
            await Page.GoToAsync(TestConstants.EmptyPage);
            var cookies = await Page.GetCookiesAsync();
            Assert.That(cookies, Has.Exactly(1).Items);
            Assert.That(cookies[0].SameSite, Is.EqualTo(SameSite.Strict));
        }

        [Test, Retry(2), PuppeteerTest("cookies.spec", "Cookie specs Page.cookies", "should properly report \"Lax\" sameSite cookie")]
        public async Task ShouldProperlyReportLaxSameSiteCookie()
        {
            Server.SetRoute("/empty.html", context =>
            {
                context.Response.Headers["Set-Cookie"] = "a=b; SameSite=Lax";
                return Task.CompletedTask;
            });
            await Page.GoToAsync(TestConstants.EmptyPage);
            var cookies = await Page.GetCookiesAsync();
            Assert.That(cookies, Has.Exactly(1).Items);
            Assert.That(cookies[0].SameSite, Is.EqualTo(SameSite.Lax));
        }

        [Test, Retry(2), PuppeteerTest("cookies.spec", "Cookie specs Page.cookies", "should get multiple cookies")]
        public async Task ShouldGetMultipleCookies()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            Assert.That(await Page.GetCookiesAsync(), Is.Empty);

            await Page.EvaluateFunctionAsync(@"() => {
                document.cookie = 'username=John Doe';
                document.cookie = 'password=1234';
            }");

            var cookies = (await Page.GetCookiesAsync()).OrderBy(c => c.Name).ToList();

            var cookie = cookies[0];
            Assert.That(cookie.Name, Is.EqualTo("password"));
            Assert.That(cookie.Value, Is.EqualTo("1234"));
            Assert.That(cookie.Domain, Is.EqualTo("localhost"));
            Assert.That(cookie.Path, Is.EqualTo("/"));
            Assert.That(cookie.Expires, Is.EqualTo(-1));
            Assert.That(cookie.Size, Is.EqualTo(12));
            Assert.That(cookie.HttpOnly, Is.False);
            Assert.That(cookie.Secure, Is.False);
            Assert.That(cookie.Session, Is.True);

            cookie = cookies[1];
            Assert.That(cookie.Name, Is.EqualTo("username"));
            Assert.That(cookie.Value, Is.EqualTo("John Doe"));
            Assert.That(cookie.Domain, Is.EqualTo("localhost"));
            Assert.That(cookie.Path, Is.EqualTo("/"));
            Assert.That(cookie.Expires, Is.EqualTo(-1));
            Assert.That(cookie.Size, Is.EqualTo(16));
            Assert.That(cookie.HttpOnly, Is.False);
            Assert.That(cookie.Secure, Is.False);
            Assert.That(cookie.Session, Is.True);
        }

        [Test, Retry(2), PuppeteerTest("cookies.spec", "Cookie specs Page.cookies", "should get cookies from multiple urls")]
        public async Task ShouldGetCookiesFromMultipleUrls()
        {
            await Page.SetCookieAsync(
                new CookieParam
                {
                    Url = "https://foo.com",
                    Name = "doggo",
                    Value = "woofs"
                },
                new CookieParam
                {
                    Url = "https://bar.com",
                    Name = "catto",
                    Value = "purrs"
                },
                new CookieParam
                {
                    Url = "https://baz.com",
                    Name = "birdo",
                    Value = "tweets"
                }
            );
            var cookies = (await Page.GetCookiesAsync("https://foo.com", "https://baz.com")).OrderBy(c => c.Name).ToList();

            Assert.That(cookies, Has.Count.EqualTo(2));

            var cookie = cookies[0];
            Assert.That(cookie.Name, Is.EqualTo("birdo"));
            Assert.That(cookie.Value, Is.EqualTo("tweets"));
            Assert.That(cookie.Domain, Is.EqualTo("baz.com"));
            Assert.That(cookie.Path, Is.EqualTo("/"));
            Assert.That(cookie.Expires, Is.EqualTo(-1));
            Assert.That(cookie.Size, Is.EqualTo(11));
            Assert.That(cookie.HttpOnly, Is.False);
            Assert.That(cookie.Secure, Is.True);
            Assert.That(cookie.Session, Is.True);

            cookie = cookies[1];
            Assert.That(cookie.Name, Is.EqualTo("doggo"));
            Assert.That(cookie.Value, Is.EqualTo("woofs"));
            Assert.That(cookie.Domain, Is.EqualTo("foo.com"));
            Assert.That(cookie.Path, Is.EqualTo("/"));
            Assert.That(cookie.Expires, Is.EqualTo(-1));
            Assert.That(cookie.Size, Is.EqualTo(10));
            Assert.That(cookie.HttpOnly, Is.False);
            Assert.That(cookie.Secure, Is.True);
            Assert.That(cookie.Session, Is.True);
        }

        [Test, Retry(2), PuppeteerTest("cookies.spec", "Cookie specs Page.cookies", "should not get cookies from subdomain")]
        public async Task ShouldNotGetCookiesFromSubdomain()
        {
            await Page.SetCookieAsync(new CookieParam
            {
                Url = "https://base_domain.com",
                Name = "doggo",
                Value = "woofs"
            });
            Assert.That(await Page.GetCookiesAsync("https://sub_domain.base_domain.com"), Is.Empty);
        }

        [Test, Retry(2), PuppeteerTest("cookies.spec", "Cookie specs Page.cookies", "should get cookies from nested path")]
        public async Task ShouldGetCookiesFromNestedPath()
        {
            await Page.SetCookieAsync(new CookieParam
            {
                Url = "https://foo.com",
                Path = "/some_path",
                Name = "doggo",
                Value = "woofs"
            });

            var cookies = await Page.GetCookiesAsync("https://foo.com/some_path/nested_path");

            Assert.That(cookies, Has.Exactly(1).Items);
        }

        [Test, Retry(2), PuppeteerTest("cookies.spec", "Cookie specs Page.cookies", "should not get cookies from not nested path")]
        public async Task ShouldNotGetCookiesFromNotNestedPath()
        {
            await Page.SetCookieAsync(new CookieParam
            {
                Url = "https://foo.com",
                Path = "/some_path",
                Name = "doggo",
                Value = "woofs"
            });

            Assert.That(await Page.GetCookiesAsync("https://foo.com/some_path_looks_like_nested"), Is.Empty);
        }

        [Test, Retry(2), PuppeteerTest("cookies.spec", "Cookie specs BrowserContext.cookies", "should find no cookies in new context")]
        public async Task ShouldFindNoCookiesInNewContext()
        {
            var context = await Browser.CreateBrowserContextAsync();
            Assert.That(await context.CookiesAsync(), Is.Empty);
        }

        [Test, Retry(2), PuppeteerTest("cookies.spec", "Cookie specs BrowserContext.cookies", "should find cookie created in page")]
        public async Task ShouldFindCookieCreatedInPage()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);

            await Page.EvaluateExpressionAsync("document.cookie = 'infoCookie = secret'");
            var cookies = await Context.CookiesAsync();
            Assert.That(cookies, Has.Exactly(1).Items);
            var cookie = cookies.First();
            Assert.That(cookie.Name, Is.EqualTo("infoCookie"));
            Assert.That(cookie.Value, Is.EqualTo("secret"));
            Assert.That(cookie.Domain, Is.EqualTo("localhost"));
            Assert.That(cookie.Path, Is.EqualTo("/"));
            Assert.That(cookie.SameParty, Is.EqualTo(false));
            Assert.That(cookie.Expires, Is.EqualTo(-1));
            Assert.That(cookie.Size, Is.EqualTo(16));
            Assert.That(cookie.HttpOnly, Is.False);
            Assert.That(cookie.Secure, Is.False);
            Assert.That(cookie.Session, Is.True);
            Assert.That(cookie.SourceScheme, Is.EqualTo(CookieSourceScheme.NonSecure));
        }

        [Test, Retry(2), PuppeteerTest("cookies.spec", "Cookie specs BrowserContext.setCookie", "should set with undefined partition key")]
        public async Task ShouldSetWithUndefinedPartitionKey()
        {
            await Context.SetCookieAsync([
                new CookieParam()
                {
                    Name = "infoCookie",
                    Value = "secret",
                    Domain = "localhost",
                    Path = "/",
                    SameParty = false,
                    Expires = -1,
                    Size = 16,
                    HttpOnly = false,
                    Secure = false,
                    Session = true,
                    SourceScheme = CookieSourceScheme.NonSecure,
                },
            ]);

            await Page.GoToAsync(TestConstants.EmptyPage);
            Assert.That(await Page.EvaluateExpressionAsync<string>("document.cookie"), Is.EqualTo("infoCookie=secret"));
        }

        [Test, Retry(2), PuppeteerTest("cookies.spec", "Cookie specs BrowserContext.setCookie", "should set cookie with string partition key")]
        public async Task ShouldSetCookieWithStringPartitionKey()
        {
            await Context.SetCookieAsync([
                new CookieParam()
                {
                    Name = "infoCookie",
                    Value = "secret",
                    Domain = "localhost",
                    Path = "/",
                    SameParty = false,
                    Expires = -1,
                    Size = 16,
                    HttpOnly = false,
                    Secure = false,
                    Session = true,
                    PartitionKey = "https://localhost:8000",
                    SourceScheme = CookieSourceScheme.NonSecure,
                },
            ]);

            await Page.GoToAsync(TestConstants.EmptyPage);
            Assert.That(await Page.EvaluateExpressionAsync<string>("document.cookie"), Is.EqualTo("infoCookie=secret"));
        }

        [Test, Retry(2), PuppeteerTest("cookies.spec", "Cookie specs BrowserContext.deleteCookies", "should delete cookies")]
        public async Task ShouldDeleteCookies()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);

            await Context.SetCookieAsync([
                new CookieParam()
                {
                    Name = "cookie1",
                    Value = "1",
                    Domain = "localhost",
                    Path = "/",
                    SameParty = false,
                    Expires = -1,
                    Size = 16,
                    HttpOnly = false,
                    Secure = false,
                    Session = true,
                    SourceScheme = CookieSourceScheme.NonSecure,
                },
                new CookieParam()
                {
                    Name = "cookie2",
                    Value = "2",
                    Domain = "localhost",
                    Path = "/",
                    SameParty = false,
                    Expires = -1,
                    Size = 16,
                    HttpOnly = false,
                    Secure = false,
                    Session = true,
                    SourceScheme = CookieSourceScheme.NonSecure,
                },
            ]);
            Assert.That(await Page.EvaluateExpressionAsync<string>("document.cookie"), Is.EqualTo("cookie1=1; cookie2=2"));

            await Context.DeleteCookieAsync([
                new CookieParam()
                {
                    Name = "cookie1",
                    Value = "1",
                    Domain = "localhost",
                    Path = "/",
                    SameParty = false,
                    Expires = -1,
                    Size = 16,
                    HttpOnly = false,
                    Secure = false,
                    Session = true,
                    SourceScheme = CookieSourceScheme.NonSecure,
                },
            ]);
            Assert.That(await Page.EvaluateExpressionAsync<string>("document.cookie"), Is.EqualTo("cookie2=2"));
        }
    }
}
