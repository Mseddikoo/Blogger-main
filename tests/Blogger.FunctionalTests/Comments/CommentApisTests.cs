using System.Net;
using System.Text;
using Blogger.FunctionalTests.Helper;
using Blogger.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Blogger.FunctionalTests.Comments;

/// <summary>
/// Functional tests for Comment and Reply endpoints:
/// - POST /comments/ (Make comment)
/// - GET /comments/approve (Approve comment via link)
/// - POST /comments/{comment-id}/reply (Reply to comment)
/// - GET /comments/replies/approve (Approve reply via link)
/// - GET /comments/{article-id} (Get approved comments)
/// - GET /comments/replies/{comment-id} (Get approved replies)
/// </summary>
public class CommentApisTests : IClassFixture<BloggerWebApplicationFactory>
{
    private readonly BloggerWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public CommentApisTests(BloggerWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Make_Comment_Should_Return_OkStatus()
    {
        // Arrange - create and publish an article first
        var articleSlug = await CreateAndPublishArticleAsync();
        var commentPayload = new
        {
            ArticleId = articleSlug,
            Content = "This is a great test article comment.",
            FullName = "John Doe",
            Email = "johndoe@example.com"
        };
        var content = new StringContent(JsonConvert.SerializeObject(commentPayload), Encoding.UTF8, "application/json");

        // Act - post comment
        var response = await _httpClient.PostAsync("comments/", content);

        // Assert - expect HTTP 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Approve_Comment_Should_Redirect_To_Article()
    {
        // Arrange - create article and comment
        var articleSlug = await CreateAndPublishArticleAsync();
        var commentPayload = new
        {
            ArticleId = articleSlug,
            Content = "Comment pending approval.",
            FullName = "Jane Doe",
            Email = "janedoe@example.com"
        };
        var content = new StringContent(JsonConvert.SerializeObject(commentPayload), Encoding.UTF8, "application/json");
        var commentResponse = await _httpClient.PostAsync("comments/", content);
        var commentResult = JsonConvert.DeserializeObject<CommentCreatedResponse>(await commentResponse.Content.ReadAsStringAsync())!;

        // Retrieve the generated approval link from the database
        string approveLink;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BloggerDbContext>();
            var comment = db.Comments.First(c => c.Id == Blogger.Domain.CommentAggregate.CommentId.Create(Guid.Parse(commentResult.CommentId)));
            approveLink = comment.ApproveLink.ApproveId;
        }

        // Act - request approval endpoint
        var response = await _httpClient.GetAsync($"comments/approve?Link={approveLink}");

        // Assert - expect redirect (302) to the article with comment id
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Reply_To_Comment_Should_Return_OkStatus()
    {
        // Arrange - create article and comment
        var articleSlug = await CreateAndPublishArticleAsync();
        var commentPayload = new
        {
            ArticleId = articleSlug,
            Content = "Initial comment for reply test.",
            FullName = "Alice",
            Email = "alice@example.com"
        };
        var content = new StringContent(JsonConvert.SerializeObject(commentPayload), Encoding.UTF8, "application/json");
        var commentResponse = await _httpClient.PostAsync("comments/", content);
        var commentResult = JsonConvert.DeserializeObject<CommentCreatedResponse>(await commentResponse.Content.ReadAsStringAsync())!;

        // Approve comment so it can receive replies
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BloggerDbContext>();
            var comment = db.Comments.First(c => c.Id == Blogger.Domain.CommentAggregate.CommentId.Create(Guid.Parse(commentResult.CommentId)));
            comment.Approve();
            db.SaveChanges();
        }

        // Act - reply to comment
        var replyPayload = new
        {
            Content = "This is a reply to the comment.",
            FullName = "Bob",
            Email = "bob@example.com"
        };
        var replyContent = new StringContent(JsonConvert.SerializeObject(replyPayload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"comments/{commentResult.CommentId}/reply", replyContent);

        // Assert - expect HTTP 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Approve_Reply_Should_Redirect_To_Article()
    {
        // Arrange - create article, comment, approve comment, and post reply
        var articleSlug = await CreateAndPublishArticleAsync();
        var commentPayload = new
        {
            ArticleId = articleSlug,
            Content = "Parent comment for approved reply test.",
            FullName = "Charlie",
            Email = "charlie@example.com"
        };
        var commentContent = new StringContent(JsonConvert.SerializeObject(commentPayload), Encoding.UTF8, "application/json");
        var commentResponse = await _httpClient.PostAsync("comments/", commentContent);
        var commentResult = JsonConvert.DeserializeObject<CommentCreatedResponse>(await commentResponse.Content.ReadAsStringAsync())!;

        var commentGuid = Guid.Parse(commentResult.CommentId);

        // Approve parent comment in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BloggerDbContext>();
            var comment = db.Comments.First(c => c.Id == Blogger.Domain.CommentAggregate.CommentId.Create(commentGuid));
            comment.Approve();
            db.SaveChanges();
        }

        // Post reply
        var replyPayload = new
        {
            Content = "Reply to be approved.",
            FullName = "David",
            Email = "david@example.com"
        };
        var replyContent = new StringContent(JsonConvert.SerializeObject(replyPayload), Encoding.UTF8, "application/json");
        var replyResponse = await _httpClient.PostAsync($"comments/{commentResult.CommentId}/reply", replyContent);
        var replyResult = JsonConvert.DeserializeObject<ReplyCreatedResponse>(await replyResponse.Content.ReadAsStringAsync())!;

        // Retrieve approval link for the reply
        string replyApproveLink;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BloggerDbContext>();
            var comment = db.Comments.First(c => c.Id == Blogger.Domain.CommentAggregate.CommentId.Create(commentGuid));
            var reply = comment.Replies.First(r => r.Id == Blogger.Domain.CommentAggregate.ReplyId.Create(Guid.Parse(replyResult.ReplyId)));
            replyApproveLink = reply.ApproveLink.ApproveId;
        }

        // Act - request reply approval endpoint with Link and comment-id query params
        var response = await _httpClient.GetAsync($"comments/replies/approve?Link={replyApproveLink}&comment-id={commentResult.CommentId}");

        // Assert - expect redirect (302)
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Get_Comments_For_Article_Should_Return_OkStatus()
    {
        // Arrange - create article
        var articleSlug = await CreateAndPublishArticleAsync();

        // Act - get comments for the article
        var response = await _httpClient.GetAsync($"comments/{articleSlug}");

        // Assert - expect HTTP 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_Replies_For_Comment_Should_Return_OkStatus()
    {
        // Arrange - create article and comment
        var articleSlug = await CreateAndPublishArticleAsync();
        var commentPayload = new
        {
            ArticleId = articleSlug,
            Content = "Comment for get replies test.",
            FullName = "Eve",
            Email = "eve@example.com"
        };
        var content = new StringContent(JsonConvert.SerializeObject(commentPayload), Encoding.UTF8, "application/json");
        var commentResponse = await _httpClient.PostAsync("comments/", content);
        var commentResult = JsonConvert.DeserializeObject<CommentCreatedResponse>(await commentResponse.Content.ReadAsStringAsync())!;

        // Act - fetch replies
        var response = await _httpClient.GetAsync($"comments/replies/{commentResult.CommentId}");

        // Assert - expect HTTP 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #region Helper Methods

    private async Task<string> CreateAndPublishArticleAsync()
    {
        var randomSuffix = StringGenerator.GenerateRandomString(8);
        var articlePayload = new
        {
            Title = $"Commentable Article {randomSuffix}",
            Body = "Article body text for comments.",
            Summary = "Article summary.",
            Tags = new[] { "comments", "testing" }
        };
        var content = new StringContent(JsonConvert.SerializeObject(articlePayload), Encoding.UTF8, "application/json");
        var postResponse = await _httpClient.PostAsync("articles/", content);
        var result = JsonConvert.DeserializeObject<ArticleCreatedResponse>(await postResponse.Content.ReadAsStringAsync())!;
        return result.ArticleId;
    }

    private class ArticleCreatedResponse
    {
        [JsonProperty("articleId")]
        public string ArticleId { get; set; } = null!;
    }

    private class CommentCreatedResponse
    {
        [JsonProperty("commentId")]
        public string CommentId { get; set; } = null!;
    }

    private class ReplyCreatedResponse
    {
        [JsonProperty("replyId")]
        public string ReplyId { get; set; } = null!;
    }

    #endregion
}
