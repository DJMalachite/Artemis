using System.Collections.ObjectModel;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Artemis.Core;
using Artemis.WebClient.Workshop.Repositories;
using IdentityModel;
using IdentityModel.Client;

namespace Artemis.WebClient.Workshop.Services;

internal class AuthenticationService : CorePropertyChanged, IAuthenticationService
{
    private const string CLIENT_ID = "artemis.desktop";
    private readonly IAuthenticationRepository _authenticationRepository;
    private readonly SemaphoreSlim _authLock = new(1);
    private readonly ObservableCollection<Claim> _claims = new();

    private readonly IDiscoveryCache _discoveryCache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BehaviorSubject<bool> _isLoggedInSubject = new(false);

    private AuthenticationToken? _token;

    public AuthenticationService(IHttpClientFactory httpClientFactory, IDiscoveryCache discoveryCache, IAuthenticationRepository authenticationRepository)
    {
        _httpClientFactory = httpClientFactory;
        _discoveryCache = discoveryCache;
        _authenticationRepository = authenticationRepository;

        Claims = new ReadOnlyObservableCollection<Claim>(_claims);
    }

    private async Task<DiscoveryDocumentResponse> GetDiscovery()
    {
        DiscoveryDocumentResponse disco = await _discoveryCache.GetAsync();
        if (disco.IsError)
            throw new ArtemisWebClientException("Failed to retrieve discovery document: " + disco.Error);

        return disco;
    }

    private void SetCurrentUser(TokenResponse response)
    {
        _token = new AuthenticationToken(response);
        _authenticationRepository.SetRefreshToken(_token.RefreshToken);

        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken? token = handler.ReadJwtToken(response.IdentityToken);
        if (token == null)
            throw new ArtemisWebClientException("Failed to read JWT token");

        _claims.Clear();
        foreach (Claim responseClaim in token.Claims)
            _claims.Add(responseClaim);

        _isLoggedInSubject.OnNext(true);
    }

    private async Task<bool> UseRefreshToken(string refreshToken)
    {
        DiscoveryDocumentResponse disco = await GetDiscovery();
        HttpClient client = _httpClientFactory.CreateClient();
        TokenResponse response = await client.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = disco.TokenEndpoint,
            ClientId = CLIENT_ID,
            RefreshToken = refreshToken
        });

        if (response.IsError)
        {
            if (response.Error == OidcConstants.TokenErrors.ExpiredToken)
                return false;

            throw new ArtemisWebClientException("Failed to request refresh token: " + response.Error);
        }

        SetCurrentUser(response);
        return false;
    }

    private static byte[] HashSha256(string inputString)
    {
        using SHA256 sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(inputString));
    }

    /// <inheritdoc />
    public IObservable<bool> IsLoggedIn => _isLoggedInSubject.AsObservable();

    /// <inheritdoc />
    public ReadOnlyObservableCollection<Claim> Claims { get; }

    public async Task<string?> GetBearer()
    {
        await _authLock.WaitAsync();

        try
        {
            // If not logged in, attempt to auto login first
            if (!_isLoggedInSubject.Value)
                await AutoLogin();

            if (_token == null)
                return null;

            // If the token is expiring, refresh it
            if (_token.Expired && !await UseRefreshToken(_token.RefreshToken))
                return null;

            return _token.AccessToken;
        }
        finally
        {
            _authLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> AutoLogin()
    {
        await _authLock.WaitAsync();

        try
        {
            if (_isLoggedInSubject.Value)
                return true;

            string? refreshToken = _authenticationRepository.GetRefreshToken();
            if (refreshToken == null)
                return false;

            return await UseRefreshToken(refreshToken);
        }
        finally
        {
            _authLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task Login(CancellationToken cancellationToken)
    {
        await _authLock.WaitAsync(cancellationToken);

        try
        {
            if (_isLoggedInSubject.Value)
                return;

            // Start a HTTP listener, this port could be in use but chances are very slim
            string redirectUri = "http://localhost:56789";
            using HttpListener listener = new();
            listener.Prefixes.Add(redirectUri + "/");
            listener.Start();

            // Discover the Identity endpoint
            DiscoveryDocumentResponse disco = await GetDiscovery();
            if (disco.AuthorizeEndpoint == null)
                throw new ArtemisWebClientException("Could not determine the authorize endpoint");

            // Generate the PKCE code verifier and code challenge
            string codeVerifier = CryptoRandom.CreateUniqueId();
            string codeChallenge = Base64Url.Encode(HashSha256(codeVerifier));
            string state = Guid.NewGuid().ToString("N");

            // Open the web browser for the user to log in and authorize the app
            RequestUrl authRequestUrl = new(disco.AuthorizeEndpoint);
            string url = authRequestUrl.CreateAuthorizeUrl(
                CLIENT_ID,
                "code",
                "openid profile email offline_access api",
                redirectUri,
                state,
                codeChallenge: codeChallenge,
                codeChallengeMethod: OidcConstants.CodeChallengeMethods.Sha256);
            Utilities.OpenUrl(url);

            // Wait for the callback with the code
            HttpListenerContext context = await listener.GetContextAsync().WaitAsync(cancellationToken);
            string? code = context.Request.QueryString.Get("code");
            string? returnState = context.Request.QueryString.Get("state");

            // Validate that a code was given and that our state matches, ensuring we don't respond to a request we did not initialize
            if (code == null || returnState != state)
                throw new ArtemisWebClientException("Did not get the expected response on the callback");

            // Redirect the browser to a page hosted by Identity indicating success 
            context.Response.StatusCode = (int) HttpStatusCode.Redirect;
            context.Response.AddHeader("Location", $"{WorkshopConstants.AUTHORITY_URL}/account/login/success");
            context.Response.Close();

            // Request auth tokens
            HttpClient client = _httpClientFactory.CreateClient();
            TokenResponse response = await client.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
            {
                Address = disco.TokenEndpoint,
                ClientId = CLIENT_ID,
                Code = code,
                CodeVerifier = codeVerifier,
                RedirectUri = redirectUri
            }, cancellationToken);

            if (response.IsError)
                throw new ArtemisWebClientException("Failed to request device authorization: " + response.Error);

            // Set the current user using the new tokens
            SetCurrentUser(response);

            listener.Stop();
            listener.Close();
        }
        finally
        {
            _authLock.Release();
        }
    }

    /// <inheritdoc />
    public void Logout()
    {
        _token = null;
        _claims.Clear();
        _authenticationRepository.SetRefreshToken(null);

        _isLoggedInSubject.OnNext(false);
    }
}