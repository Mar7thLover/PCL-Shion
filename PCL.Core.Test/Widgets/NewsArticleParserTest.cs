using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.ViewModel.Homepage;

namespace PCL.Core.Test.Widgets;

[TestClass]
public sealed class NewsArticleParserTest
{
    private const string ArticleUrl = "https://www.minecraft.net/zh-hans/article/dominos-minecraft-dungeons-ii";
    private const string ImageRoot = "https://www.minecraft.net/content/dam/minecraftnet/games/spicewood/game-characters/";

    /// <summary>
    /// 从真实的文章页面（minecraft.net 的 dominos-minecraft-dungeons-ii）裁剪而来，保留了页面的整体结构：
    /// 头部的样式与脚本、导航、文章头图与标题、两个文章网格（中间夹着轮播图）、分享区块、最新新闻和页脚。
    /// </summary>
    private const string SampleHtml = """
        <!DOCTYPE HTML>
        <html lang="zh-CN">
        <head>
            <meta charset="UTF-8"/>
            <title>Dominos Minecraft Dungeons II | Minecraft</title>
            <style>.MC_articleGridA_container { display: grid; } p > a { color: red }</style>
            <script>var teaser = "<p>not part of the article</p>"; if (1 < 2) { run(); }</script>
        </head>
        <body>
        <nav class="MC_globalNavA"><a href="/zh-hans/store">Store</a><h2>Navigation heading</h2></nav>
        <section class="MC_Bg_Inherit MC_Theme_Vanilla MC_articleHeroA_grid" data-mc-ref="mc_hero_articleHeroA">
            <div class="MC_articleHeroA" id="article-hero-a-023302e04d">
                <picture>
                    <source srcset="/content/dam/minecraftnet/games/spicewood/game-characters/MCDII_DominosPizza_netblog_1170x500.jpg" media="(min-width: 992px)"/>
                    <img src="/content/dam/minecraftnet/games/spicewood/game-characters/MCDII_DominosPizza_netblog_1170x500.jpg" class="article-head__image img-fluid" alt="Hero image that is not part of the body"/>
                </picture>
                <dl>
                    <dt>作者</dt>
                    <dd>Cristina Anderca</dd>
                </dl>
                <h1 class="MC_Heading_1">Domino’s x Minecraft Dungeons II</h1>
                <p class="MC_articleHeroA_header_subheadline">Fuel up and gear up for the battle with exclusive in-game items and more!</p>
            </div>
        </section>
        <div class="article-grid-a aem-GridColumn aem-GridColumn--default--12">
            <section class="MC_Bg_Inherit MC_Theme_Vanilla" data-mc-ref="mc_grids_articleGridA">
                <div class="MC_articleGridA_sectionRef" id="article-grid-a-b9a15ea8fa"></div>
                <div class="MC_articleGridA">
                    <div class="MC_articleGridA_container MC_articleGridA_grid">
            <div class="article-section">
            <div class="MC_articleGridA_sectionRef"></div>
            <div class="MC_articleGridA_mediaBlock">
            <div class="article-text">
            <div class="MC_articleGridA_sectionRef"></div>
            <div class="MC_Link_Style_RichText">
                <p>For me, life’s greatest pleasures come in pairs. A hot cup of coffee and a stroopwafel. Notice none of these require leaving my house either – and neither does the perfect pairing I’m introducing today: <a href="https://www.dominos.com/minecraft-dungeons-ii/">Domino’s and Minecraft Dungeons II</a>!</p>
        <p>We’ve teamed up with Domino’s to celebrate and enhance one of gaming’s most long-lived traditions: the pizza night. This limited-time partnership features a variety of pizzas in collectible boxes, in-game cosmetics, and even a chance to win Minecraft Dungeons II game codes.* </p>

            </div>
        </div>
            </div>
        </div>
                    </div>
                </div>
            </section>
        </div>
        <div class="MC_AEM_Wrapper aem-GridColumn aem-GridColumn--default--12">
            <section class="MC_Theme_Vanilla MC_Bg_Inherit   " data-mc-ref="mc_carousel_carouselD" data-aem-item="mc-carouselD">
                <div data-mc-mount="Carousel" id="carousel-d-27c1b9b6f0" class="MC_Carousel MC_CarouselD MC_Carousel__circular" data-mc-config="{&#34;slides&#34;:{&#34;colors&#34;:[&#34;MC_Bg_Inherit&#34;]},&#34;controls&#34;:{&#34;next&#34;:&#34;下一张幻灯片&#34;}}">
                    <div class="MC_Carousel_track">
                        <div class="MC_Carousel_track_slider">
                            <div class="MC_Carousel_track_slide MC_Theme_Legends" aria-label="slideAriaLabel">
                                <div class="MC_Carousel_track_slide_media MC_Carousel_track_slide_text_nobanner" style="--banner-image:url();">
                                    <picture>
                                        <source media="(max-width: 1199px)" srcset="/content/dam/minecraftnet/games/spicewood/game-characters/dominos_dominos.jpg"/>
                                        <img class="MC_Carousel_track_slide_media_img" src="/content/dam/minecraftnet/games/spicewood/game-characters/dominos_dominos.jpg" alt="A Minecraft Dungeons II hero wearing Domino&#39;s colorful colors!" loading="lazy"/>
                                    </picture>
                                </div>
                            </div>
                            <div class="MC_Carousel_track_slide MC_Theme_Legends" aria-label="slideAriaLabel">
                                <div class="MC_Carousel_track_slide_media MC_Carousel_track_slide_text_nobanner" style="--banner-image:url();">
                                    <picture>
                                        <img class="MC_Carousel_track_slide_media_img" src="/content/dam/minecraftnet/games/spicewood/game-characters/dominos_alex.jpg" alt="Alex, seen from behind, looking over her shoulder to admire her Domino&#39;s Slice slayer cape." loading="lazy"/>
                                    </picture>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
                <div class="MC_Fade"></div>
            </section>
        </div>
        <div class="article-grid-a aem-GridColumn aem-GridColumn--default--12">
            <section class="MC_Bg_Inherit MC_Theme_Vanilla" data-mc-ref="mc_grids_articleGridA">
                <div class="MC_articleGridA_sectionRef" id="article-grid-a-2d9b896d66"></div>
                <div class="MC_articleGridA">
                    <div class="MC_articleGridA_container MC_articleGridA_grid">
            <div class="article-section">
            <div class="MC_articleGridA_sectionRef"></div>
            <div class="MC_articleGridA_mediaBlock">
            <div class="article-text">
            <div class="MC_articleGridA_sectionRef"></div>
            <div class="MC_Link_Style_RichText">
                <h1>Exclusive Domino’s in-game cosmetics for Minecraft Dungeons II</h1>
        <p>Starting Monday October 26th, you can unlock one of three exclusive cosmetic items for Minecraft Dungeons II with each qualifying online order**: </p>
        <ul><li>Domino’s Skin: A Domino’s-inspired character skin </li><li>Slice Slayer Cape: A pizza-themed cape worthy of any adventure </li><li>Cheesy Cube Pet: A cheesy companion that follows you on your travels </li></ul>
        <p>&nbsp;</p>
        <h1>How to participate </h1>
        <h3>United States </h3>
        <p>In the United States, the collaboration features the Domino™, a Detroit-style pizza for one with a parmesan crust. To earn and redeem an in-game item: </p>
        <ol><li>Sign in to Domino’s® Rewards or create an account. </li><li>Order and prepay for a qualifying Domino™ through the Domino’s website or app. </li><li>Once the order is complete, allow up to 24 hours for the reward to appear under My Deals &amp; Rewards in your Domino’s account. </li><li>Brave the unknown!  </li></ol>
        <p> </p>

            </div>
        </div>

            <div class="article-media">
            <figure class="MC_imageBlockE">
                        <picture>
                            <img src="/content/dam/minecraftnet/games/spicewood/game-characters/dominos_boxes.jpg" alt="Four heroes, fighting against the Twisted Warden in their fancy Domino&#39;s cosmetics!"/>
                        </picture>
            </figure>
        </div>

            <div class="article-text">
            <div class="MC_articleGridA_sectionRef"></div>
            <div class="MC_Link_Style_RichText">
                <h3>International orders (excluding China)</h3>
        <p>While the Domino ™ isn’t available in all regions, you can still get the in-game items in many participating Domino’s locations around the world! Some markets will feature their own personal pizzas.</p>
        <p><span class="MC_Effect_TextSmallA"><i>Microsoft is not a Sponsor of this offer. </i></span></p>
        <h3>China </h3>
        <p>For more information on the MyWorld x Domino’s activation, please visit: <a href="http://www.dominos.com.cn/">http://www.dominos.com.cn/ </a></p>
            </div>
        </div>
            </div>
        </div>
                    </div>
                </div>
            </section>
        </div>
        <div class="MC_AEM_Wrapper aem-GridColumn aem-GridColumn--default--12">
            <section class=" " data-mc-ref="mc_social_socialShareA">
                <div class="MC_socialShareA" id="social-share-a-229972d3d8">
                    <hr class="MC_dividerA"/>
                    <div class="MC_socialShareA_share-container">
                        <h2 class="MC_Heading_2">分享这个故事</h2>
                        <nav aria-label="article social share options">
                            <ul>
                                <li>
                                    <a class="MC_socialShareA_social-icon" href="https://twitter.com/share?url=https%3A%2F%2Fwww.minecraft.net%2Fzh-hans%2Farticle%2Fdominos-minecraft-dungeons-ii" aria-label="Share this article on Twitter" title="Share this article on Twitter">
                                        <svg width="32" height="32" viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
        <path d="M5 29H3V27H5V29ZM29 27V25H31V29H23V27H29Z" fill="white"/>
        </svg>
                                    </a>
                                </li>
                            </ul>
                        </nav>
                    </div>
                </div>
            </section>
        </div>
        <div class="MC_AEM_Wrapper aem-GridColumn aem-GridColumn--default--12">
            <section class="MC_Bg_Inherit MC_Theme_Vanilla" data-mc-ref="mc_grids_imageGridA" data-aem-item="mc-imageGridA">
            <div class="MC_imageGridA" id="image-grid-a-0b283d8653" data-mc-mount="ImageGridA">
                <div class="MC_imageGridA_Heading_Container">
                        <h2 class="MC_Heading_2  ">Newest News</h2>
                </div>
                <ul class="MC_imageGridA_grid"></ul>
            </div>
            </section>
        </div>
        <footer class="MC_globalFooterA" data-mc-mount="GlobalFooterA">
            <h2 class="MC_Heading_5">关注 Minecraft</h2>
        </footer>
        </body>
        </html>
        """;

