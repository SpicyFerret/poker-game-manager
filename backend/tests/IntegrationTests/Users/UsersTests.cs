using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Users;

public sealed class UsersTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Register_Should_ReturnUserId()
    {
        // Act
        Guid userId = await RegisterUserAsync(UniqueEmail());

        // Assert
        userId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Login_Should_ReturnAccessAndRefreshTokens()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);

        // Act
        AccessTokens tokens = await LoginAsync(email);

        // Assert
        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_Should_ReturnProblem_WhenPasswordIsInvalid()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/login",
            new { email, password = "WrongPassword1" });

        // Assert
        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshToken_Should_ReturnNewTokens()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/refresh-token",
            new { refreshToken = tokens.RefreshToken });

        // Assert
        response.EnsureSuccessStatusCode();
        AccessTokens? rotated = await response.Content.ReadFromJsonAsync<AccessTokens>();
        rotated!.AccessToken.ShouldNotBeNullOrWhiteSpace();
        rotated.RefreshToken.ShouldNotBe(tokens.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_Should_ReturnProblem_WhenTokenIsInvalid()
    {
        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/refresh-token",
            new { refreshToken = "this-token-does-not-exist" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateProfile_Should_PersistDisplayNameAndPaymentHandle()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage update = await HttpClient.PutAsJsonAsync(
            "users/me/profile",
            new { displayName = "Dan", paymentType = "Pix", paymentHandle = "dan@example.com" });

        // Assert
        update.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        UserProfile? profile = await HttpClient.GetFromJsonAsync<UserProfile>($"users/{userId}");
        profile!.DisplayName.ShouldBe("Dan");
        profile.PaymentType.ShouldBe("Pix");
        profile.PaymentHandle.ShouldBe("dan@example.com");
    }

    [Fact]
    public async Task UpdateProfile_Should_ReturnUnauthorized_WhenNotAuthenticated()
    {
        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            "users/me/profile",
            new { displayName = "Dan" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_Should_DefaultDisplayNameToFirstName()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        UserProfile? profile = await HttpClient.GetFromJsonAsync<UserProfile>($"users/{userId}");

        // Assert
        profile!.DisplayName.ShouldBe("Test");
    }

    [Fact]
    public async Task GetById_Should_ReturnUnauthorized_WhenTokenIsInvalid()
    {
        // Arrange
        (Guid userId, _) = await RegisterAndLoginAsync();
        Authenticate("not-a-valid-token");

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users/{userId}");

        // Assert — this route goes through HasPermission, whose handler used to
        // fall through to GetUserId() and throw, answering 500 instead of 401.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed record UserProfile(string DisplayName, string? PaymentType, string? PaymentHandle);
}