    /// <summary>
    /// 把片段放进最小的文章网格结构里。
    /// </summary>
    private static string Wrap(string inner)
    {
        return "<html><body><section><div class=\"MC_articleGridA\"><div class=\"article-section\">"
               + "<div class=\"article-text\"><div class=\"MC_Link_Style_RichText\">"
               + inner
               + "</div></div></div></div></section></body></html>";
    }

    /// <summary>
    /// 把块列表转为便于比较的文本：种类|附加信息|文本。
    /// </summary>
    private static string[] Describe(IReadOnlyList<NewsArticleBlock> blocks)
    {
        return blocks.Select(block => block.Kind switch
        {
            NewsArticleBlockKind.Heading => $"H{block.Level}|{block.Text}",
            NewsArticleBlockKind.Paragraph => $"P|{block.Text}",
            NewsArticleBlockKind.ListItem => $"LI{block.Level}|{(block.Index > 0 ? block.Index + "." : "-")}|{block.Text}",
            NewsArticleBlockKind.Image => $"IMG|{block.Url}|{block.Text}",
            NewsArticleBlockKind.Caption => $"CAP|{block.Text}",
            _ => "?"
        }).ToArray();
    }

    private static string[] ParseWrapped(string inner, string? baseUrl = ArticleUrl)
    {
        return Describe(NewsArticleParser.Parse(Wrap(inner), baseUrl));
    }

    // ----- 空输入与无法识别的内容 -----

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   \r\n  ")]
    [DataRow("garbage")]
    [DataRow("<<<>>>")]
    [DataRow("<!-- comment -->")]
    [DataRow("<html><body><h1>Title</h1><p>There is no article grid on this page.</p></body></html>")]
    [DataRow("<style>.MC_articleGridA { color: red }</style><p>The marker only appears in a stylesheet.</p>")]
    [DataRow("<div class=\"MC_articleGridA\"")]
    [DataRow("<div class=\"MC_articleGridA\">")]
    [DataRow("<div class=\"MC_articleGridA\"></div><footer>Footer</footer>")]
    public void ParseShouldReturnEmptyForEmptyOrGarbageInput(string? html)
    {
        Assert.IsEmpty(NewsArticleParser.Parse(html, ArticleUrl));
    }

    // ----- 完整文章 -----

    [TestMethod]
    public void ParseShouldExtractArticleBodyInDocumentOrder()
    {
        string[] expected =
        [
            "P|For me, life’s greatest pleasures come in pairs. A hot cup of coffee and a stroopwafel. Notice none of these require leaving my house either – and neither does the perfect pairing I’m introducing today: Domino’s and Minecraft Dungeons II!",
            "P|We’ve teamed up with Domino’s to celebrate and enhance one of gaming’s most long-lived traditions: the pizza night. This limited-time partnership features a variety of pizzas in collectible boxes, in-game cosmetics, and even a chance to win Minecraft Dungeons II game codes.*",
            $"IMG|{ImageRoot}dominos_dominos.jpg|A Minecraft Dungeons II hero wearing Domino's colorful colors!",
            $"IMG|{ImageRoot}dominos_alex.jpg|Alex, seen from behind, looking over her shoulder to admire her Domino's Slice slayer cape.",
            "H1|Exclusive Domino’s in-game cosmetics for Minecraft Dungeons II",
            "P|Starting Monday October 26th, you can unlock one of three exclusive cosmetic items for Minecraft Dungeons II with each qualifying online order**:",
            "LI1|-|Domino’s Skin: A Domino’s-inspired character skin",
            "LI1|-|Slice Slayer Cape: A pizza-themed cape worthy of any adventure",
            "LI1|-|Cheesy Cube Pet: A cheesy companion that follows you on your travels",
            "H1|How to participate",
            "H3|United States",
            "P|In the United States, the collaboration features the Domino™, a Detroit-style pizza for one with a parmesan crust. To earn and redeem an in-game item:",
            "LI1|1.|Sign in to Domino’s® Rewards or create an account.",
            "LI1|2.|Order and prepay for a qualifying Domino™ through the Domino’s website or app.",
            "LI1|3.|Once the order is complete, allow up to 24 hours for the reward to appear under My Deals & Rewards in your Domino’s account.",
            "LI1|4.|Brave the unknown!",
            $"IMG|{ImageRoot}dominos_boxes.jpg|Four heroes, fighting against the Twisted Warden in their fancy Domino's cosmetics!",
            "H3|International orders (excluding China)",
            "P|While the Domino ™ isn’t available in all regions, you can still get the in-game items in many participating Domino’s locations around the world! Some markets will feature their own personal pizzas.",
            "P|Microsoft is not a Sponsor of this offer.",
            "H3|China",
            "P|For more information on the MyWorld x Domino’s activation, please visit: http://www.dominos.com.cn/"
        ];

        var blocks = NewsArticleParser.Parse(SampleHtml, ArticleUrl);

        CollectionAssert.AreEqual(expected, Describe(blocks));
    }

    [TestMethod]
    public void ParseShouldNotIncludeContentOutsideTheArticleGrid()
    {
        var text = string.Join("\n", NewsArticleParser.Parse(SampleHtml, ArticleUrl).Select(block => block.Text));

        foreach (var outside in new[]
                 {
                     "not part of the article", "Navigation heading", "Hero image", "Fuel up and gear up", "Cristina Anderca",
                     "分享这个故事", "Newest News", "关注 Minecraft", "Store"
                 })
        {
            Assert.IsFalse(text.Contains(outside), $"不应包含正文之外的内容：{outside}");
        }

        // 页面头部的大图（文章头图）也不属于正文
        Assert.IsFalse(
            NewsArticleParser.Parse(SampleHtml, ArticleUrl).Any(block => block.Url?.Contains("MCDII_DominosPizza_netblog") == true));
    }

    [TestMethod]
    public void ParseShouldKeepInlineLinksOfTheSampleArticle()
    {
        var blocks = NewsArticleParser.Parse(SampleHtml, ArticleUrl);

        var intro = blocks[0];
        Assert.AreEqual(1, intro.Links.Count);
        Assert.AreEqual("https://www.dominos.com/minecraft-dungeons-ii/", intro.Links[0].Url);
        Assert.AreEqual("Domino’s and Minecraft Dungeons II", intro.Text.Substring(intro.Links[0].Start, intro.Links[0].Length));

        var footer = blocks[blocks.Count - 1];
        Assert.AreEqual(1, footer.Links.Count);
        Assert.AreEqual("http://www.dominos.com.cn/", footer.Links[0].Url);
        Assert.AreEqual("http://www.dominos.com.cn/", footer.Text.Substring(footer.Links[0].Start, footer.Links[0].Length));
    }

    // ----- 文本 -----

    [TestMethod]
    public void ParseShouldDecodeEntitiesAndCollapseWhitespace()
    {
        CollectionAssert.AreEqual(
            new[] { "P|Tom & Jerry's <adventure> time" },
            ParseWrapped("<p>  Tom &amp;   Jerry&#39;s\n  &lt;adventure&gt;&nbsp;&nbsp;time  </p>"));
    }

    [TestMethod]
    public void ParseShouldDropEmptyParagraphs()
    {
        CollectionAssert.AreEqual(
            new[] { "P|kept" },
            ParseWrapped("<p></p><p> </p><p>&nbsp;</p><p>&#8203;</p><p><br/></p><p>kept</p>"));
    }

    [TestMethod]
    public void ParseShouldTurnLineBreaksIntoNewLines()
    {
        CollectionAssert.AreEqual(
            new[] { "P|line one\nline two\nline three" },
            ParseWrapped("<p>line one<br>line two <br/><br> line three<br></p>"));
    }

    [TestMethod]
    public void ParseShouldSplitUnclosedParagraphs()
    {
        CollectionAssert.AreEqual(
            new[] { "P|First", "P|Second bold", "P|Third" },
            ParseWrapped("<p>First<p>Second <b>bold<p>Third"));
    }

    [TestMethod]
    public void ParseShouldTreatStrayAngleBracketsAsText()
    {
        CollectionAssert.AreEqual(
            new[] { "P|1 < 2 and 3 > 2" },
            ParseWrapped("<p>1 < 2 and 3 > 2</p>"));
    }

    [TestMethod]
    public void ParseShouldIgnoreScriptsStylesCommentsAndEmbeddedWidgets()
    {
        CollectionAssert.AreEqual(
            new[] { "P|real", "P|after" },
            ParseWrapped(
                "<script>var a = \"<p>fake</p>\"; if (1 < 2) { run(); }</script>"
                + "<style>p { color: red }</style>"
                + "<svg viewBox=\"0 0 1 1\"><g><text>svg text</text></g></svg>"
                + "<!-- <p>comment</p> -->"
                + "<noscript><p>noscript</p></noscript>"
                + "<button><span>Click me</span></button>"
                + "<p>real</p>"
                + "<nav><nav><p>nested nav</p></nav><p>still nav</p></nav>"
                + "<p>after</p>"));
    }

    [TestMethod]
    public void ParseShouldReadHeadingsAndKeepTheirLevels()
    {
        CollectionAssert.AreEqual(
            new[] { "H1|Title", "H2|Sub title", "H3|Third", "H6|Smallest", "P|body" },
            ParseWrapped("<h1> Title </h1><h2>Sub <em>title</em></h2><H3>Third</H3><h6>Smallest</h6>body"));
    }

    // ----- 列表 -----

    [TestMethod]
    public void ParseShouldNumberOrderedListsAndNestLists()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "LI1|-|A", "LI1|-|B", "LI2|-|B1", "LI2|-|B2", "LI1|-|C",
                "LI1|3.|x", "LI1|4.|y", "LI2|1.|y1", "LI1|5.|z",
                "P|after"
            },
            ParseWrapped(
                "<ul><li>A</li><li>B<ul><li>B1</li><li>B2</li></ul></li><li>C</li></ul>"
                + "<ol start=\"3\"><li>x</li><li>y<ol><li>y1</li></ol></li><li>z</li></ol>"
                + "<p>after</p>"));
    }

    [TestMethod]
    public void ParseShouldRestartNumberingForEachOrderedList()
    {
        CollectionAssert.AreEqual(
            new[] { "LI1|1.|a", "LI1|2.|b", "P|between", "LI1|1.|c" },
            ParseWrapped("<ol><li>a</li><li>b</li></ol><p>between</p><ol><li>c</li></ol>"));
    }

    // ----- 图片 -----

    [TestMethod]
    public void ParseShouldResolveImageAddressesAgainstThePageUrl()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "IMG|https://www.minecraft.net/content/dam/a.jpg|An alt",
                "IMG|https://www.minecraft.net/zh-hans/article/b.png|",
                "IMG|https://cdn.example.com/c.jpg|",
                "IMG|https://www.minecraft.net/lazy.jpg|",
                "IMG|https://www.minecraft.net/s1.jpg|",
                "IMG|https://www.minecraft.net/dup.jpg|one"
            },
            ParseWrapped(
                "<img src=\"/content/dam/a.jpg\" alt=\"  An   alt \">"
                + "<img src=\"b.png\">"
                + "<img src=\"//cdn.example.com/c.jpg\">"
                + "<img src=\"data:image/gif;base64,AAAA\" data-src=\"/lazy.jpg\">"
                + "<img src=\"data:image/gif;base64,AAAA\">"
                + "<img src=\"/pixel.gif\" width=\"1\" height=\"1\">"
                + "<img srcset=\"/s1.jpg 1x, /s2.jpg 2x\">"
                + "<img src=\"/dup.jpg\" alt=\"one\"><img src=\"/dup.jpg\" alt=\"two\">"));
    }

    [TestMethod]
    public void ParseShouldUseTheDefaultSiteWhenThePageUrlIsMissingOrInvalid()
    {
        var expected = new[] { "IMG|https://www.minecraft.net/content/dam/a.jpg|" };

        CollectionAssert.AreEqual(expected, ParseWrapped("<img src=\"/content/dam/a.jpg\">", null));
        CollectionAssert.AreEqual(expected, ParseWrapped("<img src=\"/content/dam/a.jpg\">", "not a url"));
        CollectionAssert.AreEqual(expected, ParseWrapped("<img src=\"/content/dam/a.jpg\">", "ftp://example.com/x"));
    }

    [TestMethod]
    public void ParseShouldKeepImageCaptions()
    {
        CollectionAssert.AreEqual(
            new[] { "IMG|https://www.minecraft.net/a.jpg|A", "CAP|Caption text", "P|next" },
            ParseWrapped("<figure><picture><img src=\"/a.jpg\" alt=\"A\"/></picture><figcaption>Caption <i>text</i></figcaption></figure><p>next</p>"));
    }

    // ----- 链接 -----

    [TestMethod]
    public void ParseShouldRecordLinkRangesWithinTheText()
    {
        var blocks = NewsArticleParser.Parse(
            Wrap("<p>See <a href=\"/a\">the <b>first</b> link</a>, and <a href=\"https://example.com/b?x=1&amp;y=2\"> second </a>.</p>"),
            ArticleUrl);

        Assert.AreEqual(1, blocks.Count);
        var block = blocks[0];
        Assert.AreEqual("See the first link, and second .", block.Text);
        Assert.AreEqual(2, block.Links.Count);

        Assert.AreEqual("https://www.minecraft.net/a", block.Links[0].Url);
        Assert.AreEqual("the first link", block.Text.Substring(block.Links[0].Start, block.Links[0].Length));

        Assert.AreEqual("https://example.com/b?x=1&y=2", block.Links[1].Url);
        Assert.AreEqual("second", block.Text.Substring(block.Links[1].Start, block.Links[1].Length));
    }

    [TestMethod]
    public void ParseShouldIgnoreLinksThatAreNotHttp()
    {
        var blocks = NewsArticleParser.Parse(
            Wrap("<p><a href=\"javascript:void(0)\">js</a> <a href=\"mailto:a@b.c\">mail</a> <a href=\"#top\">top</a> <a>none</a> <a href=\"\">empty</a></p>"),
            ArticleUrl);

        Assert.AreEqual(1, blocks.Count);
        Assert.AreEqual("js mail top none empty", blocks[0].Text);
        Assert.IsEmpty(blocks[0].Links);
    }

    [TestMethod]
    public void ParseShouldKeepLinksThatSpanBlockBoundaries()
    {
        var blocks = NewsArticleParser.Parse(
            Wrap("<a href=\"/x\"><p>one</p><p>two</p></a>"),
            ArticleUrl);

        Assert.AreEqual(2, blocks.Count);
        foreach (var block in blocks)
        {
            Assert.AreEqual(1, block.Links.Count);
            Assert.AreEqual("https://www.minecraft.net/x", block.Links[0].Url);
            Assert.AreEqual(block.Text, block.Text.Substring(block.Links[0].Start, block.Links[0].Length));
        }
    }
}
